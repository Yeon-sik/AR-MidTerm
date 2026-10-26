namespace MidtermAR
{
    public enum ProductDisplayRoute { None, Image3D, Barcode2D, UnknownBarcode, Conflict }
    public enum ProductRegistration { Neither, ImageOnly, BarcodeOnly, Both }

    // 메인 스레드가 판독 확정과 두 경로의 충돌·표시 우선순위를 결정한다.
    public sealed class ProductRecognitionState
    {
        private readonly ProductCatalog products;
        private readonly ProductImageCatalog images;
        private readonly BarcodeConfirmation confirmation = new BarcodeConfirmation();
        private float lastObservation = float.NegativeInfinity;
        private float lastConfirmedAt = float.NegativeInfinity;
        private ProductSnapshot barcodeProduct;
        public ProductDisplayRoute Route { get; private set; }
        public ProductSnapshot Selected { get; private set; }
        public string ConfirmedBarcode { get; private set; }
        public string SnapshotDate => products.SnapshotDate;
        public int ConfirmationCount => confirmation.Count;
        public long LookupGeneration { get; private set; }
        public bool OnlineEnabled { get; private set; }
        public ProductLookupResult LookupResult { get; private set; }
        // 온라인 DTO는 Selected에 넣지 않는다. 현재 확정 바코드의 화면 데이터만 별도로 제공한다.
        public ProductSnapshot BarcodeDisplayProduct => LookupResult?.Product
            ?? (LookupResult == null || LookupResult.Status == LookupStatus.Loading || LookupResult.Status == LookupStatus.PtUnavailable ? barcodeProduct : null);
        public string SourceBadge => (LookupResult?.Badge ?? "")
            + (BarcodeDisplayProduct == barcodeProduct && barcodeProduct != null ? (LookupResult == null ? "" : " · ") + "PT 스냅샷 " + SnapshotDate : "");
        public string LookupMessage => SourceBadge + (string.IsNullOrEmpty(LookupResult?.Detail) ? "" : "\n" + LookupResult.Detail)
            + "\n읽은 번호: " + ConfirmedBarcode;

        public ProductRecognitionState(ProductCatalog products, ProductImageCatalog images)
        { this.products = products; this.images = images; }

        public bool ObserveBarcode(BarcodeReadResult result, float capturedAt, float now)
        {
            if (float.IsNaN(capturedAt) || float.IsNaN(now) || float.IsInfinity(capturedAt) || float.IsInfinity(now)
                || capturedAt <= lastObservation || capturedAt > now || now - capturedAt > BarcodeFollowState.MaxAgeSeconds) return false;
            lastObservation = capturedAt;
            var confirmed = confirmation.Observe(result, capturedAt);
            // 최신 프레임의 판독 실패·다른 번호는 이전 확정을 즉시 해제한다. 새 후보는 3회 확인한다.
            if (!confirmed) { ClearBarcode(); return false; }
            if (ConfirmedBarcode != confirmation.Candidate) { LookupGeneration++; LookupResult = null; }
            ConfirmedBarcode = confirmation.Candidate;
            products.TryFind(ConfirmedBarcode, out barcodeProduct);
            lastConfirmedAt = capturedAt;
            return true;
        }

        public void Resolve(ProductSnapshot imageProduct, bool imageConflict, float now)
        {
            var currentBarcode = now >= lastConfirmedAt && now - lastConfirmedAt <= BarcodeFollowState.MaxAgeSeconds;
            if (!currentBarcode) ClearBarcode();
            if (now - lastObservation > BarcodeFollowState.MaxAgeSeconds) confirmation.Reset();
            Selected = null; // 현재 보이는 대상만 선택 상태로 둔다. 소실 후 이전 상품을 붙잡지 않는다.
            var identity = OnlineEnabled && (LookupResult == null || LookupResult.Status == LookupStatus.Loading)
                ? null : BarcodeDisplayProduct?.catalogProductId;
            var conflict = imageProduct != null && currentBarcode
                && (identity == null || identity != imageProduct.catalogProductId);
            if (currentBarcode && LookupResult?.Status == LookupStatus.Conflict) conflict = true;
            if (conflict || imageConflict) { Route = ProductDisplayRoute.Conflict; return; }
            if (imageProduct != null) { Selected = imageProduct; Route = ProductDisplayRoute.Image3D; return; }
            if (currentBarcode)
            {
                if (BarcodeDisplayProduct != null) { Selected = barcodeProduct; Route = ProductDisplayRoute.Barcode2D; }
                else { Selected = null; Route = ProductDisplayRoute.UnknownBarcode; }
                return;
            }
            Route = ProductDisplayRoute.None;
        }

        public ProductRegistration Registration(ProductSnapshot product)
        {
            if (product == null) return ProductRegistration.Neither;
            var hasImage = images.TryFind(product, out _);
            return hasImage ? string.IsNullOrEmpty(product.barcode) ? ProductRegistration.ImageOnly : ProductRegistration.Both
                : string.IsNullOrEmpty(product.barcode) ? ProductRegistration.Neither : ProductRegistration.BarcodeOnly;
        }

        public string BarcodeBadge(ProductSnapshot product) => images.TryFind(product, out _)
            ? "앞면 이미지 등록됨 · 앞면을 비추면 3D로 표시" : "앞면 미등록 · 바코드 2D 정보";
        public string UnknownMessage => "등록이 필요한 제품\n읽은 번호: " + ConfirmedBarcode
            + "\n로컬 카탈로그(" + SnapshotDate + ") 기준 미등록";

        public void EnableOnlineLookup() { OnlineEnabled = true; }
        public void BeginLookup(long generation)
        {
            if (ConfirmedBarcode != null && generation == LookupGeneration && LookupResult == null)
                LookupResult = new ProductLookupResult(ConfirmedBarcode, LookupStatus.Loading);
        }
        public bool ApplyLookup(ProductLookupResult result, long generation)
        {
            if (result == null || ConfirmedBarcode == null || result.Barcode != ConfirmedBarcode || generation != LookupGeneration) return false;
            LookupResult = result; return true;
        }
        private void ClearBarcode()
        {
            if (ConfirmedBarcode != null) LookupGeneration++;
            ConfirmedBarcode = null; barcodeProduct = null; LookupResult = null; lastConfirmedAt = float.NegativeInfinity;
        }
        public void Suspend()
        { LookupGeneration++; confirmation.Reset(); lastObservation = float.NegativeInfinity; ClearBarcode(); Selected = null; Route = ProductDisplayRoute.None; }
        public void Reset() { Suspend(); }
    }
}
