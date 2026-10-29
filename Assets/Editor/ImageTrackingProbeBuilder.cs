using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARSubsystems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;

namespace MidtermAR.Editor
{
    public static class ImageTrackingProbeBuilder
    {
        public const string ScenePath = "Assets/Scenes/ImageTrackingProbe.unity";
        public const string LibraryPath = "Assets/Images/ProbeImageLibrary.asset";
        public const string MarkerPath = "Assets/Images/test-card.jpg";
        public const string PackageId = ProjectBuilder.PackageId;

        [MenuItem("Product AR/Image Tracking Probe/Prepare Scene")]
        public static void PrepareScene()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(MarkerPath);
            if (texture == null) throw new InvalidOperationException("Probe marker is missing: " + MarkerPath);
            var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            if (library.count == 0) library.Add();
            while (library.count > 1) library.RemoveAt(library.count - 1);
            // 기존 라이브러리도 갱신해야 마커를 교체한 재빌드에 이전 이미지가 남지 않는다.
            library.ClearDataStore();
            library.SetName(0, ImageTrackingProbe.TargetName);
            library.SetTexture(0, texture, false);
            library.SetSpecifySize(0, false); // 화면 표시·출력 크기가 정해지지 않았으므로 ARCore가 추정한다.
            EditorUtility.SetDirty(library);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Generated/NotoSansKR SDF.asset");
            if (font == null) throw new InvalidOperationException("Existing Korean font is missing.");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/URP-Performant-Renderer.asset");
            if (!renderer.rendererFeatures.OfType<ARBackgroundRendererFeature>().Any())
                throw new InvalidOperationException("AR background renderer feature is missing.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var session = new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager)).GetComponent<ARSession>();
            session.attemptUpdate = true;
            session.enabled = false;
            var origin = new GameObject("XR Origin", typeof(XROrigin)).GetComponent<XROrigin>();
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(origin.transform, false);
            var cameraObject = new GameObject("AR Camera", typeof(Camera), typeof(ARCameraManager), typeof(ARCameraBackground), typeof(TrackedPoseDriver));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(offset.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 30;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var driver = cameraObject.GetComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(new InputAction("AR Position", InputActionType.Value, "<HandheldARInputDevice>/devicePosition"));
            driver.rotationInput = new InputActionProperty(new InputAction("AR Rotation", InputActionType.Value, "<HandheldARInputDevice>/deviceRotation"));
            driver.trackingStateInput = new InputActionProperty(new InputAction("AR Tracking", InputActionType.Value, "<HandheldARInputDevice>/trackingState"));
            origin.Camera = camera;
            origin.CameraFloorOffsetObject = offset;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = 0;
            var images = origin.gameObject.AddComponent<ARTrackedImageManager>();
            images.referenceLibrary = library;
            images.requestedMaxNumberOfMovingImages = 1;
            var appObject = new GameObject("Image Tracking Probe", typeof(ARReadiness), typeof(ImageTrackingProbe));
            var ready = appObject.GetComponent<ARReadiness>();
            var probe = appObject.GetComponent<ImageTrackingProbe>();
            Assign(ready, "session", session);
            Assign(ready, "cameraManager", cameraObject.GetComponent<ARCameraManager>());
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Tracked Blue Cube";
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
            const string materialPath = "Assets/Images/ProbeCube.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor", new Color32(32, 143, 246, 255));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            cube.GetComponent<Renderer>().sharedMaterial = material;
            cube.SetActive(false);
            Assign(probe, "imageManager", images);
            Assign(probe, "session", session);
            Assign(probe, "readiness", ready);
            Assign(probe, "cameraManager", cameraObject.GetComponent<ARCameraManager>());
            Assign(probe, "cube", cube.transform);

            var canvasObject = new GameObject("Probe Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            var safe = Rect("Safe Area", canvasObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeArea>();
            var top = Rect("Status Plate", safe, new Vector2(0, 1), Vector2.one, new Vector2(24, -330), new Vector2(-24, -24));
            top.gameObject.AddComponent<Image>().color = new Color32(18, 34, 43, 240);
            Label("Title", top, font, "이미지 추적 검증 · 큐브 1개", 42, new Vector2(24, -78), new Vector2(-24, -16));
            var status = Label("Status", top, font, "AR 카메라를 준비하고 있습니다.", 34, new Vector2(24, -280), new Vector2(-24, -90));
            Assign(probe, "statusText", status);
            var bottom = Rect("Instructions", safe, Vector2.zero, new Vector2(1, 0), new Vector2(24, 24), new Vector2(-24, 310));
            bottom.gameObject.AddComponent<Image>().color = new Color32(18, 34, 43, 240);
            Label("Hint", bottom, font, "① 사진 인식  ② 이동·회전  ③ 가림·재인식\n추적용 사진이며 상품 인식 기능은 아닙니다.", 31, new Vector2(24, -142), new Vector2(-24, -16));
            AddButton(bottom, font, "다시 준비", new Vector2(24, 16), new Vector2(-528, 128), probe.Retry);
            AddButton(bottom, font, "권한 설정", new Vector2(528, 16), new Vector2(-24, 128), probe.OpenSettings);
            new GameObject("Event System", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[ImageProbe Build] Prepared one-image probe scene; original ProductAR scene unchanged.");
        }

        public static void BuildAndroid()
        {
            PrepareScene();
            PlayerSettings.companyName = "Yeonsik";
            PlayerSettings.productName = "이미지 추적 검증";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.bundleVersion = "0.1.1";
            PlayerSettings.Android.bundleVersionCode = 2;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel30;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.runInBackground = false;
            // 기존 앱과 호환되는 로컬 Android 디버그 키를 명시한다. 키 파일은 저장소에 넣지 않는다.
            var debugKey = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".android", "debug.keystore");
            if (!File.Exists(debugKey)) throw new FileNotFoundException("Existing Android debug key is required.", debugKey);
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = debugKey;
            PlayerSettings.Android.keystorePass = "android";
            PlayerSettings.Android.keyaliasName = "androiddebugkey";
            PlayerSettings.Android.keyaliasPass = "android";
            Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = "Builds/Android/ImageTrackingProbe.apk",
                target = BuildTarget.Android, options = BuildOptions.Development
            });
            Debug.Log($"[ImageProbe Build] {report.summary.result}; errors={report.summary.totalErrors}");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Image probe Android build failed.");
        }

        private static void Assign(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            return rect;
        }

        private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string value, int size, Vector2 min, Vector2 max)
        {
            var rect = Rect(name, parent, new Vector2(0, 1), Vector2.one, min, max);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSize = size; text.text = value;
            text.color = Color.white; text.raycastTarget = false;
            return text;
        }

        private static void AddButton(Transform parent, TMP_FontAsset font, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(label, parent, Vector2.zero, new Vector2(1, 0), min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color32(37, 126, 92, 255);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            UnityEventTools.AddPersistentListener(button.onClick, action);
            var textRect = Rect("Label", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = label; text.fontSize = 34; text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white; text.raycastTarget = false;
        }
    }
}
