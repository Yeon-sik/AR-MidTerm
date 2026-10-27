using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MidtermAR
{
    public readonly struct BarcodeViewportTransform
    {
        private readonly Vector2 origin, xAxis, yAxis;
        public bool IsValid => Finite(origin) && Finite(xAxis) && Finite(yAxis)
            && Mathf.Abs(xAxis.x * yAxis.y - xAxis.y * yAxis.x) > 0.00001f;

        // ARCore VIEW_NORMALIZED는 좌상단 기준, Unity viewport는 좌하단 기준이다.
        public BarcodeViewportTransform(Vector2 topLeft, Vector2 topRight, Vector2 bottomLeft)
        {
            origin = new Vector2(topLeft.x, 1f - topLeft.y);
            xAxis = new Vector2(topRight.x - topLeft.x, topLeft.y - topRight.y);
            yAxis = new Vector2(bottomLeft.x - topLeft.x, topLeft.y - bottomLeft.y);
        }

        public Vector2 Map(Vector2 imagePoint) => origin + xAxis * imagePoint.x + yAxis * imagePoint.y;
        private static bool Finite(Vector2 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);
    }

    // 네이티브 포인터는 카메라 프레임 콜백에서만 사용하고 작업 스레드로 넘기지 않는다.
    public sealed class ARCoreBarcodeCoordinates
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential)]
        private struct NativePointer { public int version; public IntPtr value; }
        private readonly float[] input = { 0, 0, 1, 0, 0, 1 };
        private readonly float[] output = new float[6];

        [DllImport("arcore_sdk_c", CallingConvention = CallingConvention.Cdecl)]
        private static extern void ArFrame_transformCoordinates2d(IntPtr session, IntPtr frame,
            int inputCoordinates, int vertexCount, [In] float[] vertices, int outputCoordinates, [Out] float[] transformed);
#endif

        public bool TryCapture(ARCameraManager manager, ARSession session, out BarcodeViewportTransform transform)
        {
            transform = default;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (manager.subsystem == null || session == null || session.subsystem == null
                || manager.subsystem.subsystemDescriptor.id != "ARCore-Camera") return false;
            var camera = manager.GetComponent<Camera>();
            var parameters = new XRCameraParams { zNear = camera.nearClipPlane, zFar = camera.farClipPlane,
                screenWidth = Screen.width, screenHeight = Screen.height, screenOrientation = Screen.orientation };
            if (!manager.subsystem.TryGetLatestFrame(parameters, out var frame)) return false;
            var sessionWrapper = session.subsystem.nativePtr;
            var frameWrapper = frame.nativePtr;
            if (sessionWrapper == IntPtr.Zero || frameWrapper == IntPtr.Zero
                || Marshal.ReadInt32(sessionWrapper) != 1 || Marshal.ReadInt32(frameWrapper) != 1) return false;
            var nativeSession = Marshal.PtrToStructure<NativePointer>(sessionWrapper).value;
            var nativeFrame = Marshal.PtrToStructure<NativePointer>(frameWrapper).value;
            if (nativeSession == IntPtr.Zero || nativeFrame == IntPtr.Zero) return false;
            for (var i = 0; i < output.Length; i++) output[i] = float.NaN;
            // ARCore C API: IMAGE_NORMALIZED=3 → VIEW_NORMALIZED=8. 회전·화면 잘림을 포함한다.
            ArFrame_transformCoordinates2d(nativeSession, nativeFrame, 3, 3, input, 8, output);
            transform = new BarcodeViewportTransform(new Vector2(output[0], output[1]),
                new Vector2(output[2], output[3]), new Vector2(output[4], output[5]));
            return transform.IsValid;
#else
            return false;
#endif
        }
    }
}
