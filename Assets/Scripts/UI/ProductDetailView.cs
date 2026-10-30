using TMPro;
using UnityEngine;

namespace MidtermAR
{
    // 3D 정보판은 12mm·15° 고정 위치에 그대로 둔다. 화면 밖으로 나간 가격·출처는 이 화면 고정 시트에서 읽는다.
    public sealed class ProductDetailView : MonoBehaviour
    {
        [SerializeField] private RectTransform sheet;
        [SerializeField] private TMP_Text productTitle;
        [SerializeField] private TMP_Text specification;
        [SerializeField] private TMP_Text body;
        public bool IsOpen => sheet.gameObject.activeSelf;
        public string ProductKey { get; private set; }

        public void Show(ProductSnapshot product, ProductImageBinding binding, string displayKey, string sourceBadge)
        {
            if (product == null) { Hide(); return; }
            ProductKey = displayKey ?? ProductIdentity.Key(product);
            // 외부 상품명·판매처를 rich text로 해석하지 않는다.
            foreach (var text in new[] { productTitle, specification, body }) text.richText = false;
            productTitle.text = product.name ?? "";
            specification.text = ProductInfoFormatter.Specification(product);
            body.text = ProductInfoFormatter.Detail(product, binding, sourceBadge);
            sheet.gameObject.SetActive(true);
        }

        public void Hide()
        {
            ProductKey = null;
            productTitle.text = specification.text = body.text = "";
            sheet.gameObject.SetActive(false);
        }
    }
}
