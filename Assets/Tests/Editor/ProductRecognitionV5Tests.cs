using System;
using System.IO;
using System.Reflection;
using MidtermAR.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

namespace MidtermAR.Tests
{
    public sealed class ProductRecognitionV5Tests
    {
        private const string A = "1516c3eb-3dfb-4e11-8040-d23fadcdaad0";
        private const string B = "11111111-1111-1111-1111-111111111111";
        private const string C = "22222222-2222-2222-2222-222222222222";
        private const string D = "33333333-3333-3333-3333-333333333333";
        private const string EanA = "8801104671507";
        private const string EanC = "5901234123457";
        private const string Unknown = "4006381333931";
        private ProductCatalog products;
        private ProductImageCatalog images;
        private ProductRecognitionState recognition;

        [SetUp]
        public void Setup()
        {
            // 이 상품 ID·데이터는 자동 시험 전용이며 운영 카탈로그에 저장하지 않는다.
            var template = JObject.Parse(File.ReadAllText("Assets/Data/demo-products.json"))["products"][0];
            var list = new JArray();
            foreach (var item in new[] { (A, EanA), (B, (string)null), (C, EanC), (D, (string)null) })
            {
                var product = template.DeepClone();
                product["catalogProductId"] = item.Item1;
                product["barcode"] = item.Item2;
                product["barcodeFormat"] = item.Item2 == null ? null : "EAN_13";
                product["price"] = null; product["nutrition"] = null;
                list.Add(product);
            }
            products = new ProductCatalog(new JObject { ["schemaVersion"] = 1, ["snapshotDate"] = "2026-10-26", ["products"] = list }.ToString());
            images = new ProductImageCatalog(new JObject { ["schemaVersion"] = 2, ["images"] = new JArray
            {
                Link("a-top", A), Link("a-bottom", A), Link("b", B)
            } }.ToString(), products);
            recognition = new ProductRecognitionState(products, images);
        }

        private static JObject Link(string name, string id) => new JObject
        { ["referenceImageName"] = name, ["catalogProductId"] = id, ["displayName"] = name, ["developmentOnly"] = true,
            ["productWidthMetres"] = .058, ["productWidthSource"] = "synthetic common width for tests" };
        private ProductSnapshot Product(string id) { Assert.That(products.TryFindById(id, out var product), Is.True); return product; }
        private void Confirm(string barcode, float start = 1)
        {
            for (var i = 0; i < 3; i++) recognition.ObserveBarcode(new BarcodeReadResult(barcode, "EAN_13"), start + i * 0.2f, start + i * 0.2f);
        }

        [TestCase(A, ProductRegistration.Both)]
        [TestCase(B, ProductRegistration.ImageOnly)]
        [TestCase(C, ProductRegistration.BarcodeOnly)]
        [TestCase(D, ProductRegistration.Neither)]
        public void RegistrationDescribesDataNotAnUnknownFrontEvent(string id, ProductRegistration expected)
            => Assert.That(recognition.Registration(Product(id)), Is.EqualTo(expected));

