using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace MidtermAR
{
    public sealed class ARReadiness : MonoBehaviour // 카메라 권한·AR 준비·센서 요청 수명 관리
    {
        [SerializeField] private ARSession session; // AR 공간 추적 세션
        [SerializeField] private ARCameraManager cameraManager; // 같은 AR 카메라의 권한·방향 제어
        public bool IsReady => !permissionBlocked && ARSession.state == ARSessionState.SessionTracking && cameraManager.permissionGranted;
        public bool CameraReady => !permissionBlocked && cameraManager.permissionGranted && CanReadCameraInState(ARSession.state);
        public static bool CanReadCameraInState(ARSessionState state) => state == ARSessionState.SessionInitializing || state == ARSessionState.SessionTracking;
        public bool IsInitializing { get; private set; }
        public bool PermissionBlocked => permissionBlocked;
        public string Status { get; private set; } = "AR 카메라를 준비하고 있습니다.";
        public string TrackingGuidance => ARSession.notTrackingReason switch
        {
            NotTrackingReason.InsufficientLight => "밝은 곳에서 책상과 주변 물체를 함께 비춰주세요.",
            NotTrackingReason.InsufficientFeatures => "무늬가 있는 책상과 주변 물체를 함께 비춰주세요.",
            NotTrackingReason.ExcessiveMotion => "휴대폰을 천천히 움직여 주세요.",
            _ => "주변을 천천히 비춰 공간 추적을 복구해 주세요."
        };
        private bool permissionBlocked;
        private bool permissionPending; // 권한 요청 응답 대기 여부
        private bool applicationPaused; // 백그라운드에서 센서 재등록 방지
        private bool started;
        private int initializationGeneration; // 이전 초기화의 늦은 권한 응답 무시
        private readonly AndroidMotionSampling motionSampling = new AndroidMotionSampling();

        private void OnEnable()
        {
            ARSession.stateChanged += OnStateChanged;
            if (started) Retry();
        }
        private void OnDisable()
        {
            ARSession.stateChanged -= OnStateChanged;
            initializationGeneration++;
            StopAllCoroutines();
            IsInitializing = false;
            permissionPending = false;
            motionSampling.SetActive(false);
        }
        private void OnDestroy() => motionSampling.Dispose();
        private void Start() { started = true; Retry(); }

        public void Retry()
        {
            if (isActiveAndEnabled && !IsInitializing) StartCoroutine(Initialize());
        }

        private IEnumerator Initialize()
        {
            IsInitializing = true;
            var requestGeneration = ++initializationGeneration;
            permissionBlocked = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                permissionPending = true;
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => { if (requestGeneration == initializationGeneration) permissionPending = false; };
                callbacks.PermissionDenied += _ =>
                { if (requestGeneration == initializationGeneration) { permissionPending = false; permissionBlocked = true; } };
                Permission.RequestUserPermission(Permission.Camera, callbacks);
                while (permissionPending) yield return null;
                permissionBlocked = !Permission.HasUserAuthorizedPermission(Permission.Camera);
                if (permissionBlocked)
                {
                    motionSampling.SetActive(false);
                    Status = "설정에서 카메라 권한을 허용한 뒤 돌아와 주세요.";
                    IsInitializing = false;
                    yield break;
                }
            }
#endif
            if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
                yield return ARSession.CheckAvailability();
            if (ARSession.state == ARSessionState.NeedsInstall)
            {
                Status = "Google Play Services for AR 설치를 확인하고 있습니다.";
                yield return ARSession.Install();
            }
            if (ARSession.state == ARSessionState.Unsupported || ARSession.state == ARSessionState.NeedsInstall)
            {
                motionSampling.SetActive(false);
                Status = "이 기기의 AR 지원과 Google Play Services for AR 설치를 확인해 주세요.";
            }
            else
            {
                // 권한 대화상자/AR 설치가 끝나도 앱이 배경이면 센서 및 세션 시작을 보류한다.
                while (applicationPaused) yield return null;
#if UNITY_ANDROID && !UNITY_EDITOR
                if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                {
                    permissionBlocked = true;
                    motionSampling.SetActive(false);
                    Status = "설정에서 카메라 권한을 허용한 뒤 돌아와 주세요.";
                    IsInitializing = false;
                    yield break;
                }
#endif
                cameraManager.requestedFacingDirection = CameraFacingDirection.World;
                session.enabled = true;
                RefreshMotionSampling();
                Status = "주변을 천천히 비춰 AR 추적을 시작해 주세요.";
            }
            IsInitializing = false;
        }

        private void OnStateChanged(ARSessionStateChangedEventArgs args)
        {
            Debug.Log("[ProductAR] AR Session: " + args.state + ", reason: " + ARSession.notTrackingReason + ", camera permission: " + cameraManager.permissionGranted);
            RefreshMotionSampling();
            if (permissionBlocked) return;
            if (args.state == ARSessionState.SessionTracking) Status = "AR 추적 중";
            else if (args.state == ARSessionState.SessionInitializing) Status = TrackingGuidance;
            else if (args.state == ARSessionState.Unsupported) Status = "이 기기에서는 AR을 사용할 수 없습니다.";
        }

        private void OnApplicationFocus(bool focused)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!focused) return;
            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                if (permissionBlocked) Retry();
                else RefreshMotionSampling();
            }
            else
            {
                permissionBlocked = true;
                motionSampling.SetActive(false);
                session.enabled = false;
                Status = "설정에서 카메라 권한을 허용한 뒤 돌아와 주세요.";
            }
#endif
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused) motionSampling.SetActive(false);
            else
            {
                OnApplicationFocus(true);
                RefreshMotionSampling();
            }
        }

        private void RefreshMotionSampling()
        {
            var state = ARSession.state;
            var available = state == ARSessionState.Ready || CanReadCameraInState(state);
            // 추적 복구 중에도 유지한다. Tracking 여부로 켜고 끄면 센서 요청 자체가 반복된다.
            motionSampling.SetActive(isActiveAndEnabled && !applicationPaused && !permissionBlocked
                && session != null && session.enabled && cameraManager != null && cameraManager.permissionGranted && available);
        }

        public void OpenSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS"))
            using (var uriClass = new AndroidJavaClass("android.net.Uri"))
            using (var uri = uriClass.CallStatic<AndroidJavaObject>("parse", "package:" + Application.identifier))
            {
                intent.Call<AndroidJavaObject>("setData", uri).Dispose();
                activity.Call("startActivity", intent);
            }
#else
            Retry();
#endif
        }
    }
}
