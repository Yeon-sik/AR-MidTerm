using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MidtermAR
{
    public enum ProductInfoTab { Nutrition, Price }

    public sealed class WorldProductPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text productTitle;
        [SerializeField] private TMP_Text specification;
        [SerializeField] private TMP_Text price;
        [SerializeField] private TMP_Text priceSource; // 관측 가격의 판매처·날짜·출처
        [SerializeField] private TMP_Text nutrition; // 기준량을 포함한 영양정보
        [SerializeField] private TMP_Text nutritionSource;
        [SerializeField] private TMP_Text provenance; // 시연용 연결·공간 고정 안내
        [SerializeField] private GameObject nutritionPage;
        [SerializeField] private GameObject pricePage;
        [SerializeField] private Button nutritionTab;
        [SerializeField] private Button priceTab;
        public ProductInfoTab CurrentTab { get; private set; }
        public event Action RescanRequested;

        public void Bind(ProductSnapshot product, ProductImageBinding binding = null)
        {
            productTitle.text = product?.name ?? "";
            specification.text = product == null ? "" : product.brand + " · " + product.specification;
            price.text = product == null ? "" : ProductInfoFormatter.Price(product);
            priceSource.text = product == null ? "" : ProductInfoFormatter.PriceSource(product)
                + (product.price?.unitPriceKrw.HasValue == true ? "\n출처: " + DisplaySource(product.price.displaySource) + "\n현재 판매 가격과 다를 수 있습니다." : "");
            nutrition.text = product == null ? "" : ProductInfoFormatter.Nutrition(product);
            nutritionSource.text = product == null ? "" : product.nutrition == null ? "확인된 영양정보 출처가 없습니다."
                : "출처: " + DisplaySource(product.nutrition.displaySource);
            provenance.text = product == null ? "" : binding?.developmentOnly == true
                ? "개발용 카드 마커 · 상품 포장 인식 아님\n시연용 상품 연결 · 로컬 데이터\n추적 이미지의 위치·방향에 표시합니다."
                : binding != null ? "시연용 상품 연결 · 로컬 데이터\n추적 이미지의 위치·방향에 표시합니다."
                : "시연용 상품 연결 · 로컬 데이터\n정보판은 배치한 공간에 고정됩니다.";
            ShowNutrition();
        }

        public void ShowNutrition() => ShowTab(ProductInfoTab.Nutrition);
        public void ShowPrice() => ShowTab(ProductInfoTab.Price);
        public void RequestRescan() => RescanRequested?.Invoke();
        private static string DisplaySource(string text) => string.IsNullOrWhiteSpace(text) ? "등록된 로컬 자료" : text;

        private void ShowTab(ProductInfoTab tab)
        {
            CurrentTab = tab;
            nutritionPage.SetActive(tab == ProductInfoTab.Nutrition);
            pricePage.SetActive(tab == ProductInfoTab.Price);
            var active = new Color32(37, 126, 92, 255);
            var inactive = new Color32(67, 83, 88, 255);
            nutritionTab.targetGraphic.color = tab == ProductInfoTab.Nutrition ? active : inactive;
            priceTab.targetGraphic.color = tab == ProductInfoTab.Price ? active : inactive;
        }
    }
}
