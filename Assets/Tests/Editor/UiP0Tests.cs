using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MidtermAR.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

namespace MidtermAR.Tests
{
    public sealed class UiP0Tests
    {
        private static ProductCatalog Catalog() => new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json"));
        private static ProductImageCatalog Images(ProductCatalog products) => new ProductImageCatalog(File.ReadAllText("Assets/Data/product-images.json"), products);
        private static ProductSnapshot Sample(bool nutrition, bool price) => new ProductSnapshot
        {
            catalogProductId = "ui-test", name = "시험 상품", barcode = "8801104671507",
            nutrition = nutrition ? new NutritionSnapshot { basisAmount = 40, basisUnit = "g", proteinGrams = 0 } : null,
            price = price ? new PriceSnapshot { unitPriceKrw = 0, seller = "시험 판매처", observedAt = "2026-10-27" } : null
        };

        [Test]
        public void BoardSitsBeyondCommonProductRightEdgeWithFixedTiltAndExactConnectorEndpoints()
        {
            var binding = new ProductImageBinding { productWidthMetres = .058f, offsetXMetres = .002f, offsetZMetres = .029f };
            Assert.That(ProductPanelLayout.TryCreate(binding, new Vector2(.058f, .06f), out var layout), Is.True);
            var common = new Vector3(binding.offsetXMetres, 0, binding.offsetZMetres);
            var corners = new[] { new Vector3(-1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0) }
                .Select(p => layout.Center + ProductPanelLayout.Rotation * (p * layout.Width * .5f)).ToArray();
            var gap = corners.Min(p => p.x) - common.x - binding.productWidthMetres * .5f;
            Assert.That(gap, Is.InRange(.01f, .015f));
            Assert.That(gap, Is.EqualTo(.012f).Within(.000001f));
            Assert.That(layout.Center.z, Is.EqualTo(common.z).Within(.000001f));
            Assert.That(Vector3.Angle(ProductPanelLayout.Rotation * Vector3.forward, Quaternion.Euler(90, 0, 0) * Vector3.forward), Is.EqualTo(15).Within(.001f));
            Assert.That(Vector3.Distance(layout.ConnectorEnd, layout.Center + ProductPanelLayout.Rotation * Vector3.left * layout.Width * .5f), Is.LessThan(.000001f));
            Assert.That(layout.ConnectorStart, Is.EqualTo(common + new Vector3(.029f, .001f, 0)));
            Assert.That(layout.Width, Is.EqualTo(.058f * 1.6f).Within(.000001f));
        }

        [Test]
        public void CropSizeAndPoseChangesDoNotMoveTheBoardFromItsCommonOrigin()
        {
            var common = new Vector3(.2f, .3f, .8f);
            var rotation = Quaternion.Euler(25, -38, 13);
            Vector3? previous = null;
            foreach (var offset in new[] { -.029f, .029f })
            {
                var binding = new ProductImageBinding { productWidthMetres = .058f, offsetZMetres = offset };
                var crop = new Vector2(.058f, offset > 0 ? .06f : .071f);
                Assert.That(ProductPanelLayout.TryCreate(binding, crop, out var layout), Is.True);
                var imagePosition = common - rotation * new Vector3(0, 0, offset);
                var boardCenter = imagePosition + rotation * layout.Center;
                if (previous.HasValue) Assert.That(Vector3.Distance(previous.Value, boardCenter), Is.LessThan(.000001f));
                previous = boardCenter;
            }
        }

        [TestCase("missing-width")]
        [TestCase("negative-width")]
        [TestCase("missing-source")]
        [TestCase("different-crop-width")]
        public void ProductionCommonWidthNeedsEvidenceAndMustAgreeAcrossCrops(string problem)
        {
            var json = JObject.Parse(File.ReadAllText("Assets/Data/product-images.json"));
            var crops = json["images"].Where(i => !(bool)i["developmentOnly"]).ToArray();
            if (problem == "missing-width") crops[0]["productWidthMetres"] = 0;
            if (problem == "negative-width") crops[0]["productWidthMetres"] = -.058;
            if (problem == "missing-source") crops[0]["productWidthSource"] = "";
            if (problem == "different-crop-width") crops[1]["productWidthMetres"] = .06;
            Assert.Throws<FormatException>(() => new ProductImageCatalog(json.ToString(), Catalog()));
        }

