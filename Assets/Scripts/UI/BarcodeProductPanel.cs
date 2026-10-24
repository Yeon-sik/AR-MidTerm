using TMPro;
using UnityEngine;

namespace MidtermAR
{
    public sealed class BarcodeProductPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private RectTransform header;
        [SerializeField] private RectTransform footer;
        [SerializeField] private RectTransform card;
        [SerializeField] private RectTransform marker;
        [SerializeField] private TMP_Text productTitle;
        [SerializeField] private TMP_Text specification;
        [SerializeField] private TMP_Text price;
        [SerializeField] private TMP_Text seller;
        [SerializeField] private TMP_Text observedAt;
        [SerializeField] private TMP_Text nutritionBasis;
        [SerializeField] private TMP_Text calories;
        [SerializeField] private TMP_Text sodium;
        [SerializeField] private TMP_Text carbs;
        [SerializeField] private TMP_Text sugars;
        [SerializeField] private TMP_Text fat;
        [SerializeField] private TMP_Text saturatedFat;
        [SerializeField] private TMP_Text protein;
        private readonly BarcodeFollowState target = new BarcodeFollowState();
        private bool cameraReady;
        public bool HasVisibleTarget { get; private set; }

        public void SetProduct(ProductSnapshot product)
        {
            target.Select(product?.barcode);
            Hide();
            if (product == null) return;
            productTitle.text = product.name;
            specification.text = product.brand + " · " + product.specification;
            price.text = ProductInfoFormatter.Price(product);
            var hasPrice = product.price?.unitPriceKrw.HasValue == true;
            seller.text = hasPrice ? "판매처  " + product.price.seller : "확인된 가격 관측이 없습니다.";
            observedAt.text = hasPrice ? product.price.observedAt + " 관측 · 개당 가격" : "판매처·관측일 정보 없음";
            var n = product.nutrition;
            nutritionBasis.text = n == null ? "기준량 정보 없음" : ProductInfoFormatter.Number(n.basisAmount, n.basisUnit) + " 기준";
            calories.text = ProductInfoFormatter.Number(n?.caloriesKcal, " kcal");
            sodium.text = ProductInfoFormatter.Number(n?.sodiumMg, " mg");
            carbs.text = ProductInfoFormatter.Number(n?.carbsGrams, " g");
            sugars.text = ProductInfoFormatter.Number(n?.sugarsGrams, " g");
            fat.text = ProductInfoFormatter.Number(n?.fatGrams, " g");
            saturatedFat.text = ProductInfoFormatter.Number(n?.saturatedFatGrams, " g");
            protein.text = ProductInfoFormatter.Number(n?.proteinGrams, " g");
        }

        public void Observe(BarcodeReadResult result, BarcodeViewportTransform transform, float capturedAt)
        {
            target.Observe(result, transform, capturedAt, Time.unscaledTime);
            RefreshVisuals();
        }

        public void SetCameraReady(bool value)
        {
            cameraReady = value;
            if (!value) ClearPosition();
        }

        public void ClearPosition() { target.Invalidate(); Hide(); }
        private void OnDisable() => ClearPosition();
        private void LateUpdate() => RefreshVisuals();

        private void RefreshVisuals()
        {
            if (!cameraReady || !target.IsVisible(Time.unscaledTime)) { Hide(); return; }
            LayoutAtViewport(target.ViewportPosition);
        }

        private void Hide()
        {
            HasVisibleTarget = false;
            card.gameObject.SetActive(false);
            marker.gameObject.SetActive(false);
        }

        // 카메라 전체의 viewport 좌표를 안전 영역 안의 UI 좌표로 바꾼다.
        public bool LayoutAtViewport(Vector2 viewport)
        {
            var canvasRect = (RectTransform)GetComponent<Canvas>().transform;
            var cameraPoint = new Vector3(Mathf.Lerp(canvasRect.rect.xMin, canvasRect.rect.xMax, viewport.x),
                Mathf.Lerp(canvasRect.rect.yMin, canvasRect.rect.yMax, viewport.y));
            Vector2 point = safeArea.InverseTransformPoint(canvasRect.TransformPoint(cameraPoint));
            var bottom = footer.localPosition.y + footer.rect.yMax + 16;
            var top = header.localPosition.y + header.rect.yMin - 16;
            var available = Rect.MinMaxRect(safeArea.rect.xMin + 16, bottom, safeArea.rect.xMax - 16, top);
            if (available.height < card.rect.height || available.width < 400 || !available.Contains(point)) { Hide(); return false; }
            card.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(720, available.width));
            var bounds = CalculateCardRect(available, point, card.rect.size, 72);
            if (bounds.width <= 0) { Hide(); return false; }
            card.anchoredPosition = bounds.position;
            marker.anchoredPosition = point;
            card.gameObject.SetActive(true);
            marker.gameObject.SetActive(true);
            HasVisibleTarget = true;
            return true;
        }

        public static Rect CalculateCardRect(Rect available, Vector2 barcode, Vector2 size, float gap)
        {
            var x = Mathf.Clamp(barcode.x - size.x * 0.5f, available.xMin, available.xMax - size.x);
            var above = barcode.y + gap;
            var below = barcode.y - gap - size.y;
            if (size.x > available.width || size.y > available.height) return default;
            if (above + size.y > available.yMax && below < available.yMin) return default;
            var y = above + size.y <= available.yMax ? above : below;
            return new Rect(x, y, size.x, size.y);
        }
    }
}
