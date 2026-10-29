using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MidtermAR.Tests
{
    public sealed class ProductInsightTests
    {
        private const string Barcode = "8801104671507";
        private static string Json => File.ReadAllText("Assets/Data/demo-products.json");
        private static ProductSnapshot Theanine()
        { new ProductCatalog(Json).TryFind("8809375984692", out var product); return product; }

        [Test]
        public void TheanineInsightsUseStructuredPackCountAndTwoObservedSellers()
        {
            var product = Theanine();
            Assert.That(ProductInsightCalculator.PricePerContentUnit(product), Is.EqualTo(13210d / 60));
            Assert.That(ProductInsightCalculator.PricePerServing(product), Is.EqualTo(13210d / 60));
            Assert.That(ProductInsightCalculator.AnnotationText(product, "servingPrice"), Is.EqualTo("1정당 약 220원\n1g당 약 220원 · 관측가 기준"));
            Assert.That(ProductInsightCalculator.AnnotationText(product, "observationSummary"), Is.EqualTo("관측 판매처 2곳 · 13,210원\nPriceTrace 스냅샷"));
            var summary = ProductInsightCalculator.ObservationSummary(product);
            Assert.That(summary.Count, Is.EqualTo(2)); Assert.That(summary.SellerCount, Is.EqualTo(2));
            Assert.That(summary.FirstDate, Is.EqualTo(new DateTime(2026, 5, 8)));
            Assert.That(summary.LastDate, Is.EqualTo(new DateTime(2026, 7, 14)));
            Assert.That(ProductInfoFormatter.WorldPrice(product), Is.EqualTo("관측가 13,210원 · 2건"));
        }

        [Test]
        public void ExplicitObservationSetControlsRangeAndExcludesListedEntries()
        {
            var p = Theanine();
            p.priceObservations[0].unitPriceKrw = 12900;
            var summary = ProductInsightCalculator.ObservationSummary(p);
            Assert.That(summary.PriceRange, Is.EqualTo("12,900~13,210원"));
            p.priceObservations[0].kind = "listed";
            summary = ProductInsightCalculator.ObservationSummary(p);
            Assert.That(summary.Count, Is.EqualTo(1)); Assert.That(summary.PriceRange, Is.EqualTo("13,210원"));
            p.price.kind = "listed";
            Assert.That(ProductInsightCalculator.PricePerServing(p), Is.Null);
            Assert.That(ProductInsightCalculator.PricePerContentUnit(p), Is.Null);
            Assert.That(ProductInfoFormatter.WorldPrice(p), Is.EqualTo("가격 정보 없음"));
        }

        [Test]
        public void SingleObservationFallsBackToRepresentativeAndEmptyExplicitSetStaysEmpty()
        {
            var p = Theanine(); p.priceObservations = null;
            Assert.That(ProductInsightCalculator.ObservationSummary(p).Count, Is.EqualTo(1));
            Assert.That(ProductInfoFormatter.WorldPrice(p), Is.EqualTo("관측가 13,210원"));
            p.priceObservations = Array.Empty<PriceSnapshot>();
            Assert.That(ProductInsightCalculator.ObservationSummary(p), Is.Null);
            p.priceObservations = null; p.price.kind = "listed";
            Assert.That(ProductInsightCalculator.ObservationSummary(p), Is.Null);
        }

        [Test]
        public void MissingCountAndEachUnitNeverParseNumbersFromSpecification()
        {
            var p = Theanine(); p.servingCount = null; p.servingUnit = null; p.servingCountSource = null;
            p.contentUnit = "each";
            Assert.That(ProductInsightCalculator.PricePerServing(p), Is.Null);
            Assert.That(ProductInsightCalculator.PricePerContentUnit(p), Is.Null);
            Assert.That(ProductInsightCalculator.AnnotationText(p, "servingPrice"), Is.Null);
            new ProductCatalog(Json).TryFind(Barcode, out var bar);
            Assert.That(bar.contentAmount, Is.EqualTo(40)); Assert.That(bar.servingCount, Is.Null);
            Assert.That(ProductInsightCalculator.PricePerServing(bar), Is.Null);
        }

        [TestCase(0d)] [TestCase(-1d)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
        public void InvalidQuantitiesAreRejectedAndNeverYieldAnInsight(double value)
        {
            var p = Theanine(); p.contentAmount = value;
            Assert.Throws<FormatException>(() => ProductCatalog.ValidateQuantities(p));
            Assert.That(ProductInsightCalculator.PricePerContentUnit(p), Is.Null);
            p = Theanine(); p.servingCount = value;
            Assert.Throws<FormatException>(() => ProductCatalog.ValidateQuantities(p));
            Assert.That(ProductInsightCalculator.PricePerServing(p), Is.Null);
        }

        [TestCase(0d)] [TestCase(-1d)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
        public void InvalidObservedPricesDoNotProduceCalculatedValuesOrSummaries(double value)
        {
            var p = Theanine(); p.price.unitPriceKrw = value; p.priceObservations = null;
            Assert.That(ProductInsightCalculator.PricePerContentUnit(p), Is.Null);
            Assert.That(ProductInsightCalculator.PricePerServing(p), Is.Null);
            Assert.That(ProductInsightCalculator.ObservationSummary(p), Is.Null);
        }

        [TestCase(220.49, "약 220원")]
        [TestCase(220.5, "약 221원")]
        [TestCase(220.51, "약 221원")]
        [TestCase(12900d, "약 12,900원")]
        public void ApproximateMoneyRoundsToWonAwayFromMidpoint(double value, string expected)
            => Assert.That(ProductInsightCalculator.Approximate(value), Is.EqualTo(expected));

        [TestCase("amount-only")]
        [TestCase("unit-only")]
        [TestCase("invalid-unit")]
        [TestCase("count-no-unit")]
        [TestCase("count-no-evidence")]
        [TestCase("unit-no-count")]
        public void StructuredQuantityPairsAndCountEvidenceFailClosed(string problem)
        {
            var json = JObject.Parse(Json); var p = (JObject)json["products"][1];
            if (problem == "amount-only") p.Remove("contentUnit");
            if (problem == "unit-only") p.Remove("contentAmount");
            if (problem == "invalid-unit") p["contentUnit"] = "kg";
            if (problem == "count-no-unit") p.Remove("servingUnit");
            if (problem == "count-no-evidence") p["servingCountSource"] = " ";
            if (problem == "unit-no-count") p.Remove("servingCount");
            Assert.Throws<FormatException>(() => new ProductCatalog(json.ToString()));
        }

        [TestCase("g")]
        [TestCase("ml")]
        [TestCase("each")]
        public void MatchedPtResponsePassesContentPairButNeverInventsServingCount(string unit)
        {
            var response = new JObject
            {
                ["contractVersion"] = PtBarcodeClient.Contract, ["barcode"] = Barcode, ["status"] = "matched", ["checkedAt"] = "2026-10-28T00:00:00+09:00",
                ["matches"] = new JArray(new JObject
                {
                    ["catalogProductId"] = "1516c3eb-3dfb-4e11-8040-d23fadcdaad0", ["standardProductId"] = "18e83c5c-86a4-4e95-a131-411381789687",
                    ["name"] = "상품", ["brand"] = "브랜드", ["contentAmount"] = 40, ["contentUnit"] = unit, ["packageCount"] = 1,
                    ["specificationLabel"] = "60정이라는 문자열도 파싱하지 않음", ["categoryName"] = "식품", ["latestPublicPrice"] = null
                })
            };
            var p = PtBarcodeClient.Parse(response.ToString(), Barcode).Product;
            Assert.That(p.contentAmount, Is.EqualTo(40)); Assert.That(p.contentUnit, Is.EqualTo(unit));
            Assert.That(p.servingCount, Is.Null); Assert.That(p.servingUnit, Is.Null); Assert.That(p.servingCountSource, Is.Null);
            response["matches"][0]["contentUnit"] = null;
            Assert.Throws<FormatException>(() => PtBarcodeClient.Parse(response.ToString(), Barcode));
        }
    }
}
