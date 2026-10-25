using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MidtermAR.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARSubsystems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

namespace MidtermAR.Tests
{
    public sealed class ProductImageTests
    {
        private const string Barcode = "8801104671507";
        private static ProductCatalog Products() => new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json"));
        private static string ImagesJson => File.ReadAllText("Assets/Data/product-images.json");
        private static ProductSnapshot Product()
        {
            Products().TryFind(Barcode, out var product);
            return product;
        }
        private static ProductImageBinding Binding() => new ProductImageBinding
        { barcode = Barcode, referenceImageName = "test-card", displayName = "브이젤 카드 · 개발용", developmentOnly = true };

        [TestCase(true, TrackingState.Tracking, 1, true)]
        [TestCase(false, TrackingState.Tracking, 1, false)]
        [TestCase(true, TrackingState.Tracking, 2, false)]
        [TestCase(true, TrackingState.Tracking, -1, false)]
        [TestCase(true, TrackingState.Limited, 1, false)]
        [TestCase(true, TrackingState.None, 0, false)]
        public void InformationRequiresMatchingCurrentImage(bool ready, TrackingState tracking, int method, bool expected)
        {
            var state = new ProductImageState();
            state.Select(Binding());
            state.BeginFrame(ready);
            state.ObserveImage("test-card", tracking, method);
            Assert.That(state.CanDisplay, Is.EqualTo(expected));
        }

        [Test]
        public void TurningFromBarcodeToFrontRetainsSelectionButImageLossHidesPanel()
        {
            var state = new ProductImageState();
            state.Select(Binding());
            for (var frame = 0; frame < 100; frame++)
            {
                state.ObserveBarcode(null);
                state.BeginFrame(true);
                state.ObserveImage("test-card", TrackingState.Tracking, 1);
                Assert.That(state.CanDisplay, Is.True, "Barcode absence must not expire an image-tracked selection.");
            }
            state.BeginFrame(true);
            Assert.That(state.CanDisplay, Is.False, "Image loss must hide the old pose immediately.");
            Assert.That(state.Binding.barcode, Is.EqualTo(Barcode));
            state.ObserveImage("test-card", TrackingState.Tracking, 1);
            Assert.That(state.CanDisplay, Is.True, "Reacquisition uses the same selected product.");
        }

        [Test]
        public void DifferentBarcodeBlocksUntilExplicitRescanAndNeverSwitchesTheProduct()
        {
            var state = new ProductImageState();
            state.Select(Binding());
            state.BeginFrame(true);
            state.ObserveImage("test-card", TrackingState.Tracking, 1);
            state.ObserveBarcode(new BarcodeReadResult("4006381333931", "EAN_13"));
            Assert.That(state.CanDisplay, Is.False);
            Assert.That(state.ConflictingBarcode, Is.EqualTo("4006381333931"));
            state.ObserveBarcode(new BarcodeReadResult(Barcode, "EAN_13"));
            state.BeginFrame(true);
            state.ObserveImage("test-card", TrackingState.Tracking, 1);
            Assert.That(state.CanDisplay, Is.False, "The original barcode does not silently clear a different-product warning.");
            Assert.That(state.Binding.barcode, Is.EqualTo(Barcode));
            state.Select(null);
            Assert.That(state.CanDisplay, Is.False);
            Assert.That(state.ConflictingBarcode, Is.Null);
            state.Select(Binding());
            state.BeginFrame(true);
            state.ObserveImage("test-card", TrackingState.Tracking, 1);
            Assert.That(state.CanDisplay, Is.True);
        }

        [TestCase("8801104671508", "EAN_13")]
        [TestCase("4006381333931", "QR_CODE")]
        public void InvalidOrOtherFormatReadCannotChangeImageSelection(string text, string format)
        {
            var state = new ProductImageState();
            state.Select(Binding());
            state.ObserveBarcode(new BarcodeReadResult(text, format));
            Assert.That(state.ConflictingBarcode, Is.Null);
        }

        [Test]
        public void OtherImageOrSimultaneousImagesCannotDisplaySelectedInformation()
        {
            var state = new ProductImageState();
            state.Select(Binding());
            state.BeginFrame(true);
            state.ObserveImage("another-product", TrackingState.Tracking, 1);
            Assert.That(state.CanDisplay, Is.False);
            state.ObserveImage("test-card", TrackingState.Tracking, 1);
            Assert.That(state.CanDisplay, Is.False, "A matching image does not override a simultaneously tracked different image.");
            state.BeginFrame(true);
            state.ObserveImage("another-product", TrackingState.Tracking, 2);
            state.ObserveImage("test-card", TrackingState.Tracking, 1);
            Assert.That(state.CanDisplay, Is.True, "A lost different image must not block reacquisition forever.");
        }

