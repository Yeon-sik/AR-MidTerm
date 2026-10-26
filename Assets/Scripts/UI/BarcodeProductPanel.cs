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
        [SerializeField] private TMP_Text priceLabel;
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
        [SerializeField] private TMP_Text registrationStatus;
        [SerializeField] private TMP_Text provenance;
        private readonly BarcodeFollowState target = new BarcodeFollowState();
        private bool cameraReady;
        public bool HasVisibleTarget { get; private set; }

        private string boundProductId;
        public void SetProduct(ProductSnapshot product, string badge = null, string sourceBadge = null, string detail = null)
        {
            var key = product?.catalogProductId ?? product?.barcode;
            var changedProduct = product == null || boundProductId == null || boundProductId != key;
            boundProductId = key;
            if (changedProduct) { target.Select(product?.barcode); Hide(); }
            if (provenance != null) provenance.text = product == null ? "" : (sourceBadge ?? "시연용 연결 · 로컬 데이터").Replace(" · PT 스냅샷", "\nPT 스냅샷");
            if (registrationStatus != null) registrationStatus.text = (badge ?? "").Replace(" · 앞면을", "\n앞면을")
                + (product?.mapping?.catalogIdentityVerification == "LOCAL_DEMO_REMOTE_ID_UNVERIFIED" ? "\n로컬 시연 · PriceTrace ID 확인 대기" : "");
            if (registrationStatus != null && product != null && sourceBadge != null)
                registrationStatus.text += "\n" + (detail ?? (product.nutrition != null ? "영양: PT 승인 자료 · 포장 대조 전" : "확인된 영양자료 없음"));
            if (product == null)
            {
                foreach (var text in new[] { productTitle, specification, price, seller, observedAt, nutritionBasis, calories, sodium, carbs, sugars, fat, saturatedFat, protein }) text.text = "";
                return;
            }
            productTitle.text = product.name;
            foreach (var text in new[] { productTitle, specification, seller, observedAt, registrationStatus, provenance })
                if (text != null) text.richText = false;
            specification.text = string.IsNullOrEmpty(product.brand) ? product.specification ?? "" : product.brand
                + (string.IsNullOrEmpty(product.specification) ? "" : " · " + product.specification);
            price.text = ProductInfoFormatter.Price(product);
            if (priceLabel != null) priceLabel.text = product.price?.kind == "listed" ? "게시 가격" : "관측 가격";
            var hasPrice = product.price?.unitPriceKrw.HasValue == true;
            seller.text = hasPrice ? "판매처  " + product.price.seller : "확인된 가격 관측이 없습니다.";
            observedAt.text = hasPrice ? product.price.observedAt.Substring(0, 10)
                + (product.price.kind == "listed" ? " 게시 · 개당 가격" : " 관측 · 개당 가격")
                + (sourceBadge == null ? "" : " · " + (product.price.kind == null ? "PT 스냅샷" : product.price.displaySource))
                : "판매처·관측일 정보 없음";
            if (hasPrice) { observedAt.enableAutoSizing = true; observedAt.fontSizeMin = 18; observedAt.fontSizeMax = 22; }
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
