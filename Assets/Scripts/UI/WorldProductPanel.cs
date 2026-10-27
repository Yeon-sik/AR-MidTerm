using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MidtermAR
{
    public enum ProductInfoTab { Nutrition, Price, None }

    public sealed class WorldProductPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text productTitle;
        [SerializeField] private TMP_Text specification;
        [SerializeField] private TMP_Text price;
        [SerializeField] private TMP_Text priceSource; // 관측 가격의 판매처·날짜·출처
        [SerializeField] private TMP_Text nutrition; // 기준량을 포함한 영양정보
        [SerializeField] private TMP_Text nutritionSource;
        [SerializeField] private TMP_Text provenance; // 개발 카드·바코드 미연결 표시
        [SerializeField] private GameObject nutritionPage;
        [SerializeField] private GameObject pricePage;
        [SerializeField] private Button nutritionTab;
        [SerializeField] private Button priceTab;
        [SerializeField] private RectTransform tabs;
        [SerializeField] private TMP_Text emptyInformation;
        [SerializeField] private TMP_Text priceDisclaimer;
        public ProductInfoTab CurrentTab { get; private set; }
        public bool HasNutrition { get; private set; }
        public bool HasPrice { get; private set; }
        private string boundProductId;

        public void Bind(ProductSnapshot product, ProductImageBinding binding = null)
        {
            var changedProduct = boundProductId != product?.catalogProductId || product == null;
            boundProductId = product?.catalogProductId;
            HasNutrition = ProductInfoFormatter.HasNutrition(product);
            HasPrice = ProductInfoFormatter.HasPrice(product);
            productTitle.text = product?.name ?? "";
            specification.text = ProductInfoFormatter.Specification(product);
            price.text = product == null ? "" : ProductInfoFormatter.WorldPrice(product);
            priceSource.text = product == null ? "" : ProductInfoFormatter.PriceSource(product);
            priceDisclaimer.text = HasPrice ? ProductInfoFormatter.PriceDisclaimer : "";
            nutrition.text = product == null ? "" : ProductInfoFormatter.Nutrition(product);
            nutritionSource.text = ProductInfoFormatter.NutritionSource(product);
            provenance.text = ProductInfoFormatter.Honesty(product, binding);
            emptyInformation.text = product == null ? "" : ProductInfoFormatter.EmptyInformation;
            foreach (var text in new[] { productTitle, specification, priceSource, nutrition, nutritionSource, provenance }) text.richText = false;
            nutritionTab.interactable = HasNutrition; priceTab.interactable = HasPrice;
            tabs.gameObject.SetActive(HasNutrition || HasPrice);
            emptyInformation.gameObject.SetActive(product != null && !HasNutrition && !HasPrice);
            var unavailableTab = CurrentTab == ProductInfoTab.Nutrition && !HasNutrition
                || CurrentTab == ProductInfoTab.Price && !HasPrice || CurrentTab == ProductInfoTab.None;
            var defaultTab = HasNutrition ? ProductInfoTab.Nutrition : HasPrice ? ProductInfoTab.Price : ProductInfoTab.None;
            ApplyTab(changedProduct || unavailableTab ? defaultTab : CurrentTab);
        }

        public void ShowNutrition() { if (HasNutrition) ApplyTab(ProductInfoTab.Nutrition); }
        public void ShowPrice() { if (HasPrice) ApplyTab(ProductInfoTab.Price); }

        private void ApplyTab(ProductInfoTab tab)
        {
            CurrentTab = tab;
            nutritionPage.SetActive(tab == ProductInfoTab.Nutrition);
            pricePage.SetActive(tab == ProductInfoTab.Price);
            var active = new Color32(37, 126, 92, 255);
            var inactive = new Color32(67, 83, 88, 255);
            nutritionTab.targetGraphic.color = tab == ProductInfoTab.Nutrition ? active : inactive;
            priceTab.targetGraphic.color = tab == ProductInfoTab.Price ? active : inactive;
            var rect = (RectTransform)transform;
            var bodyTop = tab == ProductInfoTab.None ? 184f : 280f;
            var nutritionHeight = Mathf.Max(150, nutrition.GetPreferredValues(nutrition.text, rect.rect.width - 48, Mathf.Infinity).y);
            var bodyHeight = tab == ProductInfoTab.Nutrition ? nutritionHeight + 46
                : tab == ProductInfoTab.Price ? 226 : string.IsNullOrEmpty(emptyInformation.text) ? 0 : 44;
            SetRect((RectTransform)nutritionPage.transform, bodyTop, bodyHeight);
            SetRect((RectTransform)pricePage.transform, bodyTop, bodyHeight);
            if (tab == ProductInfoTab.Nutrition)
            {
                SetRect(nutrition.rectTransform, 0, nutritionHeight, 24);
                SetRect(nutritionSource.rectTransform, nutritionHeight + 8, 38, 24);
            }
            SetRect(emptyInformation.rectTransform, bodyTop, 44, 24);
            var noticeHeight = string.IsNullOrEmpty(provenance.text) ? 0 : provenance.GetPreferredValues(provenance.text, rect.rect.width - 48, Mathf.Infinity).y;
            SetRect(provenance.rectTransform, bodyTop + bodyHeight + 12, noticeHeight, 24);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bodyTop + bodyHeight + noticeHeight + 36);
        }

        private static void SetRect(RectTransform rect, float top, float height, float inset = 0)
        { rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(inset, -top - height); rect.offsetMax = new Vector2(-inset, -top); }
    }
}
