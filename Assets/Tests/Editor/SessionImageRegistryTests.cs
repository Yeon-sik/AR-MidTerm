using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.XR.ARSubsystems;

namespace MidtermAR.Tests
{
    public sealed class SessionImageRegistryTests
    {
        private const string EanA = "4006381333931";
        private const string EanB = "5901234123457";
        private const string IdA = "11111111-1111-1111-1111-111111111111";
        private const string IdB = "22222222-2222-2222-2222-222222222222";
        private ProductCatalog products;
        private ProductImageCatalog images;
        private SessionImageRegistry registry;
        private ProductRecognitionState recognition;

        [SetUp]
        public void SetUp()
        {
            products = new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json"));
            images = new ProductImageCatalog(File.ReadAllText("Assets/Data/product-images.json"), products);
            registry = new SessionImageRegistry(products);
            recognition = new ProductRecognitionState(products, images, registry);
        }

        private static ProductSnapshot Product(string barcode = EanA, string id = null) => new ProductSnapshot
        { name = "현장 등록 시험", catalogProductId = id, barcode = barcode, barcodeFormat = "EAN_13" };

        private ImageProductTarget Add(ProductSnapshot product = null, string name = "runtime-a", string badge = "Open Food Facts 정보 · PT 바코드 연결 필요")
        {
            Assert.That(registry.TryAdd(name, product ?? Product(), null, badge, out var target), Is.True);
            return target;
        }

        private void Confirm(ProductSnapshot product, LookupStatus status = LookupStatus.ExternalFound, float start = 1)
        {
            recognition.EnableOnlineLookup();
            for (var i = 0; i < 3; i++)
                recognition.ObserveBarcode(new BarcodeReadResult(product.barcode, "EAN_13"), start + i * .1f, start + i * .1f);
            Assert.That(recognition.ApplyLookup(new ProductLookupResult(product.barcode, status, product), recognition.LookupGeneration), Is.True);
        }

        [TestCase(null, null, EanA, EanA, true)]
        [TestCase(null, null, EanA, EanB, false)]
        [TestCase(IdA, IdA, EanA, EanB, true)]
        [TestCase(IdA, IdB, EanA, EanA, false)]
        [TestCase(IdA, null, EanA, EanA, true)]
        [TestCase(null, IdA, EanA, EanA, true)]
        [TestCase(null, null, null, null, false)]
        [TestCase(null, null, "4006381333932", "4006381333932", false)]
        public void ExactIdentityNeverMergesMissingIdsOrContradictoryPtIds(string firstId, string secondId,
            string firstBarcode, string secondBarcode, bool expected)
            => Assert.That(ProductIdentity.Matches(Product(firstBarcode, firstId), Product(secondBarcode, secondId)), Is.EqualTo(expected));

        [Test]
        public void IdentityKeyKeepsPtIdAndLeadingZeroEan()
        {
            Assert.That(ProductIdentity.Key(Product(EanA, IdA)), Is.EqualTo(IdA));
            Assert.That(ProductIdentity.Key(Product("0123456789012")), Is.EqualTo("0123456789012"));
            Assert.That(ProductIdentity.Key(Product(null)), Is.Null);
            Assert.That(ProductIdentity.Matches(null, Product()), Is.False);
        }

