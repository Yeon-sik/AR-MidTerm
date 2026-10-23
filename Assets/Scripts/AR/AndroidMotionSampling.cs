using System;
using UnityEngine;

namespace MidtermAR
{
    // ARReadiness가 소유한다. 센서 값은 읽지 않고 Android 센서 요청 수명만 관리한다.
    public sealed class AndroidMotionSampling : IDisposable
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject bridge;
        private bool attempted;
        private bool active;
#endif

        public void SetActive(bool requested)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!requested) { Stop(); return; }
            if (attempted) return;
            attempted = true;
            try
            {
                if (bridge == null)
                {
                    using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                        bridge = new AndroidJavaObject("com.yeonsik.midtermar.MotionSampling", activity);
                }
                active = bridge.Call<bool>("start");
                Debug.Log("[ProductAR] Motion sampling request 100 Hz; both sensors registered=" + active);
            }
            catch (Exception error)
            {
                Debug.LogWarning("[ProductAR] Motion sampling unavailable: " + error.Message);
                // Java start()은 등록 중 예외가 생겨도 이미 등록한 리스너를 해제한다.
                active = false;
            }
#endif
        }

        private void Stop()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!attempted) return;
            try { bridge?.Call("stop"); }
            catch (Exception error) { Debug.LogWarning("[ProductAR] Motion sampling stop: " + error.Message); }
            finally
            {
                if (active) Debug.Log("[ProductAR] Motion sampling stopped");
                active = false;
                attempted = false;
            }
#endif
        }

        public void Dispose()
        {
            Stop();
#if UNITY_ANDROID && !UNITY_EDITOR
            bridge?.Dispose();
            bridge = null;
#endif
        }
    }
}
