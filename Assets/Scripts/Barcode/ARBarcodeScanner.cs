using System;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MidtermAR
{
    public sealed class ARBarcodeScanner : MonoBehaviour // AR 카메라 프레임 획득과 비동기 판독 조율
    {
        [SerializeField] private ARCameraManager cameraManager;
        [SerializeField] private ARSession session;
        [SerializeField, Range(0.2f, 1f)] private float interval = 0.35f;
        [SerializeField, Range(640, 1920)] private int longestEdge = 1920;
        [SerializeField] private float timeoutSeconds = 15f; // 한 번의 스캔 제한 시간
        [SerializeField] private bool captureViewportCoordinates = true; // 이미지 추적 정보판은 EAN 식별만 사용한다.

        public event Action<BarcodeReadResult> Decoded;
        public event Action TimedOut;
        public event Action<BarcodeReadResult, BarcodeViewportTransform, float> Observed;
        public bool IsScanning { get; private set; }
        public string FollowingBarcode { get; private set; }
        public bool HasViewportTransform { get; private set; }
        public int ConvertedFrames { get; private set; }
        public bool HasCameraFrame => Time.unscaledTime - lastFrameTime < 2.5f;
        public int ConfirmationCount => confirmation.Count;

        private readonly BarcodeDecoder decoder = new BarcodeDecoder();
        private readonly BarcodeConfirmation confirmation = new BarcodeConfirmation();
        private readonly ARCoreBarcodeCoordinates coordinates = new ARCoreBarcodeCoordinates();
        private NativeArray<byte> conversionBuffer; // CPU 이미지 변환에 재사용하는 네이티브 버퍼
        private byte[] workerPixels; // 작업 스레드에서 읽는 관리 배열
        private Task<BarcodeReadResult> pending; // 동시에 하나만 실행하는 판독 작업
        private float nextCapture;
        private float scanStarted;
        private int generation; // 스캔 재시작 전의 판독 결과 무시
        private int pendingGeneration; // 진행 중인 판독 작업의 스캔 세대
        private float pendingCapturedAt;
        private BarcodeViewportTransform pendingTransform;
        private Vector2Int pendingViewportSize;
        private bool coordinateErrorLogged;
        private bool coordinateMappingLogged;
        private bool conversionErrorLogged;
        private bool continuous;
        private float lastFrameTime = float.NegativeInfinity;
        private bool configurationAttempted; // 카메라 해상도 설정의 반복 적용 방지
        private Vector2Int lastImageSize;
        private int scanFrames;
        private string diagnosticDirectory;
        private double lastImageTimestamp = double.NegativeInfinity; // 동일 카메라 이미지의 중복 확인 방지

        private void OnEnable()
        {
            diagnosticDirectory = System.IO.Path.Combine(Application.persistentDataPath, "barcode-diagnostics");
            cameraManager.frameReceived += OnCameraFrame;
        }
        private void OnDisable()
        {
            cameraManager.frameReceived -= OnCameraFrame;
            SetScanning(false);
        }
        private void OnDestroy() { if (conversionBuffer.IsCreated) conversionBuffer.Dispose(); }

        public void SetScanning(bool value)
        {
            continuous = false;
            if (IsScanning == value && FollowingBarcode == null) return;
            IsScanning = value;
            FollowingBarcode = null;
            generation++;
            confirmation.Reset();
            if (value) { scanStarted = Time.unscaledTime; nextCapture = scanStarted; scanFrames = 0; }
        }

        // 두 인식 경로가 서로를 기다리지 않도록 시간 제한 없이 관측한다.
        public void SetContinuous(bool value)
        {
            if (continuous && value && IsScanning) return;
            SetScanning(value);
            continuous = value;
        }

        public void SetFollowing(string barcode)
        {
            continuous = false;
            if (FollowingBarcode == barcode && !IsScanning) return;
            IsScanning = false;
            FollowingBarcode = barcode;
            generation++;
            confirmation.Reset();
            nextCapture = Time.unscaledTime;
            scanFrames = 0;
        }

        private void Update()
        {
            if (pending != null && pending.IsCompleted)
            {
                var finished = pending;
                var validGeneration = pendingGeneration == generation;
                pending = null;
                if (finished.IsFaulted)
                {
                    confirmation.Reset();
                    Debug.LogWarning("[ProductAR] Barcode worker failed: " + finished.Exception?.GetBaseException().Message);
                }
                else if (!finished.IsCanceled && (IsScanning || FollowingBarcode != null) && validGeneration)
                {
                    var result = finished.Result;
                    if (IsScanning && confirmation.Observe(result, pendingCapturedAt))
                    {
                        Debug.Log("[ProductAR] Barcode confirmed across " + confirmation.Count + " camera frames: " + confirmation.Candidate);
                        Decoded?.Invoke(result);
                    }
                    var sameViewport = pendingViewportSize == new Vector2Int(Screen.width, Screen.height);
                    Observed?.Invoke(result, sameViewport ? pendingTransform : default, pendingCapturedAt);
                }
            }
            if (IsScanning && !continuous && Time.unscaledTime - scanStarted >= timeoutSeconds)
            {
                SetScanning(false);
                TimedOut?.Invoke();
            }
        }

        private void OnCameraFrame(ARCameraFrameEventArgs args)
        {
            lastFrameTime = Time.unscaledTime;
            if (!configurationAttempted && ConfigureCamera()) return;
            if ((!IsScanning && FollowingBarcode == null) || pending != null || Time.unscaledTime < nextCapture || !ARReadiness.CanReadCameraInState(ARSession.state)) return;
            nextCapture = Time.unscaledTime + (FollowingBarcode == null ? interval : 0.25f);
            if (!cameraManager.TryAcquireLatestCpuImage(out var image)) return;
            try
            {
                if (image.timestamp <= lastImageTimestamp) return;
                lastImageTimestamp = image.timestamp;
                var scale = Mathf.Min(1f, (float)longestEdge / Mathf.Max(image.width, image.height));
                var width = Mathf.Max(1, Mathf.RoundToInt(image.width * scale));
                var height = Mathf.Max(1, Mathf.RoundToInt(image.height * scale));
                var format = image.FormatSupported(TextureFormat.R8) ? TextureFormat.R8 : TextureFormat.RGBA32;
                var parameters = new XRCpuImage.ConversionParams
                {
                    inputRect = new RectInt(0, 0, image.width, image.height),
                    outputDimensions = new Vector2Int(width, height),
                    outputFormat = format,
                    transformation = XRCpuImage.Transformation.None
                };
                var size = image.GetConvertedDataSize(parameters);
                if (!conversionBuffer.IsCreated || conversionBuffer.Length != size)
                {
                    if (conversionBuffer.IsCreated) conversionBuffer.Dispose();
                    conversionBuffer = new NativeArray<byte>(size, Allocator.Persistent);
                    workerPixels = new byte[size];
                }
                image.Convert(parameters, conversionBuffer);
                conversionBuffer.CopyTo(workerPixels);
                ConvertedFrames++;
                pendingGeneration = generation;
                pendingCapturedAt = Time.unscaledTime;
                pendingViewportSize = new Vector2Int(Screen.width, Screen.height);
                pendingTransform = default;
                try
                {
                    if (captureViewportCoordinates && !coordinates.TryCapture(cameraManager, session, out pendingTransform) && !coordinateErrorLogged)
                    { Debug.LogWarning("[ProductAR] Barcode viewport transform unavailable; follow panel remains hidden until coordinates are ready."); coordinateErrorLogged = true; }
                }
                catch (Exception error)
                {
                    if (!coordinateErrorLogged) Debug.LogWarning("[ProductAR] Barcode viewport transform: " + error.Message);
                    coordinateErrorLogged = true;
                }
                HasViewportTransform = pendingTransform.IsValid;
                if (HasViewportTransform && !coordinateMappingLogged)
                {
                    coordinateMappingLogged = true;
                    Debug.Log("[ProductAR] ARCore CPU image → viewport mapping ready; origin=" + pendingTransform.Map(Vector2.zero)
                        + "; x=" + pendingTransform.Map(Vector2.right) + "; y=" + pendingTransform.Map(Vector2.up));
                }
                var pixels = workerPixels;
                var rgba = format == TextureFormat.RGBA32;
                var sampleSlot = ++scanFrames;
                var scanGeneration = generation;
                var sourceTimestamp = image.timestamp;
                pending = Task.Run(() =>
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var result = decoder.Decode(pixels, width, height, rgba);
                    timer.Stop();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    if (!rgba && sampleSlot <= 8)
                    {
                        try { BarcodeFrameDiagnostics.Save(diagnosticDirectory, sampleSlot, scanGeneration, sourceTimestamp, pixels, width, height, result?.Text, timer.Elapsed.TotalMilliseconds); }
                        catch (Exception error) { Debug.LogWarning("[ProductAR] Diagnostic frame save: " + error.Message); }
                    }
                    if (sampleSlot <= 8 || sampleSlot % 10 == 0)
                        Debug.Log($"[ProductAR] Decode attempt {sampleSlot}: {width}x{height}, {timer.Elapsed.TotalMilliseconds:F1}ms, result={result?.Text ?? "none"}");
#endif
                    return result;
                });
                if (lastImageSize != new Vector2Int(image.width, image.height))
                {
                    lastImageSize = new Vector2Int(image.width, image.height);
                    Debug.Log($"[ProductAR] CPU image source {image.width}x{image.height}; decoder {width}x{height}, {format}; autofocus={cameraManager.autoFocusEnabled}");
                }
            }
            catch (Exception error)
            {
                if (!conversionErrorLogged) Debug.LogWarning("[ProductAR] CPU conversion failed: " + error.Message);
                conversionErrorLogged = true;
            }
            finally { image.Dispose(); }
        }

        private bool ConfigureCamera()
        {
            configurationAttempted = true;
            try
            {
                cameraManager.autoFocusRequested = true;
                using (var configurations = cameraManager.GetConfigurations(Allocator.Temp))
                {
                    var bestIndex = -1;
                    var bestScore = 0;
                    for (var i = 0; i < configurations.Length; i++)
                    {
                        var c = configurations[i];
                        Debug.Log($"[ProductAR] Supported CPU camera configuration: {c.width}x{c.height}, fps={c.framerate}");
                        var score = CameraResolutionPolicy.Score(c.width, c.height, c.framerate);
                        if (score > bestScore) { bestScore = score; bestIndex = i; }
                    }
                    if (bestIndex >= 0 && cameraManager.currentConfiguration != configurations[bestIndex])
                    {
                        cameraManager.currentConfiguration = configurations[bestIndex];
                        Debug.Log("[ProductAR] Requested barcode camera configuration: " + configurations[bestIndex]);
                        return true;
                    }
                }
            }
            catch (Exception error) { Debug.LogWarning("[ProductAR] Camera configuration fallback: " + error.Message); }
            return false;
        }
    }
}