        [TestCase("lost")]
        [TestCase("limited")]
        [TestCase("last-known")]
        [TestCase("not-ready")]
        [TestCase("conflict")]
        [TestCase("denied")]
        [TestCase("pause")]
        public void OutlineConnectorAndBoardShareOneFullTrackingVisibilityGate(string reason)
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var presenter = Object.FindFirstObjectByType<TrackedImageProductPanel>();
            var products = Catalog(); var images = Images(products);
            var bindings = JObject.Parse(File.ReadAllText("Assets/Data/product-images.json"))["images"];
            var front = (string)bindings.First(i => !(bool)i["developmentOnly"])["referenceImageName"];
            var other = (string)bindings.First(i => (bool)i["developmentOnly"])["referenceImageName"];
            var state = new ProductImageState(products, images);
            Set(presenter, "state", state);
            var tracked = new GameObject("Synthetic tracked crop", typeof(ARTrackedImage)).GetComponent<ARTrackedImage>();
            try
            {
                var size = new Vector2(.058f, .061f);
                var data = new XRTrackedImage(new TrackableId(1, 2), Guid.Empty, Pose.identity, size, TrackingState.Tracking, IntPtr.Zero);
                typeof(ARTrackable<XRTrackedImage, ARTrackedImage>).GetProperty("sessionRelativeData", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tracked, data);
                tracked.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(15, 30, 7));
                Set(presenter, "selectedImage", tracked);
                state.BeginFrame(true); state.ObserveImage(front, TrackingState.Tracking, 1); state.EndFrame();
                presenter.Present(true);
                var outline = Field<LineRenderer>(presenter, "cropOutline");
                var connector = Field<LineRenderer>(presenter, "connector");
                Assert.That(presenter.HasVisibleTarget && outline.enabled && connector.enabled, Is.True);
                Assert.That(outline.loop && outline.useWorldSpace, Is.True);
                Assert.That(outline.positionCount, Is.EqualTo(4));
                Assert.That(Vector3.Distance(outline.GetPosition(0), tracked.transform.TransformPoint(new Vector3(-size.x / 2, .001f, -size.y / 2))), Is.LessThan(.000001f));
                Assert.That(Vector3.Distance(outline.GetPosition(2), tracked.transform.TransformPoint(new Vector3(size.x / 2, .001f, size.y / 2))), Is.LessThan(.000001f));
                if (reason == "pause") Call(presenter, "OnApplicationPause", true);
                else
                {
                    state.BeginFrame(reason != "not-ready");
                    if (reason != "lost") state.ObserveImage(front, reason == "limited" ? TrackingState.Limited : TrackingState.Tracking, reason == "last-known" ? 2 : 1);
                    if (reason == "conflict") state.ObserveImage(other, TrackingState.Tracking, 1);
                    state.EndFrame();
                    presenter.Present(reason != "denied");
                }
                Assert.That(presenter.HasVisibleTarget || outline.enabled || connector.enabled, Is.False, reason + " must hide all three in the same call");
            }
            finally
            {
                var panel = Field<WorldProductPanel>(presenter, "panel");
                if (panel != null) Object.DestroyImmediate(panel.gameObject);
                Object.DestroyImmediate(tracked.gameObject);
            }
        }

        [TestCase(true, true, ProductInfoTab.Nutrition)]
        [TestCase(true, false, ProductInfoTab.Nutrition)]
        [TestCase(false, true, ProductInfoTab.Price)]
        [TestCase(false, false, ProductInfoTab.None)]
        public void AvailableDataSelectsDefaultTabAndUnavailableTabsIgnoreTouch(bool nutrition, bool price, ProductInfoTab expected)
        {
            var root = PrefabUtility.LoadPrefabContents(ProjectBuilder.PanelPath);
            var events = new GameObject("UI test events", typeof(EventSystem));
            try
            {
                var panel = root.GetComponent<WorldProductPanel>(); panel.Bind(Sample(nutrition, price));
                Assert.That(panel.CurrentTab, Is.EqualTo(expected));
                Assert.That(Field<RectTransform>(panel, "tabs").gameObject.activeSelf, Is.EqualTo(nutrition || price));
                Assert.That(Field<TMP_Text>(panel, "emptyInformation").gameObject.activeSelf, Is.EqualTo(!nutrition && !price));
                foreach (var entry in new[] { ("nutritionTab", nutrition), ("priceTab", price) })
                {
                    var button = Field<Button>(panel, entry.Item1);
                    Assert.That(button.interactable, Is.EqualTo(entry.Item2));
                    Assert.That(button.GetComponent<LayoutElement>().minHeight, Is.GreaterThanOrEqualTo(72));
                    if (entry.Item2) continue;
                    button.onClick.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
                    ExecuteEvents.Execute(button.gameObject, new PointerEventData(events.GetComponent<EventSystem>()) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                    Assert.That(panel.CurrentTab, Is.EqualTo(expected));
                }
                if (!nutrition && !price) Assert.That(((RectTransform)root.transform).rect.height, Is.LessThan(300));
                if (!nutrition && price) Assert.That(((RectTransform)root.transform).rect.height, Is.LessThan(560));
            }
            finally { Object.DestroyImmediate(events); PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void AllNullNutritionIsUnavailableAndRescanUsesAvailabilityDefault()
        {
            var root = PrefabUtility.LoadPrefabContents(ProjectBuilder.PanelPath);
            try
            {
                var panel = root.GetComponent<WorldProductPanel>(); var product = Sample(true, true);
                panel.Bind(product); panel.ShowPrice(); panel.Bind(product);
                Assert.That(panel.CurrentTab, Is.EqualTo(ProductInfoTab.Price));
                panel.Bind(null); panel.Bind(product);
                Assert.That(panel.CurrentTab, Is.EqualTo(ProductInfoTab.Nutrition));
                product.nutrition = new NutritionSnapshot { basisAmount = 40, basisUnit = "g" };
                panel.Bind(product);
                Assert.That(panel.CurrentTab, Is.EqualTo(ProductInfoTab.Price));
                product.catalogProductId = "another-ui-test"; product.nutrition.proteinGrams = 0;
                panel.Bind(product);
                Assert.That(panel.CurrentTab, Is.EqualTo(ProductInfoTab.Nutrition));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void BarcodeCardHidesMissingSectionsAndCompactsEmptySpace(bool nutrition, bool price)
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var panel = Object.FindFirstObjectByType<BarcodeProductPanel>();
            panel.SetProduct(Sample(nutrition, price), sourceBadge: "PT 등록 여부 확인 불가 · PT 스냅샷 2026-10-26");
            Assert.That(Field<RectTransform>(panel, "nutritionSection").gameObject.activeSelf, Is.EqualTo(nutrition));
            Assert.That(Field<RectTransform>(panel, "priceSection").gameObject.activeSelf, Is.EqualTo(price));
            Assert.That(Field<TMP_Text>(panel, "emptyInformation").gameObject.activeSelf, Is.EqualTo(!nutrition && !price));
            Assert.That(Field<TMP_Text>(panel, "provenance").text, Does.Contain("PT 등록 확인 불가").And.Contain("2026-10-26"));
            if (!nutrition && !price) Assert.That(Field<RectTransform>(panel, "card").rect.height, Is.LessThan(240));
        }

        [Test]
        public void ConsumerCopyOmitsDeveloperExplanationsAndKeepsCardNoticeAndPriceEvidence()
        {
            var root = PrefabUtility.LoadPrefabContents(ProjectBuilder.PanelPath);
            try
            {
                var panel = root.GetComponent<WorldProductPanel>();
                panel.Bind(Sample(true, true), new ProductImageBinding { developmentOnly = true });
                Assert.That(Field<TMP_Text>(panel, "provenance").text, Is.EqualTo(ProductInfoFormatter.CardNotice));
                Assert.That(Field<TMP_Text>(panel, "priceSource").text, Does.Contain("2026-10-27").And.Contain("시험 판매처"));
                Assert.That(Field<TMP_Text>(panel, "priceDisclaimer").text, Is.EqualTo(ProductInfoFormatter.PriceDisclaimer));
                var forbidden = new[] { "시연용 상품 연결", "로컬 시연 항목", "PriceTrace ID 확인 대기", "추적 이미지의 위치", "공간에 고정", "로컬 데이터", "ui-test" };
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    foreach (var phrase in forbidden) Assert.That(text.text, Does.Not.Contain(phrase));
                Assert.That(root.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(2));
                Assert.That(root.transform.Find("Rescan"), Is.Null);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
