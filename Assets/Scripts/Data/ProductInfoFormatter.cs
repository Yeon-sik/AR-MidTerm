using System.Globalization;

namespace MidtermAR
{
    public static class ProductInfoFormatter // 출처·기준량·누락값을 보존하는 표시 문자열 생성
    {
        public const string EmptyInformation = "확인된 영양·가격 정보 없음";
        public const string CardNotice = "개발용 카드 · 상품 포장 인식 아님";
        public const string PriceDisclaimer = "현재 판매가와 다를 수 있음";
        public static bool HasPrice(ProductSnapshot p) => p?.price?.unitPriceKrw.HasValue == true;
        public static bool HasNutrition(ProductSnapshot p) => p?.nutrition != null
            && (p.nutrition.caloriesKcal.HasValue || p.nutrition.carbsGrams.HasValue || p.nutrition.proteinGrams.HasValue
                || p.nutrition.fatGrams.HasValue || p.nutrition.sugarsGrams.HasValue || p.nutrition.saturatedFatGrams.HasValue
                || p.nutrition.sodiumMg.HasValue || p.nutrition.fiberGrams.HasValue);
        public static string Specification(ProductSnapshot p) => p == null ? "" : string.IsNullOrEmpty(p.brand) ? p.specification ?? ""
            : p.brand + (string.IsNullOrEmpty(p.specification) ? "" : " · " + p.specification);
        public static string Honesty(ProductSnapshot p, ProductImageBinding binding) => p == null ? ""
            : (binding?.developmentOnly == true ? CardNotice : "")
                + (binding != null && string.IsNullOrEmpty(p.barcode) ? (binding.developmentOnly ? "\n" : "") + "바코드 미연결" : "");
        public static string CompactSource(string badge) => (badge ?? "").Replace("Open Food Facts 정보", "Open Food Facts")
            .Replace("PT 등록 여부 확인 불가", "PT 등록 확인 불가");
        public static string FrontHint(string badge) => string.IsNullOrEmpty(badge) ? "" : badge.StartsWith("앞면 이미지 등록됨")
            ? "앞면 이미지 등록됨" : "앞면 이미지 없음";
        private static readonly CultureInfo Korean = CultureInfo.GetCultureInfo("ko-KR"); // 원화 가격의 천 단위 구분 형식
        public static string Number(double? value, string unit) => value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) + unit : "정보 없음";
        public static string Price(ProductSnapshot product) => product.price?.unitPriceKrw is double value ? value.ToString("N0", Korean) + "원" : "가격 정보 없음";
        public static string PriceSource(ProductSnapshot product) => HasPrice(product)
            ? $"PriceTrace {(product.price.kind == "listed" ? "게시가" : "관측가")} · {PriceDate(product.price.observedAt)} · {product.price.seller}"
            : "확인된 가격 관측이 없습니다.";
        private static string PriceDate(string value) => string.IsNullOrEmpty(value) ? "관측일 없음" : value.Length > 10 ? value.Substring(0, 10) : value;
        public static string NutritionSource(ProductSnapshot product) => HasNutrition(product) ? "PriceTrace 승인 영양자료" : "";
        public static string Nutrition(ProductSnapshot product)
        {
            var n = product.nutrition;
            if (n == null) return "영양정보 없음";
            return $"{Number(n.basisAmount, n.basisUnit)} 기준 · {Number(n.caloriesKcal, "kcal")}\n"
                + $"탄수화물 {Number(n.carbsGrams, "g")} · 당류 {Number(n.sugarsGrams, "g")}\n"
                + $"단백질 {Number(n.proteinGrams, "g")}\n"
                + $"지방 {Number(n.fatGrams, "g")} · 포화지방 {Number(n.saturatedFatGrams, "g")}\n"
                + $"나트륨 {Number(n.sodiumMg, "mg")}";
        }
    }
}
