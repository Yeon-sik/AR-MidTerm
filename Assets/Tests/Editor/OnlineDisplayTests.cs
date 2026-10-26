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
using Object = UnityEngine.Object;

namespace MidtermAR.Tests
{
    public sealed class OnlineDisplayTests
    {
        private const string BarcodeA = "4006381333931";
        private const string BarcodeB = "5901234123457";
        private const string IdA = "11111111-1111-1111-1111-111111111111";
        private const string IdB = "22222222-2222-2222-2222-222222222222";
        private ProductARApp app;
        private ProductRecognitionState state;
        private TrackedImageProductPanel imagePanel;
        private BarcodeProductPanel barcodePanel;
        private ProductScreenView screen;

        [SetUp]
        public void Setup()
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            app = Object.FindFirstObjectByType<ProductARApp>();
            imagePanel = Object.FindFirstObjectByType<TrackedImageProductPanel>();
            barcodePanel = Object.FindFirstObjectByType<BarcodeProductPanel>();
            screen = Object.FindFirstObjectByType<ProductScreenView>();
            state = null;
        }

        [TearDown]
        public void Cleanup()
        {
            Call(app, "OnDisable");
            var world = Field<WorldProductPanel>(imagePanel, "panel");
            if (world != null) Object.DestroyImmediate(world.gameObject);
        }

        private ProductSnapshot Match(string barcode = BarcodeA, string id = IdA, float start = 1)
        {
            if (state == null) { Call(app, "Awake"); state = Field<ProductRecognitionState>(app, "recognition"); }
            state.EnableOnlineLookup();
            var catalog = new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json"));
            Assert.That(catalog.TryFind(barcode, out _), Is.False, "This regression must use an online-only product.");
            for (var i = 0; i < 3; i++) state.ObserveBarcode(new BarcodeReadResult(barcode, "EAN_13"), start + i * .1f, start + i * .1f);
            var product = new ProductSnapshot { barcode = barcode, catalogProductId = id, name = "온라인 전용 상품", brand = "시험", specification = "40g" };
            state.ApplyLookup(new ProductLookupResult(barcode, LookupStatus.Matched, product), state.LookupGeneration);
            state.Resolve(null, false, start + .2f);
            return product;
        }

        [Test]
        public void MissingLocalConfigurationAssetStillStartsWithLocalBarcodeSupport()
        {
            typeof(ProductARApp).GetField("remoteLookupJson", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(app, null);
            Call(app, "Awake");
            state = Field<ProductRecognitionState>(app, "recognition");
            Assert.That(state.OnlineEnabled, Is.False);
            Assert.That(Field<LookupCoordinator>(app, "lookup"), Is.Null);
            for (var i = 0; i < 3; i++) state.ObserveBarcode(new BarcodeReadResult("8801104671507", "EAN_13"), 1 + i * .1f, 1 + i * .1f);
            state.Resolve(null, false, 1.2f);
            Assert.That(state.Route, Is.EqualTo(ProductDisplayRoute.Barcode2D));
            Assert.That(state.DisplayProduct.name, Does.Contain("더단백"));
        }

        [Test]
        public void OnlineOnlyMatchShowsProductActionWithoutInjectingSelected()
        {
            var product = Match();
            Call(app, "Present");
            Call(app, "RefreshScreen", true);
            Assert.That(state.Selected, Is.Null);
            Assert.That(state.Route, Is.EqualTo(ProductDisplayRoute.Barcode2D));
            Assert.That(state.DisplayProduct, Is.SameAs(product));
            Assert.That(state.DisplayProductKey, Is.EqualTo(IdA));
            Assert.That(Field<string>(app, "lastDisplayedProductKey"), Is.EqualTo(IdA));
            var button = Field<Button>(screen, "primaryButton");
            Assert.That(button.gameObject.activeSelf, Is.True);
            Assert.That(button.interactable, Is.True);
            Assert.That(button.targetGraphic.color, Is.EqualTo(new Color(.2f, .3f, .34f)), "Product action must use the secondary style.");
        }

        [Test]
        public void ChangingOnlinePtIdentityClearsBothPreviouslyBoundPanels()
        {
            var first = Match(); Call(app, "Present");
            barcodePanel.SetProduct(first);
            var catalog = new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json"));
            catalog.TryFind("8801104671507", out var local);
            var images = new ProductImageCatalog(File.ReadAllText("Assets/Data/product-images.json"), catalog);
            images.TryFindByImage("test-card", out var binding);
            imagePanel.SetProduct(local, binding);
            var world = Field<WorldProductPanel>(imagePanel, "panel");
            world.ShowPrice();
            Match(BarcodeB, IdB, 1.3f); Call(app, "Present");
            Assert.That(state.Selected, Is.Null);
            Assert.That(Field<string>(app, "lastDisplayedProductKey"), Is.EqualTo(IdB));
            Assert.That(imagePanel.Binding, Is.Null);
            Assert.That(world.CurrentTab, Is.EqualTo(ProductInfoTab.Nutrition));
            Assert.That(Field<TMP_Text>(barcodePanel, "productTitle").text, Is.Empty);
        }

        [TestCase("loss")]
        [TestCase("pause")]
        [TestCase("rescan")]
        public void OnlineOnlyDisplayAndActionClearWhenTargetIsReleased(string action)
        {
            var product = Match(); Call(app, "Present"); barcodePanel.SetProduct(product);
            if (action == "loss") state.ObserveBarcode(null, 1.3f, 1.3f);
            if (action == "pause") Call(app, "OnApplicationPause", true);
            if (action == "rescan") app.RestartScan();
            state.Resolve(null, false, 1.3f); Call(app, "Present"); Call(app, "RefreshScreen", true);
            Assert.That(state.DisplayProduct, Is.Null);
            Assert.That(state.DisplayProductKey, Is.Null);
            Assert.That(state.Selected, Is.Null);
            Assert.That(barcodePanel.HasVisibleTarget, Is.False);
            Assert.That(Field<Button>(screen, "primaryButton").gameObject.activeSelf, Is.False);
        }

        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Call(object target, string name, params object[] values) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, values);
    }
}