        [TestCase("a-top", A)]
        [TestCase("b", B)]
        public void FrontRecognitionNeedsNoBarcodeSelection(string name, string id)
        {
            var state = new ProductImageState(products, images);
            state.BeginFrame(true); state.ObserveImage(name, TrackingState.Tracking, 1); state.EndFrame();
            recognition.Resolve(state.Product, state.HasImageConflict, 1);
            Assert.That(state.CanDisplay, Is.True);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.Image3D));
            Assert.That(recognition.Selected.catalogProductId, Is.EqualTo(id));
            Assert.That(recognition.ConfirmedBarcode, Is.Null);
        }

        [TestCase(true, TrackingState.Tracking, 1, true)]
        [TestCase(false, TrackingState.Tracking, 1, false)]
        [TestCase(true, TrackingState.Tracking, 2, false)]
        [TestCase(true, TrackingState.Tracking, -1, false)]
        [TestCase(true, TrackingState.Limited, 1, false)]
        [TestCase(true, TrackingState.None, 0, false)]
        public void FrontRequiresCurrentFullTracking(bool ready, TrackingState tracking, int method, bool expected)
        {
            var state = new ProductImageState(products, images);
            state.BeginFrame(ready); state.ObserveImage("a-top", tracking, method);
            Assert.That(state.CanDisplay, Is.EqualTo(expected));
        }

        [Test]
        public void SameProductCropsDoNotConflictAndPreferPreviousRegion()
        {
            var state = new ProductImageState(products, images);
            state.BeginFrame(true); state.ObserveImage("a-top", TrackingState.Tracking, 1); state.EndFrame();
            state.BeginFrame(true); state.ObserveImage("a-bottom", TrackingState.Tracking, 1); state.ObserveImage("a-top", TrackingState.Tracking, 1); state.EndFrame();
            Assert.That(state.HasImageConflict, Is.False);
            Assert.That(state.Binding.referenceImageName, Is.EqualTo("a-top"));
            state.BeginFrame(true); state.ObserveImage("a-bottom", TrackingState.Tracking, 1); state.EndFrame();
            Assert.That(state.CanDisplay, Is.True);
            Assert.That(state.Binding.referenceImageName, Is.EqualTo("a-bottom"));
            state.BeginFrame(true);
            Assert.That(state.CanDisplay, Is.False);
            state.ObserveImage("a-top", TrackingState.Tracking, 1);
            Assert.That(state.CanDisplay, Is.True);
        }

        [Test]
        public void OtherProductsConflictButLostOrUnmappedImagesDoNot()
        {
            var state = new ProductImageState(products, images);
            state.BeginFrame(true); state.ObserveImage("a-top", TrackingState.Tracking, 1); state.ObserveImage("b", TrackingState.Tracking, 1);
            Assert.That(state.CanDisplay, Is.False);
            recognition.Resolve(null, state.HasImageConflict, 1);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.Conflict));
            state.BeginFrame(true); state.ObserveImage("b", TrackingState.Tracking, 2); state.ObserveImage("a-top", TrackingState.Tracking, 1); state.ObserveImage("not-registered", TrackingState.Tracking, 1);
            recognition.Resolve(state.Product, state.HasImageConflict, 2);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.Image3D));
        }

        [TestCase(EanA, ProductDisplayRoute.Barcode2D, "앞면 이미지 등록됨")]
        [TestCase(EanC, ProductDisplayRoute.Barcode2D, "앞면 미등록")]
        [TestCase(Unknown, ProductDisplayRoute.UnknownBarcode, "기준 미등록")]
        public void BarcodeAloneResolvesKnownAndUnknownProducts(string ean, ProductDisplayRoute expected, string message)
        {
            Confirm(ean); recognition.Resolve(null, false, 1.4f);
            Assert.That(recognition.Route, Is.EqualTo(expected));
            Assert.That(expected == ProductDisplayRoute.UnknownBarcode ? recognition.UnknownMessage : recognition.BarcodeBadge(recognition.Selected), Does.Contain(message));
            if (expected == ProductDisplayRoute.UnknownBarcode)
                Assert.That(recognition.UnknownMessage, Does.Contain("로컬 카탈로그(2026-10-26)").And.Contain(ean).And.Not.Contain("PriceTrace 미등록"));
        }

        [Test]
        public void UnconfirmedInvalidFutureOldAndRepeatedFramesCannotDeclareUnknown()
        {
            recognition.ObserveBarcode(new BarcodeReadResult(Unknown, "EAN_13"), 1, 1);
            recognition.ObserveBarcode(new BarcodeReadResult(Unknown, "EAN_13"), 1, 1);
            recognition.ObserveBarcode(new BarcodeReadResult(Unknown, "EAN_13"), 1, 1);
            Assert.That(recognition.ConfirmationCount, Is.EqualTo(1));
            recognition.ObserveBarcode(new BarcodeReadResult(Unknown, "EAN_13"), 1.2f, 1.2f);
            recognition.Resolve(null, false, 1.2f);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.None));
            Assert.That(recognition.ObserveBarcode(new BarcodeReadResult(Unknown, "EAN_13"), 5, 2), Is.False);
            Assert.That(recognition.ObserveBarcode(new BarcodeReadResult(Unknown, "EAN_13"), 2, 5), Is.False);
            Assert.That(recognition.ObserveBarcode(new BarcodeReadResult(Unknown, "EAN_13"), float.NaN, 5), Is.False);
            recognition.ObserveBarcode(new BarcodeReadResult("8801104671508", "EAN_13"), 5, 5);
            recognition.ObserveBarcode(new BarcodeReadResult(Unknown, "QR_CODE"), 5.2f, 5.2f);
            recognition.Resolve(null, false, 5.2f);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.None));
            Assert.That(recognition.ConfirmedBarcode, Is.Null);
        }

        [Test]
        public void MatchingBarcodeAndImagePrioritize3DThenReturnToIndependent2D()
        {
            Confirm(EanA); recognition.Resolve(Product(A), false, 1.4f);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.Image3D));
            recognition.Resolve(null, false, 1.5f);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.Barcode2D));
            recognition.Resolve(null, false, 3);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.None));
            recognition.Resolve(Product(A), false, 4);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.Image3D));
        }

        [TestCase(EanC)]
        [TestCase(Unknown)]
        public void DifferentConfirmedBarcodeHidesBothOnlyWhileConflictIsVisible(string ean)
        {
            recognition.Resolve(Product(A), false, 0);
            Confirm(ean); recognition.Resolve(Product(A), false, 1.4f);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.Conflict));
            recognition.Resolve(null, false, 10);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.None));
            Assert.That(recognition.Selected, Is.Null);
            Assert.That(recognition.ConfirmedBarcode, Is.Null);
            recognition.Resolve(Product(B), false, 11);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.Image3D));
        }

        [Test]
        public void SuspendRejectsOldResultsAndRequiresNewBarcodeConfirmation()
        {
            Confirm(EanA); recognition.Resolve(null, false, 1.4f);
            recognition.Suspend(); recognition.Resolve(null, false, 2);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.None));
            Assert.That(recognition.Selected, Is.Null);
            Assert.That(recognition.ConfirmedBarcode, Is.Null);
            recognition.ObserveBarcode(new BarcodeReadResult(EanA, "EAN_13"), 3, 3);
            recognition.Resolve(null, false, 3);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.None));
        }

        [TestCase(EanA)]
        [TestCase(Unknown)]
        public void MissingBarcodeFrameReleasesKnownAndUnknownResultsImmediately(string ean)
        {
            Confirm(ean); recognition.Resolve(null, false, 1.4f);
            recognition.ObserveBarcode(null, 1.5f, 1.5f);
            recognition.Resolve(null, false, 1.5f);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.None));
            Assert.That(recognition.Selected, Is.Null);
            Assert.That(recognition.ConfirmedBarcode, Is.Null);
            Assert.That(recognition.ConfirmationCount, Is.Zero);
            Confirm(EanC, 1.6f); recognition.Resolve(null, false, 2);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.Barcode2D));
            Assert.That(recognition.Selected.catalogProductId, Is.EqualTo(C));
        }

        [TestCase(EanA)]
        [TestCase(Unknown)]
        public void MissingCameraResultsExpireWithoutKeepingASelectedProduct(string ean)
        {
            Confirm(ean); recognition.Resolve(null, false, 1.4f);
            recognition.Resolve(null, false, 2.1f);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.None));
            Assert.That(recognition.Selected, Is.Null);
            Assert.That(recognition.ConfirmedBarcode, Is.Null);
            Assert.That(recognition.ConfirmationCount, Is.Zero);
            recognition.ObserveBarcode(new BarcodeReadResult(ean, "EAN_13"), 2.2f, 2.2f);
            recognition.Resolve(null, false, 2.2f);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.None));
            Assert.That(recognition.ConfirmationCount, Is.EqualTo(1));
        }

        [Test]
        public void DifferentBarcodeDropsOldSelectionBeforeNewNumberIsConfirmed()
        {
            Confirm(EanA); recognition.Resolve(null, false, 1.4f);
            recognition.ObserveBarcode(new BarcodeReadResult(EanC, "EAN_13"), 1.5f, 1.5f);
            recognition.Resolve(null, false, 1.5f);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.None));
            Assert.That(recognition.Selected, Is.Null);
            Assert.That(recognition.ConfirmedBarcode, Is.Null);
            Assert.That(recognition.ConfirmationCount, Is.EqualTo(1));
            recognition.ObserveBarcode(new BarcodeReadResult(EanC, "EAN_13"), 1.7f, 1.7f);
            recognition.ObserveBarcode(new BarcodeReadResult(EanC, "EAN_13"), 1.9f, 1.9f);
            recognition.Resolve(null, false, 1.9f);
            Assert.That(recognition.Selected.catalogProductId, Is.EqualTo(C));
        }

        [Test]
        public void LostFrontReleasesSelectionAndAnotherFrontCanAppearWithoutRescan()
        {
            recognition.Resolve(Product(A), false, 1);
            recognition.Resolve(null, false, 1.01f);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.None));
            Assert.That(recognition.Selected, Is.Null);
            recognition.Resolve(Product(B), false, 1.02f);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.Image3D));
            Assert.That(recognition.Selected.catalogProductId, Is.EqualTo(B));
        }

        [Test]
        public void MissingBarcodeDoesNotClearAVisibleFrontAndExpiredFramesCannotClearNewResult()
        {
            Confirm(EanA); recognition.Resolve(Product(A), false, 1.4f);
            recognition.ObserveBarcode(null, 1.5f, 1.5f);
            recognition.Resolve(Product(A), false, 1.5f);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.Image3D));
            Confirm(EanC, 2); recognition.Resolve(null, false, 2.4f);
            recognition.ObserveBarcode(null, 1.6f, 2.4f);
            recognition.Resolve(null, false, 2.4f);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.Barcode2D));
            Assert.That(recognition.Selected.catalogProductId, Is.EqualTo(C));
        }

        [Test]
        public void EmptyImageRegistryAndMultipleProductsWithoutBarcodesAreValid()
        {
            Assert.That(products.Count, Is.EqualTo(4));
            Assert.That(products.TryFind(null, out _), Is.False);
            Assert.DoesNotThrow(() => new ProductImageCatalog("{\"schemaVersion\":2,\"images\":[]}", products));
        }

        [Test]
        public void UnknownBarcodeCannotBeGuessedFromAnImageOnlyCatalogEntry()
        {
            Assert.That(recognition.Registration(Product(B)), Is.EqualTo(ProductRegistration.ImageOnly));
            Confirm(Unknown); recognition.Resolve(null, false, 1.4f);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.UnknownBarcode));
            Assert.That(recognition.Selected, Is.Null);
        }

        [TestCase("duplicate-id")]
        [TestCase("duplicate-barcode")]
        [TestCase("blank-barcode")]
        [TestCase("missing-date")]
        [TestCase("invalid-date")]
        public void InvalidCatalogIdentityAndSnapshotDateFailClosed(string problem)
        {
            var doc = JObject.Parse(File.ReadAllText("Assets/Data/demo-products.json"));
            if (problem == "duplicate-id") ((JArray)doc["products"]).Add(doc["products"][0].DeepClone());
            if (problem == "duplicate-barcode") doc["products"][1]["barcode"] = doc["products"][0]["barcode"];
            if (problem == "blank-barcode") doc["products"][0]["barcode"] = "";
            if (problem == "missing-date") doc.Remove("snapshotDate");
            if (problem == "invalid-date") doc["snapshotDate"] = "2026-02-30";
            Assert.Throws<FormatException>(() => new ProductCatalog(doc.ToString()));
        }

        [Test]
        public void CropOffsetsKeepOneCommonProductOriginAndPreserveTheTab()
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var presenter = Object.FindFirstObjectByType<TrackedImageProductPanel>();
            var input = new GameObject("Measured crop pose input");
            var rotation = Quaternion.Euler(18, 42, -12);
            var center = new Vector3(0.3f, 0.2f, 0.8f);
            try
            {
                images.TryFindByImage("a-top", out var top); images.TryFindByImage("a-bottom", out var bottom);
                top.offsetZMetres = 0.03f; bottom.offsetZMetres = -0.03f;
                Product(A).price = new PriceSnapshot { unitPriceKrw = 1, seller = "test", observedAt = "2026-10-26" };
                foreach (var binding in new[] { top, bottom })
                {
                    presenter.SetProduct(Product(A), binding);
                    var panel = Field<WorldProductPanel>(presenter, "panel");
                    input.transform.SetPositionAndRotation(center - rotation * new Vector3(0, 0, binding.offsetZMetres), rotation);
                    Assert.That(presenter.TryApplyPose(input.transform, new Vector2(0.058f, 0.06f)), Is.True);
                    var expected = new Vector3(.029f + .012f + .0464f * Mathf.Cos(15 * Mathf.Deg2Rad), .008f + .0464f * Mathf.Sin(15 * Mathf.Deg2Rad), 0);
                    Assert.That(Vector3.Distance(panel.transform.position, center + rotation * expected), Is.LessThan(0.00001f));
                    if (binding == top) panel.ShowPrice();
                    else Assert.That(panel.CurrentTab, Is.EqualTo(ProductInfoTab.Price));
                }
            }
            finally
            {
                Object.DestroyImmediate(input);
                var panel = Field<WorldProductPanel>(presenter, "panel");
                if (panel != null) Object.DestroyImmediate(panel.gameObject);
            }
        }

        [Test]
        public void BarcodeRegistrationBadgeFitsAndClearsWithTheOldProduct()
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var panel = Object.FindFirstObjectByType<BarcodeProductPanel>();
            var catalog = new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json"));
            Assert.That(catalog.TryFind("8809375984692", out var product), Is.True);
            panel.SetProduct(product, "앞면 이미지 등록됨 · 앞면을 비추면 3D로 표시");
            var text = (TMP_Text)new SerializedObject(panel).FindProperty("registrationStatus").objectReferenceValue;
            Canvas.ForceUpdateCanvases();
            Assert.That(text.text, Does.Contain("앞면 이미지 등록됨").And.Not.Contain("PriceTrace ID 확인 대기"));
            Assert.That(text.GetPreferredValues(text.text, text.rectTransform.rect.width, Mathf.Infinity).y,
                Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1));
            panel.SetProduct(null);
            Assert.That(text.text, Is.Empty);
        }

        [Test]
        public void ContinuousBarcodeObservationHasNoInitialScanTimeout()
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var scanner = Object.FindFirstObjectByType<ARBarcodeScanner>();
            scanner.SetContinuous(true);
            typeof(ARBarcodeScanner).GetField("scanStarted", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(scanner, Time.unscaledTime - 100);
            var timedOut = false;
            scanner.TimedOut += () => timedOut = true;
            Call(scanner, "Update");
            Assert.That(timedOut, Is.False);
            Assert.That(scanner.IsScanning, Is.True);
            scanner.SetContinuous(false);
            Assert.That(scanner.IsScanning, Is.False);
        }

        [Test]
        public void WorldTabsPersistAcrossSameProductRegionChangesAndResetForDifferentProduct()
        {
            var root = PrefabUtility.LoadPrefabContents(ProjectBuilder.PanelPath);
            try
            {
                var panel = root.GetComponent<WorldProductPanel>();
                Product(A).price = new PriceSnapshot { unitPriceKrw = 1, seller = "test", observedAt = "2026-10-26" };
                images.TryFindByImage("a-top", out var top); images.TryFindByImage("a-bottom", out var bottom);
                panel.Bind(Product(A), top); panel.ShowPrice(); panel.Bind(Product(A), bottom);
                Assert.That(panel.CurrentTab, Is.EqualTo(ProductInfoTab.Price));
                images.TryFindByImage("b", out var b); panel.Bind(Product(B), b);
                Assert.That(panel.CurrentTab, Is.EqualTo(ProductInfoTab.None));
                Assert.That(Text(panel, "provenance"), Does.Contain("바코드 미연결"));
                panel.Bind(null);
                Assert.That(Text(panel, "provenance"), Is.Empty);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void AppConsumesThreeObservationsAndWorldRescanClearsBothRoutes()
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var app = Object.FindFirstObjectByType<ProductARApp>();
            var presenter = Object.FindFirstObjectByType<TrackedImageProductPanel>();
            Call(app, "Awake"); Call(app, "OnEnable");
            try
            {
                var now = Time.unscaledTime;
                for (var i = 0; i < 3; i++) Call(app, "OnObserved", new BarcodeReadResult(EanA, "EAN_13"), default(BarcodeViewportTransform), now - 0.5f + i * 0.2f);
                var state = Field<ProductRecognitionState>(app, "recognition");
                state.Resolve(null, false, now);
                Assert.That(state.Route, Is.EqualTo(ProductDisplayRoute.Barcode2D));
                Assert.That(state.Selected.barcode, Is.EqualTo(EanA));
                images.TryFindByImage("a-top", out var binding);
                presenter.SetProduct(Product(A), binding);
                var panel = Field<WorldProductPanel>(presenter, "panel");
                Assert.That(panel.transform.Find("Rescan"), Is.Null);
                var button = Field<Button>(Object.FindFirstObjectByType<ProductScreenView>(), "primaryButton");
                Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(app));
                Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(ProductARApp.PrimaryAction)));
                app.RestartScan(); // 카메라 없는 EditMode에서는 하단 연결과 동일한 재스캔 처리를 확인한다.
                Assert.That(state.Selected, Is.Null); Assert.That(state.ConfirmedBarcode, Is.Null);
                Assert.That(presenter.Binding, Is.Null); Assert.That(presenter.HasVisibleTarget, Is.False);
            }
            finally
            {
                Call(app, "OnDisable");
                var panel = Field<WorldProductPanel>(presenter, "panel");
                if (panel != null) Object.DestroyImmediate(panel.gameObject);
            }
        }

        [Test]
        public void AppReleasesLostTargetButPreservesSameProductTabAndClearsForAnotherProduct()
        {
            Product(A).price = new PriceSnapshot { unitPriceKrw = 1, seller = "test", observedAt = "2026-10-26" };
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var app = Object.FindFirstObjectByType<ProductARApp>();
            var presenter = Object.FindFirstObjectByType<TrackedImageProductPanel>();
            Call(app, "Awake");
            try
            {
                var state = Field<ProductRecognitionState>(app, "recognition");
                state.Resolve(Product(A), false, 1); Call(app, "Present");
                images.TryFindByImage("a-top", out var binding);
                presenter.SetProduct(Product(A), binding);
                var panel = Field<WorldProductPanel>(presenter, "panel");
                panel.ShowPrice(); panel.gameObject.SetActive(true);
                state.Resolve(null, false, 1.01f); Call(app, "Present");
                Assert.That(state.Selected, Is.Null);
                Assert.That(presenter.HasVisibleTarget, Is.False);
                Assert.That(panel.CurrentTab, Is.EqualTo(ProductInfoTab.Price));
                state.Resolve(Product(A), false, 1.02f); Call(app, "Present");
                Assert.That(panel.CurrentTab, Is.EqualTo(ProductInfoTab.Price));
                state.Resolve(Product(B), false, 1.03f); Call(app, "Present");
                Assert.That(presenter.Binding, Is.Null);
            }
            finally
            {
                Call(app, "OnDisable");
                var panel = Field<WorldProductPanel>(presenter, "panel");
                if (panel != null) Object.DestroyImmediate(panel.gameObject);
            }
        }

        private static string Text(WorldProductPanel panel, string field) => ((TMP_Text)new SerializedObject(panel).FindProperty(field).objectReferenceValue).text;
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
