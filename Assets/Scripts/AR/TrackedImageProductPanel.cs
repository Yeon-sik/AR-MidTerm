using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MidtermAR
{
    // 이미지 관측·자세만 소유한다. 표시 허용 여부는 앱의 공통 판정기가 결정한다.
    public sealed class TrackedImageProductPanel : MonoBehaviour
    {
        [SerializeField] private ARTrackedImageManager imageManager;
        [SerializeField] private ARSession session;
        [SerializeField] private ARReadiness readiness;
        [SerializeField] private ARCameraManager cameraManager;
        [SerializeField] private Camera arCamera;
        [SerializeField] private WorldProductPanel panelPrefab;
        private ProductImageState state;
        private readonly ARCoreImageObservation observation = new ARCoreImageObservation();
        private readonly HashSet<TrackableId> freshImages = new HashSet<TrackableId>();
        private WorldProductPanel panel;
        private string boundProductId;
        private ARTrackedImage selectedImage;
        private bool paused;
        private float lastCameraFrame = float.NegativeInfinity;
        public event Action RescanRequested;
        public bool HasVisibleTarget => panel != null && panel.gameObject.activeSelf;
        public ProductImageBinding Binding { get; private set; }
        public ProductSnapshot CurrentProduct => state?.CanDisplay == true ? state.Product : null;
        public bool HasImageConflict => state?.HasImageConflict == true;
        public bool TrackingMethodUnavailable => observation.Unavailable;
        public IReferenceImageLibrary ReferenceLibrary => imageManager.referenceLibrary;

        public void Configure(ProductCatalog products, ProductImageCatalog images)
        { state = new ProductImageState(products, images); InvalidatePose(); }

        private void OnEnable()
        {
            InvalidatePose();
            cameraManager.frameReceived += OnCameraFrame;
            imageManager.trackablesChanged.AddListener(OnImagesChanged);
        }
        private void OnDisable()
        {
            cameraManager.frameReceived -= OnCameraFrame;
            imageManager.trackablesChanged.RemoveListener(OnImagesChanged);
            InvalidatePose();
        }
        private void OnCameraFrame(ARCameraFrameEventArgs args) { if (!paused) lastCameraFrame = Time.unscaledTime; }
        private void OnImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> changes)
        {
            if (paused) return;
            foreach (var image in changes.added) freshImages.Add(image.trackableId);
            foreach (var image in changes.updated) freshImages.Add(image.trackableId);
            foreach (var image in changes.removed) freshImages.Remove(image.Key);
        }

        // 같은 상품의 재획득·크롭 전환에서는 탭을 유지한다.
        public void SetProduct(ProductSnapshot product, ProductImageBinding binding)
        {
            if (product != null && binding != null && product.catalogProductId != binding.catalogProductId)
                throw new ArgumentException("상품정보와 이미지의 상품 ID가 일치하지 않습니다.");
            if (product != null && boundProductId == product.catalogProductId && Binding == binding) return;
            boundProductId = product?.catalogProductId;
            Binding = product == null ? null : binding;
            if (product == null || binding == null) { if (panel != null) panel.Bind(null); Hide(); return; }
            if (panel == null)
            {
                panel = Instantiate(panelPrefab);
                panel.name = "Tracked Product Information";
                panel.GetComponent<Canvas>().worldCamera = arCamera;
                panel.RescanRequested += OnRescanRequested;
            }
            panel.Bind(product, binding);
            Hide();
        }

        public void SampleFrame()
        {
            if (state == null) return;
            var ready = !paused && Time.unscaledTime - lastCameraFrame < 1f && readiness.IsReady
                && imageManager.isActiveAndEnabled && imageManager.subsystem != null && imageManager.subsystem.running;
            state.BeginFrame(ready);
            selectedImage = null;
            if (ready)
            {
                foreach (var image in imageManager.trackables)
                    if (freshImages.Contains(image.trackableId))
                        state.ObserveImage(image.referenceImage.name, image.trackingState, observation.GetMethod(session, image));
                state.EndFrame();
                if (state.CanDisplay)
                    foreach (var image in imageManager.trackables)
                        if (image.referenceImage.name == state.Binding.referenceImageName && freshImages.Contains(image.trackableId))
                        { selectedImage = image; break; }
            }
        }

        public void Present(bool allowed)
        {
            if (!allowed || state?.CanDisplay != true || selectedImage == null) { Hide(); return; }
            SetProduct(state.Product, state.Binding);
            if (!TryApplyPose(selectedImage.transform, selectedImage.size)) { Hide(); return; }
            panel.gameObject.SetActive(true);
        }

        public void InvalidatePose()
        {
            lastCameraFrame = float.NegativeInfinity;
            freshImages.Clear();
            selectedImage = null;
            state?.BeginFrame(false);
            Hide();
        }
        public void ResetSelection() { state?.Reset(); SetProduct(null, null); InvalidatePose(); }

        // UI의 XY 면을 이미지의 XZ 면과 맞추고 크롭 중심에서 공통 원점까지의 오프셋을 반영한다.
        public bool TryApplyPose(Transform image, Vector2 size)
        {
            if (panel == null || image == null || size.x <= 0 || size.y <= 0
                || float.IsNaN(size.x) || float.IsInfinity(size.x) || float.IsNaN(size.y) || float.IsInfinity(size.y)) return false;
            var rect = (RectTransform)panel.transform;
            var width = Mathf.Clamp(size.x * 1.6f, 0.14f, 0.28f) * 0.6f; // 앞면 정보판만 기존 가로·세로의 60%로 표시한다.
            rect.SetPositionAndRotation(image.TransformPoint(new Vector3(Binding?.offsetXMetres ?? 0, 0.008f, Binding?.offsetZMetres ?? 0)), image.rotation * Quaternion.Euler(90, 0, 0));
            rect.localScale = Vector3.one * (width / rect.rect.width);
            return true;
        }
        private void Hide() { if (panel != null) panel.gameObject.SetActive(false); }
        private void OnRescanRequested() { InvalidatePose(); RescanRequested?.Invoke(); }
        private void OnApplicationPause(bool value) { paused = value; InvalidatePose(); }
        private void OnDestroy()
        {
            if (panel == null) return;
            panel.RescanRequested -= OnRescanRequested;
            Destroy(panel.gameObject);
        }
    }
}
