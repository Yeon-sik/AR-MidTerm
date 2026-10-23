using System;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MidtermAR
{
    public sealed class ARBarcodeScanner : MonoBehaviour
    {
        [SerializeField] private ARCameraManager cameraManager;
        [SerializeField, Range(0.2f, 1f)] private float interval = 0.35f;
        [SerializeField, Range(640, 1920)] private int longestEdge = 1920;
        [SerializeField] private float timeoutSeconds = 15f;

        public event Action<BarcodeReadResult> Decoded;
        public event Action TimedOut;
        public bool IsScanning { get; private set; }
        public int ConvertedFrames { get; private set; }
        public bool HasCameraFrame => Time.unscaledTime - lastFrameTime < 2.5f;
        public int ConfirmationCount => confirmation.Count;

        private readonly BarcodeDecoder decoder = new BarcodeDecoder();
        private readonly BarcodeConfirmation confirmation = new BarcodeConfirmation();
        private NativeArray<byte> conversionBuffer;
        private byte[] workerPixels;
        private Task<BarcodeReadResult> pending;
        private float nextCapture;
        private float scanStarted;
        private int generation;
        private int pendingGeneration;
        private float pendingCapturedAt;
        private bool conversionErrorLogged;
        private float lastFrameTime = float.NegativeInfinity;
        private bool configurationAttempted;
        private Vector2Int lastImageSize;
        private int scanFrames;
        private string diagnosticDirectory;
        private double lastImageTimestamp = double.NegativeInfinity;

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
            if (IsScanning == value) return;
            IsScanning = value;
            generation++;
            confirmation.Reset();
            if (value) { scanStarted = Time.unscaledTime; nextCapture = scanStarted; scanFrames = 0; }
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
                else if (!finished.IsCanceled && IsScanning && validGeneration)
                {
                    var confirmed = confirmation.Observe(finished.Result, pendingCapturedAt);
                    if (confirmation.Count == 1)
                        Debug.Log("[ProductAR] Unconfirmed barcode candidate: " + confirmation.Candidate);
                    if (confirmed)
                    {
                        Debug.Log("[ProductAR] Barcode confirmed across " + confirmation.Count + " camera frames: " + confirmation.Candidate);
                        Decoded?.Invoke(new BarcodeReadResult(confirmation.Candidate, "EAN_13"));
                    }
                }
            }
            if (IsScanning && Time.unscaledTime - scanStarted >= timeoutSeconds)
            {
                SetScanning(false);
                TimedOut?.Invoke();
            }
        }

        private void OnCameraFrame(ARCameraFrameEventArgs args)
        {
            lastFrameTime = Time.unscaledTime;
            if (!configurationAttempted && ConfigureCamera()) return;
            if (!IsScanning || pending != null || Time.unscaledTime < nextCapture || !ARReadiness.CanReadCameraInState(ARSession.state)) return;
            nextCapture = Time.unscaledTime + interval;
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
