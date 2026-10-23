namespace MidtermAR
{
    public enum ProductPhase { Scanning, AwaitingPlacement, Placed, UnknownBarcode, ScanFailed, DataUnavailable }

    public sealed class ProductWorkflow
    {
        private readonly ProductCatalog catalog;
        public ProductPhase Phase { get; private set; } = ProductPhase.Scanning;
        public ProductSnapshot Selected { get; private set; }
        public string LastBarcode { get; private set; }
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
    }
}