        [TestCase("unknown-barcode")]
        [TestCase("duplicate-barcode")]
        [TestCase("duplicate-name")]
        [TestCase("empty-name")]
        [TestCase("unknown-field")]
        public void InvalidImageMappingsFailClosed(string problem)
        {
            var doc = JObject.Parse(ImagesJson);
            var list = (JArray)doc["images"];
            if (problem == "unknown-barcode") list[0]["barcode"] = "4006381333931";
            if (problem == "duplicate-barcode") list.Add(list[0].DeepClone());
            var products = Products();
            if (problem == "duplicate-name")
            {
                var second = list[0].DeepClone();
                second["barcode"] = "4006381333931";
                list.Add(second);
                var productsJson = JObject.Parse(File.ReadAllText("Assets/Data/demo-products.json"));
                var secondProduct = productsJson["products"][0].DeepClone();
                secondProduct["barcode"] = "4006381333931";
                secondProduct["catalogProductId"] = "11111111-1111-1111-1111-111111111111";
                secondProduct["nutrition"]["sourceCatalogProductId"] = "11111111-1111-1111-1111-111111111111";
                ((JArray)productsJson["products"]).Add(secondProduct);
                products = new ProductCatalog(productsJson.ToString());
            }
            if (problem == "empty-name") list[0]["referenceImageName"] = " ";
            if (problem == "unknown-field") list[0]["referenceImageNam"] = "typo";
            Assert.Throws<FormatException>(() => new ProductImageCatalog(doc.ToString(), products));
        }

        [Test]
        public void ProductionMarkerRequiresMeasuredSizeAndRegisteredImageName()
        {
            var library = Object.Instantiate(AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(ProjectBuilder.ImageLibraryPath));
            try
            {
                var dev = new ProductImageCatalog(ImagesJson, Products());
                Assert.DoesNotThrow(() => dev.ValidateLibrary(library));
                var json = JObject.Parse(ImagesJson);
                json["images"][0]["developmentOnly"] = false;
                var production = new ProductImageCatalog(json.ToString(), Products());
                Assert.Throws<FormatException>(() => production.ValidateLibrary(library));
                library.SetSpecifySize(0, true);
                library.SetSize(0, new Vector2(0.063f, 0.088f)); // 검증용 가상 입력이며 실측값으로 저장하지 않는다.
                Assert.DoesNotThrow(() => production.ValidateLibrary(library));
                library.SetName(0, "unmapped-image");
                Assert.Throws<FormatException>(() => production.ValidateLibrary(library));
            }
            finally { Object.DestroyImmediate(library); }
        }

        [Test]
        public void ProductSceneConnectsOneCameraImageTrackingAndTouchPanel()
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var presenter = Object.FindFirstObjectByType<TrackedImageProductPanel>();
            Assert.That(presenter, Is.Not.Null);
            var images = Object.FindFirstObjectByType<ARTrackedImageManager>();
            Assert.That(images.referenceLibrary, Is.EqualTo(AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(ProjectBuilder.ImageLibraryPath)));
            new ProductImageCatalog(ImagesJson, Products()).ValidateLibrary(images.referenceLibrary);
            Assert.That(Object.FindObjectsByType<ARCameraManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindFirstObjectByType<BarcodeProductPanel>().enabled, Is.False);
            Assert.That(Object.FindFirstObjectByType<ARRaycastManager>().enabled, Is.False);
            Assert.That(Object.FindFirstObjectByType<ARAnchorManager>().enabled, Is.False);
            var scanner = Object.FindFirstObjectByType<ARBarcodeScanner>();
            Assert.That(new SerializedObject(scanner).FindProperty("captureViewportCoordinates").boolValue, Is.False);
            var panel = AssetDatabase.LoadAssetAtPath<WorldProductPanel>(ProjectBuilder.PanelPath);
            Assert.That(panel.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(panel.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(panel.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(3));
        }

        [Test]
        public void ActualAppBarcodeToPanelAndRescanButtonClearAllSelection()
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var app = Object.FindFirstObjectByType<ProductARApp>();
            var presenter = Object.FindFirstObjectByType<TrackedImageProductPanel>();
            Call(app, "Awake");
            Call(app, "OnEnable");
            try
            {
                Call(app, "OnDecoded", new BarcodeReadResult(Barcode, "EAN_13"));
                var workflow = Field<ProductWorkflow>(app, "workflow");
                Assert.That(workflow.Phase, Is.EqualTo(ProductPhase.TrackingImage));
                Assert.That(presenter.Binding.developmentOnly, Is.True);
                var panel = Field<WorldProductPanel>(presenter, "panel");
                panel.ShowPrice();
                Assert.That(Text(panel, "provenance"), Does.Contain("상품 포장 인식 아님"));
                Call(app, "OnObserved", null, default(BarcodeViewportTransform), Time.unscaledTime);
                Assert.That(workflow.Selected.barcode, Is.EqualTo(Barcode));
                Click(panel.transform.Find("Rescan").GetComponent<Button>());
                Assert.That(workflow.Phase, Is.EqualTo(ProductPhase.Scanning));
                Assert.That(workflow.Selected, Is.Null);
                Assert.That(presenter.Binding, Is.Null);
                Assert.That(presenter.HasVisibleTarget, Is.False);
                Assert.That(panel.CurrentTab, Is.EqualTo(ProductInfoTab.Nutrition));
                Assert.That(Text(panel, "price"), Is.Empty);
                Call(app, "OnDecoded", new BarcodeReadResult("4006381333931", "EAN_13"));
                Assert.That(workflow.Phase, Is.EqualTo(ProductPhase.UnknownBarcode));
                Assert.That(workflow.Selected, Is.Null);
                Assert.That(presenter.Binding, Is.Null);
            }
            finally
            {
                Call(app, "OnDisable");
                DestroyPanel(presenter);
            }
        }

