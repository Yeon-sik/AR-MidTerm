namespace MidtermAR
{
    public enum ProductPhase { Scanning, AwaitingPlacement, Placed, UnknownBarcode, ScanFailed, DataUnavailable, Following, TrackingImage }

    public sealed class ProductWorkflow // Unity 객체와 분리한 상품 선택 상태 흐름
    {
        private readonly ProductCatalog catalog;
        public ProductPhase Phase { get; private set; } = ProductPhase.Scanning; // 스캔부터 배치까지의 현재 단계
        public ProductSnapshot Selected { get; private set; } // 정확한 바코드로 조회된 상품
        public string LastBarcode { get; private set; } // 미등록 안내에도 표시할 마지막 판독 번호
        public bool CanPlace => Selected != null && Phase == ProductPhase.AwaitingPlacement;

        public ProductWorkflow(ProductCatalog catalog) { this.catalog = catalog; StartScan(); }

        public void StartScan()
        {
            Selected = null;
            LastBarcode = null;
            Phase = catalog == null ? ProductPhase.DataUnavailable : ProductPhase.Scanning;
        }

        public bool AcceptBarcode(string raw)
        {
            if (Phase != ProductPhase.Scanning || !BarcodeIdentity.TryNormalizeEan13(raw, out var normalized)) return false;
            LastBarcode = normalized;
            Phase = catalog.TryFind(normalized, out var product) ? ProductPhase.AwaitingPlacement : ProductPhase.UnknownBarcode;
            Selected = product;
            return true;
        }

        public void ScanTimedOut() { if (Phase == ProductPhase.Scanning) Phase = ProductPhase.ScanFailed; }
        public void MarkPlaced() { if (CanPlace) Phase = ProductPhase.Placed; }
        public void RequestPlacement() { if (Selected != null) Phase = ProductPhase.AwaitingPlacement; }
        public void FollowBarcode() { if (Selected != null) Phase = ProductPhase.Following; }
        public void FollowImage() { if (Selected != null) Phase = ProductPhase.TrackingImage; }
    }
}