        [Test]
        public void RegistrationDeepCopiesProductNutritionPriceAndEvidence()
        {
            var original = Product();
            original.mapping = new MappingEvidence { source = "원래 연결 근거", scope = "시험" };
            original.price = new PriceSnapshot { unitPriceKrw = 100, source = "PT", observedAt = "2026-10-28", kind = "observed" };
            original.priceObservations = new[] { new PriceSnapshot { unitPriceKrw = 90, source = "이전 관측" }, null };
            original.nutrition = new NutritionSnapshot { caloriesKcal = 0, basisAmount = 100, basisUnit = "g", provider = "Open Food Facts",
                source = "원자료 URL", sourceUpdatedAt = "2026-10-27T00:00:00.0000000Z" };
            var target = Add(original);
            original.name = "변경된 조회 결과";
            original.barcode = EanB;
            original.mapping.source = "변경";
            original.price.unitPriceKrw = 200;
            original.priceObservations[0].source = "변경";
            original.nutrition.provider = "변경";
            original.nutrition.caloriesKcal = 999;
            original.nutrition.sourceUpdatedAt = "변경";
            Assert.That(target.DisplayProduct, Is.Not.SameAs(original));
            Assert.That(target.DisplayProduct.name, Is.EqualTo("현장 등록 시험"));
            Assert.That(target.IdentityKey, Is.EqualTo(EanA));
            Assert.That(target.DisplayProduct.mapping.source, Is.EqualTo("원래 연결 근거"));
            Assert.That(target.DisplayProduct.price.unitPriceKrw, Is.EqualTo(100));
            Assert.That(target.DisplayProduct.priceObservations[0].source, Is.EqualTo("이전 관측"));
            Assert.That(target.DisplayProduct.priceObservations[1], Is.Null);
            Assert.That(target.DisplayProduct.nutrition.provider, Is.EqualTo("Open Food Facts"));
            Assert.That(target.DisplayProduct.nutrition.caloriesKcal, Is.EqualTo(0));
            Assert.That(target.DisplayProduct.nutrition.sourceUpdatedAt, Does.StartWith("2026-10-27"));
            Assert.That(SessionImageRegistry.CopyProduct(null), Is.Null);
        }

        [Test]
        public void PublicCopyFreezesButtonTimeBeforeNativeAddCompletes()
        {
            var original = Product();
            var captured = SessionImageRegistry.CopyProduct(original);
            original.name = "뒤늦은 다른 조회";
            var target = Add(captured);
            captured.name = "캡처 객체 재사용";
            Assert.That(target.DisplayProduct.name, Is.EqualTo("현장 등록 시험"));
        }

        [Test]
        public void SessionRegistrationDoesNotMutateStaticCatalogOrLeakIntoAnotherSession()
        {
            var count = products.Count;
            var target = Add();
            Assert.That(registry.TryFindByImage("runtime-a", out var found), Is.True);
            Assert.That(found, Is.SameAs(target));
            Assert.That(found.LocalProduct, Is.Null);
            Assert.That(found.Binding.runtimeRegistered, Is.True);
            Assert.That(found.Binding.developmentOnly, Is.False);
            Assert.That(found.Binding.catalogProductId, Is.Null);
            Assert.That(found.Binding.productWidthMetres, Is.Zero);
            Assert.That(products.Count, Is.EqualTo(count));
            Assert.That(products.TryFind(EanA, out _), Is.False);
            Assert.That(images.TryFindByImage("runtime-a", out _), Is.False);
            Assert.That(new SessionImageRegistry(products).ContainsProduct(Product()), Is.False);
        }

        [Test]
        public void DuplicateNameAndDuplicateIdentityAreRejected()
        {
            Add();
            Assert.That(registry.TryAdd("runtime-a", Product(EanB), null, "OFF", out _), Is.False);
            Assert.That(registry.TryAdd("another-name", Product(), null, "OFF", out _), Is.False);
            Assert.That(registry.Count, Is.EqualTo(1));
        }

        [TestCase(null, EanA)]
        [TestCase("", EanA)]
        [TestCase("runtime-invalid", "4006381333932")]
        [TestCase("runtime-invalid", null)]
        public void InvalidNameOrUnidentifiedProductCannotRegister(string name, string barcode)
            => Assert.That(registry.TryAdd(name, Product(barcode), null, "OFF", out _), Is.False);

        [Test]
        public void OnlyActualLocalCatalogObjectCanBeSelected()
        {
            Assert.That(products.TryFind("8801104671507", out var local), Is.True);
            var online = SessionImageRegistry.CopyProduct(local);
            Assert.That(registry.TryAdd("runtime-invalid", online, online, "PT", out _), Is.False);
            Assert.That(new SessionImageRegistry().TryAdd("runtime-invalid", online, local, "PT", out _), Is.False);
            Assert.That(registry.TryAdd("runtime-local", online, local, "PT 등록 상품", out var target), Is.True);
            recognition.ResolveImage(target, false, 1);
            Assert.That(recognition.Selected, Is.SameAs(local));
            Assert.That(recognition.ImageDisplayProduct, Is.SameAs(target.DisplayProduct).And.Not.SameAs(local));
        }

