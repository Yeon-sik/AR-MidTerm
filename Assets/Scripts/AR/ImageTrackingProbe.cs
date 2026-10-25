using System;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MidtermAR
{
    // 메인 스레드에서 이미지의 현재 자세만 사용한다. 상품 조회·바코드 흐름은 실행하지 않는다.
    public sealed class ImageTrackingProbe : MonoBehaviour
    {
        public const string TargetName = "test-card";
        [SerializeField] private ARTrackedImageManager imageManager;
        [SerializeField] private ARSession session;
        [SerializeField] private ARReadiness readiness;
        [SerializeField] private ARCameraManager cameraManager;
        [SerializeField] private Transform cube;
        [SerializeField] private TMP_Text statusText;
        private bool paused;
        private bool visible;
        private bool cameraFresh;
        private int acquisitions;
        private float nextPoseLog;
        private string previousStatus;
        private readonly ARCoreImageObservation observation = new ARCoreImageObservation();

        // 가림 뒤 LAST_KNOWN_POSE를 상품의 현재 자세로 사용하지 않는다.
        public static bool CanDisplay(bool ready, bool paused, TrackingState state, int method, bool expectedImage)
            => ImageTrackingPolicy.CanDisplay(ready, paused, state, method, expectedImage);

        private void OnEnable()
        {
            cameraFresh = false;
            cameraManager.frameReceived += OnCameraFrame;
            Debug.Log("[ImageProbe] Ready; target=" + TargetName + "; build=" + Application.version);
        }

        private void OnCameraFrame(ARCameraFrameEventArgs args) { if (!paused) cameraFresh = true; }

        private void LateUpdate()
        {
            ARTrackedImage selected = null;
            var method = -1;
            if (!paused && cameraFresh && readiness.IsReady && imageManager.subsystem != null && imageManager.subsystem.running)
            {
                foreach (var tracked in imageManager.trackables)
                {
                    if (tracked.referenceImage.name != TargetName) continue;
                    method = observation.GetMethod(session, tracked);
                    if (!CanDisplay(readiness.IsReady, paused, tracked.trackingState, method, true)) continue;
                    selected = tracked;
                    break;
                }
            }

            if (selected != null)
            {
                // 참조 사진의 실측 폭을 꾸며 넣지 않는다. 추정된 이미지 폭에 비례한 큐브다.
                var edge = Mathf.Clamp(selected.size.x * 0.18f, 0.015f, 0.08f);
                // 큐브 수명은 이 컴포넌트가 소유한다. 트래커 객체가 제거돼도 함께 파괴되지 않는다.
                cube.position = selected.transform.TransformPoint(new Vector3(0, edge * 0.5f + 0.003f, 0));
                cube.rotation = selected.transform.rotation;
                cube.localScale = Vector3.one * edge;
                if (!visible) acquisitions++;
                SetVisible(true);
                SetStatus("추적 중 · 파란 큐브가 마커에 붙어 있습니다.\n상품과 휴대폰을 각각 이동·회전해 주세요.\n인식/재획득 " + acquisitions + "회 · 크기는 추정값");
                if (Time.unscaledTime >= nextPoseLog)
                {
                    nextPoseLog = Time.unscaledTime + 1f;
                    Debug.Log("[ImageProbe] FULL_TRACKING; id=" + selected.trackableId
                        + "; position=" + selected.transform.position.ToString("F3")
                        + "; rotation=" + selected.transform.eulerAngles.ToString("F1") + "; estimatedSize=" + selected.size);
                }
            }
            else
            {
                SetVisible(false);
                var text = !readiness.IsReady ? readiness.Status
                    : observation.Unavailable ? "추적 상태를 확인하지 못했습니다. 화면을 유지하고 로그를 확인해 주세요."
                    : acquisitions > 0 ? "마커를 놓쳤습니다. 큐브를 숨겼습니다.\n같은 사진을 다시 비춰 주세요."
                    : "브이젤 카드 사진을 비춰 주세요.\n카드 전체가 보이게 천천히 접근해 주세요.";
                SetStatus(text + "\n인식/재획득 " + acquisitions + "회 · 상태 " + method);
            }
        }

        private void SetVisible(bool value)
        {
            if (visible != value) Debug.Log("[ImageProbe] Cube visible=" + value + "; acquisitions=" + acquisitions);
            visible = value;
            if (cube != null) cube.gameObject.SetActive(value);
        }

        private void SetStatus(string text)
        {
            if (previousStatus == text) return;
            previousStatus = text;
            statusText.text = text;
        }

        public void Retry() => readiness.Retry();
        public void OpenSettings() => readiness.OpenSettings();
        private void OnApplicationPause(bool value) { paused = value; cameraFresh = false; SetVisible(false); }
        private void OnDisable() { cameraManager.frameReceived -= OnCameraFrame; SetVisible(false); }
        private void OnDestroy() { if (cube != null) Destroy(cube.gameObject); }
    }

    // Unity ARCore의 version 1 포인터를 빌려 읽는다. 보관·해제하거나 다른 스레드로 넘기지 않는다.
    public sealed class ARCoreImageObservation
    {
        public bool Unavailable { get; private set; }
#if UNITY_ANDROID && !UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential)]
        private struct NativePointer { public int version; public IntPtr value; }

        [DllImport("arcore_sdk_c", CallingConvention = CallingConvention.Cdecl)]
        private static extern void ArAugmentedImage_getTrackingMethod(IntPtr session, IntPtr image, out int method);
#endif

        public int GetMethod(ARSession session, ARTrackedImage image)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Unavailable || session.subsystem == null || session.subsystem.subsystemDescriptor.id != "ARCore-Session") return -1;
            var sessionWrapper = session.subsystem.nativePtr;
            var imageWrapper = image.nativePtr;
            if (sessionWrapper == IntPtr.Zero || imageWrapper == IntPtr.Zero
                || Marshal.ReadInt32(sessionWrapper) != 1 || Marshal.ReadInt32(imageWrapper) != 1) return -1;
            var nativeSession = Marshal.PtrToStructure<NativePointer>(sessionWrapper).value;
            var nativeImage = Marshal.PtrToStructure<NativePointer>(imageWrapper).value;
            if (nativeSession == IntPtr.Zero || nativeImage == IntPtr.Zero) return -1;
            try
            {
                ArAugmentedImage_getTrackingMethod(nativeSession, nativeImage, out var method);
                return method;
            }
            catch (Exception error) when (error is DllNotFoundException || error is EntryPointNotFoundException)
            {
                Unavailable = true;
                Debug.LogError("[ImageProbe] Native tracking method unavailable: " + error.Message);
            }
#endif
            return -1;
        }
    }
}