        [Test]
        public void PanelFollowsImagePositionRotationAndScaleAndPauseClearsPoseOnly()
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var presenter = Object.FindFirstObjectByType<TrackedImageProductPanel>();
            var image = new GameObject("Pose input");
            try
            {
                presenter.SetProduct(Product(), Binding());
                var panel = Field<WorldProductPanel>(presenter, "panel");
                var canvas = panel.GetComponent<Canvas>();
                Assert.That(canvas.worldCamera, Is.EqualTo(Camera.main));
                panel.ShowPrice();
                foreach (var rotation in new[] { Quaternion.identity, Quaternion.Euler(28, 65, -17) })
                {
                    image.transform.SetPositionAndRotation(new Vector3(1, 2, 3), rotation);
                    Assert.That(presenter.TryApplyPose(image.transform, new Vector2(0.1f, 0.15f)), Is.True);
                    Assert.That(Vector3.Distance(panel.transform.position, image.transform.TransformPoint(new Vector3(0, 0.008f, 0))), Is.LessThan(0.00001f));
                    Assert.That(Quaternion.Angle(panel.transform.rotation, rotation * Quaternion.Euler(90, 0, 0)), Is.LessThan(0.001f));
                    Assert.That(((RectTransform)panel.transform).rect.width * panel.transform.lossyScale.x, Is.EqualTo(0.16f).Within(0.00001f));
                }
                Assert.That(presenter.TryApplyPose(image.transform, new Vector2(float.NaN, 1)), Is.False);
                Assert.That(presenter.TryApplyPose(image.transform, Vector2.zero), Is.False);
                Field<HashSet<TrackableId>>(presenter, "freshImages").Add(new TrackableId(7, 1));
                Call(presenter, "OnCameraFrame", default(ARCameraFrameEventArgs));
                Assert.That(Field<float>(presenter, "lastCameraFrame"), Is.Not.EqualTo(float.NegativeInfinity));
                panel.gameObject.SetActive(true);
                Call(presenter, "OnApplicationPause", true);
                Assert.That(presenter.HasVisibleTarget, Is.False);
                Call(presenter, "OnApplicationPause", false);
                Assert.That(Field<HashSet<TrackableId>>(presenter, "freshImages"), Is.Empty);
                Assert.That(Field<float>(presenter, "lastCameraFrame"), Is.EqualTo(float.NegativeInfinity));
                Assert.That(presenter.Binding.barcode, Is.EqualTo(Barcode));
                Assert.That(panel.CurrentTab, Is.EqualTo(ProductInfoTab.Price), "Pose recovery must not reset the user's tab.");
                Assert.Throws<ArgumentException>(() => presenter.SetProduct(new ProductSnapshot { barcode = "4006381333931" }, Binding()));
            }
            finally { Object.DestroyImmediate(image); DestroyPanel(presenter); }
        }

