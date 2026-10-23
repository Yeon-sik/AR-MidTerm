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
        [SerializeField] private ARPlaneManager planeManager; // 배치 가능한 현실 수평면 감지
        [SerializeField] private ARRaycastManager raycastManager; // 화면 터치와 현실 평면의 교점 계산
        [SerializeField] private ARAnchorManager anchorManager; // 정보판의 현실 공간 고정점 관리
        [SerializeField] private Camera arCamera;
        [SerializeField] private WorldProductPanel panelPrefab; // 앵커 아래에 생성할 정보판 원본
        public event Action Placed;
        public event Action<string> Notice;
        public bool AllowPlacement { get; set; }
        public bool IsBusy { get; private set; }
        public bool HasPlacement => anchor != null && panel != null;
        public bool HasTrackedPlane { get; private set; }
        public bool PlacementTracking => HasPlacement && anchor.trackingState == TrackingState.Tracking && ARSession.state == ARSessionState.SessionTracking;
        private readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private ARAnchor anchor; // 현재 정보판을 고정하는 앵커 하나
        private WorldProductPanel panel;
        private ProductSnapshot product;
        private int generation; // 취소된 비동기 배치 결과 무시
        private float nextVisualUpdate; // 평면 표시 갱신 간격 제한
        private int lastTrackedPlaneCount = -1; // 평면 수가 바뀔 때만 로그 출력
        private bool? lastPlacementTracking;

        public void SetProduct(ProductSnapshot value)
        {
            ClearPlacement();
            product = value;
            planeManager.enabled = value != null;
            RefreshPlaneVisuals();
            Debug.Log("[ProductAR] Plane detection " + (value != null ? "enabled for " + value.name : "disabled"));
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
        private void OnDisable()
        {
            generation++;
            if (panel != null) panel.gameObject.SetActive(false);
            RefreshPlaneVisuals();
        }

        private void Update()
        {
            var tracking = PlacementTracking;
            if (panel != null) panel.gameObject.SetActive(tracking);
            if (lastPlacementTracking != tracking)
            {
                lastPlacementTracking = tracking;
                Debug.Log("[ProductAR] Placement tracking=" + tracking + "; anchor=" + (anchor != null ? anchor.trackableId.ToString() : "none"));
            }
            if (Time.unscaledTime >= nextVisualUpdate)
            {
                nextVisualUpdate = Time.unscaledTime + 0.2f;
                RefreshPlaneVisuals();
            }
            if (!AllowPlacement || IsBusy || product == null || ARSession.state != ARSessionState.SessionTracking) return;
            if (!TryGetPressedPosition(out var screenPosition)) return;
            if (IsOverUI(screenPosition))
            { Debug.Log("[ProductAR] Placement touch skipped over UI at " + screenPosition); return; }
            Place(screenPosition);
        }

        private void RefreshPlaneVisuals()
        {
            if (planeManager == null) return;
            var count = 0;
            var show = isActiveAndEnabled && planeManager.enabled && AllowPlacement && ARSession.state == ARSessionState.SessionTracking;
            foreach (var plane in planeManager.trackables)
            {
                var valid = IsUsablePlane(plane);
                if (valid) count++;
                SetPlaneVisualization(plane, valid && show);
            }
            HasTrackedPlane = planeManager.enabled && count > 0;
            if (lastTrackedPlaneCount == count) return;
            lastTrackedPlaneCount = count;
            Debug.Log("[ProductAR] Usable horizontal planes=" + count);
        }

        private static bool IsUsablePlane(ARPlane plane) => plane != null && plane.subsumedBy == null
            && plane.trackingState == TrackingState.Tracking && plane.alignment == PlaneAlignment.HorizontalUp;

        private static void SetPlaneVisualization(ARPlane plane, bool visible)
        {
            // 메시 갱신 컴포넌트가 매 프레임 렌더러를 제어하므로 표시 책임도 함께 켜고 끈다.
            if (plane.TryGetComponent<ARPlaneMeshVisualizer>(out var visualizer)) visualizer.enabled = visible;
            else if (plane.TryGetComponent<MeshRenderer>(out var renderer)) renderer.enabled = visible;
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
            {
                Debug.Log("[ProductAR] Placement raycast missed at " + screenPosition);
                Notice?.Invoke("아직 표면을 찾지 못했습니다. 책상을 천천히 비춘 뒤 다시 눌러주세요.");
                return;
            }
            var hitIndex = hits.FindIndex(candidate => IsUsablePlane(planeManager.GetPlane(candidate.trackableId)));
            if (hitIndex < 0)
            {
                Debug.Log("[ProductAR] Placement raycast found no usable horizontal plane; hits=" + hits.Count);
                Notice?.Invoke("이 위치는 아직 배치할 수 없습니다. 표시된 책상 표면을 눌러주세요.");
                return;
            }
            var hit = hits[hitIndex];
            var forward = Vector3.ProjectOnPlane(hit.pose.position - arCamera.transform.position, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(arCamera.transform.forward, Vector3.up);
            var rotation = forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity;
            var requestGeneration = generation;
            IsBusy = true;
            try
            {
                var result = await anchorManager.TryAddAnchorAsync(new Pose(hit.pose.position, rotation));
                if (!result.status.IsSuccess() || result.value == null)
                {
                    Debug.LogWarning("[ProductAR] Anchor creation failed; status=" + result.status + "; plane=" + hit.trackableId + "; position=" + hit.pose.position);
                    if (this != null) Notice?.Invoke("공간에 고정하지 못했습니다. 주변을 비추고 다시 눌러주세요.");
                    return;
                }
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
                RefreshPlaneVisuals();
                Placed?.Invoke();
                Debug.Log("[ProductAR] World panel placed; anchor=" + created.trackableId + "; plane=" + hit.trackableId + "; position=" + hit.pose.position);
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
