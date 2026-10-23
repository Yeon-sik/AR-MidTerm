using TMPro;
using UnityEngine;

namespace MidtermAR
{
    public sealed class WorldProductPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text productTitle;
        [SerializeField] private TMP_Text specification;
        [SerializeField] private TMP_Text price;
        [SerializeField] private TMP_Text priceSource;
        [SerializeField] private TMP_Text nutrition;
        [SerializeField] private TMP_Text provenance;

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