        [TestCase("complete")]
        [TestCase("partial")]
        [TestCase("missing")]
        public void WorldTabsPreserveDataSourcesMissingValuesAndClearPreviousData(string dataState)
        {
            var root = PrefabUtility.LoadPrefabContents(ProjectBuilder.PanelPath);
            try
            {
                var panel = root.GetComponent<WorldProductPanel>();
                var product = Product();
                panel.Bind(product, Binding());
                if (dataState == "partial") { product.price.unitPriceKrw = 0; product.nutrition.sugarsGrams = 0; product.nutrition.fatGrams = null; }
                if (dataState == "missing") { product.price = null; product.nutrition = null; }
                panel.Bind(product, Binding());
                Click(root.transform.Find("Information Tabs/Price Tab").GetComponent<Button>());
                Assert.That(panel.CurrentTab, Is.EqualTo(ProductInfoTab.Price));
                Assert.That(root.transform.Find("Price Page").gameObject.activeSelf, Is.True);
                Assert.That(root.transform.Find("Nutrition Page").gameObject.activeSelf, Is.False);
                if (dataState == "missing")
                {
                    Assert.That(Text(panel, "price"), Is.EqualTo("가격 정보 없음"));
                    Assert.That(Text(panel, "priceSource"), Does.Not.Contain("1,400").And.Not.Contain("2026-05-08"));
                    Assert.That(Text(panel, "nutrition"), Is.EqualTo("영양정보 없음"));
                    Assert.That(Text(panel, "nutritionSource"), Does.Not.Contain("pricetrace_manual"));
                }
                else
                {
                    Assert.That(Text(panel, "price"), Is.EqualTo(dataState == "partial" ? "0원" : "1,400원"));
                    Assert.That(Text(panel, "priceSource"), Does.Contain(product.price.seller).And.Contain(product.price.observedAt).And.Contain(product.price.displaySource));
                    Assert.That(Text(panel, "nutritionSource"), Does.Contain(product.nutrition.displaySource));
                    Assert.That(Text(panel, "nutrition"), Does.Contain(dataState == "partial" ? "당류 0g" : "당류 7g"));
                    Assert.That(Text(panel, "nutrition"), Does.Contain(dataState == "partial" ? "지방 정보 없음" : "지방 7g"));
                }
                Click(root.transform.Find("Information Tabs/Nutrition Tab").GetComponent<Button>());
                Assert.That(panel.CurrentTab, Is.EqualTo(ProductInfoTab.Nutrition));
                Assert.That(root.transform.Find("Nutrition Page").gameObject.activeSelf, Is.True);
                panel.Bind(null);
                foreach (var field in new[] { "productTitle", "specification", "price", "priceSource", "nutrition", "nutritionSource", "provenance" })
                    Assert.That(Text(panel, field), Is.Empty, field + " must clear on rescan.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WorldSpaceButtonHasPointerTargetAndChangesTab(bool onImagePlane)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ProjectBuilder.PanelPath));
            var cameraObject = new GameObject("UI test camera", typeof(Camera));
            var eventObject = new GameObject("UI test event system", typeof(EventSystem));
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -1);
                camera.orthographic = true;
                camera.orthographicSize = 0.3f;
                camera.pixelRect = new Rect(0, 0, 1080, 1920);
                if (onImagePlane)
                {
                    root.transform.rotation = Quaternion.Euler(90, 0, 0);
                    camera.transform.SetPositionAndRotation(new Vector3(0, 1, 0), Quaternion.Euler(90, 0, 0));
                }
                root.GetComponent<Canvas>().worldCamera = camera;
                Canvas.ForceUpdateCanvases();
                var button = root.transform.Find("Information Tabs/Price Tab").GetComponent<Button>();
                EnableEditorClick(button);
                var pointer = new PointerEventData(eventObject.GetComponent<EventSystem>())
                { position = camera.WorldToScreenPoint(button.transform.TransformPoint(((RectTransform)button.transform).rect.center)) };
                // EditMode에서는 렌더되지 않은 Graphic.depth가 -1이다. 네이티브 draw 결과를 흉내 내지 않고
                // 실제 UI API의 포인터 영역, 카메라 연결과 버튼 이벤트 전달을 검증한다.
                Assert.That(root.GetComponent<GraphicRaycaster>().eventCamera, Is.EqualTo(camera));
                Assert.That(button.targetGraphic.raycastTarget, Is.True);
                Assert.That(RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform, pointer.position, camera), Is.True);
                Assert.That(RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform, new Vector2(-10000, -10000), camera), Is.False);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Assert.That(root.GetComponent<WorldProductPanel>().CurrentTab, Is.EqualTo(ProductInfoTab.Price));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(eventObject); }
        }

        private static string Text(WorldProductPanel panel, string field)
            => ((TMP_Text)new SerializedObject(panel).FindProperty(field).objectReferenceValue).text;
        private static T Field<T>(object target, string name)
            => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Call(object target, string method, params object[] args)
            => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private static void Click(Button button) { EnableEditorClick(button); button.onClick.Invoke(); }
        private static void EnableEditorClick(Button button)
        {
            Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1), "The prefab must wire the actual button handler.");
            // 프리팹의 RuntimeOnly 리스너를 저장하지 않고 EditMode에서 실행한다.
            button.onClick.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
        }
        private static void DestroyPanel(TrackedImageProductPanel presenter)
        {
            var panel = Field<WorldProductPanel>(presenter, "panel");
            if (panel != null) Object.DestroyImmediate(panel.gameObject);
        }
    }
}
