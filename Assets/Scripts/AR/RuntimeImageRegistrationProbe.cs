#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
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
        private readonly ARCoreBarcodeCoordinates coordinates = new ARCoreBarcodeCoordinates();
        private readonly ARCoreImageObservation observation = new ARCoreImageObservation();
        private readonly RuntimeImageRegistrationEvidence evidence = new RuntimeImageRegistrationEvidence();
        private MutableRuntimeReferenceImageLibrary library;
        private AddReferenceImageJobState addJob;
        private Texture2D preview;
        private bool captureRequested, adding, paused, subscribed;
        private int generation, jobGeneration, originalImageCount;
        private string pendingName;
        private string status = "Wait for AR tracking. Fill the guide with a flat, detailed package front.";
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
        public enum PointerAction { None, Capture, Cancel }

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

        private void OnEnable() => Subscribe();
        private void OnDisable() { Unsubscribe(); Cancel(); }

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
            if (adding || captureRequested) return;
            if (!Ready) { SetStatus("AR is not ready. Move the phone slowly and retry."); return; }
            generation++;
            evidence.Reset();
            captureRequested = true;
            requestedAt = Time.unscaledTime;
            requestedScreen = new Vector2Int(Screen.width, Screen.height);
            requestedOrientation = Screen.orientation;
            SetStatus("Capturing the next camera frame...");
        }

        public void Cancel()
        {
            generation++;
            captureRequested = false;
            evidence.Reset();
            // Native add jobs cannot be cancelled. Ignore their result, then release resources on completion.
            SetStatus(adding ? "Cancelled. Waiting for the native add job to finish..." : "Cancelled. No tracking result is selected.");
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
            if (!captureRequested || !Ready) return;
            captureRequested = false;
            if (requestedScreen != new Vector2Int(Screen.width, Screen.height) || requestedOrientation != Screen.orientation)
            { SetStatus("Screen orientation changed. Align the guide and capture again."); return; }
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
                    SetStatus("Checking image quality and adding it... This is not recognition success yet.");
                    Debug.Log($"[RuntimeRegistrationProbe] add scheduled; name={pendingName}; crop={outputWidth}x{outputHeight}; "
                        + $"source={cpuImage.width}x{cpuImage.height}; specifiedSize=false; originalImages={originalImageCount}; "
                        + $"viewport={GuideViewport}; cpuTimestamp={cpuImage.timestamp:F6}");
                }
            }
            catch (Exception error)
            {
                SetStatus("Capture/add failed: " + error.Message);
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
            { captureRequested = false; SetStatus("No camera frame arrived. Check AR readiness and retry."); }
            if (adding && addJob.jobHandle.IsCompleted)
            {
                addJob.jobHandle.Complete();
                adding = false;
                var result = addJob.status;
                Debug.Log("[RuntimeRegistrationProbe] add completed; status=" + result + "; name=" + pendingName
                    + "; resultCurrent=" + (jobGeneration == generation && !paused));
                if (jobGeneration != generation || paused)
                    SetStatus("Cancelled add job finished. Its image is ignored for this probe.");
                else if (result == AddReferenceImageJobStatus.Success)
                {
                    evidence.Registered(pendingName);
                    SetStatus("Image added. Aim at the SAME front. Move the phone slightly to estimate its size.");
                }
                else SetStatus(result == AddReferenceImageJobStatus.ErrorInvalidImage
                    ? "Image quality rejected. Use a flat face with more detail and less glare."
                    : "Image was not added: " + result + ". Retry with another capture.");
            }
            if (!Ready || Time.unscaledTime - lastImageUpdate > .75f) evidence.Hide();
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
                    if (Ready && !adding && !captureRequested) RequestCapture();
                    break;
                case PointerAction.Cancel:
                    if (captureRequested || adding || RegistrationAdded) Cancel();
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
            if (string.IsNullOrEmpty(evidence.ImageName) || image.referenceImage.name != evidence.ImageName) return;
            var wasFirst = evidence.FirstTracking;
            var wasLost = evidence.ObservedLoss;
            var wasReacquired = evidence.Reacquired;
            var method = session == null ? -1 : observation.GetMethod(session, image);
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
            if (evidence.Reacquired) SetStatus("REACQUISITION CONFIRMED after observed loss. Check the size and pose log.");
            else if (evidence.ObservedLoss) SetStatus("Loss observed. Bring the SAME front back into view.");
            else if (evidence.FirstTracking) SetStatus("First FULL_TRACKING confirmed. Move the package completely OUT of view, then bring it back.");
        }

        private void SetStatus(string text)
        {
            if (status == text) return;
            status = text;
            Debug.Log("[RuntimeRegistrationProbe] " + text);
        }

        private void OnGUI()
        {
            var textSize = Mathf.Clamp(Screen.width / 32, 18, 40);
            var style = new GUIStyle(GUI.skin.label) { fontSize = textSize, wordWrap = true };
            var button = new GUIStyle(GUI.skin.button) { fontSize = textSize };
            var top = new Rect(12, Screen.safeArea.yMin + 12, Screen.width - 24, Screen.height * .22f);
            GUI.Box(top, GUIContent.none);
            GUI.Label(new Rect(top.x + 12, top.y + 8, top.width - 24, top.height - 16),
                "RUNTIME IMAGE REGISTRATION PROBE\n" + status + "\nAR: " + ARSession.state
                + " | current tracking: " + evidence.Visible + "\nEstimated size: " + evidence.EstimatedSize.ToString("F4")
                + " m | position: " + trackedPosition.ToString("F2"), style);
            var guide = GuideViewport;
            var rect = new Rect(guide.x * Screen.width, (1 - guide.yMax) * Screen.height,
                guide.width * Screen.width, guide.height * Screen.height);
            var previous = GUI.color;
            GUI.color = evidence.Visible ? Color.green : Color.yellow;
            const float edge = 4;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, edge), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - edge, rect.width, edge), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, edge, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - edge, rect.y, edge, rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
            if (preview != null) GUI.DrawTexture(new Rect(12, Screen.height * .78f, Screen.width * .22f, Screen.height * .16f), preview, ScaleMode.ScaleToFit);
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            GUI.enabled = Ready && !adding && !captureRequested;
            GUI.Box(CaptureButtonRect(screenSize), "Capture front", button);
            GUI.enabled = captureRequested || adding || RegistrationAdded;
            GUI.Box(CancelButtonRect(screenSize), "Cancel / reset probe", button);
            GUI.enabled = true;
        }

        private void OnDestroy()
        {
            if (adding) addJob.jobHandle.Complete();
            if (preview != null) Destroy(preview);
        }
    }
}
#endif
