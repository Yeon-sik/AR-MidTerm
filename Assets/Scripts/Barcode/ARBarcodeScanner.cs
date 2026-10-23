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
        [SerializeField, Range(320, 1600)] private int longestEdge = 960;
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

        private void OnEnable() { cameraManager.frameReceived += OnCameraFrame; }
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
            if (value) { scanStarted = Time.unscaledTime; nextCapture = scanStarted; }
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
            if (!IsScanning || pending != null || Time.unscaledTime < nextCapture || !ARReadiness.CanReadCameraInState(ARSession.state)) return;
            nextCapture = Time.unscaledTime + interval;
            if (!cameraManager.TryAcquireLatestCpuImage(out var image)) return;
            try
            {
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
                pending = Task.Run(() => decoder.Decode(pixels, width, height, rgba));
                if (ConvertedFrames == 1) Debug.Log($"[ProductAR] CPU image conversion available: {width}x{height}, {format}");
            }
            catch (Exception error)
            {
                if (!conversionErrorLogged) Debug.LogWarning("[ProductAR] CPU conversion failed: " + error.Message);
                conversionErrorLogged = true;
            }
            finally { image.Dispose(); }
        }
    }
}
