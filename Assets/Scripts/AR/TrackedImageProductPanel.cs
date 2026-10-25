using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MidtermAR
{
    public sealed class TrackedImageProductPanel : MonoBehaviour
    {
        [SerializeField] private ARTrackedImageManager imageManager;
        [SerializeField] private ARSession session;
        [SerializeField] private ARReadiness readiness;
        [SerializeField] private ARCameraManager cameraManager;
        [SerializeField] private Camera arCamera;
        [SerializeField] private WorldProductPanel panelPrefab;
        private readonly ProductImageState state = new ProductImageState();
        private readonly ARCoreImageObservation observation = new ARCoreImageObservation();
        private readonly HashSet<TrackableId> freshImages = new HashSet<TrackableId>();
        private WorldProductPanel panel;
        private bool paused;
        private float lastCameraFrame = float.NegativeInfinity;

        public event Action RescanRequested;
        public bool HasVisibleTarget => panel != null && panel.gameObject.activeSelf;
        public ProductImageBinding Binding => state.Binding;
        public string ConflictingBarcode => state.ConflictingBarcode;
        public bool HasImageConflict => state.HasImageConflict;
        public bool TrackingMethodUnavailable => observation.Unavailable;
        public IReferenceImageLibrary ReferenceLibrary => imageManager.referenceLibrary;

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

        private void OnCameraFrame(ARCameraFrameEventArgs args)
        {
            if (!paused) lastCameraFrame = Time.unscaledTime;
        }

        private void OnImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> changes)
        {
            if (paused) return;
            foreach (var image in changes.added) freshImages.Add(image.trackableId);
            foreach (var image in changes.updated) freshImages.Add(image.trackableId);
            foreach (var image in changes.removed) freshImages.Remove(image.Key);
        }

        public void SetProduct(ProductSnapshot product, ProductImageBinding binding)
        {
            if (product != null && binding != null && product.barcode != binding.barcode)
                throw new ArgumentException("상품정보와 이미지의 바코드가 일치하지 않습니다.");
            state.Select(product == null ? null : binding);
            InvalidatePose();
            if (product == null || binding == null) { if (panel != null) panel.Bind(null); return; }
            if (panel == null)
            {
                panel = Instantiate(panelPrefab);
                panel.name = "Tracked Product Information";
                panel.GetComponent<Canvas>().worldCamera = arCamera;
                panel.RescanRequested += OnRescanRequested;
            }
            panel.Bind(product, binding);
            panel.gameObject.SetActive(false);
        }

        public void ObserveBarcode(BarcodeReadResult result)
        {
            state.ObserveBarcode(result);
            if (!state.CanDisplay) Hide();
        }

        public void InvalidatePose()
        {
            lastCameraFrame = float.NegativeInfinity;
            freshImages.Clear();
            state.BeginFrame(false);
            Hide();
        }

        private void LateUpdate()
        {
            var ready = !paused && Time.unscaledTime - lastCameraFrame < 1f && readiness.IsReady
                && imageManager.isActiveAndEnabled && imageManager.subsystem != null && imageManager.subsystem.running;
            state.BeginFrame(ready);
            ARTrackedImage selected = null;
            if (ready && state.Binding != null)
            {
                foreach (var image in imageManager.trackables)
                {
                    if (!freshImages.Contains(image.trackableId)) continue;
                    var method = observation.GetMethod(session, image);
                    state.ObserveImage(image.referenceImage.name, image.trackingState, method);
                    if (image.referenceImage.name == state.Binding.referenceImageName
                        && ImageTrackingPolicy.CanDisplay(true, false, image.trackingState, method, true)) selected = image;
                }
            }
            if (!state.CanDisplay || selected == null || panel == null || !TryApplyPose(selected.transform, selected.size))
            { Hide(); return; }
            panel.gameObject.SetActive(true);
        }

        // UI의 XY 면을 추적 이미지의 XZ 면에 맞춘다. 카메라 방향으로 돌리지 않는다.
        public bool TryApplyPose(Transform image, Vector2 size)
        {
            if (panel == null || image == null || size.x <= 0 || size.y <= 0
                || float.IsNaN(size.x) || float.IsInfinity(size.x) || float.IsNaN(size.y) || float.IsInfinity(size.y)) return false;
            var rect = (RectTransform)panel.transform;
            var width = Mathf.Clamp(size.x * 1.6f, 0.14f, 0.28f);
            rect.SetPositionAndRotation(image.TransformPoint(new Vector3(0, 0.008f, 0)), image.rotation * Quaternion.Euler(90, 0, 0));
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
