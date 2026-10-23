using System.Globalization;

namespace MidtermAR
{
    public static class ProductInfoFormatter // 출처·기준량·누락값을 보존하는 표시 문자열 생성
    {
        private static readonly CultureInfo Korean = CultureInfo.GetCultureInfo("ko-KR"); // 원화 가격의 천 단위 구분 형식
        public static string Number(double? value, string unit) => value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) + unit : "정보 없음";
        public static string Price(ProductSnapshot product) => product.price?.unitPriceKrw is double value ? value.ToString("N0", Korean) + "원" : "가격 정보 없음";
        public static string PriceSource(ProductSnapshot product) => product.price?.unitPriceKrw.HasValue == true
            ? $"{product.price.seller}\n{product.price.observedAt} 관측 · 로컬 스냅샷"
            : "확인된 가격 관측이 없습니다.";
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
