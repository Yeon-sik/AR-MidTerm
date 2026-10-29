using System.IO;
using System.Reflection;
using MidtermAR.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MidtermAR.Tests
{
    public sealed class RuntimeRegistrationAppTests
    {
        private const string Barcode = "8801045014869";

        [TestCase(LookupStatus.Matched, true)]
        [TestCase(LookupStatus.ExternalFound, true)]
        [TestCase(LookupStatus.Loading, false)]
        [TestCase(LookupStatus.NotFound, false)]
        [TestCase(LookupStatus.Conflict, false)]
        [TestCase(LookupStatus.Invalid, false)]
        [TestCase(LookupStatus.PtUnavailable, false)]
        [TestCase(LookupStatus.ExternalUnavailable, false)]
        public void RegistrationRequiresAnIdentifiedCurrentProduct(LookupStatus status, bool expected)
        {
            var catalog = Catalog();
            var images = Images(catalog);
            var registry = new SessionImageRegistry(catalog);
            var state = new ProductRecognitionState(catalog, images, registry);
            state.EnableOnlineLookup();
            Confirm(state, Barcode);
            var product = new ProductSnapshot { barcode = Barcode, name = "시험 상품",
                catalogProductId = status == LookupStatus.Matched ? "11111111-1111-1111-1111-111111111111" : null };
            state.ApplyLookup(new ProductLookupResult(Barcode, status, product), state.LookupGeneration);
            state.Resolve(null, false, 1.2f);
            Assert.That(ProductARApp.CanOfferFrontRegistration(state, images, registry), Is.EqualTo(expected));
            state.ObserveBarcode(null, 1.3f, 1.3f);
            state.Resolve(null, false, 1.3f);
            Assert.That(ProductARApp.CanOfferFrontRegistration(state, images, registry), Is.False);
        }

        [Test]
        public void DevelopmentCardDoesNotBlockPackageRegistrationButRealFrontDoes()
        {
            var catalog = Catalog();
            var images = Images(catalog);
            var registry = new SessionImageRegistry(catalog);
            var barState = new ProductRecognitionState(catalog, images, registry);
            Confirm(barState, "8801104671507");
            Assert.That(ProductARApp.CanOfferFrontRegistration(barState, images, registry), Is.True);
            catalog.TryFind("8801104671507", out var bar);
            Assert.That(registry.TryAdd("runtime-bar", bar, bar, "PT 스냅샷", out _), Is.True);
            Assert.That(ProductARApp.CanOfferFrontRegistration(barState, images, registry), Is.False);
            var theanineState = new ProductRecognitionState(catalog, images, registry);
            Confirm(theanineState, "8809375984692");
            Assert.That(ProductARApp.CanOfferFrontRegistration(theanineState, images, registry), Is.False);
        }

        [Test]
        public void GeneratedSceneConnectsRegistrationButtonAndSharedArCamera()
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var app = Object.FindFirstObjectByType<ProductARApp>();
            var controller = Object.FindFirstObjectByType<RuntimeProductRegistrationController>();
            var screen = Object.FindFirstObjectByType<ProductScreenView>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(Field<RuntimeProductRegistrationController>(app, "frontRegistration"), Is.SameAs(controller));
            var button = Field<Button>(screen, "moveButton");
            Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(app));
            Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(ProductARApp.BeginFrontRegistration)));
            Assert.That(Field<Camera>(controller, "arCamera"), Is.SameAs(Camera.main));
        }

        [Test]
        public void CompletionBindsTheFrozenTargetEvenAfterBarcodeWasReleased()
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var app = Object.FindFirstObjectByType<ProductARApp>();
            Call(app, "Awake");
            try
            {
                var original = new ProductSnapshot { name = "진라면", barcode = Barcode };
                Set(app, "pendingRegistrationProduct", SessionImageRegistry.CopyProduct(original));
                Set(app, "pendingRegistrationSource", "Open Food Facts 정보 · PT 바코드 연결 필요");
                original.name = "이후 바뀐 상품";
                Field<ProductRecognitionState>(app, "recognition").Suspend();
                Call(app, "OnFrontImageAdded", "runtime-ramen");
                Assert.That(Field<SessionImageRegistry>(app, "sessionImages").TryFindByImage("runtime-ramen", out var target), Is.True);
                Assert.That(target.DisplayProduct.name, Is.EqualTo("진라면"));
                Assert.That(target.DisplayProduct.barcode, Is.EqualTo(Barcode));
                Assert.That(target.LocalProduct, Is.Null);
                Assert.That(target.SourceBadge, Does.Contain("PT 바코드 연결 필요"));
                Assert.That(Field<ProductSnapshot>(app, "pendingRegistrationProduct"), Is.Null);
            }
            finally { Call(app, "OnDisable"); Call(app, "OnDestroy"); }
        }

        [Test]
        public void LateCancelledRegistrationStateEventDoesNotClearANewBarcode()
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var app = Object.FindFirstObjectByType<ProductARApp>();
            Call(app, "Awake");
            try
            {
                var state = Field<ProductRecognitionState>(app, "recognition");
                Confirm(state, Barcode);
                state.ApplyLookup(new ProductLookupResult(Barcode, LookupStatus.ExternalFound,
                    new ProductSnapshot { name = "새 상품", barcode = Barcode }), state.LookupGeneration);
                state.Resolve(null, false, 1.2f);
                Set(app, "registrationScreenActive", false);
                Call(app, "OnRegistrationStateChanged");
                Assert.That(state.ConfirmedBarcode, Is.EqualTo(Barcode));
                Assert.That(state.DisplayProduct.name, Is.EqualTo("새 상품"));
            }
            finally { Call(app, "OnDisable"); Call(app, "OnDestroy"); }
        }

        [TestCase(Barcode, true)]
        [TestCase("3017624010701", false)]
        public void CancelledNativeSuccessExplainsRestartOnlyForTheSameProduct(string visibleBarcode, bool expected)
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var app = Object.FindFirstObjectByType<ProductARApp>();
            Call(app, "Awake");
            try
            {
                var controller = Field<RuntimeProductRegistrationController>(app, "frontRegistration");
                var lifecycle = Field<RegistrationLifecycle>(controller, "lifecycle");
                lifecycle.Begin(ProductIdentity.Key(new ProductSnapshot { barcode = Barcode }), "진라면");
                var attempt = lifecycle.StartJob();
                lifecycle.Cancel();
                Assert.That(lifecycle.Complete(attempt, true), Is.False);
                var state = Field<ProductRecognitionState>(app, "recognition");
                state.EnableOnlineLookup();
                Confirm(state, visibleBarcode);
                state.ApplyLookup(new ProductLookupResult(visibleBarcode, LookupStatus.ExternalFound,
                    new ProductSnapshot { name = "현재 상품", barcode = visibleBarcode }), state.LookupGeneration);
                state.Resolve(null, false, 1.2f);
                Call(app, "RefreshScreen", true);
                var screen = Field<ProductScreenView>(app, "screen");
                Assert.That(Field<TMPro.TMP_Text>(screen, "statusMessage").text.Contains(RuntimeProductRegistrationController.RestartNotice), Is.EqualTo(expected));
                Assert.That(state.ConfirmedBarcode, Is.EqualTo(visibleBarcode));
            }
            finally { Call(app, "OnDisable"); Call(app, "OnDestroy"); }
        }

        private static void Confirm(ProductRecognitionState state, string barcode)
        {
            for (var i = 0; i < 3; i++) state.ObserveBarcode(new BarcodeReadResult(barcode, "EAN_13"), 1 + i * .1f, 1 + i * .1f);
            state.Resolve(null, false, 1.2f);
        }
        private static ProductCatalog Catalog() => new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json"));
        private static ProductImageCatalog Images(ProductCatalog catalog) => new ProductImageCatalog(File.ReadAllText("Assets/Data/product-images.json"), catalog);
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Call(object target, string name, params object[] values) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, values);
    }
}
