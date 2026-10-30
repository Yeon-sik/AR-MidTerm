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
        public const string AnnotationPath = "Assets/Prefabs/ProductAnnotationLabel.prefab";
        public const string ImageLibraryPath = "Assets/Images/ProductImageLibrary.asset";
        public const string PackageId = "com.yeonsik.midtermar";
        // 영상에서 약 40cm 거리 4~6px 목표, 측정값 아님. 최종 두께는 실물·영상 확인 후 결정한다.
        public const float OutlineWidthMetres = 0.0025f;
        public const float ConnectorWidthMetres = 0.002f;
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

        [MenuItem("Product AR/Update Camera UI")]
        public static void UpdateCameraUI()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var app = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<ProductARApp>(true)).Single();
            var scanner = app.GetComponent<ARBarcodeScanner>();
            var session = UnityEngine.Object.FindFirstObjectByType<ARSession>();
            Assign(scanner, "session", session);
            app.GetComponent<ProductPlacement>().enabled = false;
            UnityEngine.Object.FindFirstObjectByType<ARPlaneManager>().enabled = false;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Camera UI" || root.name == "UI Event System") UnityEngine.Object.DestroyImmediate(root);
            Assign(app, "screen", CreateOverlay(EnsureFont(), app));
            ConfigureImageTracking(app, CreatePanel(EnsureFont()));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[ProductAR Build] Compact camera UI updated.");
        }

        [MenuItem("Product AR/Prepare Image Tracking Scene (No Build)")]
        public static void PrepareImageTrackingScene()
        {
            // 씬·프리팹만 갱신한다. PlayerSettings, 빌드 목록, APK 생성 경로는 실행하지 않는다.
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            UpdateCameraUI();
            Debug.Log("[ProductAR] Image tracking scene prepared; no player build requested.");
        }

        public static void CaptureBarcodeFollowPreviews()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var view = UnityEngine.Object.FindFirstObjectByType<ProductScreenView>();
            var follow = view.GetComponent<BarcodeProductPanel>();
            var catalog = new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json"));
            catalog.TryFind("8801104671507", out var product);
            // 렌더 검증 전용 데이터. 상품 카탈로그나 실행 씬에는 저장하지 않는다.
            var missing = new ProductSnapshot { name = product.name, brand = product.brand, specification = product.specification, barcode = product.barcode };
            var partial = new ProductSnapshot
            {
                name = product.name, brand = product.brand, specification = product.specification, barcode = product.barcode,
                price = new PriceSnapshot { unitPriceKrw = 0, seller = product.price.seller, observedAt = product.price.observedAt },
                nutrition = new NutritionSnapshot { basisAmount = 40, basisUnit = "g", sugarsGrams = 0, sodiumMg = 0 }
            };
            var off = OffProductClient.Parse(@"{""code"":""8809778493005"",""status"":""success"",""result"":{""id"":""product_found""},""errors"":[],""product"":{""product_name"":""OFF 영양 검증용 식품"",""brands"":""검증용"",""quantity"":""400 g"",""product_quantity_unit"":""g"",""last_modified_t"":1793145600,""nutriments"":{""energy-kcal_100g"":250,""proteins_100g"":5,""sugars_100g"":0,""sodium_100g"":0.4}}}", "8809778493005");
            var canvas = view.GetComponent<Canvas>();
            var camera = new GameObject("Follow UI Preview", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(78, 94, 100, 255);
            camera.orthographic = true;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Directory.CreateDirectory("artifacts/ui/barcode-follow");
            foreach (var height in new[] { 1920, 2400 })
            {
                var target = new RenderTexture(1080, height, 24);
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                Capture("scanning", null);
                Capture("following-center", new Vector2(0.5f, 0.45f));
                Capture("following-left", new Vector2(0.2f, 0.5f));
                Capture("following-top", new Vector2(0.8f, 0.78f));
                Capture("lost", null, true);
                Capture("data-missing", new Vector2(0.5f, 0.45f), sample: missing);
                Capture("data-partial-zero", new Vector2(0.5f, 0.45f), sample: partial);
                Capture("off-nutrition", new Vector2(0.5f, 0.45f), sample: off);
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(target);

                void Capture(string name, Vector2? position, bool lost = false, ProductSnapshot sample = null)
                {
                    follow.SetProduct(position.HasValue || lost ? sample ?? product : null,
                        sourceBadge: sample == off ? "Open Food Facts 정보 · PT 바코드 연결 필요" : null);
                    view.Show(position.HasValue ? "2 / 2 · 바코드 따라가는 중" : lost ? "2 / 2 · 바코드 위치 찾는 중" : "1 / 2 · 바코드 스캔",
                        position.HasValue ? "상품정보를 자동으로 표시해요" : lost ? "바코드를 다시 비춰주세요" : "바코드를 비춰주세요",
                        position.HasValue ? "바코드가 보이는 동안 정보창이 따라가요." : "바코드 전체가 선명하게 보이도록\n휴대폰을 잠시 멈춰주세요.",
                        lost ? product.name + " · " + product.specification : "", "다른 상품 스캔", position.HasValue || lost, false, false, false, lost,
                        secondaryAction: position.HasValue || lost);
                    Canvas.ForceUpdateCanvases();
                    if (position.HasValue && !follow.LayoutAtViewport(position.Value)) throw new InvalidOperationException("Follow preview could not fit: " + name);
                    Canvas.ForceUpdateCanvases();
                    foreach (var text in view.GetComponentsInChildren<TMP_Text>())
                    {
                        text.ForceMeshUpdate(true, true);
                        if (text.isTextOverflowing) throw new InvalidOperationException("Follow text overflow: " + name + "/" + text.name);
                    }
                    camera.Render();
                    var previous = RenderTexture.active;
                    var pixels = new Texture2D(1080, height, TextureFormat.RGB24, false);
                    try
                    {
                        RenderTexture.active = target;
                        pixels.ReadPixels(new Rect(0, 0, 1080, height), 0, 0); pixels.Apply();
                        File.WriteAllBytes($"artifacts/ui/barcode-follow/{name}-{height}.png", pixels.EncodeToPNG());
                    }
                    finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(pixels); }
                    Debug.Log($"[ProductAR Follow UI] {name} 1080x{height} PASS");
                }
            }
        }

        public static void CaptureCameraUIPreviews()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var view = UnityEngine.Object.FindFirstObjectByType<ProductScreenView>();
            var canvas = view.GetComponent<Canvas>();
            var cameraObject = new GameObject("UI Preview Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(78, 94, 100, 255);
            camera.orthographic = true;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Directory.CreateDirectory("artifacts/ui");
            const string product = "더단백 크런치바 초코 · 40g · 1개";
            foreach (var height in new[] { 1920, 2400 })
            {
                var target = new RenderTexture(1080, height, 24);
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                Capture("scan", "1 / 3 · 바코드 스캔", "바코드를 비춰주세요", "바코드 전체가 선명하게 보이도록\n휴대폰을 잠시 멈춰주세요.", "", false, false, false);
                Capture("surface", "2 / 3 · 표면 찾는 중", "책상 표면을 찾아주세요", "상품 옆 책상과 주변 물체를\n천천히 여러 각도에서 비춰주세요.", product, true, false, false);
                Capture("place", "2 / 3 · 배치 위치 선택", "초록색 표면을 눌러주세요", "상품 옆 원하는 위치를 누르면\n그 자리에 정보판이 놓여요.", product, true, false, false);
                Capture("placed", "3 / 3 · 배치 완료", "공간에 정보를 배치했어요", "상품을 옮겼다면 재배치를 눌러주세요.", product, true, true, true);
                Capture("reposition", "2 / 3 · 배치 위치 선택", "초록색 표면을 눌러주세요", "상품 옆 원하는 위치를 누르면\n그 자리에 정보판이 놓여요.", product, true, true, false, moveAction: "재배치 취소");
                Capture("recovery", "3 / 3 · 공간 추적 복구 중", "정보판 위치를 다시 찾는 중", "추적이 끊겨 정보판을 잠시 숨겼어요.\n무늬가 있는 책상과 주변 물체를 함께 비춰주세요.", product, true, false, true, attention: true);
                Capture("unknown", "1 / 3 · 상품 확인 필요", "시연 데이터에 없는 상품이에요", "읽은 번호: 4006381333931\n포장의 번호를 확인하고 다시 스캔해 주세요.", "", true, false, false, error: true);
                Capture("permission", "카메라 권한 필요", "카메라 사용을 허용해 주세요", "설정에서 카메라 권한을 허용한 뒤 돌아와 주세요.", "", true, false, false, error: true, action: "설정 열기");
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(target);

                void Capture(string name, string step, string title, string message, string summary,
                    bool primary, bool move, bool delete, bool error = false, bool attention = false, string moveAction = "재배치", string action = null)
                {
                    view.Show(step, title, message, summary, action ?? (summary.Length == 0 ? "다시 스캔" : "다른 상품 스캔"), primary, move, delete, error, attention, moveAction);
                    Canvas.ForceUpdateCanvases();
                    foreach (var text in view.GetComponentsInChildren<TMP_Text>())
                    {
                        text.ForceMeshUpdate(true, true);
                        if (text.isTextOverflowing) throw new InvalidOperationException("UI preview text overflow: " + name + "/" + text.name);
                    }
                    var safe = (RectTransform)view.transform.Find("Safe Area");
                    var header = (RectTransform)safe.Find("Header");
                    var footer = (RectTransform)safe.Find("Actions");
                    Debug.Log($"[ProductAR UI] {name} 1080x{height}: UI height fraction={(header.rect.height + footer.rect.height) / safe.rect.height:F3}");
                    camera.Render();
                    var previous = RenderTexture.active;
                    try
                    {
                        RenderTexture.active = target;
                        var image = new Texture2D(1080, height, TextureFormat.RGB24, false);
                        image.ReadPixels(new Rect(0, 0, 1080, height), 0, 0);
                        image.Apply();
                        File.WriteAllBytes($"artifacts/ui/{name}-{height}.png", image.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(image);
                    }
                    finally { RenderTexture.active = previous; }
                }
            }
        }

        [MenuItem("Product AR/Build Android APK")]
        public static void BuildAndroid() => BuildAndroidPlayer(false);

        [MenuItem("Product AR/Build Android Release APK")]
        public static void BuildAndroidRelease() => BuildAndroidPlayer(true);

        private static void BuildAndroidPlayer(bool release)
        {
            if (!File.Exists(ScenePath)) PrepareProject();
            ConfigureAndroid();
            Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Android/ProductAR.apk",
                target = BuildTarget.Android,
                options = release ? BuildOptions.None : BuildOptions.Development
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

        public static void CaptureAnnotationPreviews()
        {
            // 글자·줄바꿈 검사용 정면 렌더다. 카메라 추적 성공과 구분한다.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Annotation typography preview", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -1);
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Ink;
            Directory.CreateDirectory("artifacts/ui-annotations");
            var catalog = new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json"));
            catalog.TryFind("8809375984692", out var product);
            try
            {
                foreach (var kind in new[] { "servingPrice", "observationSummary" })
                {
                    var widget = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AnnotationPath));
                    try
                    {
                        widget.GetComponent<WorldProductAnnotation>().SetContent(ProductInsightCalculator.AnnotationText(product, kind));
                        var rect = (RectTransform)widget.transform.Find("Label");
                        rect.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                        rect.localScale = Vector3.one * (ProductAnnotationLayout.Width / rect.rect.width);
                        widget.SetActive(true);
                        Capture(widget, "editor-" + kind, 1080, 360, .02f);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(widget); }
                }
                var panel = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath));
                try
                {
                    panel.GetComponent<WorldProductPanel>().Bind(product);
                    panel.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    panel.transform.localScale = Vector3.one * (.0928f / ((RectTransform)panel.transform).rect.width);
                    Capture(panel, "editor-world-price", 1080, 960, .05f);
                }
                finally { UnityEngine.Object.DestroyImmediate(panel); }
            }
            finally { UnityEngine.Object.DestroyImmediate(cameraObject); }

            void Capture(GameObject root, string name, int width, int height, float orthographicSize)
            {
                camera.orthographicSize = orthographicSize;
                var target = new RenderTexture(width, height, 24); camera.targetTexture = target;
                var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                var previous = RenderTexture.active;
                try
                {
                    Canvas.ForceUpdateCanvases();
                    foreach (var text in root.GetComponentsInChildren<TMP_Text>())
                    {
                        text.ForceMeshUpdate(true, true);
                        if (text.isTextOverflowing) throw new InvalidOperationException("Annotation preview text overflow: " + text.name);
                        foreach (var c in text.text.Where(c => !char.IsWhiteSpace(c)))
                            if (!text.font.HasCharacter(c, true, true)) throw new InvalidOperationException("Annotation preview missing glyph: " + c);
                    }
                    camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                    File.WriteAllBytes("artifacts/ui-annotations/" + name + ".png", pixels.EncodeToPNG());
                    Debug.Log("[ProductAR Annotation Preview] " + name + " PASS (Editor typography only)");
                }
                finally
                {
                    RenderTexture.active = previous; camera.targetTexture = null;
                    UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        internal static void ConfigureAndroid()
        {
            PlayerSettings.companyName = "Yeonsik";
            PlayerSettings.productName = "상품정보 AR";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.bundleVersion = "0.7.0";
            PlayerSettings.Android.bundleVersionCode = 16;
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
            PlayerSettings.Android.forceInternetPermission = true;
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
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (asset == null)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJKkr-Regular.otf");
                if (font == null) throw new InvalidOperationException("Korean font is missing.");
                asset = TMP_FontAsset.CreateFontAsset(font, 60, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                asset.name = "NotoSansKR SDF";
                AssetDatabase.CreateAsset(asset, path);
            }
            var source = string.Join("\n", Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText))
                + File.ReadAllText("Assets/Data/demo-products.json")
                + File.ReadAllText("Assets/Data/product-images.json")
                + string.Concat(Enumerable.Range(32, 95).Select(x => (char)x));
            var characters = new string(source.Where(c => !char.IsControl(c)).Distinct().ToArray());
            asset.TryAddCharacters(characters, out var missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[ProductAR Build] Font missing chars: " + missing);
            if (!AssetDatabase.Contains(asset.material)) AssetDatabase.AddObjectToAsset(asset.material, asset);
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
            var root = new GameObject("ProductInfoPanel", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster), typeof(WorldProductPanel));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(640, 960);
            rect.localScale = Vector3.one * 0.00042f;
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var background = root.AddComponent<UnityEngine.UI.Image>();
            background.color = Paper;
            background.raycastTarget = false;
            var view = root.GetComponent<WorldProductPanel>();
            Assign(view, "productTitle", Text(rect, "Title", "", font, 34, Ink, 24, 20, 592, 100));
            Assign(view, "specification", Text(rect, "Specification", "", font, 26, Ink, 24, 124, 592, 44));
            var tabs = Rect("Information Tabs", rect);
            tabs.anchorMin = new Vector2(0, 1); tabs.anchorMax = Vector2.one;
            tabs.offsetMin = new Vector2(24, -256); tabs.offsetMax = new Vector2(-24, -184);
            var layout = tabs.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 12; layout.childControlWidth = layout.childControlHeight = layout.childForceExpandWidth = true;
            var nutritionButton = Button(tabs, "Nutrition Tab", "영양", font, Green, 1, out _);
            var priceButton = Button(tabs, "Price Tab", "가격", font, Ink, 1, out _);
            nutritionButton.GetComponent<UnityEngine.UI.LayoutElement>().minHeight = 72;
            priceButton.GetComponent<UnityEngine.UI.LayoutElement>().minHeight = 72;
            Assign(view, "nutritionTab", nutritionButton); Assign(view, "priceTab", priceButton);
            Assign(view, "tabs", tabs);
            UnityEventTools.AddPersistentListener(nutritionButton.onClick, view.ShowNutrition);
            UnityEventTools.AddPersistentListener(priceButton.onClick, view.ShowPrice);
            var nutritionPage = Rect("Nutrition Page", rect);
            var pricePage = Rect("Price Page", rect);
            foreach (var page in new[] { nutritionPage, pricePage })
            {
                page.anchorMin = new Vector2(0, 1); page.anchorMax = Vector2.one;
                page.offsetMin = new Vector2(0, -676); page.offsetMax = new Vector2(0, -280);
            }
            Assign(view, "nutritionPage", nutritionPage.gameObject); Assign(view, "pricePage", pricePage.gameObject);
            Assign(view, "nutrition", Text(nutritionPage, "Nutrition", "", font, 30, Ink, 24, 0, 592, 240));
            Assign(view, "nutritionSource", Text(nutritionPage, "NutritionSource", "", font, 20, Ink, 24, 250, 592, 146));
            Text(pricePage, "PriceLabel", "관측 가격 · 개당", font, 26, Green, 24, 0, 592, 40);
            var priceText = Text(pricePage, "Price", "", font, 50, Ink, 24, 44, 592, 92);
            priceText.enableAutoSizing = true; priceText.fontSizeMin = 34; priceText.fontSizeMax = 50;
            priceText.textWrappingMode = TextWrappingModes.NoWrap;
            Assign(view, "price", priceText);
            var source = Text(pricePage, "PriceSource", "", font, 21, Ink, 24, 146, 592, 36);
            source.enableAutoSizing = true; source.fontSizeMin = 18; source.fontSizeMax = 21;
            source.textWrappingMode = TextWrappingModes.NoWrap;
            Assign(view, "priceSource", source);
            Assign(view, "priceDisclaimer", Text(pricePage, "PriceDisclaimer", "", font, 23, Ink, 24, 190, 592, 36));
            Assign(view, "provenance", Text(rect, "Provenance", "", font, 23, Ink, 24, 696, 592, 120));
            Assign(view, "emptyInformation", Text(rect, "Empty Information", "", font, 26, Ink, 24, 184, 592, 44));
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
            Assign(scanner, "session", session);
            placement.enabled = false;
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
            Assign(app, "screen", screen);
            ConfigureImageTracking(app, panel);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void ConfigureImageTracking(ProductARApp app, WorldProductPanel panel)
        {
            var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(ImageLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
                AssetDatabase.CreateAsset(library, ImageLibraryPath);
                library.Add();
                library.SetName(0, ImageTrackingProbe.TargetName);
                library.SetTexture(0, AssetDatabase.LoadAssetAtPath<Texture2D>(ImageTrackingProbeBuilder.MarkerPath), false);
                library.SetSpecifySize(0, false); // 개발용 카드의 실측값은 아직 없다.
                EditorUtility.SetDirty(library);
            }
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/product-images.json");
            RegisterPackageCrop(library, "theanine-top", "Assets/Images/theanine-top.jpg", 0.058f);
            RegisterPackageCrop(library, "theanine-bottom", "Assets/Images/theanine-bottom.jpg", 0.058f);
            var catalog = new ProductImageCatalog(json.text, new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json")));
            catalog.ValidateLibrary(library);
            var origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
            origin.GetComponent<ARPlaneManager>().enabled = false;
            origin.GetComponent<ARRaycastManager>().enabled = false;
            origin.GetComponent<ARAnchorManager>().enabled = false;
            var images = origin.GetComponent<ARTrackedImageManager>() ?? origin.gameObject.AddComponent<ARTrackedImageManager>();
            images.referenceLibrary = library;
            images.requestedMaxNumberOfMovingImages = 2; // 다른 등록 이미지가 동시에 보이면 숨길 수 있도록 한다.
            var presenter = app.GetComponent<TrackedImageProductPanel>() ?? app.gameObject.AddComponent<TrackedImageProductPanel>();
            var camera = origin.Camera;
            Assign(presenter, "imageManager", images);
            Assign(presenter, "session", UnityEngine.Object.FindFirstObjectByType<ARSession>());
            Assign(presenter, "readiness", app.GetComponent<ARReadiness>());
            Assign(presenter, "cameraManager", camera.GetComponent<ARCameraManager>());
            Assign(presenter, "arCamera", camera);
            Assign(presenter, "panelPrefab", panel);
            var registration = app.GetComponent<RuntimeProductRegistrationController>()
                ?? app.gameObject.AddComponent<RuntimeProductRegistrationController>();
            registration.Configure(camera.GetComponent<ARCameraManager>(), images, camera,
                UnityEngine.Object.FindFirstObjectByType<ARSession>(), EnsureFont());
            Assign(app, "frontRegistration", registration);
            var oldVisuals = app.transform.Find("Tracking Visuals");
            if (oldVisuals != null) UnityEngine.Object.DestroyImmediate(oldVisuals.gameObject);
            var visuals = new GameObject("Tracking Visuals"); visuals.transform.SetParent(app.transform, false);
            Directory.CreateDirectory("Assets/Materials");
            const string linePath = "Assets/Materials/ProductTrackingLine.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(linePath);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material, linePath); }
            material.SetColor("_BaseColor", new Color32(40, 205, 132, 255));
            EditorUtility.SetDirty(material);
            var annotations = visuals.AddComponent<ProductAnnotationPresenter>();
            Assign(annotations, "annotationPrefab", CreateAnnotationLabel(EnsureFont(), material));
            Assign(presenter, "annotations", annotations);
            var annotationSettings = new SerializedObject(presenter);
            // 포장 표기 주석은 보류한다. 원본 연구 코드는 보존하되 제출 앱에는 표시하지 않는다.
            annotationSettings.FindProperty("showAnnotations").boolValue = false;
            annotationSettings.ApplyModifiedPropertiesWithoutUndo();
            // 등록한 상품 공통 영역(없으면 추적 크롭)의 윤곽이다. 크롭 경계와 표시 경계를 분리한다.
            Assign(presenter, "productOutline", Line("Product Outline", 4, true, OutlineWidthMetres));
            Assign(presenter, "connector", Line("Product Connector", 2, false, ConnectorWidthMetres));
            LineRenderer Line(string name, int points, bool loop, float width)
            {
                var go = new GameObject(name); go.transform.SetParent(visuals.transform, false);
                var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = material;
                line.useWorldSpace = true; line.loop = loop; line.positionCount = points;
                line.startWidth = line.endWidth = width;
                line.alignment = LineAlignment.View;
                line.numCornerVertices = 4;
                line.numCapVertices = loop ? 0 : 4; // 닫힌 윤곽은 끝이 없고, 연결선의 양 끝만 둥글게 한다.
                line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false; line.enabled = false;
                return line;
            }
            Assign(app, "imagePanel", presenter);
            Assign(app, "imageBindingsJson", json);
            // 파일이 없으면 null을 직렬화한다. 키를 포함한 예제 파일을 대신 사용하지 않는다.
            Assign(app, "remoteLookupJson", AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Config/remote-lookup.local.json"));
            Assign(app, "barcodePanel", UnityEngine.Object.FindFirstObjectByType<BarcodeProductPanel>());
            var scannerSettings = new SerializedObject(app.GetComponent<ARBarcodeScanner>());
            scannerSettings.FindProperty("captureViewportCoordinates").boolValue = true;
            scannerSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static WorldProductAnnotation CreateAnnotationLabel(TMP_FontAsset font, Material material)
        {
            var root = new GameObject("Product Annotation", typeof(WorldProductAnnotation));
            var panel = new GameObject("Label", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.Image));
            panel.transform.SetParent(root.transform, false);
            var rect = (RectTransform)panel.transform;
            // UI 1단위당 0.125mm, 40단위 글자 em 높이 5mm. 실제 가독성은 실물로 확인한다.
            rect.sizeDelta = new Vector2(920, 208);
            panel.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            panel.GetComponent<UnityEngine.UI.Image>().color = Paper;
            panel.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var text = Text(rect, "Annotation Text", "", font, 40, Ink, 32, 16, 856, 176);
            text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap;
            text.richText = false; text.raycastTarget = false;
            // 선은 축소하는 Canvas의 형제가 되어 월드 단위 두께 2mm를 유지한다.
            var leader = new GameObject("Leader").AddComponent<LineRenderer>(); leader.transform.SetParent(root.transform, false);
            leader.sharedMaterial = material; leader.useWorldSpace = true; leader.positionCount = 2;
            leader.startWidth = leader.endWidth = ConnectorWidthMetres;
            leader.alignment = LineAlignment.View; leader.numCornerVertices = 4;
            leader.numCapVertices = 0; // 끝 캡이 앵커를 지나 인쇄 안쪽까지 덮지 않게 한다.
            leader.shadowCastingMode = ShadowCastingMode.Off; leader.receiveShadows = false; leader.enabled = false;
            var widget = root.GetComponent<WorldProductAnnotation>();
            Assign(widget, "label", text); Assign(widget, "labelPanel", rect); Assign(widget, "leader", leader);
            PrefabUtility.SaveAsPrefabAsset(root, AnnotationPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(AnnotationPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<GameObject>(AnnotationPath).GetComponent<WorldProductAnnotation>();
        }

        private static void RegisterPackageCrop(XRReferenceImageLibrary library, string name, string path, float width)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidOperationException("상품 크롭 이미지가 없습니다: " + path);
            var index = -1;
            for (var i = 0; i < library.count; i++) if (library[i].name == name) index = i;
            if (index < 0) { library.Add(); index = library.count - 1; }
            library.SetName(index, name);
            library.SetTexture(index, texture, false);
            library.SetSpecifySize(index, true);
            library.SetSize(index, new Vector2(width, width * texture.height / texture.width));
            EditorUtility.SetDirty(library);
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
            top.offsetMin = new Vector2(24, -148); top.offsetMax = new Vector2(-24, -24);
            var bottom = Plate("Actions", safe, Ink);
            bottom.anchorMin = Vector2.zero; bottom.anchorMax = new Vector2(1, 0); bottom.pivot = new Vector2(0.5f, 0);
            bottom.offsetMin = new Vector2(24, 24); bottom.offsetMax = new Vector2(-24, 140);
            var view = canvasObject.GetComponent<ProductScreenView>();
            var follow = canvasObject.AddComponent<BarcodeProductPanel>();
            Assign(follow, "safeArea", safe); Assign(follow, "header", top); Assign(follow, "footer", bottom);
            CreateFollowingCard(font, safe, follow);
            follow.enabled = true; // 바코드 단독 인식 경로의 화면 정보판
            Assign(view, "statusStep", Text(top, "Step", "카메라 준비 중", font, 27, new Color(1f, 0.82f, 0.43f), 24, 14, 940, 40));
            Assign(view, "statusTitle", Text(top, "Status", "카메라를 준비하고 있어요", font, 38, Color.white, 24, 58, 940, 56));
            Assign(view, "statusMessage", Text(bottom, "Instruction", "주변을 천천히 비춰주세요.", font, 30, Color.white, 24, 20, 940, 76));
            Assign(view, "productSummary", Text(bottom, "Product", "", font, 28, new Color(0.78f, 0.9f, 0.86f), 24, 20, 940, 44));
            var buttons = Rect("Buttons", bottom);
            buttons.anchorMin = new Vector2(0, 1); buttons.anchorMax = Vector2.one; buttons.pivot = new Vector2(0.5f, 1);
            buttons.offsetMin = new Vector2(24, -132); buttons.offsetMax = new Vector2(-24, 0);
            var layout = buttons.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 18; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true;
            var primary = Button(buttons, "Primary", "다시 스캔", font, Green, 1.4f, out var primaryLabel);
            var move = Button(buttons, "Reposition", "앞면 등록", font, Green, 1, out var moveLabel);
            var detailButton = Button(buttons, "Detail", "상세 보기", font, Green, 1, out _);
            var delete = Button(buttons, "Delete", "삭제", font, new Color(0.2f, 0.3f, 0.34f), 1, out _);
            Assign(view, "primaryLabel", primaryLabel); Assign(view, "primaryButton", primary);
            Assign(view, "moveLabel", moveLabel);
            Assign(view, "moveButton", move); Assign(view, "deleteButton", delete);
            Assign(view, "detailButton", detailButton); Assign(view, "header", top);
            Assign(view, "instructionPanel", bottom); Assign(view, "buttons", buttons);
            UnityEventTools.AddPersistentListener(primary.onClick, app.PrimaryAction);
            UnityEventTools.AddPersistentListener(move.onClick, app.BeginFrontRegistration);
            UnityEventTools.AddPersistentListener(detailButton.onClick, app.ShowDetail);
            Assign(app, "detail", CreateDetailSheet(font, safe));
            var events = new GameObject("UI Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            Canvas.ForceUpdateCanvases();
            view.Show("카메라 준비 중", "카메라를 준비하고 있어요", "잠시만 기다려 주세요.", "", "다시 확인", false, false, false, false, true);
            return view;
        }

        // 안전 영역 전체를 덮는 불투명 시트다. 마지막 자식이라 안내·정보판 위에 그려지고 뒤쪽 터치를 막는다.
        private static ProductDetailView CreateDetailSheet(TMP_FontAsset font, RectTransform safe)
        {
            var detail = safe.GetComponentInParent<Canvas>().gameObject.AddComponent<ProductDetailView>();
            var sheet = Plate("Product Detail", safe, Paper);
            sheet.offsetMin = new Vector2(24, 24); sheet.offsetMax = new Vector2(-24, -24);
            var title = Text(sheet, "DetailTitle", "", font, 40, Ink, 32, 32, 968, 108);
            title.fontStyle = FontStyles.Bold;
            title.enableAutoSizing = true; title.fontSizeMin = 30; title.fontSizeMax = 40;
            var specification = Text(sheet, "DetailSpecification", "", font, 28, Ink, 32, 144, 968, 44);
            Rule(sheet, "Detail Rule", 200, 3, Ink);
            var body = Text(sheet, "DetailBody", "", font, 30, Ink, 32, 220, 968, 0);
            // 본문은 닫기 버튼 위까지 늘린다. 긴 관측 목록은 글자를 줄여 시트 안에 모두 담는다.
            body.rectTransform.anchorMin = Vector2.zero;
            body.rectTransform.offsetMin = new Vector2(32, 180);
            body.enableAutoSizing = true; body.fontSizeMin = 20; body.fontSizeMax = 30;
            var close = Button(sheet, "Close", "닫기", font, Ink, 1, out _);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = Vector2.zero; closeRect.anchorMax = new Vector2(1, 0);
            closeRect.offsetMin = new Vector2(24, 24); closeRect.offsetMax = new Vector2(-24, 156);
            Assign(detail, "sheet", sheet); Assign(detail, "productTitle", title);
            Assign(detail, "specification", specification); Assign(detail, "body", body);
            UnityEventTools.AddPersistentListener(close.onClick, detail.Hide);
            sheet.gameObject.SetActive(false);
            return detail;
        }

        private static void CreateFollowingCard(TMP_FontAsset font, RectTransform safe, BarcodeProductPanel follow)
        {
            var card = Plate("Barcode Product", safe, Paper);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = Vector2.zero;
            card.sizeDelta = new Vector2(720, 770);
            card.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            Assign(follow, "card", card);
            Assign(follow, "productTitle", Text(card, "ProductTitle", "", font, 32, Ink, 24, 12, 672, 54));
            Assign(follow, "specification", Text(card, "Specification", "", font, 24, Ink, 24, 68, 672, 38));

            var priceBlock = Plate("Observed Price", card, new Color32(231, 238, 230, 255));
            priceBlock.anchorMin = new Vector2(0, 1); priceBlock.anchorMax = Vector2.one;
            priceBlock.offsetMin = new Vector2(24, -256); priceBlock.offsetMax = new Vector2(-24, -116);
            priceBlock.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var priceLabel = Text(priceBlock, "PriceLabel", "관측 가격", font, 24, Green, 16, 12, 640, 40);
            Assign(follow, "priceLabel", priceLabel);
            priceLabel.rectTransform.anchorMax = new Vector2(0.45f, 1);
            var amount = Text(priceBlock, "Price", "", font, 40, Ink, 16, 0, 640, 66);
            amount.rectTransform.anchorMin = new Vector2(0.45f, 1);
            amount.alignment = TextAlignmentOptions.TopRight;
            amount.fontStyle = FontStyles.Bold;
            Assign(follow, "price", amount);
            var sellerSource = Text(priceBlock, "Seller", "", font, 22, Ink, 16, 62, 640, 38);
            sellerSource.enableAutoSizing = true; sellerSource.fontSizeMin = 18; sellerSource.fontSizeMax = 22;
            sellerSource.textWrappingMode = TextWrappingModes.NoWrap;
            Assign(follow, "seller", sellerSource);
            Assign(follow, "observedAt", Text(priceBlock, "ObservedAt", "", font, 22, Ink, 16, 100, 640, 36));
            Assign(follow, "priceSection", priceBlock);
            var nutritionBlock = Rect("Nutrition Section", card);
            Assign(follow, "nutritionSection", nutritionBlock);

            var nutritionTitle = Text(nutritionBlock, "NutritionTitle", "영양정보", font, 32, Ink, 0, 0, 672, 50);
            nutritionTitle.fontStyle = FontStyles.Bold;
            nutritionTitle.rectTransform.anchorMax = new Vector2(0.5f, 1);
            var basis = Text(nutritionBlock, "NutritionBasis", "", font, 24, Ink, 0, 10, 672, 38);
            basis.rectTransform.anchorMin = new Vector2(0.5f, 1);
            basis.alignment = TextAlignmentOptions.TopRight;
            Assign(follow, "nutritionBasis", basis);
            Rule(nutritionBlock, "Nutrition Top Rule", 54, 6, Ink);
            NutritionRow("Calories", "열량", "calories", 328, 42, 27, true);
            Rule(nutritionBlock, "Calories Rule", 106, 4, Ink);
            NutritionRow("Sodium", "나트륨", "sodium", 378, 36);
            NutritionRow("Carbs", "탄수화물", "carbs", 414, 36);
            NutritionRow("Sugars", "당류", "sugars", 450, 36, indent: true);
            NutritionRow("Fat", "지방", "fat", 486, 36);
            NutritionRow("SaturatedFat", "포화지방", "saturatedFat", 522, 36, indent: true);
            NutritionRow("Protein", "단백질", "protein", 558, 36);
            Rule(nutritionBlock, "Nutrition Bottom Rule", 330, 4, Ink);
            Assign(follow, "nutritionSource", Text(nutritionBlock, "Nutrition Source", "", font, 21, Ink, 0, 340, 672, 34));
            Assign(follow, "provenance", Text(card, "Provenance", "", font, 20, Ink, 24, 606, 672, 38));
            Assign(follow, "registrationStatus", Text(card, "Registration", "", font, 20, Ink, 24, 668, 672, 90));
            Assign(follow, "emptyInformation", Text(card, "Empty Information", "", font, 24, Ink, 24, 116, 672, 52));

            void NutritionRow(string name, string label, string field, float y, float height, float size = 24, bool bold = false, bool indent = false)
            {
                y -= 266;
                var row = Rect(name + " Row", nutritionBlock);
                row.anchorMin = new Vector2(0, 1); row.anchorMax = Vector2.one;
                row.offsetMin = new Vector2(0, -y - height); row.offsetMax = new Vector2(0, -y);
                var heading = Text(row, "Label", label, font, size, Ink, indent ? 20 : 0, 0, 672, height);
                heading.rectTransform.anchorMax = new Vector2(0.6f, 1);
                var value = Text(row, "Amount", "", font, size, Ink, 0, 0, 672, height);
                value.rectTransform.anchorMin = new Vector2(0.6f, 1);
                heading.alignment = TextAlignmentOptions.MidlineLeft;
                value.alignment = TextAlignmentOptions.MidlineRight;
                if (bold) heading.fontStyle = value.fontStyle = FontStyles.Bold;
                Assign(follow, field, value);
                if (!bold && name != "Protein") Rule(nutritionBlock, name + " Rule", y + height, 1, new Color32(183, 191, 187, 255));
            }
            var marker = Plate("Barcode Position", safe, Green);
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(18, 18);
            marker.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            Assign(follow, "marker", marker);
            card.gameObject.SetActive(false); marker.gameObject.SetActive(false);
        }

        private static void Rule(RectTransform parent, string name, float y, float thickness, Color color)
        {
            var rule = Plate(name, parent, color);
            rule.anchorMin = new Vector2(0, 1); rule.anchorMax = Vector2.one;
            rule.offsetMin = new Vector2(24, -y - thickness); rule.offsetMax = new Vector2(-24, -y);
            rule.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
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
