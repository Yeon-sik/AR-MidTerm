using TMPro;
using UnityEngine;

namespace MidtermAR
{
    public sealed class WorldProductPanel : MonoBehaviour // 현실 공간에 놓인 정보판의 텍스트 연결
    {
        [SerializeField] private TMP_Text productTitle;
        [SerializeField] private TMP_Text specification;
        [SerializeField] private TMP_Text price;
        [SerializeField] private TMP_Text priceSource; // 관측 가격의 판매처·날짜·출처
        [SerializeField] private TMP_Text nutrition; // 기준량을 포함한 영양정보
        [SerializeField] private TMP_Text provenance; // 시연용 연결·공간 고정 안내

        public void Bind(ProductSnapshot product)
        {
            productTitle.text = product.name;
            specification.text = product.brand + " · " + product.specification;
            price.text = ProductInfoFormatter.Price(product);
            priceSource.text = ProductInfoFormatter.PriceSource(product);
            nutrition.text = ProductInfoFormatter.Nutrition(product);
            provenance.text = "시연용 상품 연결 · 로컬 데이터\n정보판은 배치한 공간에 고정됩니다.";
        }
    }
}
