using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MidtermAR.Tests
{
    public sealed class OnlineLookupTests
    {
        private const string A = "8801104671507";
        private const string B = "4006381333931";
        private const string Id = "1516c3eb-3dfb-4e11-8040-d23fadcdaad0";
        private const string OtherId = "18e83c5c-86a4-4e95-a131-411381789687";
        private ProductCatalog products;
        private ProductRecognitionState state;

        [SetUp]
        public void Setup()
        {
            products = new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json"));
            state = new ProductRecognitionState(products, new ProductImageCatalog(File.ReadAllText("Assets/Data/product-images.json"), products));
            state.EnableOnlineLookup();
        }
        private void Confirm(string barcode, float start = 1)
        {
            for (var i = 0; i < 3; i++) state.ObserveBarcode(new BarcodeReadResult(barcode, "EAN_13"), start + i * .1f, start + i * .1f);
            state.Resolve(null, false, start + .2f);
        }
        private static ProductSnapshot Product(string barcode = A, string id = Id) => new ProductSnapshot
        { catalogProductId = id, barcode = barcode, name = "온라인 상품명", brand = "브랜드", specification = "40g" };
        private ProductSnapshot Local() { products.TryFind(A, out var product); return product; }
        private static ProductLookupResult Match(string barcode = A, string id = Id) => new ProductLookupResult(barcode, LookupStatus.Matched, Product(barcode, id));
        private static TaskCompletionSource<ProductLookupResult> Deferred() => new TaskCompletionSource<ProductLookupResult>();

        [Test]
        public void LocalSnapshotAppearsImmediatelyThenOnlineUpdatesWithoutReplacingSelected()
        {
            Confirm(A);
            var selected = state.Selected;
            var completion = Deferred();
            using (var coordinator = new LookupCoordinator(new FakeLookup { Read = (_, __) => completion.Task }))
            {
                coordinator.Tick(state, 1.2);
                Assert.That(state.BarcodeDisplayProduct, Is.SameAs(selected));
                Assert.That(state.SourceBadge, Does.Contain("PT 조회 중").And.Contain("PT 스냅샷 2026-10-26"));
                completion.SetResult(Match()); coordinator.Tick(state, 1.3); state.Resolve(null, false, 1.3f);
                Assert.That(state.BarcodeDisplayProduct.name, Is.EqualTo("온라인 상품명"));
                Assert.That(state.Selected, Is.SameAs(selected));
                Assert.That(state.Selected.name, Is.Not.EqualTo(state.BarcodeDisplayProduct.name));
                Assert.That(state.SourceBadge, Is.EqualTo("PT 등록 상품"));
            }
        }

        [Test]
        public void LateAResponseDuringBScanOnlyCachesA()
        {
            var a = Deferred(); var b = Deferred();
            var fake = new FakeLookup { Read = (barcode, _) => barcode == A ? a.Task : b.Task };
            using (var coordinator = new LookupCoordinator(fake))
            {
                Confirm(A); coordinator.Tick(state, 1.2);
                Confirm(B, 1.3f); coordinator.Tick(state, 1.5);
                a.SetResult(Match()); coordinator.Tick(state, 1.6); state.Resolve(null, false, 1.6f);
                Assert.That(state.ConfirmedBarcode, Is.EqualTo(B));
                Assert.That(state.LookupResult.Barcode, Is.EqualTo(B));
                Assert.That(state.BarcodeDisplayProduct, Is.Null);
                Assert.That(coordinator.CachedCount, Is.EqualTo(1));
                Assert.That(fake.Calls, Is.EqualTo(2));
            }
        }

        [TestCase("loss")]
        [TestCase("expiry")]
        [TestCase("pause")]
        [TestCase("rescan")]
        public void LateResponseCannotReviveLostOrResetTarget(string action)
        {
            var completion = Deferred();
            using (var coordinator = new LookupCoordinator(new FakeLookup { Read = (_, __) => completion.Task }))
            {
                Confirm(A); coordinator.Tick(state, 1.2);
                var generation = state.LookupGeneration;
                if (action == "loss") state.ObserveBarcode(null, 1.3f, 1.3f);
                if (action == "expiry") state.Resolve(null, false, 2);
                if (action == "pause") state.Suspend();
                if (action == "rescan") state.Reset();
                completion.SetResult(Match()); coordinator.Tick(state, 2); state.Resolve(null, false, 2);
                Assert.That(state.Route, Is.EqualTo(ProductDisplayRoute.None));
                Assert.That(state.Selected, Is.Null);
                Assert.That(state.ConfirmedBarcode, Is.Null);
                Assert.That(state.LookupResult, Is.Null);
                Assert.That(state.ApplyLookup(Match(), generation), Is.False);
                Assert.That(coordinator.CachedCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void CacheRequiresNewThreeFrameConfirmationAndExpires()
        {
            var fake = new FakeLookup { Read = (_, __) => Task.FromResult(Match()) };
            using (var coordinator = new LookupCoordinator(fake))
            {
                Confirm(A); coordinator.Tick(state, 1.2); coordinator.Tick(state, 1.3);
                state.Suspend(); coordinator.Tick(state, 2);
                Assert.That(state.LookupResult, Is.Null);
                Confirm(A, 3); coordinator.Tick(state, 3.2);
                Assert.That(state.LookupResult.Status, Is.EqualTo(LookupStatus.Matched));
                Assert.That(fake.Calls, Is.EqualTo(1));
                state.Reset(); Confirm(A, 310); coordinator.Tick(state, 310.2);
                Assert.That(fake.Calls, Is.EqualTo(2));
            }
        }

        [Test]
        public void SameConfirmedNumberHasOneRequestAcrossFramesAndReconfirmation()
        {
            var completion = Deferred();
            var fake = new FakeLookup { Read = (_, __) => completion.Task };
            using (var coordinator = new LookupCoordinator(fake))
            {
                Confirm(A); coordinator.Tick(state, 1.2); coordinator.Tick(state, 1.3);
                state.ObserveBarcode(null, 1.31f, 1.31f); Confirm(A, 1.4f); coordinator.Tick(state, 1.6);
                Assert.That(fake.Calls, Is.EqualTo(1));
                Assert.That(coordinator.PendingCount, Is.EqualTo(1));
                completion.SetResult(Match()); coordinator.Tick(state, 1.7);
                Assert.That(state.LookupResult.Status, Is.EqualTo(LookupStatus.Matched));
            }
        }

        [Test]
        public void TimeoutCancelsAfterFiveSecondsWithoutMissCache()
        {
            var completion = Deferred(); CancellationToken token = default;
            using (var coordinator = new LookupCoordinator(new FakeLookup { Read = (_, cancellation) => { token = cancellation; return completion.Task; } }))
            {
                Confirm(A); coordinator.Tick(state, 1.2); coordinator.Tick(state, 6.21);
                Assert.That(token.IsCancellationRequested, Is.True);
                Assert.That(state.LookupResult.Status, Is.EqualTo(LookupStatus.PtUnavailable));
                Assert.That(coordinator.CachedCount, Is.Zero);
                completion.SetResult(Match()); coordinator.Tick(state, 7);
                Assert.That(state.LookupResult.Status, Is.EqualTo(LookupStatus.PtUnavailable));
            }
        }

        [TestCase(LookupStatus.PtUnavailable)]
        [TestCase(LookupStatus.ExternalUnavailable)]
        [TestCase(LookupStatus.Conflict)]
        [TestCase(LookupStatus.Invalid)]
        public void FailuresAreNeverNegativeCachedAndRetryIsBounded(LookupStatus status)
        {
            var fake = new FakeLookup { Read = (_, __) => Task.FromResult(new ProductLookupResult(A, status)) };
            using (var coordinator = new LookupCoordinator(fake))
            {
                Confirm(A); coordinator.Tick(state, 1.2); coordinator.Tick(state, 1.3);
                Assert.That(coordinator.CachedCount, Is.Zero);
                state.Reset(); Confirm(A, 2); coordinator.Tick(state, 2.2);
                Assert.That(fake.Calls, Is.EqualTo(1));
                state.Reset(); Confirm(A, 20); coordinator.Tick(state, 20.2);
                Assert.That(fake.Calls, Is.EqualTo(2));
            }
        }

        [TestCase(Id, ProductDisplayRoute.Image3D)]
        [TestCase(OtherId, ProductDisplayRoute.Conflict)]
        public void FrontIdentityUsesOnlinePtIdAndLoadingHidesConflict(string id, ProductDisplayRoute expected)
        {
            Confirm(A); state.BeginLookup(state.LookupGeneration); state.Resolve(Local(), false, 1.2f);
            Assert.That(state.Route, Is.EqualTo(ProductDisplayRoute.Conflict));
            state.ApplyLookup(Match(A, id), state.LookupGeneration); state.Resolve(Local(), false, 1.3f);
            Assert.That(state.Route, Is.EqualTo(expected));
        }

        [TestCase(LookupStatus.ExternalFound)]
        [TestCase(LookupStatus.NotFound)]
        [TestCase(LookupStatus.ExternalUnavailable)]
        [TestCase(LookupStatus.Conflict)]
        public void UnlinkedOrConflictingBarcodeNeverUsesLocalIdToAgreeWithFront(LookupStatus status)
        {
            Confirm(A);
            state.ApplyLookup(new ProductLookupResult(A, status, status == LookupStatus.ExternalFound ? Product(A, null) : null), state.LookupGeneration);
            state.Resolve(Local(), false, 1.2f);
            Assert.That(state.Route, Is.EqualTo(ProductDisplayRoute.Conflict));
        }

        [TestCase("matched", LookupStatus.Matched, 0)]
        [TestCase("not_linked", LookupStatus.ExternalFound, 1)]
        [TestCase("conflict", LookupStatus.Conflict, 0)]
        [TestCase("invalid", LookupStatus.Invalid, 0)]
        public async Task ProviderOrderAndStatusAreExplicit(string status, LookupStatus expected, int offCalls)
        {
            var pt = new FakePt { Reply = new PtBarcodeReply { Status = status, Product = status == "matched" ? Product() : null } };
            var off = new FakeOff { Product = Product(A, null) };
            var result = await new ProductLookup(pt, off, null, products).Lookup(A, CancellationToken.None);
            Assert.That(result.Status, Is.EqualTo(expected));
            Assert.That(off.Calls, Is.EqualTo(offCalls));
            if (status == "matched") Assert.That(result.Product.price, Is.SameAs(Local().price));
        }

        [TestCase(false, LookupStatus.NotFound)]
        [TestCase(true, LookupStatus.ExternalUnavailable)]
        public async Task OffMissAndErrorHaveDifferentCacheSemantics(bool error, LookupStatus expected)
        {
            var result = await new ProductLookup(new FakePt { Reply = new PtBarcodeReply { Status = "not_linked" } },
                new FakeOff { Error = error }, null, products).Lookup(A, CancellationToken.None);
            Assert.That(result.Status, Is.EqualTo(expected));
            Assert.That(result.Cacheable, Is.EqualTo(!error));
        }

        [Test]
        public async Task NutritionMismatchIsDiscardedAndOtherPtIdCannotBorrowLocalPrice()
        {
            var result = await new ProductLookup(new FakePt { Reply = new PtBarcodeReply { Status = "matched", Product = Product(A, OtherId) } }, null,
                new FakeNutrition { Snapshot = new NutritionSnapshot { sourceCatalogProductId = Id, caloriesKcal = 99 } }, products).Lookup(A, CancellationToken.None);
            Assert.That(result.Status, Is.EqualTo(LookupStatus.Matched));
            Assert.That(result.Product.nutrition, Is.Null);
            Assert.That(result.Product.price, Is.Null);
        }

        [TestCase(401)] [TestCase(429)] [TestCase(500)] [TestCase(503)] [TestCase(404)] [TestCase(0)]
        public async Task PtHttpFailureNeverFallsBackToOff(int status)
        {
            var config = new RemoteLookupConfig { ptUrl = "https://example.invalid", ptPublishableKey = "sb_publishable_test" };
            var http = new FakeHttp { Reply = new LookupHttpReply { Status = status, Body = "{}" } };
            var off = new FakeOff();
            var result = await new ProductLookup(new PtBarcodeClient(config, http), off, null, products).Lookup(A, CancellationToken.None);
            Assert.That(result.Status, Is.EqualTo(LookupStatus.PtUnavailable));
            Assert.That(off.Calls, Is.Zero);
        }

        private static JObject PtJson() => new JObject
        {
            ["contractVersion"] = PtBarcodeClient.Contract, ["barcode"] = A, ["status"] = "matched", ["checkedAt"] = "2026-10-27T00:00:00+09:00",
            ["matches"] = new JArray(new JObject { ["catalogProductId"] = Id, ["standardProductId"] = OtherId, ["name"] = "상품",
                ["brand"] = "브랜드", ["contentAmount"] = 40, ["contentUnit"] = "g", ["packageCount"] = 1,
                ["specificationLabel"] = "40g · 1개", ["categoryName"] = "식품", ["latestPublicPrice"] = null })
        };

        [TestCase("version")] [TestCase("identity")] [TestCase("matches")] [TestCase("timestamp")]
        [TestCase("id")] [TestCase("number")] [TestCase("name")] [TestCase("status")] [TestCase("price")]
        public void MalformedPtContractFailsClosed(string problem)
        {
            var json = PtJson();
            if (problem == "version") json["contractVersion"] = "v2";
            if (problem == "identity") json["barcode"] = B;
            if (problem == "matches") ((JArray)json["matches"]).Clear();
            if (problem == "timestamp") json["checkedAt"] = "yesterday";
            if (problem == "id") json["matches"][0]["catalogProductId"] = "unknown";
            if (problem == "number") json["matches"][0]["packageCount"] = -1;
            if (problem == "name") json["matches"][0]["name"] = 123;
            if (problem == "status") json["status"] = "not_linked";
            if (problem == "price") ((JObject)json["matches"][0]).Remove("latestPublicPrice");
            Assert.Throws<FormatException>(() => PtBarcodeClient.Parse(json.ToString(), A));
        }

        [Test]
        public void ExactPtContractAndListedPricePreserveZeroAndSource()
        {
            var json = PtJson();
            json["matches"][0]["latestPublicPrice"] = new JObject { ["amountKrw"] = 0, ["kind"] = "listed", ["seller"] = "판매처",
                ["observedAt"] = "2026-10-26T12:00:00Z", ["sourceLabel"] = "공개 게시 가격" };
            var result = PtBarcodeClient.Parse(json.ToString(), A);
            Assert.That(result.Product.price.unitPriceKrw, Is.EqualTo(0));
            Assert.That(result.Product.price.kind, Is.EqualTo("listed"));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ConflictCardinalityFollowsCurrentlyPublishedPtV1Contract(int count)
        {
            var json = PtJson(); json["status"] = "conflict";
            var matches = (JArray)json["matches"];
            if (count == 0) matches.Clear();
            if (count == 2)
            {
                var second = matches[0].DeepClone(); second["catalogProductId"] = OtherId; matches.Add(second);
            }
            if (count < 2) Assert.Throws<FormatException>(() => PtBarcodeClient.Parse(json.ToString(), A));
            else Assert.That(PtBarcodeClient.Parse(json.ToString(), A).Status, Is.EqualTo("conflict"));
        }

        [Test]
        public void OffV3PrefersKoreanAndHasNoInventedPriceNutritionOrPtId()
        {
            var json = new JObject { ["code"] = A, ["status"] = "success", ["result"] = new JObject { ["id"] = "product_found" }, ["errors"] = new JArray(),
                ["product"] = new JObject { ["product_name_ko"] = "한국어", ["product_name"] = "English", ["brands"] = "브랜드", ["quantity"] = "40g" } };
            var product = OffProductClient.Parse(json.ToString(), A);
            Assert.That(product.name, Is.EqualTo("한국어")); Assert.That(product.catalogProductId, Is.Null);
            Assert.That(product.price, Is.Null); Assert.That(product.nutrition, Is.Null);
            Assert.Throws<FormatException>(() => OffProductClient.Parse(json.ToString(), B));
        }

        [TestCase("product_not_found", false)]
        [TestCase("invalid_code", true)]
        [TestCase("server_error", true)]
        public void Off404RequiresStructuredProductNotFound(string error, bool throws)
        {
            var json = new JObject { ["code"] = A, ["status"] = "failure", ["result"] = new JObject { ["id"] = "product_not_found" },
                ["errors"] = new JArray(new JObject { ["message"] = new JObject { ["id"] = error } }) };
            if (throws) Assert.Throws<FormatException>(() => OffProductClient.Parse(json.ToString(), A, 404));
            else Assert.That(OffProductClient.Parse(json.ToString(), A, 404), Is.Null);
            Assert.Throws<FormatException>(() => OffProductClient.Parse(json.ToString(), A, 200));
        }

        [Test]
        public async Task OffUsesLimitedFieldsUserAgentAndAppBudget()
        {
            var http = new FakeHttp { Reply = new LookupHttpReply { Status = 500, Body = "{}" } };
            var limit = new LookupRateLimit();
            var client = new OffProductClient(new RemoteLookupConfig { offContact = "test@example.invalid" }, http, "0.6.0", () => 0, limit);
            for (var i = 0; i < 16; i++) { try { await client.Lookup(A, CancellationToken.None); } catch (IOException) { } }
            Assert.That(http.Calls, Is.EqualTo(15));
            Assert.That(http.UserAgent, Is.EqualTo("ProductAR/0.6.0 (test@example.invalid)"));
            Assert.That(http.Url, Does.Contain("/api/v3/product/").And.Contain("fields=product_name_ko,product_name,brands,quantity").And.Not.Contain("nutriments"));
            Assert.That(limit.TryAcquire(60), Is.True);
        }

        [TestCase(null)] [TestCase("")] [TestCase("invalid json")] [TestCase("{}")]
        [TestCase("{\"enabled\":false}")] [TestCase("{\"enabled\":true,\"ptUrl\":\"http://example.invalid\",\"ptPublishableKey\":\"service_role\"}")]
        public void MissingOrInvalidConfigurationIsLocalOnly(string json) => Assert.That(RemoteLookupConfig.Parse(json).PtReady, Is.False);

        [Test]
        public void NutritionSnakeCaseContractPreservesNullAndZeroAndRejectsWrongApproval()
        {
            var row = new JObject { ["contract_version"] = "nutrition-read.v1", ["catalog_product_id"] = Id,
                ["nutrition_food_id"] = "nutrition-id", ["basis_amount"] = 40, ["basis_unit"] = "g", ["prep_state"] = "unspecified",
                ["source_type"] = "pricetrace_manual", ["nutrition_values"] = new JObject
                { ["calories_kcal"] = 185, ["protein_grams"] = 15, ["carbs_grams"] = 15, ["fat_grams"] = 7,
                    ["sodium_mg"] = 0, ["saturated_fat_grams"] = null, ["sugars_grams"] = null, ["fiber_grams"] = null } };
            var result = PublicNutritionClient.Parse(new JArray(row).ToString(), Id);
            Assert.That(result.caloriesKcal, Is.EqualTo(185));
            Assert.That(result.sodiumMg, Is.EqualTo(0));
            Assert.That(result.fiberGrams, Is.Null);
            Assert.That(result.sourceCatalogProductId, Is.EqualTo(Id));
            Assert.Throws<FormatException>(() => PublicNutritionClient.Parse(row.ToString(), OtherId));
            row["contract_version"] = "nutrition-read.v2";
            Assert.Throws<FormatException>(() => PublicNutritionClient.Parse(row.ToString(), Id));
            Assert.That(PublicNutritionClient.Parse("[]", Id), Is.Null);
        }

        [Test]
        public async Task NutritionRequestUsesPricetraceNamespaceAndExactId()
        {
            var http = new FakeHttp { Reply = new LookupHttpReply { Status = 200, Body = "[]" } };
            var config = new RemoteLookupConfig { nutritionUrl = "https://example.invalid", nutritionPublishableKey = "sb_publishable_test_123456789" };
            Assert.That(await new PublicNutritionClient(config, http).Lookup(Id, CancellationToken.None), Is.Null);
            var body = JObject.Parse(http.Body);
            Assert.That((string)body["p_namespace"], Is.EqualTo("pricetrace"));
            Assert.That((string)body["p_catalog_product_id"], Is.EqualTo(Id));
        }

        [Test]
        public async Task CanceledOrTimedOutPtRequestNeverCallsOff()
        {
            var off = new FakeOff();
            var result = await new ProductLookup(new CanceledPt(), off, null, products).Lookup(A, CancellationToken.None);
            Assert.That(result.Status, Is.EqualTo(LookupStatus.PtUnavailable));
            Assert.That(off.Calls, Is.Zero);
        }

        [Test]
        public void UserConfirmedTheanineUsesPtIdentityAndTwoPriceObservations()
        {
            products.TryFind("8809375984692", out var product);
            Assert.That(product.catalogProductId, Is.EqualTo(OtherId));
            Assert.That(product.price.unitPriceKrw, Is.EqualTo(13210));
            Assert.That(product.price.observationId, Is.EqualTo("b88e89f58a6c9737"));
            Assert.That(product.priceObservations.Length, Is.EqualTo(2));
            Assert.That(product.priceObservations[0].confidence, Is.EqualTo("low"));
            Assert.That(product.nutrition, Is.Null);
        }

        private sealed class FakeLookup : IProductLookup
        {
            public int Calls;
            public Func<string, CancellationToken, Task<ProductLookupResult>> Read;
            public Task<ProductLookupResult> Lookup(string barcode, CancellationToken cancellation) { Calls++; return Read(barcode, cancellation); }
        }
        private sealed class FakePt : IPtBarcodeClient
        { public PtBarcodeReply Reply; public Task<PtBarcodeReply> Lookup(string barcode, CancellationToken cancellation) => Task.FromResult(Reply); }
        private sealed class CanceledPt : IPtBarcodeClient
        { public Task<PtBarcodeReply> Lookup(string barcode, CancellationToken cancellation) => Task.FromException<PtBarcodeReply>(new OperationCanceledException()); }
        private sealed class FakeOff : IOffProductClient
        {
            public int Calls; public bool Error; public ProductSnapshot Product;
            public Task<ProductSnapshot> Lookup(string barcode, CancellationToken cancellation)
            { Calls++; return Error ? Task.FromException<ProductSnapshot>(new IOException()) : Task.FromResult(Product); }
        }
        private sealed class FakeNutrition : IPublicNutritionClient
        { public NutritionSnapshot Snapshot; public Task<NutritionSnapshot> Lookup(string id, CancellationToken cancellation) => Task.FromResult(Snapshot); }
        private sealed class FakeHttp : ILookupHttp
        {
            public LookupHttpReply Reply; public int Calls; public string Url, UserAgent, Body;
            public Task<LookupHttpReply> Send(string url, string body, string key, string userAgent, CancellationToken cancellation)
            { Calls++; Url = url; UserAgent = userAgent; Body = body; return Task.FromResult(Reply); }
        }
    }
}
