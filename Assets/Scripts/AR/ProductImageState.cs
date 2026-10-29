using UnityEngine.XR.ARSubsystems;

namespace MidtermAR
{
    // 바코드 선택과 독립적으로 현재 프레임의 이미지를 상품 ID에 연결한다.
    public sealed class ProductImageState
    {
        private readonly ProductCatalog products;
        private readonly ProductImageCatalog images;
        private readonly SessionImageRegistry sessionImages;
        private string preferredImage;
        public ImageProductTarget Target { get; private set; }
        public ProductImageBinding Binding => Target?.Binding;
        public ProductSnapshot Product => Target?.DisplayProduct;
        public bool HasImageConflict { get; private set; }
        public bool Ready { get; private set; }
        public bool CanDisplay => Ready && Product != null && !HasImageConflict;

        public ProductImageState(ProductCatalog products, ProductImageCatalog images, SessionImageRegistry sessionImages = null)
        { this.products = products; this.images = images; this.sessionImages = sessionImages; }

        public void BeginFrame(bool ready)
        {
            Ready = ready;
            Target = null;
            HasImageConflict = false;
        }

        public void ObserveImage(string name, TrackingState state, int method)
        {
            if (!ImageTrackingPolicy.CanDisplay(Ready, false, state, method, true)) return;
            ImageProductTarget target;
            if (images.TryFindByImage(name, out var binding) && products.TryFindById(binding.catalogProductId, out var product))
                target = ImageProductTarget.FromLocal(product, binding, "PT 스냅샷 " + products.SnapshotDate);
            else if (sessionImages == null || !sessionImages.TryFindByImage(name, out target)) return;
            if (Product != null && !ProductIdentity.Matches(Product, target.DisplayProduct)) HasImageConflict = true;
            if (Target == null || name == preferredImage) Target = target;
        }

        public void EndFrame() { if (CanDisplay) preferredImage = Binding.referenceImageName; }
        public void Reset() { preferredImage = null; BeginFrame(false); }
    }
}
