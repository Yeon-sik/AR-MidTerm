using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MidtermAR.Editor;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ZXing;
using FormatException = System.FormatException;

namespace MidtermAR.Tests
{
    public sealed class ProductARTests
    {
        private static string Json => File.ReadAllText("Assets/Data/demo-products.json");

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void BarcodeLocationRemainsInTheOriginalImageCoordinatesAfterRotation(int turns)
        {
            var width = 1200; var height = 800;
            var pixels = Enumerable.Repeat((byte)255, width * height).ToArray();
            var bars = new MultiFormatWriter().encode("8801104671507", BarcodeFormat.EAN_13, 600, 180);
            var expected = new Rect(100, 140, 600, 180);
            for (var y = 0; y < 180; y++)
                for (var x = 0; x < 600; x++) pixels[(y + 140) * width + x + 100] = bars[x, y] ? (byte)0 : (byte)255;
            for (var turn = 0; turn < turns; turn++)
            {
                var rotated = new byte[pixels.Length];
                for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++) rotated[x * height + height - 1 - y] = pixels[y * width + x];
                expected = new Rect(height - expected.yMax, expected.xMin, expected.height, expected.width);
                pixels = rotated;
                (width, height) = (height, width);
            }
            var result = new BarcodeDecoder().Decode(pixels, width, height);
            Assert.That(result?.Text, Is.EqualTo("8801104671507"));
            Assert.That(result.HasPosition, Is.True);
            foreach (var point in new[] { result.ImageStart, result.ImageEnd })
                Assert.That(expected.Contains(new Vector2(point.x * width, point.y * height)), Is.True,
                    "Rotated decode point must refer to the barcode in the original input, not the temporary rotated buffer.");
        }

