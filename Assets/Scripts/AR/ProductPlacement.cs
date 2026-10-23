using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MidtermAR
{
    public sealed class ProductPlacement : MonoBehaviour
    {
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARAnchorManager anchorManager;
        [SerializeField] private Camera arCamera;
        [SerializeField] private WorldProductPanel panelPrefab;
        public event Action Placed;
        public event Action<string> Notice;
        public bool AllowPlacement { get; set; }
        public bool IsBusy { get; private set; }
        public bool HasPlacement => anchor != null && panel != null;
        public bool HasTrackedPlane { get; private set; }
        public bool PlacementTracking => HasPlacement && anchor.trackingState == TrackingState.Tracking && ARSession.state == ARSessionState.SessionTracking;
        private readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private ARAnchor anchor;
        private WorldProductPanel panel;
        private ProductSnapshot product;
        private int generation;
        private float nextVisualUpdate;

        public void SetProduct(ProductSnapshot value)
        {
            ClearPlacement();
            product = value;
            planeManager.enabled = value != null;
        }

        public void ClearPlacement()
        {
            generation++;
            if (panel != null) Destroy(panel.gameObject);
            panel = null;
            ReleaseAnchor(anchor);
            anchor = null;
        }

        private void OnDestroy() { ClearPlacement(); }
        private void OnDisable() { generation++; if (panel != null) panel.gameObject.SetActive(false); }

        private void Update()
        {
            if (panel != null) panel.gameObject.SetActive(PlacementTracking);
            if (Time.unscaledTime >= nextVisualUpdate)
            {
                nextVisualUpdate = Time.unscaledTime + 0.2f;
                HasTrackedPlane = false;
                foreach (var plane in planeManager.trackables)
                {
                    var valid = plane.trackingState == TrackingState.Tracking && plane.alignment == PlaneAlignment.HorizontalUp;
                    HasTrackedPlane |= valid;
                    if (plane.TryGetComponent<MeshRenderer>(out var renderer))
                        renderer.enabled = valid && AllowPlacement && ARSession.state == ARSessionState.SessionTracking;
                }
            }
            if (!AllowPlacement || IsBusy || product == null || ARSession.state != ARSessionState.SessionTracking) return;
            if (TryGetPressedPosition(out var screenPosition) && !IsOverUI(screenPosition)) Place(screenPosition);
        }

        private static bool TryGetPressedPosition(out Vector2 position)
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            { position = Touchscreen.current.primaryTouch.position.ReadValue(); return true; }
#if UNITY_EDITOR
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            { position = Mouse.current.position.ReadValue(); return true; }
#endif
            position = default;
            return false;
        }

        private bool IsOverUI(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
            return uiHits.Count > 0;
        }

        private async void Place(Vector2 screenPosition)
        {
            if (!raycastManager.Raycast(screenPosition, hits, TrackableType.PlaneWithinPolygon))
            { Notice?.Invoke("아직 표면을 찾지 못했습니다. 책상을 천천히 비춘 뒤 다시 눌러주세요."); return; }
            var hit = hits[0];
            var plane = planeManager.GetPlane(hit.trackableId);
            if (plane == null || plane.alignment != PlaneAlignment.HorizontalUp || plane.trackingState != TrackingState.Tracking) return;
            var forward = Vector3.ProjectOnPlane(hit.pose.position - arCamera.transform.position, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(arCamera.transform.forward, Vector3.up);
            var rotation = forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity;
            var requestGeneration = generation;
            IsBusy = true;
            try
            {
                var result = await anchorManager.TryAddAnchorAsync(new Pose(hit.pose.position, rotation));
                if (!result.status.IsSuccess() || result.value == null)
                { if (this != null) Notice?.Invoke("공간에 고정하지 못했습니다. 주변을 비추고 다시 눌러주세요."); return; }
                var created = result.value;
                if (this == null || !isActiveAndEnabled || requestGeneration != generation || !AllowPlacement
                    || ARSession.state != ARSessionState.SessionTracking || product == null)
                { ReleaseAnchor(created); return; }
                if (panel != null) panel.gameObject.SetActive(false);
                var previous = anchor;
                anchor = created;
                if (panel == null) panel = Instantiate(panelPrefab);
                panel.transform.SetParent(anchor.transform, false);
                panel.transform.localPosition = new Vector3(0f, 0.22f, 0f);
                panel.transform.localRotation = Quaternion.identity;
                panel.Bind(product);
                panel.gameObject.SetActive(PlacementTracking);
                ReleaseAnchor(previous);
                AllowPlacement = false;
                Placed?.Invoke();
                Debug.Log("[ProductAR] World panel placed at " + hit.pose.position);
            }
            catch (Exception error)
            {
                if (this != null) { Debug.LogWarning("[ProductAR] Anchor creation: " + error.Message); Notice?.Invoke("배치하지 못했습니다. 다시 눌러주세요."); }
            }
            finally { if (this != null) IsBusy = false; }
        }

        private void ReleaseAnchor(ARAnchor value)
        {
            if (value == null) return;
            if (anchorManager != null && anchorManager.isActiveAndEnabled && anchorManager.subsystem != null && anchorManager.TryRemoveAnchor(value)) return;
            Destroy(value.gameObject);
        }
    }
}
