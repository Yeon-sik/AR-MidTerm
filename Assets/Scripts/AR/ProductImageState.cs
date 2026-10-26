using UnityEngine.XR.ARSubsystems;

namespace MidtermAR
{
    // 바코드 선택과 독립적으로 현재 프레임의 이미지를 상품 ID에 연결한다.
    public sealed class ProductImageState
    {
        private readonly ProductCatalog products;
        private readonly ProductImageCatalog images;
        private string preferredImage;
        public ProductImageBinding Binding { get; private set; }
        public ProductSnapshot Product { get; private set; }
        public bool HasImageConflict { get; private set; }
        public bool Ready { get; private set; }
        public bool CanDisplay => Ready && Product != null && !HasImageConflict;

        public ProductImageState(ProductCatalog products, ProductImageCatalog images)
        { this.products = products; this.images = images; }

        public void BeginFrame(bool ready)
        {
            Ready = ready;
            Product = null;
            Binding = null;
            HasImageConflict = false;
        }

        public void ObserveImage(string name, TrackingState state, int method)
        {
            if (!ImageTrackingPolicy.CanDisplay(Ready, false, state, method, true)
                || !images.TryFindByImage(name, out var binding) || !products.TryFindById(binding.catalogProductId, out var product)) return;
            if (Product != null && Product.catalogProductId != product.catalogProductId) HasImageConflict = true;
            if (Binding == null || name == preferredImage) { Binding = binding; Product = product; }
        }

        public void EndFrame() { if (CanDisplay) preferredImage = Binding.referenceImageName; }
        public void Reset() { preferredImage = null; BeginFrame(false); }
    }
}
