#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MidtermAR
{
    // Development-only feasibility probe. It never changes the product catalog or normal app routing.
    public sealed class RuntimeImageRegistrationProbe : MonoBehaviour
    {
        [SerializeField] private ARCameraManager cameraManager;
        [SerializeField] private ARTrackedImageManager imageManager;
        [SerializeField] private Camera arCamera;
        [SerializeField] private ARSession session;
        [SerializeField] private TMP_FontAsset uiFont;
        private readonly ARCoreBarcodeCoordinates coordinates = new ARCoreBarcodeCoordinates();
        private readonly ARCoreImageObservation observation = new ARCoreImageObservation();
        private readonly RuntimeImageRegistrationEvidence evidence = new RuntimeImageRegistrationEvidence();
        private readonly Dictionary<string, string> detectedImageStates = new Dictionary<string, string>();
        private readonly Dictionary<string, float> nextDetectedImageLogs = new Dictionary<string, float>();
        private MutableRuntimeReferenceImageLibrary library;
        private AddReferenceImageJobState addJob;
        private Texture2D preview;
        private RuntimeImageRegistrationProbeView view;
        private bool captureRequested, adding, paused, subscribed, hasNativeImageAdded;
        private int generation, jobGeneration, originalImageCount;
        private string pendingName;
        private string status = "평평하고 무늬가 있는 앞면을 틀에 채운 뒤 한 번만 촬영해주세요.";
        private string lastCameraDiagnostics;
        private int cameraTextureCount;
        private float nextCameraDiagnostics;
        private float lastCameraFrame = float.NegativeInfinity;
        private float lastImageUpdate = float.NegativeInfinity;
        private float nextPoseLog;
        private float requestedAt;
        private Vector2Int requestedScreen;
        private ScreenOrientation requestedOrientation;
        private Vector3 trackedPosition;

        // Normalized Unity viewport coordinates. The capture uses precisely this on-screen region.
        public static Rect GuideViewport => new Rect(.16f, .25f, .68f, .5f);
        public string Status => status;
        public bool RegistrationAdded => !string.IsNullOrEmpty(evidence.ImageName);
        public bool ReacquisitionConfirmed => evidence.Reacquired;
        private bool CanCapture => CanStartCapture(Ready, adding, captureRequested, hasNativeImageAdded);
        private bool CanReset => !hasNativeImageAdded && (captureRequested || adding);
        public enum PointerAction { None, Capture, Cancel }

        public static bool CanStartCapture(bool ready, bool adding, bool capturing, bool registered)
            => ready && !adding && !capturing && !registered;

        public void SetUiFont(TMP_FontAsset font) => uiFont = font;

        private void Start()
        {
            if (uiFont == null) { Debug.LogError("[RuntimeRegistrationProbe] Noto Korean UI font is missing."); return; }
            var ui = new GameObject("Registration Instructions", typeof(RectTransform));
            ui.transform.SetParent(transform, false);
            view = ui.AddComponent<RuntimeImageRegistrationProbeView>();
            view.Initialize(uiFont);
            RefreshView();
        }

        private static Rect CaptureButtonRect(Vector2Int size)
            => new Rect(size.x * .27f, size.y * .8f, size.x * .69f, size.y * .075f);
        private static Rect CancelButtonRect(Vector2Int size)
            => new Rect(size.x * .27f, size.y * .89f, size.x * .69f, size.y * .065f);

        public static PointerAction ActionAtPointer(Vector2 screenPoint, Vector2Int screenSize)
        {
            if (screenSize.x <= 0 || screenSize.y <= 0 || screenPoint.x < 0 || screenPoint.y < 0
                || screenPoint.x > screenSize.x || screenPoint.y > screenSize.y)
                return PointerAction.None;
            // Input System positions use bottom-left; the OnGUI drawing rectangles use top-left.
            var guiPoint = new Vector2(screenPoint.x, screenSize.y - screenPoint.y);
            if (CaptureButtonRect(screenSize).Contains(guiPoint)) return PointerAction.Capture;
            if (CancelButtonRect(screenSize).Contains(guiPoint)) return PointerAction.Cancel;
            return PointerAction.None;
        }

        public void Configure(ARCameraManager camera, ARTrackedImageManager images, Camera view)
        {
            Unsubscribe();
            cameraManager = camera;
            imageManager = images;
            arCamera = view;
            if (isActiveAndEnabled) Subscribe();
        }

        public void Configure(ARCameraManager camera, ARTrackedImageManager images, Camera view, ARSession arSession)
        {
            session = arSession;
            Configure(camera, images, view);
        }

        private bool Ready => !paused && cameraManager != null && cameraManager.permissionGranted
            && session != null && ARSession.state == ARSessionState.SessionTracking
            && imageManager != null && imageManager.isActiveAndEnabled
            && imageManager.subsystem != null && imageManager.subsystem.running
            && Time.unscaledTime - lastCameraFrame < 1f;

        private void OnEnable() { Subscribe(); if (view != null) view.gameObject.SetActive(true); }
        private void OnDisable() { Unsubscribe(); Cancel(); if (view != null) view.gameObject.SetActive(false); }

        private void Subscribe()
        {
            if (subscribed || cameraManager == null || imageManager == null) return;
            cameraManager.frameReceived += OnCameraFrame;
            imageManager.trackablesChanged.AddListener(OnImagesChanged);
            subscribed = true;
            Debug.Log("[RuntimeRegistrationProbe] enabled; version=" + Application.version);
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (cameraManager != null) cameraManager.frameReceived -= OnCameraFrame;
            if (imageManager != null) imageManager.trackablesChanged.RemoveListener(OnImagesChanged);
            subscribed = false;
        }

        public void RequestCapture()
        {
            if (adding || captureRequested || hasNativeImageAdded) return;
            if (!Ready) { SetStatus("주변을 천천히 비춰 공간 추적을 준비한 뒤 촬영해주세요."); return; }
            generation++;
            evidence.Reset();
            captureRequested = true;
            requestedAt = Time.unscaledTime;
            requestedScreen = new Vector2Int(Screen.width, Screen.height);
            requestedOrientation = Screen.orientation;
            SetStatus("카메라의 앞면 사진을 가져오고 있어요. 잠시만 유지해주세요.");
        }

        public void Cancel()
        {
            generation++;
            captureRequested = false;
            evidence.Reset();
            // Native add jobs cannot be cancelled. Ignore their result, then release resources on completion.
            SetStatus(adding ? "촬영을 취소했어요. 처리 중인 작업이 끝나면 다시 촬영할 수 있어요."
                : "이번 확인을 초기화했어요. 앞면을 틀에 맞추고 새로 촬영해주세요.");
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
            cameraTextureCount = args.textures?.Count ?? 0;
            LogCameraDiagnostics("frame-state-change");
            if (!captureRequested || !Ready) return;
            captureRequested = false;
            LogCameraDiagnostics("before-capture", true);
            if (requestedScreen != new Vector2Int(Screen.width, Screen.height) || requestedOrientation != Screen.orientation)
            { SetStatus("화면 방향이 바뀌었어요. 앞면을 틀에 다시 맞추고 촬영해주세요."); return; }
            try
            {
                if (arCamera == null || arCamera.rect != new Rect(0, 0, 1, 1))
                    throw new InvalidOperationException("The probe requires a full-screen AR camera.");
                EnsureMutableLibrary();
                if (!coordinates.TryCapture(cameraManager, session, out var transform))
                    throw new InvalidOperationException("CPU image to viewport mapping is unavailable.");
                if (!cameraManager.TryAcquireLatestCpuImage(out var cpuImage))
                    throw new InvalidOperationException("No camera CPU image is available. Retry capture.");
                using (cpuImage)
                {
                    if (!cpuImage.FormatSupported(TextureFormat.RGB24))
                        throw new NotSupportedException("Camera RGB24 conversion is unavailable.");
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
                    if (preview != null) Destroy(preview);
                    preview = new Texture2D(outputWidth, outputHeight, TextureFormat.RGB24, false);
                    preview.LoadRawTextureData(pixels);
                    preview.Apply(false, false);
                    pendingName = "runtime-probe-" + Guid.NewGuid().ToString("N");
                    jobGeneration = generation;
                    // null means unknown physical size; no fabricated measurement enters this probe.
                    addJob = library.ScheduleAddImageWithValidationJob(preview, pendingName, null);
                    adding = true;
                    SetStatus("사진의 인식 가능 여부를 확인하고 있어요. 아직 실물 인식이 확인된 단계는 아니에요.");
                    Debug.Log($"[RuntimeRegistrationProbe] add scheduled; name={pendingName}; crop={outputWidth}x{outputHeight}; "
                        + $"source={cpuImage.width}x{cpuImage.height}; specifiedSize=false; originalImages={originalImageCount}; "
                        + $"viewport={GuideViewport}; cpuTimestamp={cpuImage.timestamp:F6}");
                }
            }
            catch (Exception error)
            {
                SetStatus("앞면을 등록하지 못했어요. 잠시 후 다시 촬영해주세요.");
                Debug.LogWarning("[RuntimeRegistrationProbe] " + error);
            }
        }

        private void EnsureMutableLibrary()
        {
            if (!imageManager.descriptor.supportsMutableLibrary)
                throw new NotSupportedException("This device has no mutable image library support.");
            if (library != null && ReferenceEquals(imageManager.referenceLibrary, library)) return;
            var current = imageManager.referenceLibrary;
            if (current == null) throw new InvalidOperationException("A pre-registered image library must be present.");
            originalImageCount = current.count;
            library = current as MutableRuntimeReferenceImageLibrary;
            if (library == null && current is XRReferenceImageLibrary serialized)
            {
                library = imageManager.CreateRuntimeLibrary(serialized) as MutableRuntimeReferenceImageLibrary;
                if (library != null) imageManager.referenceLibrary = library;
            }
            if (library == null || !library.supportsValidation)
                throw new NotSupportedException("Validated runtime image addition is unavailable.");
            if (library.count != originalImageCount)
                throw new InvalidOperationException("The original image library was not preserved.");
            Debug.Log("[RuntimeRegistrationProbe] mutable library ready; preservedOriginalCount=" + originalImageCount);
        }

        private void Update()
        {
            ReadPointerInput();
            if (captureRequested && Time.unscaledTime - requestedAt > 4f)
            { captureRequested = false; SetStatus("카메라 사진을 받지 못했어요. 카메라가 준비되면 다시 촬영해주세요."); }
            if (adding && addJob.jobHandle.IsCompleted)
            {
                addJob.jobHandle.Complete();
                adding = false;
                var result = addJob.status;
                // The native library cannot remove a successful add, even if its UI request was cancelled.
                // A second capture of the same print would create competing references and invalidate this trial.
                if (result == AddReferenceImageJobStatus.Success) hasNativeImageAdded = true;
                Debug.Log("[RuntimeRegistrationProbe] add completed; status=" + result + "; name=" + pendingName
                    + "; resultCurrent=" + (jobGeneration == generation && !paused));
                LogCameraDiagnostics("after-registration", true);
                if (jobGeneration != generation || paused)
                    SetStatus("취소한 작업이 끝났어요. 앞면을 틀에 맞추고 새로 촬영해주세요.");
                else if (result == AddReferenceImageJobStatus.Success)
                {
                    evidence.Registered(pendingName);
                    SetStatus("앞면 사진이 등록됐어요. 다시 촬영하지 말고 같은 앞면을 계속 비춰주세요.");
                }
                else SetStatus(result == AddReferenceImageJobStatus.ErrorInvalidImage
                    ? "인식하기 어려운 사진이에요. 반사가 적고 무늬가 많은 평평한 면을 촬영해주세요."
                    : "앞면을 등록하지 못했어요. 다른 사진으로 다시 촬영해주세요.");
            }
            if (!Ready || Time.unscaledTime - lastImageUpdate > .75f) evidence.Hide();
            RefreshView();
        }

        private void ReadPointerInput()
        {
            var touch = Touchscreen.current?.primaryTouch;
            if (touch != null && touch.press.wasPressedThisFrame)
            {
                HandlePointer(touch.position.ReadValue());
                return; // A touch can also synthesize a mouse press; process the physical input once.
            }
            var mouse = Mouse.current;
            if ((touch == null || !touch.press.isPressed) && mouse != null && mouse.leftButton.wasPressedThisFrame)
                HandlePointer(mouse.position.ReadValue());
        }

        private void HandlePointer(Vector2 position)
        {
            switch (ActionAtPointer(position, new Vector2Int(Screen.width, Screen.height)))
            {
                case PointerAction.Capture:
                    if (CanCapture) RequestCapture();
                    break;
                case PointerAction.Cancel:
                    if (CanReset) Cancel();
                    break;
            }
        }

        private void OnImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> changes)
        {
            foreach (var image in changes.added) Observe(image);
            foreach (var image in changes.updated) Observe(image);
            foreach (var removed in changes.removed)
                if (removed.Value != null && removed.Value.referenceImage.name == evidence.ImageName)
                {
                    evidence.Observe(evidence.ImageName, Ready, TrackingState.None, 0, Vector2.zero);
                    SetEvidenceStatus();
                }
        }

        private void Observe(ARTrackedImage image)
        {
            var method = session == null ? -1 : observation.GetMethod(session, image);
            LogDetectedImage(image, method);
            if (string.IsNullOrEmpty(evidence.ImageName) || image.referenceImage.name != evidence.ImageName) return;
            var wasFirst = evidence.FirstTracking;
            var wasLost = evidence.ObservedLoss;
            var wasReacquired = evidence.Reacquired;
            evidence.Observe(image.referenceImage.name, Ready, image.trackingState, method, image.size);
            lastImageUpdate = Time.unscaledTime;
            trackedPosition = image.transform.position;
            SetEvidenceStatus();
            if (wasFirst != evidence.FirstTracking || wasLost != evidence.ObservedLoss || wasReacquired != evidence.Reacquired
                || evidence.Visible && Time.unscaledTime >= nextPoseLog)
            {
                nextPoseLog = Time.unscaledTime + 1f;
                Debug.Log($"[RuntimeRegistrationProbe] observation; name={evidence.ImageName}; state={image.trackingState}; "
                    + $"method={method}; validFullTracking={evidence.Visible}; estimatedSize={image.size.ToString("F4")}; "
                    + $"position={trackedPosition.ToString("F3")}; rotation={image.transform.eulerAngles.ToString("F1")}; "
                    + $"first={evidence.FirstTracking}; loss={evidence.ObservedLoss}; reacquired={evidence.Reacquired}");
            }
        }

        private void SetEvidenceStatus()
        {
            if (evidence.Reacquired) SetStatus("화면 밖으로 뺐던 같은 앞면을 다시 인식했어요.");
            else if (evidence.ObservedLoss) SetStatus("상품이 사라진 것을 확인했어요. 같은 앞면을 다시 비춰주세요.");
            else if (evidence.FirstTracking) SetStatus("앞면을 처음 인식했어요. 상품을 완전히 화면 밖으로 빼주세요.");
        }

        private void SetStatus(string text)
        {
            if (status == text) return;
            status = text;
            Debug.Log("[RuntimeRegistrationProbe] " + text);
        }

        private void RefreshView()
        {
            if (view != null) view.Refresh(Ready, adding, captureRequested, evidence, status, preview,
                CanCapture, CanReset, hasNativeImageAdded);
        }

        private void LogDetectedImage(ARTrackedImage image, int method)
        {
            var name = image.referenceImage.name ?? "unnamed";
            var state = $"state={image.trackingState}; method={method}";
            if (detectedImageStates.TryGetValue(name, out var previous) && previous == state) return;
            if (nextDetectedImageLogs.TryGetValue(name, out var next) && Time.unscaledTime < next) return;
            detectedImageStates[name] = state;
            nextDetectedImageLogs[name] = Time.unscaledTime + 1f;
            Debug.Log($"[RuntimeRegistrationProbe] detected image; name={name}; {state}; "
                + $"selectedTrial={name == evidence.ImageName}; size={image.size.ToString("F4")}");
        }

        private void LogCameraDiagnostics(string reason, bool force = false)
        {
            var background = cameraManager.GetComponent<ARCameraBackground>();
            var snapshot = $"session={ARSession.state}; notTrackingReason={ARSession.notTrackingReason}; "
                + $"backgroundEnabled={background != null && background.enabled}; "
                + $"backgroundRenderingEnabled={background != null && background.backgroundRenderingEnabled}; "
                + $"renderingMode={(background == null ? "missing" : background.currentRenderingMode.ToString())}; "
                + $"materialPresent={background != null && background.material != null}; textures={cameraTextureCount}; "
                + $"configuration={cameraManager.currentConfiguration?.ToString() ?? "none"}";
            if (!force && (snapshot == lastCameraDiagnostics || Time.unscaledTime < nextCameraDiagnostics)) return;
            lastCameraDiagnostics = snapshot;
            nextCameraDiagnostics = Time.unscaledTime + 1f;
            Debug.Log("[RuntimeRegistrationProbe] camera diagnostics; reason=" + reason + "; " + snapshot);
        }

        private void OnDestroy()
        {
            if (adding) addJob.jobHandle.Complete();
            if (preview != null) Destroy(preview);
        }
    }
}
#endif
