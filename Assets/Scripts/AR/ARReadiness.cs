using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace MidtermAR
{
    public sealed class ARReadiness : MonoBehaviour
    {
        [SerializeField] private ARSession session;
        [SerializeField] private ARCameraManager cameraManager;
        public bool IsReady => !permissionBlocked && ARSession.state == ARSessionState.SessionTracking && cameraManager.permissionGranted;
        public bool CameraReady => !permissionBlocked && cameraManager.permissionGranted && CanReadCameraInState(ARSession.state);
        public static bool CanReadCameraInState(ARSessionState state) => state == ARSessionState.SessionInitializing || state == ARSessionState.SessionTracking;
        public bool IsInitializing { get; private set; }
        public bool PermissionBlocked => permissionBlocked;
        public string Status { get; private set; } = "AR 카메라를 준비하고 있습니다.";
        private bool permissionBlocked;
        private bool permissionPending;

        private void OnEnable() { ARSession.stateChanged += OnStateChanged; }
        private void OnDisable() { ARSession.stateChanged -= OnStateChanged; }
        private void Start() { Retry(); }

        public void Retry()
        {
            if (!IsInitializing) StartCoroutine(Initialize());
        }

        private IEnumerator Initialize()
        {
            IsInitializing = true;
            permissionBlocked = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                permissionPending = true;
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => permissionPending = false;
                callbacks.PermissionDenied += _ => { permissionPending = false; permissionBlocked = true; };
                Permission.RequestUserPermission(Permission.Camera, callbacks);
                while (permissionPending) yield return null;
                permissionBlocked = !Permission.HasUserAuthorizedPermission(Permission.Camera);
                if (permissionBlocked)
                {
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
                Status = "이 기기의 AR 지원과 Google Play Services for AR 설치를 확인해 주세요.";
            else
            {
                cameraManager.requestedFacingDirection = CameraFacingDirection.World;
                session.enabled = true;
                Status = "주변을 천천히 비춰 AR 추적을 시작해 주세요.";
            }
            IsInitializing = false;
        }

        private void OnStateChanged(ARSessionStateChangedEventArgs args)
        {
            Debug.Log("[ProductAR] AR Session: " + args.state + ", camera permission: " + cameraManager.permissionGranted);
            if (permissionBlocked) return;
            if (args.state == ARSessionState.SessionTracking) Status = "AR 추적 중";
            else if (args.state == ARSessionState.SessionInitializing) Status = "주변을 천천히 비춰 공간 추적을 복구해 주세요.";
            else if (args.state == ARSessionState.Unsupported) Status = "이 기기에서는 AR을 사용할 수 없습니다.";
        }

        private void OnApplicationFocus(bool focused)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!focused || IsInitializing) return;
            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                if (permissionBlocked) Retry();
            }
            else
            {
                permissionBlocked = true;
                session.enabled = false;
                Status = "설정에서 카메라 권한을 허용한 뒤 돌아와 주세요.";
            }
#endif
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
