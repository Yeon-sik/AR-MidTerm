using System;
using System.IO;
using System.Reflection;
using MidtermAR.Editor;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

namespace MidtermAR.Tests
{
    public sealed class RuntimeRegisteredPanelTests
    {
        private const string OffBarcode = "8801045014869";
        private const string OtherBarcode = "8801045520728";
        private const string OffBadge = "Open Food Facts 정보 · PT 바코드 연결 필요";

        [TestCase(.04f, .08f)]
        [TestCase(.058f, .058f)]
        [TestCase(.12f, .18f)]
        public void EstimatedSizeKeepsRightGapTiltAndOutlineOrigin(float width, float height)
        {
            var binding = RuntimeBinding();
            Assert.That(binding.developmentOnly, Is.False);
            Assert.That(ProductPanelLayout.TryCreate(binding, new Vector2(width, height), out var layout), Is.True);
            Assert.That(layout.ConnectorStart, Is.EqualTo(new Vector3(width * .5f, .001f, 0)));
            Assert.That(layout.ConnectorEnd.x - layout.ConnectorStart.x, Is.EqualTo(.012f).Within(.000001f));
            var leftEdge = layout.Center + ProductPanelLayout.Rotation * Vector3.left * layout.Width * .5f;
            Assert.That(Vector3.Distance(leftEdge, layout.ConnectorEnd), Is.LessThan(.000001f));
            Assert.That(leftEdge.x - width * .5f, Is.EqualTo(.012f).Within(.000001f));
            Assert.That(Quaternion.Angle(ProductPanelLayout.Rotation,
                Quaternion.AngleAxis(15, Vector3.forward) * Quaternion.Euler(90, 0, 0)), Is.LessThan(.001f));
            Assert.That(layout.Center.z, Is.Zero);
        }

        [TestCase(0, .1f)]
        [TestCase(.1f, 0)]
        [TestCase(float.NaN, .1f)]
        [TestCase(.1f, float.PositiveInfinity)]
        public void InvalidEstimatedSizeDoesNotShowRuntimePanel(float width, float height)
            => Assert.That(ProductPanelLayout.TryCreate(RuntimeBinding(), new Vector2(width, height), out _), Is.False);

