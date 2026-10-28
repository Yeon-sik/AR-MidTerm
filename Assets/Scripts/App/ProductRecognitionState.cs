namespace MidtermAR
{
    public enum ProductDisplayRoute { None, Image3D, Barcode2D, UnknownBarcode, Conflict }
    public enum ProductRegistration { Neither, ImageOnly, BarcodeOnly, Both }

    // 메인 스레드가 판독 확정과 두 경로의 충돌·표시 우선순위를 결정한다.
    public sealed class ProductRecognitionState
    {
        private readonly ProductCatalog products;
        private readonly ProductImageCatalog images;
        private readonly SessionImageRegistry sessionImages;
        private readonly BarcodeConfirmation confirmation = new BarcodeConfirmation();
        private float lastObservation = float.NegativeInfinity;
        private float lastConfirmedAt = float.NegativeInfinity;
        private ProductSnapshot barcodeProduct;
        public ProductDisplayRoute Route { get; private set; }
        public ProductSnapshot Selected { get; private set; }
        public ProductSnapshot ImageDisplayProduct { get; private set; }
        private string imageSourceBadge;
        public string ConfirmedBarcode { get; private set; }
        public string SnapshotDate => products.SnapshotDate;
        public int ConfirmationCount => confirmation.Count;
        public long LookupGeneration { get; private set; }
        public bool OnlineEnabled { get; private set; }
        public ProductLookupResult LookupResult { get; private set; }
        // 온라인 DTO는 Selected에 넣지 않는다. 현재 확정 바코드의 화면 데이터만 별도로 제공한다.
        public ProductSnapshot BarcodeDisplayProduct => LookupResult?.Product
            ?? (LookupResult == null || LookupResult.Status == LookupStatus.Loading || LookupResult.Status == LookupStatus.PtUnavailable ? barcodeProduct : null);
        // 화면 동작은 실제 표시 경로를 따른다. Selected는 로컬/앞면 선택 상태로 유지한다.
        public ProductSnapshot DisplayProduct => Route == ProductDisplayRoute.Image3D ? ImageDisplayProduct
            : Route == ProductDisplayRoute.Barcode2D ? BarcodeDisplayProduct : null;
        public string DisplayProductKey => ProductIdentity.Key(DisplayProduct);
        public string SourceBadge => Route == ProductDisplayRoute.Image3D ? imageSourceBadge ?? "" : (LookupResult?.Badge ?? "")
            + (BarcodeDisplayProduct == barcodeProduct && barcodeProduct != null ? (LookupResult == null ? "" : " · ") + "PT 스냅샷 " + SnapshotDate : "");
        public string LookupMessage => SourceBadge + (string.IsNullOrEmpty(LookupResult?.Detail) ? "" : "\n" + LookupResult.Detail)
            + "\n읽은 번호: " + ConfirmedBarcode;

        public ProductRecognitionState(ProductCatalog products, ProductImageCatalog images, SessionImageRegistry sessionImages = null)
        { this.products = products; this.images = images; this.sessionImages = sessionImages; }

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
            // 기존 호출은 로컬 앞면 인식용이다. 온라인 현장 등록은 ResolveImage로 표시/선택을 분리한다.
            ImageProductTarget target = null;
            if (imageProduct != null && products.TryFindById(imageProduct.catalogProductId, out var local))
            {
                images.TryFind(local, out var binding);
                target = ImageProductTarget.FromLocal(local, binding, "PT 스냅샷 " + SnapshotDate);
            }
            ResolveImage(target, imageConflict, now);
        }

        public void ResolveImage(ImageProductTarget imageTarget, bool imageConflict, float now)
        {
            var currentBarcode = now >= lastConfirmedAt && now - lastConfirmedAt <= BarcodeFollowState.MaxAgeSeconds;
            if (!currentBarcode) ClearBarcode();
            if (now - lastObservation > BarcodeFollowState.MaxAgeSeconds) confirmation.Reset();
            Selected = null; // 현재 보이는 대상만 선택 상태로 둔다. 소실 후 이전 상품을 붙잡지 않는다.
            ImageDisplayProduct = null;
            imageSourceBadge = null;
            var barcodeIdentity = OnlineEnabled && (LookupResult == null || LookupResult.Status == LookupStatus.Loading)
                ? null : BarcodeDisplayProduct;
            // 사전 등록 앞면은 기존처럼 PT ID가 같아야 한다. EAN만으로 연결하는 것은 현장 등록 대상에 한정한다.
            var sameImageIdentity = imageTarget?.Binding?.runtimeRegistered == true
                ? ProductIdentity.Matches(barcodeIdentity, imageTarget.DisplayProduct)
                : barcodeIdentity?.catalogProductId != null
                    && barcodeIdentity.catalogProductId == imageTarget?.DisplayProduct?.catalogProductId;
            var conflict = imageTarget != null && currentBarcode
                && !sameImageIdentity;
            if (currentBarcode && LookupResult?.Status == LookupStatus.Conflict) conflict = true;
            if (conflict || imageConflict) { Route = ProductDisplayRoute.Conflict; return; }
            if (imageTarget != null)
            {
                ImageDisplayProduct = imageTarget.DisplayProduct;
                if (imageTarget.LocalProduct != null && products.TryFindById(imageTarget.LocalProduct.catalogProductId, out var local)
                    && ReferenceEquals(local, imageTarget.LocalProduct)) Selected = local;
                imageSourceBadge = imageTarget.SourceBadge;
                Route = ProductDisplayRoute.Image3D;
                return;
            }
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
            var hasImage = images.TryFind(product, out _) || sessionImages?.ContainsProduct(product) == true;
            return hasImage ? string.IsNullOrEmpty(product.barcode) ? ProductRegistration.ImageOnly : ProductRegistration.Both
                : string.IsNullOrEmpty(product.barcode) ? ProductRegistration.Neither : ProductRegistration.BarcodeOnly;
        }

        public string BarcodeBadge(ProductSnapshot product) => sessionImages?.ContainsProduct(product) == true
            ? "앞면 현장 등록됨 · 이번 실행에서만 유지"
            : images.TryFind(product, out _) ? "앞면 이미지 등록됨 · 앞면을 비추면 3D로 표시" : "앞면 미등록 · 바코드 2D 정보";
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
        { LookupGeneration++; confirmation.Reset(); lastObservation = float.NegativeInfinity; ClearBarcode(); Selected = null;
            ImageDisplayProduct = null; imageSourceBadge = null; Route = ProductDisplayRoute.None; }
        public void Reset() { Suspend(); }
    }
}
