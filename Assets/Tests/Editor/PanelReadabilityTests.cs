using System.IO;
using System.Reflection;
using MidtermAR.Editor;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MidtermAR.Tests
{
    // A25 화면 진단의 세 문제: 가격·출처 잘림, 크롭만 감싸는 윤곽, 정보판을 가리는 상단 안내.
    public sealed class PanelReadabilityTests
    {
        private const string Theanine = "8809375984692";
        private static ProductCatalog Catalog() => new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json"));
        private static ProductImageCatalog Images(ProductCatalog products) => new ProductImageCatalog(File.ReadAllText("Assets/Data/product-images.json"), products);

        [Test]
        public void OutlineUsesRegisteredWholeProductAndFallsBackToTrackedCrop()
        {
            var whole = new ProductImageBinding { productWidthMetres = .058f, productHeightMetres = .1164f, offsetXMetres = .002f, offsetZMetres = .029f };
            var corners = ProductPanelLayout.OutlineCorners(whole, new Vector2(.058f, .0582f));
            Assert.That(corners, Has.Length.EqualTo(4));
            Assert.That(Vector3.Distance(corners[0], new Vector3(.002f - .029f, .001f, .029f - .0582f)), Is.LessThan(.000001f));
            Assert.That(Vector3.Distance(corners[2], new Vector3(.002f + .029f, .001f, .029f + .0582f)), Is.LessThan(.000001f));
            // 높이 근거가 없는 포장·현장 등록·개발 카드는 실제 추적 크롭을 그대로 그린다.
            foreach (var crop in new[]
            {
                new ProductImageBinding { productWidthMetres = .058f, offsetZMetres = .029f },
                new ProductImageBinding { runtimeRegistered = true },
                new ProductImageBinding { developmentOnly = true },
                null
            })
            {
                Assert.That(ProductPanelLayout.OutlinesWholeProduct(crop), Is.False);
                corners = ProductPanelLayout.OutlineCorners(crop, new Vector2(.075f, .12f));
                Assert.That(corners[0], Is.EqualTo(new Vector3(-.0375f, .001f, -.06f)));
                Assert.That(corners[2], Is.EqualTo(new Vector3(.0375f, .001f, .06f)));
            }
        }

        [Test]
        public void RegisteredCropsOfOneProductDrawTheSameWholeOutline()
        {
            var images = Images(Catalog());
            Assert.That(images.TryFindByImage("theanine-top", out var top), Is.True);
            Assert.That(images.TryFindByImage("theanine-bottom", out var bottom), Is.True);
            // 크롭 원점이 달라도 상품 공통 원점 기준 윤곽은 같아야 크롭 전환에서 테두리가 튀지 않는다.
            var fromTop = ProductPanelLayout.OutlineCorners(top, new Vector2(.058f, .0582f));
            var fromBottom = ProductPanelLayout.OutlineCorners(bottom, new Vector2(.058f, .0582f));
            for (var i = 0; i < 4; i++)
            {
                var topCommon = fromTop[i] - new Vector3(top.offsetXMetres, 0, top.offsetZMetres);
                var bottomCommon = fromBottom[i] - new Vector3(bottom.offsetXMetres, 0, bottom.offsetZMetres);
                Assert.That(Vector3.Distance(topCommon, bottomCommon), Is.LessThan(.000001f));
            }
        }

        [Test]
        public void DetailListsEveryObservationSourceAndMissingNutritionWithoutAbbreviation()
        {
            Assert.That(Catalog().TryFind(Theanine, out var product), Is.True);
            var text = ProductInfoFormatter.Detail(product, null, null);
            Assert.That(text, Does.StartWith("가격\n" + ProductInfoFormatter.WorldPrice(product)));
            foreach (var observation in product.priceObservations)
            {
                Assert.That(text, Does.Contain(observation.seller));
                Assert.That(text, Does.Contain(observation.observedAt));
                Assert.That(text, Does.Contain(observation.displaySource));
            }
            Assert.That(text, Does.Contain(ProductInfoFormatter.PriceDisclaimer));
            Assert.That(text, Does.Contain("영양정보\n영양정보 없음"));
            Assert.That(text, Does.Not.Contain("출처\n"), "사전 등록 포장에는 3D 정보판에 없는 출처 문구를 만들지 않는다.");

            var bare = new ProductSnapshot { name = "자료 없는 상품", barcode = "4006381333931" };
            var runtime = ProductInfoFormatter.Detail(bare, new ProductImageBinding { runtimeRegistered = true }, "Open Food Facts 정보");
            Assert.That(runtime, Does.Contain("가격\n가격 정보 없음"));
            Assert.That(runtime, Does.Contain("영양정보\n영양정보 없음"));
            Assert.That(runtime, Does.Contain("출처\nOpen Food Facts 정보\n현장 등록 · 크기는 추정값"));

            // 관측 목록이 없으면 대표 관측가의 판매처·날짜를 그대로 쓴다. 0원과 게시가를 구분한다.
            var single = new ProductSnapshot { name = "단일 관측", price = new PriceSnapshot { unitPriceKrw = 0, seller = "시험 판매처", observedAt = "2026-10-27" } };
            Assert.That(ProductInfoFormatter.Detail(single, null, null), Does.Contain("PriceTrace 관측가 · 2026-10-27 · 시험 판매처"));
            single.price.kind = "listed";
            Assert.That(ProductInfoFormatter.Detail(single, null, null), Does.Contain("가격\n가격 정보 없음"));
            Assert.That(ProductInfoFormatter.Detail(null, null, null), Is.Empty);
        }

        [Test]
        public void GeneratedSceneConnectsDetailButtonSheetAndHeader()
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var app = Object.FindFirstObjectByType<ProductARApp>();
            var screen = Object.FindFirstObjectByType<ProductScreenView>();
            var detail = Field<ProductDetailView>(app, "detail");
            Assert.That(detail, Is.Not.Null);
            var button = Field<Button>(screen, "detailButton");
            Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(app));
            Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(ProductARApp.ShowDetail)));
            var safe = screen.transform.Find("Safe Area");
            Assert.That(Field<RectTransform>(screen, "header"), Is.SameAs(safe.Find("Header")));
            var sheet = Field<RectTransform>(detail, "sheet");
            Assert.That(sheet.parent, Is.SameAs(safe));
            Assert.That(sheet.GetSiblingIndex(), Is.EqualTo(safe.childCount - 1), "상세 시트는 안내·바코드 정보판 위에 그린다.");
            Assert.That(sheet.GetComponent<Image>().raycastTarget, Is.True, "열린 시트는 뒤쪽 버튼·3D 탭 터치를 막는다.");
            Assert.That(detail.IsOpen, Is.False);
            var close = sheet.GetComponentInChildren<Button>(true);
            Assert.That(close.onClick.GetPersistentTarget(0), Is.SameAs(detail));
            Assert.That(close.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(ProductDetailView.Hide)));
        }

        [TestCase(1920)]
        [TestCase(2400)]
        public void HeaderFoldsAndDetailSheetFitsInsideThePortraitSafeArea(int height)
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var screen = Object.FindFirstObjectByType<ProductScreenView>();
            var app = Object.FindFirstObjectByType<ProductARApp>();
            var detail = Field<ProductDetailView>(app, "detail");
            var canvas = screen.GetComponent<Canvas>();
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, height);
            Canvas.ForceUpdateCanvases();
            var header = Field<RectTransform>(screen, "header");
            var detailButton = Field<Button>(screen, "detailButton");

            screen.Show("앞면 · 3D 정보", "앞면에 상품정보를 표시해요", "", "", "다른 상품 스캔", true, false, false, false,
                secondaryAction: true, detailEnabled: true, showHeader: false);
            Canvas.ForceUpdateCanvases();
            Assert.That(header.gameObject.activeSelf, Is.False);
            Assert.That(detailButton.gameObject.activeInHierarchy && detailButton.interactable, Is.True);
            Assert.That(screen.GetComponentsInChildren<Button>().Length, Is.EqualTo(2));
            foreach (var active in screen.GetComponentsInChildren<Button>())
                Assert.That(((RectTransform)active.transform).rect.height, Is.GreaterThanOrEqualTo(132));
            AssertNoOverflow(screen);

            Assert.That(Catalog().TryFind(Theanine, out var product), Is.True);
            detail.Show(product, null, null, null);
            Canvas.ForceUpdateCanvases();
            var sheet = Field<RectTransform>(detail, "sheet");
            var safe = (RectTransform)sheet.parent;
            Assert.That(detail.IsOpen, Is.True);
            Assert.That(detail.ProductKey, Is.EqualTo(product.catalogProductId));
            Assert.That(sheet.rect.width, Is.LessThanOrEqualTo(safe.rect.width));
            Assert.That(sheet.rect.height, Is.LessThanOrEqualTo(safe.rect.height));
            Assert.That(Field<TMP_Text>(detail, "body").text, Is.EqualTo(ProductInfoFormatter.Detail(product, null, null)));
            AssertNoOverflow(sheet);
            Assert.That(((RectTransform)sheet.GetComponentInChildren<Button>().transform).rect.height, Is.GreaterThanOrEqualTo(132));

            detail.Hide();
            screen.Show("상품 인식", "앞면이나 바코드를 비춰주세요", "상품을 화면 안에 선명하게 보여주세요.", "", "다른 상품 스캔",
                false, false, false, false);
            Assert.That(detail.IsOpen, Is.False);
            Assert.That(Field<TMP_Text>(detail, "body").text, Is.Empty);
            Assert.That(header.gameObject.activeSelf, Is.True);
            Assert.That(detailButton.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void HeaderFoldsOnlyWhileTheWorldPanelIsActuallyVisible()
        {
            Assert.That(ProductARApp.ShowsWorldPanel(true, ProductDisplayRoute.Image3D, true), Is.True);
            Assert.That(ProductARApp.ShowsWorldPanel(true, ProductDisplayRoute.Image3D, false), Is.False, "위치 확인 중에는 안내를 유지한다.");
            Assert.That(ProductARApp.ShowsWorldPanel(false, ProductDisplayRoute.Image3D, true), Is.False);
            foreach (var route in new[] { ProductDisplayRoute.None, ProductDisplayRoute.Barcode2D, ProductDisplayRoute.UnknownBarcode, ProductDisplayRoute.Conflict })
                Assert.That(ProductARApp.ShowsWorldPanel(true, route, true), Is.False);
        }

        [Test]
        public void DetailOpensOnlyForTheVisibleBoardSurvivesLossAndClosesForAnotherProductOrRescan()
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var app = Object.FindFirstObjectByType<ProductARApp>();
            var presenter = Object.FindFirstObjectByType<TrackedImageProductPanel>();
            var detail = Field<ProductDetailView>(app, "detail");
            Call(app, "Awake");
            try
            {
                var catalog = Field<ProductCatalog>(app, "localCatalog");
                var images = Field<ProductImageCatalog>(app, "imageCatalog");
                Assert.That(catalog.TryFind(Theanine, out var theanine), Is.True);
                Assert.That(catalog.TryFind("8801104671507", out var other), Is.True);
                var state = Field<ProductRecognitionState>(app, "recognition");
                state.Resolve(theanine, false, 1);
                Assert.That(state.Route, Is.EqualTo(ProductDisplayRoute.Image3D));
                images.TryFindByImage("theanine-top", out var binding);
                presenter.SetProduct(theanine, binding);
                app.ShowDetail();
                Assert.That(detail.IsOpen, Is.False, "숨긴 정보판의 상품은 상세보기로 열지 않는다.");

                var panel = Field<WorldProductPanel>(presenter, "panel");
                panel.gameObject.SetActive(true);
                app.ShowDetail();
                Assert.That(detail.IsOpen, Is.True);
                Assert.That(detail.ProductKey, Is.EqualTo(ProductIdentity.Key(theanine)));
                var position = panel.transform.position; var rotation = panel.transform.rotation;

                state.Resolve(null, false, 1.01f); Call(app, "Present");
                Assert.That(presenter.HasVisibleTarget, Is.False);
                Assert.That(detail.IsOpen, Is.True, "화면 고정 상세보기는 공간 표시가 아니므로 같은 상품의 소실 중에도 읽을 수 있다.");
                Assert.That(panel.transform.position, Is.EqualTo(position));
                Assert.That(panel.transform.rotation, Is.EqualTo(rotation));

                state.Resolve(theanine, false, 1.02f); Call(app, "Present");
                Assert.That(detail.IsOpen, Is.True);
                state.Resolve(other, false, 1.03f); Call(app, "Present");
                Assert.That(detail.IsOpen, Is.False, "다른 상품이 표시되면 이전 상품의 상세보기를 닫는다.");

                state.Resolve(theanine, false, 1.04f); Call(app, "Present");
                presenter.SetProduct(theanine, binding); panel.gameObject.SetActive(true);
                app.ShowDetail();
                Assert.That(detail.IsOpen, Is.True);
                app.RestartScan();
                Assert.That(detail.IsOpen, Is.False);
            }
            finally
            {
                Call(app, "OnDisable");
                var panel = Field<WorldProductPanel>(presenter, "panel");
                if (panel != null) Object.DestroyImmediate(panel.gameObject);
            }
        }

        private static void AssertNoOverflow(Component root)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate(true, true);
                Assert.That(text.isTextOverflowing, Is.False, text.name + ": text overflow");
            }
        }
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
