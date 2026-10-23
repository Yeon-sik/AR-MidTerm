using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;

namespace MidtermAR.Editor
{
    public static class ProjectBuilder
    {
        public const string ScenePath = "Assets/Scenes/ProductAR.unity";
        public const string PanelPath = "Assets/Prefabs/ProductInfoPanel.prefab";
        public const string PackageId = "com.yeonsik.midtermar";
        private static readonly Color Ink = new Color32(18, 34, 43, 255);
        private static readonly Color Paper = new Color32(249, 248, 241, 255);
        private static readonly Color Green = new Color32(37, 126, 92, 255);

        [MenuItem("Product AR/Prepare Project and Scene")]
        public static void PrepareProject() // 참조가 연결된 실행 씬·프리팹 생성
        {
            Directory.CreateDirectory("Assets/Prefabs");
            Directory.CreateDirectory("Assets/Fonts/Generated");
            AssetDatabase.Refresh();
            ConfigureAndroid();
            EnsureRenderer();
            var font = EnsureFont();
            var panel = CreatePanel(font);
            CreateScene(font, panel);
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[ProductAR Build] Project prepared: " + ScenePath);
        }

        [MenuItem("Product AR/Build Android APK")]
        public static void BuildAndroid() // 지정 실행 씬을 Android APK로 빌드
        {
            if (!File.Exists(ScenePath)) PrepareProject();
            ConfigureAndroid();
            Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Android/ProductAR.apk",
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });
            Debug.Log($"[ProductAR Build] {report.summary.result}; bytes={report.summary.totalSize}; errors={report.summary.totalErrors}");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Android APK build failed.");
        }

        public static void CapturePanelPreview()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var panel = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<WorldProductPanel>(PanelPath));
            panel.transform.position = new Vector3(0, 0.22f, 0);
            var cameraObject = new GameObject("Preview Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0.22f, -1);
            camera.orthographic = true; camera.orthographicSize = 0.225f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Ink;
            var target = new RenderTexture(900, 1300, 24);
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate(true, true);
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(900, 1300, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 900, 1300), 0, 0); image.Apply();
            Directory.CreateDirectory("artifacts/ui");
            File.WriteAllBytes("artifacts/ui/world-panel-preview.png", image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
            Debug.Log("[ProductAR Build] Editor-only world panel preview captured.");
        }

        private static void ConfigureAndroid()
        {
            PlayerSettings.companyName = "Yeonsik";
            PlayerSettings.productName = "상품정보 AR";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel30;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.runInBackground = false;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void EnsureRenderer()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/URP-Performant-Renderer.asset");
            if (renderer == null) throw new InvalidOperationException("Template URP renderer was not found.");
            if (!renderer.rendererFeatures.OfType<ARBackgroundRendererFeature>().Any())
            {
                var feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
                feature.name = "AR Camera Background";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
                EditorUtility.SetDirty(renderer);
            }
        }

        private static TMP_FontAsset EnsureFont()
        {
            const string path = "Assets/Fonts/Generated/NotoSansKR SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJKkr-Regular.otf");
            if (font == null) throw new InvalidOperationException("Korean font is missing.");
            var asset = TMP_FontAsset.CreateFontAsset(font, 60, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            asset.name = "NotoSansKR SDF";
            AssetDatabase.CreateAsset(asset, path);
            var source = string.Join("\n", Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText))
                + File.ReadAllText("Assets/Data/demo-products.json")
                + string.Concat(Enumerable.Range(32, 95).Select(x => (char)x));
            var characters = new string(source.Where(c => !char.IsControl(c)).Distinct().ToArray());
            asset.TryAddCharacters(characters, out var missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[ProductAR Build] Font missing chars: " + missing);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (var texture in asset.atlasTextures)
            {
                texture.name = "NotoSansKR Atlas";
                if (!AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, asset);
            }
            var serialized = new SerializedObject(asset);
            var clear = serialized.FindProperty("m_ClearDynamicDataOnBuild");
            if (clear != null) { clear.boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static WorldProductPanel CreatePanel(TMP_FontAsset font)
        {
            var root = new GameObject("ProductInfoPanel", typeof(RectTransform), typeof(Canvas), typeof(WorldProductPanel));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(640, 940);
            rect.localScale = Vector3.one * 0.00042f;
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var background = root.AddComponent<UnityEngine.UI.Image>();
            background.color = Paper;
            background.raycastTarget = false;
            var view = root.GetComponent<WorldProductPanel>();
            Assign(view, "productTitle", Text(rect, "Title", "더단백 크런치바 초코", font, 44, Ink, 28, 26, 584, 120));
            Assign(view, "specification", Text(rect, "Specification", "빙그레 · 40g · 1개", font, 29, Ink, 28, 156, 584, 48));
            Text(rect, "PriceLabel", "관측 가격", font, 27, Green, 28, 230, 584, 42);
            Assign(view, "price", Text(rect, "Price", "1,400원", font, 72, Ink, 28, 274, 584, 128));
            Assign(view, "priceSource", Text(rect, "PriceSource", "", font, 27, Ink, 28, 416, 584, 112));
            Assign(view, "nutrition", Text(rect, "Nutrition", "", font, 31, Ink, 28, 566, 584, 250));
            Assign(view, "provenance", Text(rect, "Provenance", "", font, 24, Ink, 28, 842, 584, 80));
            var catalog = new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json"));
            catalog.TryFind("8801104671507", out var product);
            view.Bind(product);
            PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(PanelPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath).GetComponent<WorldProductPanel>();
        }

        private static void CreateScene(TMP_FontAsset font, WorldProductPanel panel)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sessionObject = new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));
            var session = sessionObject.GetComponent<ARSession>();
            session.attemptUpdate = true;
            session.enabled = false;
            var originObject = new GameObject("XR Origin", typeof(XROrigin));
            var origin = originObject.GetComponent<XROrigin>();
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(originObject.transform, false);
            var cameraObject = new GameObject("AR Camera", typeof(Camera), typeof(ARCameraManager), typeof(ARCameraBackground), typeof(TrackedPoseDriver));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(offset.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ink;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var driver = cameraObject.GetComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(new InputAction("AR Position", InputActionType.Value, "<HandheldARInputDevice>/devicePosition"));
            driver.rotationInput = new InputActionProperty(new InputAction("AR Rotation", InputActionType.Value, "<HandheldARInputDevice>/deviceRotation"));
            driver.trackingStateInput = new InputActionProperty(new InputAction("AR Tracking", InputActionType.Value, "<HandheldARInputDevice>/trackingState"));
            origin.Camera = camera;
            origin.CameraFloorOffsetObject = offset;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = 0;
            var planes = originObject.AddComponent<ARPlaneManager>();
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            planes.planePrefab = CreatePlanePrefab();
            planes.enabled = false;
            var rays = originObject.AddComponent<ARRaycastManager>();
            var anchors = originObject.AddComponent<ARAnchorManager>();
            var appObject = new GameObject("Product AR", typeof(ARReadiness), typeof(ARBarcodeScanner), typeof(ProductPlacement), typeof(ProductARApp));
            var readiness = appObject.GetComponent<ARReadiness>();
            var scanner = appObject.GetComponent<ARBarcodeScanner>();
            var placement = appObject.GetComponent<ProductPlacement>();
            var app = appObject.GetComponent<ProductARApp>();
            Assign(readiness, "session", session);
            Assign(readiness, "cameraManager", cameraObject.GetComponent<ARCameraManager>());
            Assign(scanner, "cameraManager", cameraObject.GetComponent<ARCameraManager>());
            Assign(placement, "planeManager", planes);
            Assign(placement, "raycastManager", rays);
            Assign(placement, "anchorManager", anchors);
            Assign(placement, "arCamera", camera);
            panel = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath).GetComponent<WorldProductPanel>();
            if (panel == null) throw new InvalidOperationException("World panel prefab component is missing.");
            Assign(placement, "panelPrefab", panel);
            var screen = CreateOverlay(font, app);
            Assign(app, "catalogJson", AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/demo-products.json"));
            Assign(app, "readiness", readiness);
            Assign(app, "scanner", scanner);
            Assign(app, "placement", placement);
            Assign(app, "screen", screen);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static GameObject CreatePlanePrefab()
        {
            const string materialPath = "Assets/Prefabs/PlaneSurface.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor", new Color(0.2f, 0.8f, 0.55f, 0.18f));
                material.SetFloat("_Surface", 1);
                material.SetFloat("_ZWrite", 0);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var plane = new GameObject("PlacementPlane", typeof(ARPlane), typeof(MeshFilter), typeof(MeshRenderer), typeof(ARPlaneMeshVisualizer));
            plane.GetComponent<MeshRenderer>().sharedMaterial = material;
            var prefab = PrefabUtility.SaveAsPrefabAsset(plane, "Assets/Prefabs/PlacementPlane.prefab");
            UnityEngine.Object.DestroyImmediate(plane);
            return prefab;
        }

        private static ProductScreenView CreateOverlay(TMP_FontAsset font, ProductARApp app)
        {
            var canvasObject = new GameObject("Camera UI", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(ProductScreenView));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            var safe = Rect("Safe Area", canvasObject.transform);
            safe.gameObject.AddComponent<SafeArea>();
            var top = Plate("Header", safe, Ink);
            top.anchorMin = new Vector2(0, 1); top.anchorMax = Vector2.one; top.pivot = new Vector2(0.5f, 1);
            top.offsetMin = new Vector2(24, -222); top.offsetMax = new Vector2(-24, -24);
            Text(top, "AppTitle", "상품정보 AR", font, 44, Color.white, 28, 22, 920, 64);
            Text(top, "Intro", "바코드로 찾고, 상품 옆 공간에 배치", font, 30, Color.white, 28, 94, 920, 56);
            var bottom = Plate("Actions", safe, Ink);
            bottom.anchorMin = Vector2.zero; bottom.anchorMax = new Vector2(1, 0); bottom.pivot = new Vector2(0.5f, 0);
            bottom.offsetMin = new Vector2(24, 24); bottom.offsetMax = new Vector2(-24, 584);
            var view = canvasObject.GetComponent<ProductScreenView>();
            Assign(view, "statusTitle", Text(bottom, "Status", "AR 카메라 준비 중", font, 39, Color.white, 28, 28, 940, 72));
            Assign(view, "statusMessage", Text(bottom, "Instruction", "주변을 천천히 비춰주세요.", font, 31, Color.white, 28, 112, 940, 126));
            Assign(view, "productSummary", Text(bottom, "Product", "시연 상품 · 더단백 크런치바 초코 40g", font, 28, new Color(0.78f, 0.9f, 0.86f), 28, 260, 940, 94));
            var buttons = Rect("Buttons", bottom);
            buttons.anchorMin = Vector2.zero; buttons.anchorMax = new Vector2(1, 0); buttons.pivot = new Vector2(0.5f, 0);
            buttons.offsetMin = new Vector2(24, 24); buttons.offsetMax = new Vector2(-24, 164);
            var layout = buttons.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 18; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true;
            var primary = Button(buttons, "Primary", "다시 스캔", font, Green, 1.4f, out var primaryLabel);
            var move = Button(buttons, "Reposition", "재배치", font, new Color(0.2f, 0.3f, 0.34f), 1, out _);
            var delete = Button(buttons, "Delete", "삭제", font, new Color(0.2f, 0.3f, 0.34f), 1, out _);
            Assign(view, "primaryLabel", primaryLabel); Assign(view, "primaryButton", primary);
            Assign(view, "moveButton", move); Assign(view, "deleteButton", delete);
            UnityEventTools.AddPersistentListener(primary.onClick, app.PrimaryAction);
            UnityEventTools.AddPersistentListener(move.onClick, app.Reposition);
            UnityEventTools.AddPersistentListener(delete.onClick, app.DeletePanel);
            var events = new GameObject("UI Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            return view;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        private static RectTransform Plate(string name, Transform parent, Color color)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color;
            return rect;
        }
        private static TextMeshProUGUI Text(Transform parent, string name, string value, TMP_FontAsset font, float size, Color color, float x, float y, float width, float height)
        {
            var rect = Rect(name, parent);
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(x, -y - height); rect.offsetMax = new Vector2(-x, -y);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSize = size; text.color = color; text.text = value;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            return text;
        }
        private static UnityEngine.UI.Button Button(Transform parent, string name, string label, TMP_FontAsset font, Color color, float flex, out TMP_Text text)
        {
            var rect = Plate(name, parent, color);
            var element = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); element.flexibleWidth = flex; element.minHeight = 132;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            var colors = button.colors; colors.disabledColor = new Color(0.5f, 0.55f, 0.56f, 0.65f); colors.pressedColor = new Color(0.7f, 0.85f, 0.8f); button.colors = colors;
            var labelRect = Rect("Label", rect);
            var tmp = labelRect.gameObject.AddComponent<TextMeshProUGUI>(); tmp.font = font; tmp.fontSize = 34; tmp.color = Color.white;
            tmp.text = label; tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false; text = tmp;
            return button;
        }
        private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException(target.name + " is missing field " + field);
            property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