        [Test]
        public void MismatchedLocalProductCannotBeAttachedToRuntimeImage()
        {
            Assert.That(products.TryFind("8801104671507", out var local), Is.True);
            Assert.That(registry.TryAdd("runtime-invalid", Product(EanA, IdA), local, "PT", out _), Is.False);
        }

        [Test]
        public void StaticJsonCannotClaimRuntimeSizeExemption()
        {
            var json = JObject.Parse(File.ReadAllText("Assets/Data/product-images.json"));
            json["images"][0]["runtimeRegistered"] = true;
            Assert.Throws<FormatException>(() => new ProductImageCatalog(json.ToString(), products));
        }

        [TestCase(null, "Open Food Facts 정보 · PT 바코드 연결 필요")]
        [TestCase(IdA, "PT 등록 상품")]
        public void OnlineFrontDisplaysWithoutInjectingLocalSelected(string id, string badge)
        {
            var target = Add(Product(EanA, id), badge: badge);
            recognition.ResolveImage(target, false, 1);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.Image3D));
            Assert.That(recognition.Selected, Is.Null);
            Assert.That(recognition.DisplayProduct, Is.SameAs(target.DisplayProduct));
            Assert.That(recognition.DisplayProductKey, Is.EqualTo(id ?? EanA));
            Assert.That(recognition.SourceBadge, Is.EqualTo(badge));
            Assert.That(recognition.Registration(target.DisplayProduct), Is.EqualTo(ProductRegistration.Both));
            Assert.That(recognition.BarcodeBadge(target.DisplayProduct), Is.EqualTo("앞면 현장 등록됨 · 이번 실행에서만 유지"));
        }

        [Test]
        public void RuntimeImageNeedsCurrentFullTrackingAndClearsOnNextFrame()
        {
            Add();
            var state = new ProductImageState(products, images, registry);
            state.BeginFrame(true);
            state.ObserveImage("runtime-a", TrackingState.Tracking, 1);
            state.EndFrame();
            Assert.That(state.CanDisplay, Is.True);
            Assert.That(state.Target.LocalProduct, Is.Null);
            Assert.That(state.Product, Is.SameAs(state.Target.DisplayProduct));
            state.BeginFrame(true);
            state.ObserveImage("runtime-a", TrackingState.Limited, 2);
            Assert.That(state.Target, Is.Null);
            Assert.That(state.CanDisplay, Is.False);
            state.BeginFrame(false);
            state.ObserveImage("runtime-a", TrackingState.Tracking, 1);
            Assert.That(state.CanDisplay, Is.False);
        }

        [Test]
        public void TwoDifferentOffImagesConflictDespiteBothHavingNullPtId()
        {
            Add(); Add(Product(EanB), "runtime-b");
            var state = new ProductImageState(products, images, registry);
            state.BeginFrame(true);
            state.ObserveImage("runtime-a", TrackingState.Tracking, 1);
            state.ObserveImage("runtime-b", TrackingState.Tracking, 1);
            recognition.ResolveImage(state.Target, state.HasImageConflict, 1);
            Assert.That(state.HasImageConflict, Is.True);
            Assert.That(state.CanDisplay, Is.False);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.Conflict));
            Assert.That(recognition.DisplayProduct, Is.Null);
        }

        [Test]
        public void SameStaticAndRuntimeProductRemainOneTarget()
        {
            Assert.That(products.TryFind("8801104671507", out var local), Is.True);
            Assert.That(images.TryFind(local, out var binding), Is.True);
            Assert.That(registry.TryAdd("runtime-local", local, local, "PT", out _), Is.True);
            var state = new ProductImageState(products, images, registry);
            state.BeginFrame(true);
            state.ObserveImage(binding.referenceImageName, TrackingState.Tracking, 1);
            state.ObserveImage("runtime-local", TrackingState.Tracking, 1);
            Assert.That(state.HasImageConflict, Is.False);
            Assert.That(state.CanDisplay, Is.True);
            Assert.That(state.Target.LocalProduct, Is.SameAs(local));
        }

        [Test]
        public void RuntimeRegistrationBadgeTakesPriorityOverExistingDevelopmentCard()
        {
            Assert.That(products.TryFind("8801104671507", out var local), Is.True);
            Assert.That(images.TryFind(local, out var binding), Is.True);
            Assert.That(binding.developmentOnly, Is.True);
            Assert.That(recognition.BarcodeBadge(local), Is.EqualTo("앞면 이미지 등록됨 · 앞면을 비추면 3D로 표시"));
            Assert.That(registry.TryAdd("runtime-package-front", local, local, "PT 등록 상품", out _), Is.True);
            Assert.That(recognition.BarcodeBadge(local), Is.EqualTo("앞면 현장 등록됨 · 이번 실행에서만 유지"));
        }

        [TestCase(EanA, ProductDisplayRoute.Image3D)]
        [TestCase(EanB, ProductDisplayRoute.Conflict)]
        public void OffBarcodeAndRuntimeFrontCompareExactBarcode(string barcode, ProductDisplayRoute expected)
        {
            var target = Add();
            Confirm(Product(barcode));
            recognition.ResolveImage(target, false, 1.2f);
            Assert.That(recognition.Route, Is.EqualTo(expected));
            Assert.That(recognition.Selected, Is.Null);
        }

        [Test]
        public void ContradictoryPtIdsHideEvenWhenBarcodeIsIdentical()
        {
            var target = Add(Product(EanA, IdA), badge: "PT 등록 상품");
            Confirm(Product(EanA, IdB), LookupStatus.Matched);
            recognition.ResolveImage(target, false, 1.2f);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.Conflict));
        }

        [TestCase("8801104671507")]
        [TestCase("8809375984692")]
        public void StaticFrontStillConflictsWithOffResultForTheSameBarcode(string barcode)
        {
            Assert.That(products.TryFind(barcode, out var local), Is.True);
            Assert.That(images.TryFind(local, out var binding), Is.True);
            var target = ImageProductTarget.FromLocal(local, binding, "PT 스냅샷 " + products.SnapshotDate);
            Confirm(Product(barcode));
            recognition.ResolveImage(target, false, 1.2f);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.Conflict));
            Assert.That(recognition.DisplayProduct, Is.Null);
            Assert.That(recognition.Selected, Is.Null);
            Assert.That(recognition.BarcodeBadge(local), Is.EqualTo("앞면 이미지 등록됨 · 앞면을 비추면 3D로 표시"));
        }

        [Test]
        public void PendingLookupStillConflictsWithImageUntilIdentityIsKnown()
        {
            var target = Add();
            Confirm(Product());
            recognition.ObserveBarcode(null, 1.3f, 1.3f);
            for (var i = 0; i < 3; i++) recognition.ObserveBarcode(new BarcodeReadResult(EanA, "EAN_13"), 1.4f + i * .1f, 1.4f + i * .1f);
            recognition.BeginLookup(recognition.LookupGeneration);
            recognition.ResolveImage(target, false, 1.6f);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.Conflict));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LossAndPauseClearImageDisplayAndSelection(bool pause)
        {
            recognition.ResolveImage(Add(), false, 1);
            if (pause) recognition.Suspend(); else recognition.ResolveImage(null, false, 2);
            Assert.That(recognition.Route, Is.EqualTo(ProductDisplayRoute.None));
            Assert.That(recognition.ImageDisplayProduct, Is.Null);
            Assert.That(recognition.DisplayProductKey, Is.Null);
            Assert.That(recognition.Selected, Is.Null);
            Assert.That(recognition.SourceBadge, Is.Empty);
        }

        [Test]
        public void LateLookupAfterLossCannotReplaceRegisteredImageSnapshot()
        {
            var target = Add();
            Confirm(Product());
            var oldGeneration = recognition.LookupGeneration;
            recognition.ObserveBarcode(null, 1.3f, 1.3f);
            var changed = Product(); changed.name = "늦은 응답";
            Assert.That(recognition.ApplyLookup(new ProductLookupResult(EanA, LookupStatus.ExternalFound, changed), oldGeneration), Is.False);
            recognition.ResolveImage(target, false, 1.4f);
            Assert.That(recognition.DisplayProduct.name, Is.EqualTo("현장 등록 시험"));
            Assert.That(recognition.LookupResult, Is.Null);
            Assert.That(recognition.Selected, Is.Null);
        }
    }
}