        [Test]
        public void FollowTargetMapsPortraitAndCropAndRejectsStaleOrDifferentProducts()
        {
            var target = new BarcodeFollowState();
            target.Select("8801104671507");
            var portrait = new BarcodeViewportTransform(new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 1));
            var known = new BarcodeReadResult("8801104671507", "EAN_13", new Vector2(0.2f, 0.6f), new Vector2(0.4f, 0.6f));
            target.Observe(known, portrait, 1, 1.1f);
            Assert.That(target.IsVisible(1.1f), Is.True);
            Assert.That(Vector2.Distance(target.ViewportPosition, new Vector2(0.6f, 0.3f)), Is.LessThan(0.0001f));
            target.Observe(null, portrait, 1.3f, 1.3f);
            Assert.That(target.IsVisible(1.3f), Is.True, "A single missed frame should not flicker the panel.");
            Assert.That(target.IsVisible(1.7f), Is.False, "A lost barcode must not leave a floating panel behind.");
            target.Observe(known, portrait, 1, 2);
            Assert.That(target.IsVisible(2), Is.False, "Slow worker results are stale at capture time.");
            target.Observe(known, portrait, 3, 3);
            target.Observe(new BarcodeReadResult("4006381333931", "EAN_13"), portrait, 3.1f, 3.1f);
            Assert.That(target.IsVisible(3.1f), Is.False);
            Assert.That(target.Barcode, Is.EqualTo("8801104671507"));
            target.Observe(known, default, 4, 4);
            Assert.That(target.IsVisible(4), Is.False, "Missing geometry must not fall back to screen center.");
            var crop = new BarcodeViewportTransform(new Vector2(-1, 0), new Vector2(1, 0), new Vector2(-1, 1));
            target.Observe(known, crop, 5, 5);
            Assert.That(target.IsVisible(5), Is.False, "Decoded points outside the displayed camera crop are hidden.");
            target.Observe(known, portrait, 6, 6);
            target.Select(null);
            Assert.That(target.IsVisible(6), Is.False, "Rescan removes both identity and position.");
        }

        [TestCase(100, 300)]
        [TestCase(500, 900)]
        [TestCase(900, 1600)]
        public void FollowCardFitsVisibleCameraAndDoesNotCoverItsBarcode(float x, float y)
        {
            var available = new Rect(24, 300, 1032, 1450);
            var point = new Vector2(x, y);
            var card = BarcodeProductPanel.CalculateCardRect(available, point, new Vector2(720, 648), 72);
            Assert.That(card.width, Is.GreaterThan(0));
            Assert.That(card.xMin, Is.GreaterThanOrEqualTo(available.xMin));
            Assert.That(card.xMax, Is.LessThanOrEqualTo(available.xMax));
            Assert.That(card.yMin, Is.GreaterThanOrEqualTo(available.yMin));
            Assert.That(card.yMax, Is.LessThanOrEqualTo(available.yMax));
            Assert.That(card.Contains(point), Is.False);
        }

        [TestCase("complete")]
        [TestCase("partial")]
        [TestCase("missing")]
        public void NutritionAndPriceLabelPreservesAmountsUnitsAndMissingData(string dataState)
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath, OpenSceneMode.Single);
            var panel = UnityEngine.Object.FindFirstObjectByType<BarcodeProductPanel>();
            new ProductCatalog(Json).TryFind("8801104671507", out var product);
            panel.SetProduct(product);
            if (dataState == "partial")
            {
                product.price.unitPriceKrw = 0;
                product.nutrition.sugarsGrams = 0;
                product.nutrition.fatGrams = null;
            }
            else if (dataState == "missing")
            {
                product.price = null;
                product.nutrition = null;
            }
            panel.SetProduct(product);
            var fields = new SerializedObject(panel);
            string Display(string field) => ((TMP_Text)fields.FindProperty(field).objectReferenceValue).text;
            if (dataState == "missing")
            {
                Assert.That(Display("price"), Is.EqualTo("가격 정보 없음"));
                Assert.That(Display("seller"), Is.EqualTo("확인된 가격 관측이 없습니다."));
                Assert.That(Display("observedAt"), Is.EqualTo("판매처·관측일 정보 없음"));
                Assert.That(Display("nutritionBasis"), Is.EqualTo("기준량 정보 없음"));
                foreach (var field in new[] { "calories", "sodium", "carbs", "sugars", "fat", "saturatedFat", "protein" })
                    Assert.That(Display(field), Is.EqualTo("정보 없음"), field + " must clear the previous product's value.");
            }
            else
            {
                Assert.That(Display("price"), Is.EqualTo(dataState == "partial" ? "0원" : "1,400원"));
                Assert.That(Display("seller"), Is.EqualTo("판매처  국군복지단 바다마을마트"));
                Assert.That(Display("observedAt"), Is.EqualTo("2026-05-08 관측 · 개당 가격"));
                Assert.That(Display("nutritionBasis"), Is.EqualTo("40g 기준"));
                Assert.That(Display("calories"), Is.EqualTo("185 kcal"));
                Assert.That(Display("sodium"), Is.EqualTo("200 mg"));
                Assert.That(Display("carbs"), Is.EqualTo("15 g"));
                Assert.That(Display("sugars"), Is.EqualTo(dataState == "partial" ? "0 g" : "7 g"));
                Assert.That(Display("fat"), Is.EqualTo(dataState == "partial" ? "정보 없음" : "7 g"));
                Assert.That(Display("saturatedFat"), Is.EqualTo("5 g"));
                Assert.That(Display("protein"), Is.EqualTo("15 g"));
            }
        }

        [Test]
        public void KnownBarcodeCanFollowImmediatelyWithoutAPlacementClick()
        {
            var flow = new ProductWorkflow(new ProductCatalog(Json));
            flow.AcceptBarcode("8801104671507");
            flow.FollowBarcode();
            Assert.That(flow.Phase, Is.EqualTo(ProductPhase.Following));
            Assert.That(flow.CanPlace, Is.False);
            flow.StartScan();
            flow.AcceptBarcode("4006381333931");
            flow.FollowBarcode();
            Assert.That(flow.Phase, Is.EqualTo(ProductPhase.UnknownBarcode));
        }

        [Test]
        public void ScannerFollowingCanStopAndRestartWithoutKeepingTheOldTarget()
        {
            var root = new GameObject("Scanner mode test");
            root.SetActive(false);
            try
            {
                var scanner = root.AddComponent<ARBarcodeScanner>();
                scanner.SetScanning(true);
                scanner.SetFollowing("8801104671507");
                Assert.That(scanner.IsScanning, Is.False, "Following must not run the initial scan timeout.");
                Assert.That(scanner.FollowingBarcode, Is.EqualTo("8801104671507"));
                scanner.SetScanning(false);
                Assert.That(scanner.FollowingBarcode, Is.Null, "Pause/disable must stop continuous decoding too.");
                scanner.SetScanning(true);
                Assert.That(scanner.IsScanning, Is.True);
                Assert.That(scanner.FollowingBarcode, Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void CameraOverlayPreservesCameraAreaAndReadableTouchTargets(bool selected, bool placed)
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath, OpenSceneMode.Single);
            var view = UnityEngine.Object.FindFirstObjectByType<ProductScreenView>();
            var canvas = view.GetComponent<Canvas>();
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, 1920);
            Canvas.ForceUpdateCanvases();
            view.Show(placed ? "3 / 3 · 배치 완료" : selected ? "2 / 3 · 배치 위치 선택" : "1 / 3 · 바코드 스캔",
                placed ? "공간에 정보를 배치했어요" : "바코드를 비춰주세요",
                "바코드 전체가 선명하게 보이도록\n휴대폰을 잠시 멈춰주세요.",
                selected ? "더단백 크런치바 초코 · 40g · 1개" : "", "다른 상품 스캔", selected, placed, placed, false);
            Canvas.ForceUpdateCanvases();
            var safe = (RectTransform)view.transform.Find("Safe Area");
            var header = (RectTransform)safe.Find("Header");
            var footer = (RectTransform)safe.Find("Actions");
            Assert.That((header.rect.height + footer.rect.height) / safe.rect.height, Is.LessThan(0.25f), "Camera UI must occupy less than one quarter of the portrait screen.");
            Assert.That(header.offsetMin.y, Is.GreaterThan(-1920 * 0.2f));
            Assert.That(footer.offsetMax.y, Is.LessThan(1920 * 0.3f), "Keep the center clear for plane taps.");
            foreach (var text in view.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate(true, true);
                Assert.That(text.isTextOverflowing, Is.False, text.name + ": camera UI text overflow");
            }
            foreach (var button in view.GetComponentsInChildren<Button>())
                Assert.That(((RectTransform)button.transform).rect.height, Is.GreaterThanOrEqualTo(132), "Compact UI must preserve button touch height.");
            Assert.That(view.GetComponentsInChildren<Button>().Length, Is.EqualTo(placed ? 3 : selected ? 1 : 0));
        }

        [TestCase(ARSessionState.Ready, false)]
        [TestCase(ARSessionState.Unsupported, false)]
        [TestCase(ARSessionState.SessionInitializing, true)]
        [TestCase(ARSessionState.SessionTracking, true)]
        public void BarcodeCameraDoesNotWaitForWorldTracking(ARSessionState state, bool expected)
        {
            Assert.That(ARReadiness.CanReadCameraInState(state), Is.EqualTo(expected));
        }

        [TestCase("8801104671507", true)]
        [TestCase("8 801104 671507", true)]
        [TestCase("0123456789012", true)]
        [TestCase("8801104671508", false)]
        [TestCase("880110467150", false)]
        [TestCase("８８０１１０４６７１５０７", false)]
        [TestCase(null, false)]
        public void Ean13ChecksLengthDigitsChecksumWithoutRemovingZero(string raw, bool expected)
        {
            Assert.That(BarcodeIdentity.TryNormalizeEan13(raw, out var normalized), Is.EqualTo(expected));
            if (raw == "0123456789012") Assert.That(normalized, Is.EqualTo(raw));
        }

        [Test]
        public void CatalogUsesExactIdentityAndPreservesUnknownVersusZero()
        {
            var catalog = new ProductCatalog(Json);
            Assert.That(catalog.TryFind("8801104671507", out var product), Is.True);
            Assert.That(product.catalogProductId, Is.EqualTo("1516c3eb-3dfb-4e11-8040-d23fadcdaad0"));
            Assert.That(product.mapping.scope, Is.EqualTo("demo-only"));
            Assert.That(product.nutrition.fiberGrams, Is.Null);
            Assert.That(product.nutrition.sugarsGrams, Is.EqualTo(7));
            Assert.That(product.price.unitPriceKrw, Is.EqualTo(1400));
            Assert.That(product.price.observedAt, Is.EqualTo("2026-05-08"));
            Assert.That(ProductInfoFormatter.Number(null, "g"), Is.EqualTo("정보 없음"));
            Assert.That(ProductInfoFormatter.Number(0, "g"), Is.EqualTo("0g"));
            Assert.That(catalog.TryFind("4006381333931", out _), Is.False);
        }

        [Test]
        public void CatalogRejectsWrongNutritionLinkAndDuplicateBarcode()
        {
            var badLink = Json.Replace("\"sourceCatalogProductId\": \"1516c3eb-3dfb-4e11-8040-d23fadcdaad0\"", "\"sourceCatalogProductId\": \"00000000-0000-0000-0000-000000000000\"");
            Assert.Throws<FormatException>(() => new ProductCatalog(badLink));
            var data = Newtonsoft.Json.Linq.JObject.Parse(Json);
            ((Newtonsoft.Json.Linq.JArray)data["products"]).Add(data["products"][0].DeepClone());
            Assert.Throws<FormatException>(() => new ProductCatalog(data.ToString()));
            Assert.Throws<FormatException>(() => new ProductCatalog(Json.Replace("\"unitPriceKrw\": 1400", "\"unitPriceKrw\": -1")));
        }

        [Test]
        public void MissingPriceAndNutritionRemainMissing()
        {
            var data = Newtonsoft.Json.Linq.JObject.Parse(Json);
            data["products"][0]["price"] = null;
            data["products"][0]["nutrition"] = null;
            var catalog = new ProductCatalog(data.ToString());
            catalog.TryFind("8801104671507", out var product);
            Assert.That(product.price, Is.Null);
            Assert.That(product.nutrition, Is.Null);
            Assert.That(ProductInfoFormatter.Price(product), Is.EqualTo("가격 정보 없음"));
            Assert.That(ProductInfoFormatter.Nutrition(product), Is.EqualTo("영양정보 없음"));
        }

        [Test]
        public void WorkflowRejectsRepeatedScanAndClearsStaleProduct()
        {
            var flow = new ProductWorkflow(new ProductCatalog(Json));
            Assert.That(flow.AcceptBarcode("8801104671507"), Is.True);
            Assert.That(flow.CanPlace, Is.True);
            Assert.That(flow.AcceptBarcode("4006381333931"), Is.False);
            flow.MarkPlaced();
            Assert.That(flow.Phase, Is.EqualTo(ProductPhase.Placed));
            flow.RequestPlacement();
            Assert.That(flow.CanPlace, Is.True);
            flow.StartScan();
            Assert.That(flow.Selected, Is.Null);
            flow.AcceptBarcode("4006381333931");
            Assert.That(flow.Phase, Is.EqualTo(ProductPhase.UnknownBarcode));
            Assert.That(flow.CanPlace, Is.False);
            flow.StartScan();
            flow.ScanTimedOut();
            Assert.That(flow.Phase, Is.EqualTo(ProductPhase.ScanFailed));
            Assert.That(new ProductWorkflow(null).Phase, Is.EqualTo(ProductPhase.DataUnavailable));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DecoderReadsGeneratedEan13IncludingNinetyDegreeRotation(bool rotate)
        {
            const int width = 960, height = 320;
            var bits = new MultiFormatWriter().encode("8801104671507", BarcodeFormat.EAN_13, width, height);
            var pixels = new byte[width * height];
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    pixels[rotate ? x * height + (height - y - 1) : y * width + x] = bits[x, y] ? (byte)0 : (byte)255;
            var result = new BarcodeDecoder().Decode(pixels, rotate ? height : width, rotate ? width : height);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Text, Is.EqualTo("8801104671507"));
            Assert.That(result.Format, Is.EqualTo("EAN_13"));
        }

        [Test]
        public void BarcodeCameraPrefersDetailedCpuFramesWithinBoundedMemory()
        {
            Assert.That(CameraResolutionPolicy.Score(1920, 1080, 30), Is.GreaterThan(CameraResolutionPolicy.Score(640, 480, 60)));
            Assert.That(CameraResolutionPolicy.Score(1280, 720, 30), Is.GreaterThan(CameraResolutionPolicy.Score(640, 480, 30)));
            Assert.That(CameraResolutionPolicy.Score(1920, 1080, 30), Is.GreaterThan(CameraResolutionPolicy.Score(1920, 1080, 60)));
            Assert.That(CameraResolutionPolicy.Score(3840, 2160, 30), Is.Zero);
            Assert.That(CameraResolutionPolicy.Score(0, 1080, 30), Is.Zero);
        }

        [Test]
        public void BarcodeNeedsThreeConsecutiveMatchingCameraFrames()
        {
            var confirmation = new BarcodeConfirmation();
            var known = new BarcodeReadResult("8801104671507", "EAN_13");
            Assert.That(confirmation.Observe(known, 0), Is.False);
            Assert.That(confirmation.Observe(known, 0.35f), Is.False);
            Assert.That(confirmation.Observe(known, 0.7f), Is.True);
            confirmation.Reset();
            Assert.That(confirmation.Count, Is.Zero);
            Assert.That(confirmation.Observe(known, 1), Is.False, "Rescan must not reuse previous confidence");
        }

        [Test]
        public void BarcodeDisagreementMissingFramesAndExpiredReadsResetConfidence()
        {
            var confirmation = new BarcodeConfirmation();
            var known = new BarcodeReadResult("8801104671507", "EAN_13");
            foreach (var text in new[] { "5505256701701", "7201143677507" })
                Assert.That(confirmation.Observe(new BarcodeReadResult(text, "EAN_13"), 0), Is.False);
            Assert.That(confirmation.Observe(known, 0.35f), Is.False);
            Assert.That(confirmation.Count, Is.EqualTo(1));
            Assert.That(confirmation.Observe(null, 0.7f), Is.False);
            Assert.That(confirmation.Observe(known, 1.05f), Is.False);
            Assert.That(confirmation.Count, Is.EqualTo(1));
            Assert.That(confirmation.Observe(known, 3), Is.False);
            Assert.That(confirmation.Count, Is.EqualTo(1));
            Assert.That(confirmation.Observe(new BarcodeReadResult("8801104671508", "EAN_13"), 3.35f), Is.False);
            Assert.That(confirmation.Count, Is.Zero);
        }

        [TestCase("5505256701701")]
        [TestCase("7201143677507")]
        public void ConfirmedUnknownBarcodeNeverFallsBackToDemoProduct(string barcode)
        {
            var confirmation = new BarcodeConfirmation();
            var read = new BarcodeReadResult(barcode, "EAN_13");
            confirmation.Observe(read, 0);
            confirmation.Observe(read, 0.35f);
            Assert.That(confirmation.Observe(read, 0.7f), Is.True);
            var flow = new ProductWorkflow(new ProductCatalog(Json));
            Assert.That(flow.AcceptBarcode(confirmation.Candidate), Is.True);
            Assert.That(flow.LastBarcode, Is.EqualTo(barcode));
            Assert.That(flow.Phase, Is.EqualTo(ProductPhase.UnknownBarcode));
            Assert.That(flow.Selected, Is.Null);
        }

        [Test]
        public void SceneHasWiredComponentsAndTrackedCamera()
        {
            var scene = EditorSceneManager.OpenScene(ProjectBuilder.ScenePath, OpenSceneMode.Single);
            var components = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            Assert.That(components.All(component => component != null), Is.True, "Missing scene script");
            Assert.That(components.OfType<ARSession>().Count(), Is.EqualTo(1));
            Assert.That(components.OfType<TrackedPoseDriver>().Count(), Is.EqualTo(1));
            Assert.That(components.OfType<BarcodeProductPanel>().Count(), Is.EqualTo(1));
            Assert.That(components.OfType<ProductPlacement>().Single().enabled, Is.False, "Follow mode must not also handle placement touches.");
            Assert.That(components.OfType<ARPlaneManager>().Single().enabled, Is.False);
            Assert.That(EditorBuildSettings.scenes.Single(s => s.enabled).path, Is.EqualTo(ProjectBuilder.ScenePath));
            foreach (var component in components.Where(c => c.GetType().Namespace == "MidtermAR"))
            {
                var iterator = new SerializedObject(component).GetIterator();
                while (iterator.NextVisible(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                        Assert.That(iterator.objectReferenceValue, Is.Not.Null, component.name + ": " + iterator.propertyPath);
            }
        }

        [Test]
        public void PlaneVisualizationCannotBeRestoredByThePackagesNextUpdate() // 패키지의 다음 프레임에도 평면 숨김이 유지되는지 검증
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/PlacementPlane.prefab");
            var sessionField = typeof(ARSession).GetField("s_State", BindingFlags.Static | BindingFlags.NonPublic);
            var previousSession = sessionField.GetValue(null);
            var visualizer = root.GetComponent<ARPlaneMeshVisualizer>();
            Mesh createdMesh = null;
            try
            {
                sessionField.SetValue(null, ARSessionState.SessionTracking);
                SetPlaneData(root.GetComponent<ARPlane>(), TrackingState.Tracking);
                // EditMode에서 실제 패키지의 프레임 로직을 직접 실행해 기기 없이 표시 경쟁을 재현한다.
                typeof(ARPlaneMeshVisualizer).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(visualizer, null);
                createdMesh = visualizer.mesh;
                var packageUpdate = typeof(ARPlaneMeshVisualizer).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                var applyVisibility = typeof(ProductPlacement).GetMethod("SetPlaneVisualization", BindingFlags.Static | BindingFlags.NonPublic);
                var renderer = root.GetComponent<MeshRenderer>();
                renderer.enabled = false;
                packageUpdate.Invoke(visualizer, null);
                Assert.That(renderer.enabled, Is.True, "Old renderer-only hiding is overwritten by the actual package update.");

                applyVisibility.Invoke(null, new object[] { root.GetComponent<ARPlane>(), false });
                Assert.That(visualizer.enabled, Is.False);
                for (var frame = 0; frame < 4; frame++)
                {
                    // 비활성 컴포넌트의 Update까지 강제로 호출해도 패키지가 다시 표시하지 못해야 한다.
                    packageUpdate.Invoke(visualizer, null);
                    Assert.That(renderer.enabled, Is.False, "Hidden plane was restored on frame " + frame);
                }

                applyVisibility.Invoke(null, new object[] { root.GetComponent<ARPlane>(), true });
                packageUpdate.Invoke(visualizer, null);
                Assert.That(visualizer.enabled, Is.True);
                Assert.That(renderer.enabled, Is.True, "Repositioning must allow the tracked plane to appear again.");
            }
            finally
            {
                sessionField.SetValue(null, previousSession);
                if (createdMesh != null) UnityEngine.Object.DestroyImmediate(createdMesh);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void SubsumedAndLimitedPlanesAreNotPlacementCandidates() // 병합되거나 추적 제한된 평면의 배치 제외 검증
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/PlacementPlane.prefab");
            var replacement = new GameObject("Replacement plane");
            try
            {
                var plane = root.GetComponent<ARPlane>();
                var replacementPlane = replacement.AddComponent<ARPlane>();
                var usable = typeof(ProductPlacement).GetMethod("IsUsablePlane", BindingFlags.Static | BindingFlags.NonPublic);
                SetPlaneData(plane, TrackingState.Tracking);
                Assert.That(usable.Invoke(null, new object[] { plane }), Is.True);
                typeof(ARPlane).GetProperty("subsumedBy").SetValue(plane, replacementPlane);
                Assert.That(usable.Invoke(null, new object[] { plane }), Is.False, "A merged plane must not count as a placement surface.");
                typeof(ARPlane).GetProperty("subsumedBy").SetValue(plane, null);
                SetPlaneData(plane, TrackingState.Limited);
                Assert.That(usable.Invoke(null, new object[] { plane }), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(replacement);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetPlaneData(ARPlane plane, TrackingState state)
        {
            var data = new BoundedPlane(new TrackableId(1, 1), TrackableId.invalidId, Pose.identity,
                Vector2.zero, Vector2.one, PlaneAlignment.HorizontalUp, state, IntPtr.Zero,
                PlaneClassifications.Table, TrackableId.invalidId);
            typeof(ARTrackable<BoundedPlane, ARPlane>).GetProperty("sessionRelativeData", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(plane, data);
        }

        [Test]
        public void KoreanWorldPanelHasGlyphsAndTextFits()
        {
            var root = PrefabUtility.LoadPrefabContents(ProjectBuilder.PanelPath);
            try
            {
                var catalog = new ProductCatalog(Json);
                catalog.TryFind("8801104671507", out var product);
                root.GetComponent<WorldProductPanel>().Bind(product);
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    foreach (var character in text.text.Where(c => !char.IsWhiteSpace(c)))
                        Assert.That(text.font.HasCharacter(character, true, true), Is.True, "Missing glyph " + character);
                    text.ForceMeshUpdate(true, true);
                    Assert.That(text.isTextOverflowing, Is.False, text.name + ": text overflow");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
