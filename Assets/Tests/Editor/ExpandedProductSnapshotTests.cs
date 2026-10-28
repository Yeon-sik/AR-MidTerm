using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MidtermAR.Tests
{
    public sealed class ExpandedProductSnapshotTests
    {
        [TestCase("8801045014869", "268e9335-1b73-457c-8fac-ef0228aa6489", 120, 510, 75, 12, 18, 4, 9, 1760)]
        [TestCase("8801045520728", "10ae810a-4155-4af7-9021-430ea5847277", 108, 460, 69, 10, 16, 4, 8, 1710)]
        [TestCase("8809778493005", "f31002f8-c226-44b2-8914-e6e7bfb059fb", 121, 405, 85, 9, 3.1, 15, .6, 1200)]
        public void NewPackageNutritionHasExactIdentityBasisAndNoInventedPrice(string barcode, string id,
            double grams, double calories, double carbs, double protein, double fat, double sugars,
            double saturatedFat, double sodium)
        {
            var catalog = new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json"));
            Assert.That(catalog.Count, Is.EqualTo(5));
            Assert.That(catalog.SnapshotDate, Is.EqualTo("2026-10-28"));
            Assert.That(catalog.TryFind(barcode, out var product), Is.True);
            Assert.That(product.catalogProductId, Is.EqualTo(id));
            Assert.That(product.price, Is.Null);
            var n = product.nutrition;
            Assert.That(n.sourceCatalogProductId, Is.EqualTo(id));
            Assert.That(n.provider, Is.EqualTo("Fitness Nutrition"));
            Assert.That(n.basisAmount, Is.EqualTo(grams));
            Assert.That(n.basisUnit, Is.EqualTo("g"));
            Assert.That(n.caloriesKcal, Is.EqualTo(calories));
            Assert.That(n.carbsGrams, Is.EqualTo(carbs));
            Assert.That(n.proteinGrams, Is.EqualTo(protein));
            Assert.That(n.fatGrams, Is.EqualTo(fat));
            Assert.That(n.sugarsGrams, Is.EqualTo(sugars));
            Assert.That(n.saturatedFatGrams, Is.EqualTo(saturatedFat));
            Assert.That(n.sodiumMg, Is.EqualTo(sodium));
            Assert.That(n.fiberGrams, Is.Null);

            EditorSceneManager.OpenScene(MidtermAR.Editor.ProjectBuilder.ScenePath);
            var panel = Object.FindFirstObjectByType<BarcodeProductPanel>();
            panel.SetProduct(product, sourceBadge: "PT 등록 상품");
            Assert.That(Field<RectTransform>(panel, "priceSection").gameObject.activeSelf, Is.False);
            Assert.That(Field<RectTransform>(panel, "nutritionSection").gameObject.activeSelf, Is.True);
            Assert.That(Field<TMP_Text>(panel, "price").text, Is.EqualTo("가격 정보 없음"));
            Assert.That(Field<TMP_Text>(panel, "nutritionSource").text, Is.EqualTo("Fitness Nutrition · PT 승인"));
            Assert.That(Field<TMP_Text>(panel, "nutritionBasis").text, Does.Contain(grams.ToString("0")).And.Contain("g"));
            Assert.That(Field<TMP_Text>(panel, "provenance").text, Does.Contain("PT 등록 상품"));
        }

        private static T Field<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
