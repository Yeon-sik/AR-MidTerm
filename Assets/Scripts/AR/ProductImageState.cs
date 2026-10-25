using UnityEngine.XR.ARSubsystems;

namespace MidtermAR
{
    // 카메라/네이티브 API 없이 검증하는 표시 정책. 바코드 소실 타이머를 사용하지 않는다.
    public sealed class ProductImageState
    {
        public ProductImageBinding Binding { get; private set; }
        public string ConflictingBarcode { get; private set; }
        public bool HasImageConflict { get; private set; }
        public bool HasCurrentImage { get; private set; }
        public bool Ready { get; private set; }
        public bool CanDisplay => Binding != null && Ready && HasCurrentImage && !HasImageConflict && ConflictingBarcode == null;

        public void Select(ProductImageBinding binding)
        {
            Binding = binding;
            ConflictingBarcode = null;
            BeginFrame(false);
        }

        public void BeginFrame(bool ready)
        {
            Ready = ready;
            HasCurrentImage = false;
            HasImageConflict = false;
        }

        public void ObserveImage(string name, TrackingState state, int method)
        {
            if (Binding == null || !Ready || !ImageTrackingPolicy.CanDisplay(true, false, state, method, true)) return;
            if (name == Binding.referenceImageName) HasCurrentImage = true;
            else HasImageConflict = true;
        }

        public void ObserveBarcode(BarcodeReadResult result)
        {
            if (Binding != null && result != null && result.Format == "EAN_13"
                && BarcodeIdentity.TryNormalizeEan13(result.Text, out var normalized) && normalized != Binding.barcode)
                ConflictingBarcode = normalized; // 다른 상품을 읽으면 명시적 재스캔 전까지 표시하지 않는다.
        }
    }
}
