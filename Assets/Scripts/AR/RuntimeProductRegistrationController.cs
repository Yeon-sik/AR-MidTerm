using System;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MidtermAR
{
    public sealed class RuntimeProductRegistrationController : MonoBehaviour
    {
        [SerializeField] private ARCameraManager cameraManager;
        [SerializeField] private ARTrackedImageManager imageManager;
        [SerializeField] private Camera arCamera;
        [SerializeField] private ARSession session;
        [SerializeField] private TMP_FontAsset uiFont;
        private readonly ARCoreBarcodeCoordinates coordinates = new ARCoreBarcodeCoordinates();
        private readonly RegistrationLifecycle lifecycle = new RegistrationLifecycle();
        private RegistrationLifecycle.Attempt attempt;
        private AddReferenceImageJobState addJob;
        private MutableRuntimeReferenceImageLibrary library;
        private RuntimeProductRegistrationView view;
        private Texture2D capturedTexture;
        private bool captureRequested, jobScheduled, paused, subscribed;
        private float requestedAt;
        private float lastCameraFrame = float.NegativeInfinity;
        private Vector2Int requestedScreen;
        private ScreenOrientation requestedOrientation;
        private string referenceName;

        public event Action<string> ImageAdded;
        public event Action StateChanged;
        public bool IsActive => lifecycle.IsActive;
        public bool IsBusy => captureRequested || lifecycle.IsBusy;
        public string Status { get; private set; } = "";
        public const string RestartNotice = "취소한 사진이 이미 처리됐어요. 다시 등록하려면 앱을 완전히 종료한 뒤 실행해주세요.";
        public bool HasProcessedProduct(string productKey) => lifecycle.HasNativeImage(productKey);
        public static Rect GuideViewport => new Rect(.16f, .25f, .68f, .5f);
        public static float? RequestedPhysicalWidthMetres => null;

        public void Configure(ARCameraManager camera, ARTrackedImageManager images, Camera viewCamera,
            ARSession arSession, TMP_FontAsset font)
        {
            Unsubscribe();
            cameraManager = camera;
            imageManager = images;
            arCamera = viewCamera;
            session = arSession;
            uiFont = font;
            if (isActiveAndEnabled) Subscribe();
        }

        public bool CanBegin(string productKey) => isActiveAndEnabled && !paused && lifecycle.CanBegin(productKey)
            && cameraManager != null && imageManager != null && arCamera != null && session != null && uiFont != null;

        public bool Begin(string productKey, string productLabel)
        {
            if (!CanBegin(productKey))
            {
                if (lifecycle.HasNativeImage(productKey))
                    SetStatus("이 상품의 앞면은 이번 실행에서 이미 등록했어요. 다시 등록하려면 앱을 완전히 종료한 뒤 실행해주세요.");
                return false;
            }
            if (!lifecycle.Begin(productKey, productLabel)) return false;
            EnsureView();
            SetStatus(RuntimeProductRegistrationView.ReadyInstruction);
            RefreshView();
            StateChanged?.Invoke();
            return true;
        }

        public void RequestCapture()
        {
            if (!IsActive || IsBusy) return;
            if (!Ready) { SetStatus("책상과 주변을 천천히 비춰 공간 추적을 준비해주세요."); return; }
            captureRequested = true;
            requestedAt = Time.unscaledTime;
            requestedScreen = new Vector2Int(Screen.width, Screen.height);
            requestedOrientation = Screen.orientation;
            SetStatus("앞면을 촬영하고 있어요. 잠시 그대로 유지해주세요.");
            StateChanged?.Invoke();
        }

        public void Cancel()
        {
            if (!IsActive && !IsBusy) return;
            captureRequested = false;
            lifecycle.Cancel();
            SetStatus(jobScheduled ? "등록을 취소했어요. 진행 중인 작업이 끝날 때까지 잠시 기다려주세요." : "앞면 등록을 취소했어요.");
            RefreshView();
            StateChanged?.Invoke();
        }

        private bool Ready => !paused && cameraManager != null && cameraManager.permissionGranted
            && session != null && ARSession.state == ARSessionState.SessionTracking
            && imageManager != null && imageManager.isActiveAndEnabled
            && imageManager.subsystem != null && imageManager.subsystem.running
            && Time.unscaledTime - lastCameraFrame < 1f;

        private void OnEnable() => Subscribe();
        private void OnDisable() { Unsubscribe(); Cancel(); }
        private void Subscribe()
        {
            if (subscribed || cameraManager == null) return;
            cameraManager.frameReceived += OnCameraFrame;
            subscribed = true;
        }
        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (cameraManager != null) cameraManager.frameReceived -= OnCameraFrame;
            subscribed = false;
        }

        private void OnApplicationPause(bool value)
        {
            paused = value;
            lastCameraFrame = float.NegativeInfinity;
            if (value) Cancel();
        }

        private void OnCameraFrame(ARCameraFrameEventArgs args)
        {
            if (paused) return;
            lastCameraFrame = Time.unscaledTime;
            if (!captureRequested || !IsActive || !Ready) return;
            captureRequested = false;
            if (requestedScreen != new Vector2Int(Screen.width, Screen.height) || requestedOrientation != Screen.orientation)
            { SetStatus("화면 방향이 바뀌었어요. 앞면을 틀에 다시 맞추고 촬영해주세요."); StateChanged?.Invoke(); return; }
            try
            {
                EnsureMutableLibrary();
                if (arCamera.rect != new Rect(0, 0, 1, 1)) throw new InvalidOperationException("A full-screen AR camera is required.");
                if (!coordinates.TryCapture(cameraManager, session, out var transform))
                    throw new InvalidOperationException("CPU image mapping is unavailable.");
                if (!cameraManager.TryAcquireLatestCpuImage(out var cpuImage))
                    throw new InvalidOperationException("No CPU camera frame is available.");
                using (cpuImage)
                {
                    if (!cpuImage.FormatSupported(TextureFormat.RGB24)) throw new NotSupportedException("RGB24 camera conversion is unavailable.");
                    var reduction = Mathf.Min(1f, 1600f / Mathf.Max(cpuImage.width, cpuImage.height));
                    var sourceWidth = Mathf.Max(1, Mathf.RoundToInt(cpuImage.width * reduction));
                    var sourceHeight = Mathf.Max(1, Mathf.RoundToInt(cpuImage.height * reduction));
                    var conversion = new XRCpuImage.ConversionParams
                    {
                        inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height),
                        outputDimensions = new Vector2Int(sourceWidth, sourceHeight),
                        outputFormat = TextureFormat.RGB24,
                        transformation = XRCpuImage.Transformation.None
                    };
                    using var converted = new NativeArray<byte>(cpuImage.GetConvertedDataSize(conversion), Allocator.Temp);
                    cpuImage.Convert(conversion, converted);
                    var guidePixels = new Vector2(GuideViewport.width * Screen.width, GuideViewport.height * Screen.height);
                    var outputScale = Mathf.Min(1f, 1000f / Mathf.Max(guidePixels.x, guidePixels.y));
                    var outputWidth = Mathf.Max(1, Mathf.RoundToInt(guidePixels.x * outputScale));
                    var outputHeight = Mathf.Max(1, Mathf.RoundToInt(guidePixels.y * outputScale));
                    var pixels = RuntimeImageRegistrationCapture.CropRgb24(converted.ToArray(), sourceWidth, sourceHeight,
                        transform, GuideViewport, outputWidth, outputHeight);
                    capturedTexture = new Texture2D(outputWidth, outputHeight, TextureFormat.RGB24, false);
                    capturedTexture.LoadRawTextureData(pixels);
                    capturedTexture.Apply(false, false);
                    referenceName = "session-front-" + Guid.NewGuid().ToString("N");
                    attempt = lifecycle.StartJob();
                    addJob = library.ScheduleAddImageWithValidationJob(capturedTexture, referenceName, RequestedPhysicalWidthMetres);
                    jobScheduled = true;
                    SetStatus("사진의 특징을 확인하고 앞면을 등록하고 있어요.");
                    Debug.Log($"[ProductAR Registration] add scheduled; image={referenceName}; source={cpuImage.width}x{cpuImage.height}; "
                        + $"crop={outputWidth}x{outputHeight}; specifiedSize=false; viewport={GuideViewport}; cpuTimestamp={cpuImage.timestamp:F6}");
                }
            }
            catch (Exception error)
            {
                if (attempt != null && !jobScheduled) lifecycle.Complete(attempt, false);
                ReleaseCaptureTexture();
                SetStatus("앞면을 등록하지 못했어요. 카메라가 준비되면 다시 촬영해주세요.");
                Debug.LogWarning("[ProductAR Registration] capture/add failed: " + error.Message);
            }
            StateChanged?.Invoke();
        }

        private void EnsureMutableLibrary()
        {
            if (!imageManager.descriptor.supportsMutableLibrary)
                throw new NotSupportedException("Mutable image libraries are unavailable.");
            if (library != null && library.supportsValidation && ReferenceEquals(imageManager.referenceLibrary, library)) return;
            var current = imageManager.referenceLibrary;
            if (current == null) throw new InvalidOperationException("The original reference library is missing.");
            var originalCount = current.count;
            library = current as MutableRuntimeReferenceImageLibrary;
            if (library == null && current is XRReferenceImageLibrary serialized)
            {
                library = imageManager.CreateRuntimeLibrary(serialized) as MutableRuntimeReferenceImageLibrary;
                if (library != null) imageManager.referenceLibrary = library;
            }
            if (library == null || !library.supportsValidation || library.count != originalCount)
                throw new InvalidOperationException("A validated library preserving the original images is required.");
        }

        private void Update()
        {
            if (IsActive) ReadPointerInput();
            if (captureRequested && Time.unscaledTime - requestedAt > 4f)
            {
                captureRequested = false;
                SetStatus("카메라 사진을 받지 못했어요. 잠시 후 다시 촬영해주세요.");
                StateChanged?.Invoke();
            }
            if (jobScheduled && addJob.jobHandle.IsCompleted) CompleteJob();
            RefreshView();
        }

        private void CompleteJob()
        {
            addJob.jobHandle.Complete();
            var result = addJob.status;
            jobScheduled = false;
            var publish = lifecycle.Complete(attempt, result == AddReferenceImageJobStatus.Success);
            Debug.Log($"[ProductAR Registration] add completed; image={referenceName}; status={result}; current={publish}; libraryCount={library?.count}");
            ReleaseCaptureTexture();
            if (publish)
            {
                SetStatus("앞면을 등록했어요. 촬영할 때의 모양을 유지하고 앞면 전체를 정면에서 비춰주세요.");
                RefreshView();
                try { ImageAdded?.Invoke(referenceName); }
                finally { StateChanged?.Invoke(); }
                return;
            }
            if (IsActive) SetStatus(result == AddReferenceImageJobStatus.ErrorInvalidImage
                ? "무늬가 더 많고 반사가 적은 평평한 면을 촬영해주세요."
                : "앞면을 등록하지 못했어요. 다른 사진으로 다시 촬영해주세요.");
            else SetStatus(result == AddReferenceImageJobStatus.Success
                ? RestartNotice
                : "취소한 등록 작업이 끝났어요.");
            StateChanged?.Invoke();
        }

        private void ReadPointerInput()
        {
            var touch = Touchscreen.current?.primaryTouch;
            if (touch != null && touch.press.wasPressedThisFrame)
            { HandlePointer(touch.position.ReadValue()); return; }
            var mouse = Mouse.current;
            if ((touch == null || !touch.press.isPressed) && mouse != null && mouse.leftButton.wasPressedThisFrame)
                HandlePointer(mouse.position.ReadValue());
        }

        private void HandlePointer(Vector2 position)
        {
            var normalized = new Vector2(position.x / Screen.width, position.y / Screen.height);
            if (RuntimeProductRegistrationView.CancelArea.Contains(normalized)) Cancel();
            else if (!IsBusy && Ready && RuntimeProductRegistrationView.CaptureArea.Contains(normalized)) RequestCapture();
        }

        private void EnsureView()
        {
            if (view != null) return;
            var ui = new GameObject("Front Registration", typeof(RectTransform));
            ui.transform.SetParent(transform, false);
            view = ui.AddComponent<RuntimeProductRegistrationView>();
            view.Initialize(uiFont);
        }

        private void RefreshView()
        {
            if (view == null) return;
            view.gameObject.SetActive(IsActive);
            if (IsActive) view.Refresh(lifecycle.ProductLabel, Ready, IsBusy, Status);
        }

        private void SetStatus(string value) => Status = value;

        private void ReleaseCaptureTexture()
        {
            if (capturedTexture == null) return;
            Destroy(capturedTexture);
            capturedTexture = null;
        }

        private void OnDestroy()
        {
            // The native job owns a copied buffer; also keep its source texture alive until completion.
            if (jobScheduled) { addJob.jobHandle.Complete(); lifecycle.Complete(attempt, addJob.status == AddReferenceImageJobStatus.Success); }
            ReleaseCaptureTexture();
        }
    }
}