        [Test]
        public void RuntimeSizeAllowanceDoesNotPermitUnmeasuredStaticPackage()
        {
            Assert.That(ProductPanelLayout.TryCreate(new ProductImageBinding(), new Vector2(.06f, .12f), out _), Is.False);
            var binding = RuntimeBinding();
            binding.productWidthMetres = .058f;
            Assert.That(ProductPanelLayout.TryCreate(binding, new Vector2(.12f, .18f), out var layout), Is.True);
            Assert.That(layout.ConnectorStart.x, Is.EqualTo(.029f).Within(.000001f));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        [TestCase(true, true)]
        public void RuntimePanelNamesMissingDataWithoutOpeningEmptyTabs(bool hasNutrition, bool hasPrice)
        {
            var root = PrefabUtility.LoadPrefabContents(ProjectBuilder.PanelPath);
            try
            {
                var panel = root.GetComponent<WorldProductPanel>();
                var product = Sample(OffBarcode, hasNutrition, hasPrice);
                var badge = hasPrice ? "PT 등록 상품 · 온라인" : OffBadge;
                panel.Bind(product, RuntimeBinding(), ProductIdentity.Key(product), badge);
                Assert.That(Text(panel, "productTitle"), Is.EqualTo(product.name));
                Assert.That(Text(panel, "provenance"), Does.Contain(badge).And.Contain("현장 등록").And.Not.Contain("개발용 카드"));
                Assert.That(Field<Button>(panel, "priceTab").interactable, Is.EqualTo(hasPrice));
                Assert.That(Field<Button>(panel, "nutritionTab").interactable, Is.EqualTo(hasNutrition));
                Assert.That(panel.CurrentTab, Is.EqualTo(hasNutrition ? ProductInfoTab.Nutrition : hasPrice ? ProductInfoTab.Price : ProductInfoTab.None));
                var missing = Field<TMP_Text>(panel, "emptyInformation");
                Assert.That(missing.gameObject.activeSelf, Is.EqualTo(!hasNutrition || !hasPrice));
                if (!hasPrice) Assert.That(missing.text, Does.Contain("가격 정보 없음"));
                if (!hasNutrition) Assert.That(missing.text, Does.Contain("영양정보 없음"));
                if (hasNutrition && !hasPrice)
                {
                    Assert.That(Text(panel, "nutrition"), Does.Contain("100g 기준"));
                    Assert.That(Text(panel, "nutritionSource"), Does.Contain("Open Food Facts"));
                }
                if (missing.gameObject.activeSelf)
                {
                    var body = Field<GameObject>(panel, hasNutrition ? "nutritionPage" : "pricePage");
                    if (hasNutrition || hasPrice)
                        Assert.That(missing.rectTransform.offsetMax.y, Is.LessThan(((RectTransform)body.transform).offsetMin.y));
                    Assert.That(Field<TMP_Text>(panel, "provenance").rectTransform.offsetMax.y, Is.LessThan(missing.rectTransform.offsetMin.y));
                }
                panel.Bind(null);
                Assert.That(Text(panel, "provenance"), Is.Empty);
                Assert.That(Text(panel, "emptyInformation"), Is.Empty);
                Assert.That(missing.gameObject.activeSelf, Is.False);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void BarcodeIdentityResetsTabsAcrossExternalProductsAndPreservesSameProduct()
        {
            var root = PrefabUtility.LoadPrefabContents(ProjectBuilder.PanelPath);
            try
            {
                var panel = root.GetComponent<WorldProductPanel>();
                // 두 탭 전환을 검증하는 합성 DTO다. 실제 OFF 응답에 가격이 있다는 뜻이 아니다.
                var first = Sample(OffBarcode, true, true);
                var second = Sample(OtherBarcode, true, true);
                Assert.That(first.catalogProductId, Is.Null);
                Assert.That(second.catalogProductId, Is.Null);
                panel.Bind(first, RuntimeBinding(), ProductIdentity.Key(first), OffBadge);
                panel.ShowPrice();
                panel.Bind(SessionImageRegistry.CopyProduct(first), RuntimeBinding(), ProductIdentity.Key(first), OffBadge);
                Assert.That(panel.CurrentTab, Is.EqualTo(ProductInfoTab.Price));
                panel.Bind(second, RuntimeBinding(), ProductIdentity.Key(second), OffBadge);
                Assert.That(panel.CurrentTab, Is.EqualTo(ProductInfoTab.Nutrition));
                panel.ShowPrice();
                panel.Bind(first, RuntimeBinding());
                Assert.That(panel.CurrentTab, Is.EqualTo(ProductInfoTab.Nutrition), "기존 두 인수 호출도 바코드 키를 사용한다.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PresenterUsesRegisteredDisplayProductAndSourceAndClearsOnLoss(bool localSnapshot)
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var presenter = Object.FindFirstObjectByType<TrackedImageProductPanel>();
            var products = new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json"));
            var registry = new SessionImageRegistry(products);
            var product = Sample(OffBarcode, false, false);
            ProductSnapshot local = null;
            var badge = OffBadge;
            if (localSnapshot)
            {
                Assert.That(products.TryFind("8801104671507", out local), Is.True);
                product = local;
                badge = "PT 등록 여부 확인 불가 · 로컬 스냅샷 " + products.SnapshotDate;
            }
            Assert.That(registry.TryAdd("runtime-panel", product, local, badge, out var target), Is.True);
            presenter.Configure(products, new ProductImageCatalog(File.ReadAllText("Assets/Data/product-images.json"), products), registry);
            var state = Field<ProductImageState>(presenter, "state");
            var image = new GameObject("Synthetic runtime image", typeof(ARTrackedImage)).GetComponent<ARTrackedImage>();
            try
            {
                SetTrackedData(image, new Vector2(.075f, .12f));
                Set(presenter, "selectedImage", image);
                state.BeginFrame(true); state.ObserveImage("runtime-panel", TrackingState.Tracking, 1); state.EndFrame();
                presenter.Present(true);
                Assert.That(presenter.ImageTarget, Is.SameAs(target));
                Assert.That(presenter.CurrentProduct, Is.SameAs(target.DisplayProduct));
                Assert.That(presenter.HasVisibleTarget, Is.True);
                var panel = Field<WorldProductPanel>(presenter, "panel");
                Assert.That(Text(panel, "productTitle"), Is.EqualTo(product.name));
                Assert.That(Text(panel, "provenance"), Does.Contain(badge));
                Assert.That(Text(panel, "provenance"), Does.Not.Contain("개발용 카드"));
                if (localSnapshot)
                {
                    panel.ShowPrice();
                    presenter.Present(true);
                    Assert.That(panel.CurrentTab, Is.EqualTo(ProductInfoTab.Price));
                }
                var outline = Field<LineRenderer>(presenter, "cropOutline");
                var connector = Field<LineRenderer>(presenter, "connector");
                Assert.That(outline.enabled && connector.enabled, Is.True);
                Assert.That(outline.GetPosition(0), Is.EqualTo(new Vector3(-.0375f, .001f, -.06f)));
                Assert.That(connector.GetPosition(1).x - connector.GetPosition(0).x, Is.EqualTo(.012f).Within(.000001f));
                state.BeginFrame(true); state.EndFrame(); presenter.Present(true);
                Assert.That(presenter.ImageTarget, Is.Null);
                Assert.That(presenter.HasVisibleTarget, Is.False);
                Assert.That(outline.enabled || connector.enabled, Is.False);
                presenter.ResetSelection();
                Assert.That(Text(panel, "productTitle"), Is.Empty);
                Assert.That(Text(panel, "provenance"), Is.Empty);
            }
            finally
            {
                var panel = Field<WorldProductPanel>(presenter, "panel");
                if (panel != null) Object.DestroyImmediate(panel.gameObject);
                Object.DestroyImmediate(image.gameObject);
            }
        }

        private static ProductImageBinding RuntimeBinding() => new ProductImageBinding { runtimeRegistered = true };
        private static ProductSnapshot Sample(string barcode, bool nutrition, bool price) => new ProductSnapshot
        {
            name = "현장 등록 시험 상품", barcode = barcode,
            nutrition = nutrition ? new NutritionSnapshot { basisAmount = 100, basisUnit = "g", caloriesKcal = 539,
                provider = price ? "Fitness Nutrition" : "Open Food Facts" } : null,
            price = price ? new PriceSnapshot { unitPriceKrw = 1400, kind = "observed", seller = "시험 판매처", observedAt = "2026-10-28" } : null
        };
        private static void SetTrackedData(ARTrackedImage image, Vector2 size)
        {
            var data = new XRTrackedImage(new TrackableId(41, 42), Guid.Empty, Pose.identity, size, TrackingState.Tracking, IntPtr.Zero);
            typeof(ARTrackable<XRTrackedImage, ARTrackedImage>).GetProperty("sessionRelativeData", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(image, data);
        }
        private static string Text(object target, string name) => Field<TMP_Text>(target, name).text;
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
