using System.Threading;
using System.Threading.Tasks;

namespace MidtermAR
{
    public enum LookupStatus { Loading, Matched, ExternalFound, NotFound, Conflict, Invalid, PtUnavailable, ExternalUnavailable }

    public sealed class ProductLookupResult
    {
        public string Barcode { get; }
        public LookupStatus Status { get; }
        public ProductSnapshot Product { get; }
        public string Detail { get; }
        public bool Cacheable => Status == LookupStatus.Matched || Status == LookupStatus.ExternalFound || Status == LookupStatus.NotFound;
        public ProductLookupResult(string barcode, LookupStatus status, ProductSnapshot product = null, string detail = null)
        { Barcode = barcode; Status = status; Product = product; Detail = detail; }

        public string Badge => Status == LookupStatus.Loading ? "PT 조회 중…"
            : Status == LookupStatus.Matched ? "PT 등록 상품"
            : Status == LookupStatus.ExternalFound ? "Open Food Facts 정보 · PT 바코드 연결 필요"
            : Status == LookupStatus.NotFound ? "PT 바코드 연결 필요 · 상품정보를 찾지 못했습니다"
            : Status == LookupStatus.Conflict ? "연결 확인 필요"
            : Status == LookupStatus.Invalid ? "바코드 확인 필요"
            : Status == LookupStatus.ExternalUnavailable ? "PT 바코드 연결 필요 · 외부 조회 실패 · 다시 시도"
            : "PT 등록 여부 확인 불가";
    }

    public sealed class PtBarcodeReply
    {
        public string Status;
        public ProductSnapshot Product;
    }

    public interface IPtBarcodeClient { Task<PtBarcodeReply> Lookup(string barcode, CancellationToken cancellation); }
    public interface IOffProductClient { Task<ProductSnapshot> Lookup(string barcode, CancellationToken cancellation); }
    public interface IPublicNutritionClient { Task<NutritionSnapshot> Lookup(string catalogProductId, CancellationToken cancellation); }
    public interface IProductLookup { Task<ProductLookupResult> Lookup(string barcode, CancellationToken cancellation); }

    // 공급자 순서와 실패 의미를 한 곳에서 결정한다. 자동 재시도는 0회, 명시적 재스캔으로 재시도한다.
    public sealed class ProductLookup : IProductLookup
    {
        private readonly IPtBarcodeClient pt;
        private readonly IOffProductClient off;
        private readonly IPublicNutritionClient nutrition;
        private readonly ProductCatalog local;
        public ProductLookup(IPtBarcodeClient pt, IOffProductClient off, IPublicNutritionClient nutrition, ProductCatalog local)
        { this.pt = pt; this.off = off; this.nutrition = nutrition; this.local = local; }

        public async Task<ProductLookupResult> Lookup(string barcode, CancellationToken cancellation)
        {
            if (!BarcodeIdentity.TryNormalizeEan13(barcode, out var normalized) || normalized != barcode)
                return new ProductLookupResult(barcode, LookupStatus.Invalid);
            PtBarcodeReply reply;
            try { reply = await pt.Lookup(barcode, cancellation); }
            catch { return new ProductLookupResult(barcode, LookupStatus.PtUnavailable); }
            if (reply == null) return new ProductLookupResult(barcode, LookupStatus.PtUnavailable);
            if (reply.Status == "conflict") return new ProductLookupResult(barcode, LookupStatus.Conflict);
            if (reply.Status == "invalid") return new ProductLookupResult(barcode, LookupStatus.Invalid);
            if (reply.Status == "matched" && reply.Product != null)
            {
                var product = reply.Product;
                if (product.barcode != barcode || string.IsNullOrEmpty(product.catalogProductId))
                    return new ProductLookupResult(barcode, LookupStatus.PtUnavailable);
                // 다른 PT ID에 로컬 가격·영양을 가져다 붙이지 않는다.
                if (product.price == null && local.TryFind(barcode, out var snapshot) && snapshot.catalogProductId == product.catalogProductId)
                    product.price = snapshot.price;
                string detail = null;
                try
                {
                    product.nutrition = nutrition == null ? null : await nutrition.Lookup(product.catalogProductId, cancellation);
                    if (product.nutrition != null && product.nutrition.sourceCatalogProductId != product.catalogProductId)
                    { product.nutrition = null; detail = "영양 연결 확인 불가"; }
                }
                catch { product.nutrition = null; detail = "영양 조회 확인 불가"; }
                return new ProductLookupResult(barcode, LookupStatus.Matched, product, detail);
            }
            if (reply.Status != "not_linked") return new ProductLookupResult(barcode, LookupStatus.PtUnavailable);
            try
            {
                if (off == null) return new ProductLookupResult(barcode, LookupStatus.ExternalUnavailable);
                var product = await off.Lookup(barcode, cancellation);
                if (product != null && product.barcode != barcode) return new ProductLookupResult(barcode, LookupStatus.ExternalUnavailable);
                return new ProductLookupResult(barcode, product == null ? LookupStatus.NotFound : LookupStatus.ExternalFound, product);
            }
            catch { return new ProductLookupResult(barcode, LookupStatus.ExternalUnavailable); }
        }
    }
}
