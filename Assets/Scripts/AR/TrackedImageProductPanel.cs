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
        [SerializeField] private LineRenderer cropOutline;
        [SerializeField] private LineRenderer connector;
        [SerializeField] private ProductAnnotationPresenter annotations;
        [SerializeField] private bool showAnnotations = false;
        private ProductImageState state;
        private readonly ARCoreImageObservation observation = new ARCoreImageObservation();
        private readonly HashSet<TrackableId> freshImages = new HashSet<TrackableId>();
        private WorldProductPanel panel;
        private ProductSnapshot boundProduct;
        private string boundDisplayKey;
        private string boundSourceBadge;
        private ARTrackedImage selectedImage;
        private bool paused;
        private float lastCameraFrame = float.NegativeInfinity;
        public bool HasVisibleTarget => panel != null && panel.gameObject.activeSelf;
        public ProductImageBinding Binding { get; private set; }
        public ImageProductTarget ImageTarget => state?.CanDisplay == true ? state.Target : null;
        public ProductSnapshot CurrentProduct => state?.CanDisplay == true ? state.Product : null;
        public bool HasImageConflict => state?.HasImageConflict == true;
        public bool TrackingMethodUnavailable => observation.Unavailable;
        public IReferenceImageLibrary ReferenceLibrary => imageManager.referenceLibrary;

        public void Configure(ProductCatalog products, ProductImageCatalog images, SessionImageRegistry registry = null)
        { state = new ProductImageState(products, images, registry); annotations?.Configure(images); InvalidatePose(); }

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
        public void SetProduct(ProductSnapshot product, ProductImageBinding binding, string displayKey = null, string sourceBadge = null)
        {
            if (product != null && binding != null && product.catalogProductId != binding.catalogProductId)
                throw new ArgumentException("상품정보와 이미지의 상품 ID가 일치하지 않습니다.");
            var key = product == null ? null : displayKey ?? ProductIdentity.Key(product);
            if (product != null && ReferenceEquals(boundProduct, product) && boundDisplayKey == key
                && Binding == binding && boundSourceBadge == sourceBadge) return;
            boundProduct = product;
            boundDisplayKey = key;
            boundSourceBadge = sourceBadge;
            Binding = product == null ? null : binding;
            if (product == null || binding == null) { if (panel != null) panel.Bind(null); annotations?.Bind(null); Hide(); return; }
            if (panel == null)
            {
                panel = Instantiate(panelPrefab);
                panel.name = "Tracked Product Information";
                panel.GetComponent<Canvas>().worldCamera = arCamera;
            }
            panel.Bind(product, binding, key, sourceBadge);
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
            var target = state.Target;
            // 사전 등록 앞면의 기존 표시를 보존하고, 현장 등록에는 식별 시점의 출처를 함께 표시한다.
            SetProduct(target.DisplayProduct, target.Binding, target.IdentityKey,
                target.Binding.runtimeRegistered ? target.SourceBadge : null);
            annotations?.Bind(showAnnotations ? state.Product : null);
            if (!TryApplyPose(selectedImage.transform, selectedImage.size)) { Hide(); return; }
            panel.gameObject.SetActive(true);
            if (cropOutline != null) cropOutline.enabled = true;
            if (connector != null) connector.enabled = true;
            annotations?.SetVisible(showAnnotations);
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

        // 공통 원점 오른쪽에 고정 배치한다. 카메라나 화면 경계는 위치 계산에 사용하지 않는다.
        public bool TryApplyPose(Transform image, Vector2 size)
        {
            if (panel == null || image == null || !ProductPanelLayout.TryCreate(Binding, size, out var layout)) return false;
            var rect = (RectTransform)panel.transform;
            rect.SetPositionAndRotation(image.TransformPoint(layout.Center), image.rotation * ProductPanelLayout.Rotation);
            rect.localScale = Vector3.one * (layout.Width / rect.rect.width);
            if (connector != null) connector.SetPositions(new[] { image.TransformPoint(layout.ConnectorStart), image.TransformPoint(layout.ConnectorEnd) });
            var x = size.x * .5f; var z = size.y * .5f;
            if (cropOutline != null) cropOutline.SetPositions(new[] { image.TransformPoint(new Vector3(-x, .001f, -z)), image.TransformPoint(new Vector3(x, .001f, -z)),
                image.TransformPoint(new Vector3(x, .001f, z)), image.TransformPoint(new Vector3(-x, .001f, z)) });
            var height = layout.Width * rect.rect.height / rect.rect.width;
            annotations?.ApplyPose(image, Binding, new Rect(Binding.productWidthMetres * .5f + ProductPanelLayout.GapMetres,
                -height * .5f, layout.Width * Mathf.Cos(ProductPanelLayout.TiltDegrees * Mathf.Deg2Rad), height));
            return true;
        }
        private void Hide()
        {
            if (panel != null) panel.gameObject.SetActive(false);
            if (cropOutline != null) cropOutline.enabled = false;
            if (connector != null) connector.enabled = false;
            annotations?.SetVisible(false);
        }
        private void OnApplicationPause(bool value) { paused = value; InvalidatePose(); }
        private void OnDestroy()
        {
            if (panel == null) return;
            Destroy(panel.gameObject);
        }
    }
}
