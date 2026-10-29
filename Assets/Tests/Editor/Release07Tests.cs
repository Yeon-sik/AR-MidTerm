using System.IO;
using System.Reflection;
using MidtermAR.Editor;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MidtermAR.Tests
{
    public sealed class Release07Tests
    {
        [Test]
        public void SubmissionSceneDisablesDeferredAnnotationsAndPreservesFixedRightGeometry()
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var presenter = Object.FindFirstObjectByType<TrackedImageProductPanel>();
            Assert.That(Field<bool>(presenter, "showAnnotations"), Is.False);
            Assert.That(ProductPanelLayout.GapMetres, Is.EqualTo(.012f));
            Assert.That(ProductPanelLayout.TiltDegrees, Is.EqualTo(15f));
            Assert.That(typeof(ProductPanelLayout).GetMethod("TryChoose"), Is.Null,
                "Submission must not reposition a board according to the camera viewport.");
            Assert.That(Object.FindObjectsByType<WorldProductAnnotation>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
        }

        [Test]
        public void OffNutritionCardReplacesPtPriceAndKeepsItsOwnSourceBasisAndZero()
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var panel = Object.FindFirstObjectByType<BarcodeProductPanel>();
            var catalog = new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json"));
            catalog.TryFind("8801104671507", out var pt);
            panel.SetProduct(pt, sourceBadge: "PT 등록 상품");
            var off = OffProductClient.Parse(@"{""code"":""8809778493005"",""status"":""success"",""result"":{""id"":""product_found""},""errors"":[],""product"":{""product_name"":""OFF 시험 식품"",""product_quantity_unit"":""g"",""nutriments"":{""energy-kcal_100g"":250,""proteins_100g"":5,""sugars_100g"":0,""sodium_100g"":0.4}}}", "8809778493005");
            panel.SetProduct(off, sourceBadge: "Open Food Facts 정보 · PT 바코드 연결 필요");
            Assert.That(Field<RectTransform>(panel, "priceSection").gameObject.activeSelf, Is.False);
            Assert.That(Field<RectTransform>(panel, "nutritionSection").gameObject.activeSelf, Is.True);
            Assert.That(Field<TMP_Text>(panel, "emptyInformation").gameObject.activeSelf, Is.True);
            Assert.That(Field<TMP_Text>(panel, "emptyInformation").text, Is.EqualTo("가격 정보 없음"));
            Assert.That(Field<TMP_Text>(panel, "nutritionSource").text, Is.EqualTo("Open Food Facts"));
            Assert.That(Field<TMP_Text>(panel, "nutritionBasis").text, Is.EqualTo("100g 기준"));
            Assert.That(Field<TMP_Text>(panel, "sugars").text, Is.EqualTo("0 g"));
            Assert.That(Field<TMP_Text>(panel, "sodium").text, Is.EqualTo("400 mg"));
            Assert.That(Field<TMP_Text>(panel, "provenance").text, Does.Contain("PT 바코드 연결 필요"));
            Assert.That(Field<TMP_Text>(panel, "price").text, Is.EqualTo("가격 정보 없음"));
        }

        [Test]
        public void ListedPriceCannotLeakIntoBarcodeCardThroughLocalData()
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var panel = Object.FindFirstObjectByType<BarcodeProductPanel>();
            panel.SetProduct(new ProductSnapshot { name = "게시가 제외 시험", barcode = "8809778493005",
                price = new PriceSnapshot { kind = "listed", unitPriceKrw = 1000 } });
            Assert.That(Field<RectTransform>(panel, "priceSection").gameObject.activeSelf, Is.False);
            Assert.That(Field<TMP_Text>(panel, "price").text, Is.EqualTo("가격 정보 없음"));
        }

        private static T Field<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
