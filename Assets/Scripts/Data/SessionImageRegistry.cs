using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MidtermAR
{
    public static class ProductIdentity
    {
        public static string Key(ProductSnapshot product)
        {
            if (product == null) return null;
            if (!string.IsNullOrWhiteSpace(product.catalogProductId)) return product.catalogProductId;
            return BarcodeIdentity.TryNormalizeEan13(product.barcode, out var barcode) ? barcode : null;
        }

        public static bool Matches(ProductSnapshot first, ProductSnapshot second)
        {
            if (first == null || second == null) return false;
            if (!string.IsNullOrWhiteSpace(first.catalogProductId) && !string.IsNullOrWhiteSpace(second.catalogProductId))
                return string.Equals(first.catalogProductId, second.catalogProductId, StringComparison.Ordinal);
            return BarcodeIdentity.TryNormalizeEan13(first.barcode, out var firstBarcode)
                && BarcodeIdentity.TryNormalizeEan13(second.barcode, out var secondBarcode)
                && firstBarcode == secondBarcode;
        }
    }

    public sealed class ImageProductTarget
    {
        public ProductSnapshot DisplayProduct { get; }
        public ProductSnapshot LocalProduct { get; }
        public ProductImageBinding Binding { get; }
        public string IdentityKey { get; }
        public string SourceBadge { get; }

        internal ImageProductTarget(ProductSnapshot displayProduct, ProductSnapshot localProduct,
            ProductImageBinding binding, string sourceBadge)
        {
            DisplayProduct = displayProduct;
            LocalProduct = localProduct;
            Binding = binding;
            IdentityKey = ProductIdentity.Key(displayProduct);
            SourceBadge = sourceBadge ?? "";
        }

        public static ImageProductTarget FromLocal(ProductSnapshot product, ProductImageBinding binding, string sourceBadge)
            => product == null ? null : new ImageProductTarget(product, product, binding, sourceBadge);
    }

    // 현장 등록은 이 앱 실행의 메모리에만 보관한다. 정적 카탈로그나 이미지 JSON을 수정하지 않는다.
    public sealed class SessionImageRegistry
    {
        private readonly ProductCatalog localCatalog;
        private readonly Dictionary<string, ImageProductTarget> images = new Dictionary<string, ImageProductTarget>(StringComparer.Ordinal);
        public int Count => images.Count;

        public SessionImageRegistry(ProductCatalog localCatalog = null) { this.localCatalog = localCatalog; }

        public bool TryAdd(string referenceImageName, ProductSnapshot displayProduct, ProductSnapshot localProduct,
            string sourceBadge, out ImageProductTarget target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(referenceImageName) || ProductIdentity.Key(displayProduct) == null
                || string.IsNullOrWhiteSpace(displayProduct.name) || images.ContainsKey(referenceImageName)
                || ContainsProduct(displayProduct)) return false;
            // 온라인 DTO를 로컬 선택 상태로 주입하지 않는다. 실제 카탈로그의 객체만 허용한다.
            if (localProduct != null && (localCatalog == null
                || !localCatalog.TryFindById(localProduct.catalogProductId, out var knownLocal)
                || !ReferenceEquals(knownLocal, localProduct) || !ProductIdentity.Matches(displayProduct, localProduct))) return false;
            var snapshot = CopyProduct(displayProduct);
            var binding = new ProductImageBinding
            {
                catalogProductId = snapshot.catalogProductId,
                referenceImageName = referenceImageName,
                displayName = snapshot.name,
                runtimeRegistered = true
            };
            target = new ImageProductTarget(snapshot, localProduct, binding, sourceBadge);
            images.Add(referenceImageName, target);
            return true;
        }

        public bool TryFindByImage(string referenceImageName, out ImageProductTarget target)
        {
            target = null;
            return referenceImageName != null && images.TryGetValue(referenceImageName, out target);
        }

        public bool ContainsProduct(ProductSnapshot product)
        {
            foreach (var target in images.Values)
                if (ProductIdentity.Matches(target.DisplayProduct, product)) return true;
            return false;
        }

        // 등록 버튼 시점과 추가 완료 시점에 각각 복사한다. 이후 조회 결과 변경이 가격·영양·출처를 바꾸지 않는다.
        public static ProductSnapshot CopyProduct(ProductSnapshot product) => product == null ? null
            : JsonConvert.DeserializeObject<ProductSnapshot>(JsonConvert.SerializeObject(product));
    }
}
