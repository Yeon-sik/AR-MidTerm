using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MidtermAR.Tests
{
    public sealed class OffNutritionTests
    {
        private const string Barcode = "8801104671507";

        private static JObject Product(JObject values = null) => new JObject
        {
            ["product_name"] = "OFF food", ["quantity"] = "40 g", ["product_quantity_unit"] = "g",
            ["nutriments"] = values
        };

        private static ProductSnapshot Parse(JObject product)
        {
            var response = new JObject
            {
                ["code"] = Barcode, ["status"] = "success", ["result"] = new JObject { ["id"] = "product_found" },
                ["errors"] = new JArray(), ["product"] = product
            };
            return OffProductClient.Parse(response.ToString(), Barcode);
        }

        [Test]
        public void StandardHundredGramValuesPreserveZeroAndConvertSodiumWithoutInventingPriceOrPtIdentity()
        {
            var product = Product(new JObject
            {
                ["energy-kcal_100g"] = 185, ["carbohydrates_100g"] = 15, ["proteins_100g"] = 15,
                ["fat_100g"] = 7, ["sugars_100g"] = 0, ["saturated-fat_100g"] = 5,
                ["sodium_100g"] = .2, ["sodium_unit"] = "mg", ["fiber_100g"] = null
            });
            var parsed = Parse(product); var n = parsed.nutrition;
            Assert.That(parsed.catalogProductId, Is.Null); Assert.That(parsed.price, Is.Null);
            Assert.That(n.basisAmount, Is.EqualTo(100)); Assert.That(n.basisUnit, Is.EqualTo("g"));
            Assert.That(n.caloriesKcal, Is.EqualTo(185)); Assert.That(n.carbsGrams, Is.EqualTo(15));
            Assert.That(n.proteinGrams, Is.EqualTo(15)); Assert.That(n.fatGrams, Is.EqualTo(7));
            Assert.That(n.sugarsGrams, Is.Zero); Assert.That(n.saturatedFatGrams, Is.EqualTo(5));
            Assert.That(n.sodiumMg, Is.EqualTo(200)); Assert.That(n.fiberGrams, Is.Null);
            Assert.That(n.prepState, Is.EqualTo("as_sold"));
            Assert.That(ProductInfoFormatter.Nutrition(parsed), Does.Contain("100g 기준").And.Contain("당류 0g").And.Contain("나트륨 200mg"));
        }

        [TestCase("energy-kj_100g")]
        [TestCase("energy_100g")]
        public void StandardKilojoulesConvertToKcalRegardlessOfContributorUnit(string field)
        {
            var product = Product(new JObject { [field] = 418.4, ["energy_unit"] = "kcal" });
            Assert.That(Parse(product).nutrition.caloriesKcal, Is.EqualTo(100).Within(.000001));
        }

        [Test]
        public void ExplicitKcalIncludingZeroTakesPriorityOverKilojoules()
        {
            var product = Product(new JObject { ["energy-kcal_100g"] = 0, ["energy-kj_100g"] = 418.4 });
            Assert.That(Parse(product).nutrition.caloriesKcal, Is.Zero);
        }

        [TestCase("g", null, "g")]
        [TestCase("ml", "ml", "ml")]
        [TestCase(null, "ml", "ml")]
        [TestCase("ML", null, "ml")]
        [TestCase(null, null, "g/100ml")]
        [TestCase("g", "ml", "g/100ml")]
        [TestCase("each", null, "g/100ml")]
        public void BasisUsesNormalizedUnitsAndDisclosesMissingOrConflictingUnits(string pack, string serving, string expected)
        {
            var product = Product(new JObject { ["proteins_100g"] = 1 });
            product["product_quantity_unit"] = pack; product["serving_quantity_unit"] = serving;
            product["nutrition_data_per"] = "100g"; // OFF는 이 값만으로 액체 여부를 구분하지 않는다.
            var parsed = Parse(product);
            Assert.That(parsed.nutrition.basisUnit, Is.EqualTo(expected));
            Assert.That(ProductInfoFormatter.Nutrition(parsed), Does.StartWith("100" + expected + " 기준"));
        }

        [Test]
        public void ServingAndPreparedValuesAreNeverMisrepresentedAsAsSoldHundredGramValues()
        {
            var product = Product(new JObject
            {
                ["proteins"] = 10, ["proteins_value"] = 10, ["proteins_serving"] = 10,
                ["proteins_prepared_100g"] = 2, ["salt_100g"] = .5
            });
            product["nutrition_data_per"] = "serving";
            Assert.That(Parse(product).nutrition, Is.Null);
            product["nutriments"]["proteins_100g"] = 20;
            Assert.That(Parse(product).nutrition.proteinGrams, Is.EqualTo(20));
            Assert.That(Parse(product).nutrition.sodiumMg, Is.Null); // 소금 수치로 나트륨을 추정하지 않는다.
        }

        [TestCase(null)]
        [TestCase("null")]
        [TestCase("{}")]
        [TestCase("[]")]
        [TestCase("{\"fat_100g\":null}")]
        [TestCase("{\"fat_100g\":-1}")]
        public void MissingOrEntirelyInvalidNutrientsKeepProductButNoNutrition(string raw)
        {
            var product = Product();
            if (raw == null) product.Remove("nutriments"); else product["nutriments"] = JToken.Parse(raw);
            var parsed = Parse(product);
            Assert.That(parsed.name, Is.EqualTo("OFF food")); Assert.That(parsed.nutrition, Is.Null);
        }

        [Test]
        public void ExplicitNoNutritionFlagOverridesResidualNutriments()
        {
            var product = Product(new JObject { ["fat_100g"] = 12 });
            product["no_nutrition_data"] = "on";
            Assert.That(Parse(product).nutrition, Is.Null);
        }

        [TestCase("-1")]
        [TestCase("\"NaN\"")]
        [TestCase("\"Infinity\"")]
        [TestCase("\"-Infinity\"")]
        [TestCase("\"1e999\"")]
        [TestCase("\"12,5\"")]
        [TestCase("true")]
        [TestCase("{}")]
        public void InvalidFieldIsDroppedWithoutDiscardingOtherNutrients(string raw)
        {
            var values = new JObject { ["fat_100g"] = JToken.Parse(raw), ["proteins_100g"] = 0 };
            var n = Parse(Product(values)).nutrition;
            Assert.That(n.fatGrams, Is.Null); Assert.That(n.proteinGrams, Is.Zero);
        }

        [Test]
        public void NumericStringsAndNumbersAreCultureIndependentAndOverflowIsDiscarded()
        {
            var oldCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var n = Parse(Product(new JObject
                {
                    ["fat_100g"] = "1.25", ["proteins_100g"] = 2.5, ["sodium_100g"] = double.MaxValue
                })).nutrition;
                Assert.That(n.fatGrams, Is.EqualTo(1.25)); Assert.That(n.proteinGrams, Is.EqualTo(2.5));
                Assert.That(n.sodiumMg, Is.Null);
            }
            finally { CultureInfo.CurrentCulture = oldCulture; }
        }

        [Test]
        public void OffSourceAndRecordUpdateDateNeverClaimPtApprovalOrNutritionVerification()
        {
            var product = Product(new JObject { ["fat_100g"] = 1 });
            product["last_modified_t"] = new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            var parsed = Parse(product); var n = parsed.nutrition;
            Assert.That(n.provider, Is.EqualTo("Open Food Facts"));
            Assert.That(n.source, Is.EqualTo("https://world.openfoodfacts.org/product/" + Barcode));
            Assert.That(n.sourceUpdatedAt, Does.StartWith("2026-10-28T00:00:00"));
            Assert.That(n.sourceCatalogProductId, Is.Null); Assert.That(n.nutritionFoodId, Is.Null);
            Assert.That(ProductInfoFormatter.NutritionSource(parsed), Is.EqualTo("Open Food Facts · 자료 갱신 2026-10-28"));
        }

        [TestCase(null)]
        [TestCase("\"yesterday\"")]
        [TestCase("-1")]
        [TestCase("1.5")]
        [TestCase("253402300800")]
        public void InvalidOrMissingUpdateTimeDoesNotInventADate(string raw)
        {
            var product = Product(new JObject { ["fat_100g"] = 1 });
            if (raw != null) product["last_modified_t"] = JToken.Parse(raw);
            var parsed = Parse(product);
            Assert.That(parsed.nutrition.sourceUpdatedAt, Is.Null);
            Assert.That(ProductInfoFormatter.NutritionSource(parsed), Is.EqualTo("Open Food Facts"));
        }

        [Test]
        public void PublicAndLegacyApprovedNutritionUseFitnessNutritionSource()
        {
            var product = new ProductSnapshot { nutrition = new NutritionSnapshot { caloriesKcal = 0, provider = "Fitness Nutrition" } };
            Assert.That(ProductInfoFormatter.NutritionSource(product), Is.EqualTo("Fitness Nutrition · PT 승인"));
            product.nutrition.provider = null; product.nutrition.sourceCatalogProductId = "approved-local-id";
            Assert.That(ProductInfoFormatter.NutritionSource(product), Is.EqualTo("Fitness Nutrition · PT 승인"));
        }

        [Test]
        public void UnknownNutritionSourceIsNotAssignedPtApproval()
        {
            var product = new ProductSnapshot { nutrition = new NutritionSnapshot { caloriesKcal = 0 } };
            Assert.That(ProductInfoFormatter.NutritionSource(product), Is.EqualTo("영양자료 · 출처 미확인"));
        }

        [Test]
        public void ListedPriceIsHiddenAtFormattingBoundaryAndObservedZeroRemainsValid()
        {
            var product = new ProductSnapshot { price = new PriceSnapshot { unitPriceKrw = 0, kind = "listed" } };
            Assert.That(ProductInfoFormatter.HasPrice(product), Is.False);
            Assert.That(ProductInfoFormatter.Price(product), Is.EqualTo("가격 정보 없음"));
            Assert.That(ProductInfoFormatter.WorldPrice(product), Is.EqualTo("가격 정보 없음"));
            Assert.That(ProductInfoFormatter.PriceSource(product), Does.Not.Contain("게시가"));
            product.price.kind = "observed";
            Assert.That(ProductInfoFormatter.HasPrice(product), Is.True);
            Assert.That(ProductInfoFormatter.Price(product), Is.EqualTo("0원"));
        }
    }
}
