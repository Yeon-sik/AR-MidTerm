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
        [SerializeField] private TMP_Text provenance; // 조회 출처·연결 상태·등록 방식 표시
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
        private string boundDisplayKey;
        private bool showRuntimeMissingInformation;

        public void Bind(ProductSnapshot product, ProductImageBinding binding = null, string displayKey = null, string sourceBadge = null)
        {
            var key = product == null ? null : displayKey ?? ProductIdentity.Key(product);
            var changedProduct = boundDisplayKey != key || product == null;
            boundDisplayKey = key;
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
            if (product != null && !string.IsNullOrWhiteSpace(sourceBadge))
                provenance.text = sourceBadge + (string.IsNullOrEmpty(provenance.text) ? "" : "\n" + provenance.text);
            emptyInformation.text = product == null ? "" : ProductInfoFormatter.EmptyInformation;
            // 현장 등록은 이름만 확인된 상품도 표시한다. 없는 자료를 빈 탭으로 열지 않고 명시한다.
            showRuntimeMissingInformation = product != null && binding?.runtimeRegistered == true && (!HasPrice || !HasNutrition);
            if (showRuntimeMissingInformation)
                emptyInformation.text = !HasPrice && !HasNutrition ? "가격 정보 없음\n영양정보 없음"
                    : !HasPrice ? "가격 정보 없음" : "영양정보 없음";
            foreach (var text in new[] { productTitle, specification, priceSource, nutrition, nutritionSource, provenance }) text.richText = false;
            nutritionTab.interactable = HasNutrition; priceTab.interactable = HasPrice;
            tabs.gameObject.SetActive(HasNutrition || HasPrice);
            emptyInformation.gameObject.SetActive(showRuntimeMissingInformation || product != null && !HasNutrition && !HasPrice);
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
            var missingHeight = showRuntimeMissingInformation
                ? Mathf.Max(44, emptyInformation.GetPreferredValues(emptyInformation.text, rect.rect.width - 48, Mathf.Infinity).y) : 44;
            var missingTop = bodyTop;
            if (showRuntimeMissingInformation)
            {
                if (tab == ProductInfoTab.None) bodyHeight = missingHeight;
                else { missingTop += bodyHeight + 8; bodyHeight += missingHeight + 8; }
            }
            SetRect(emptyInformation.rectTransform, missingTop, missingHeight, 24);
            var noticeHeight = string.IsNullOrEmpty(provenance.text) ? 0 : provenance.GetPreferredValues(provenance.text, rect.rect.width - 48, Mathf.Infinity).y;
            SetRect(provenance.rectTransform, bodyTop + bodyHeight + 12, noticeHeight, 24);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bodyTop + bodyHeight + noticeHeight + 36);
        }

        private static void SetRect(RectTransform rect, float top, float height, float inset = 0)
        { rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(inset, -top - height); rect.offsetMax = new Vector2(-inset, -top); }
    }
}
