using System;
using System.Globalization;
using System.Linq;

namespace MidtermAR
{
    public sealed class ProductObservationSummary
    {
        public int Count { get; }
        public int SellerCount { get; }
        public double Minimum { get; }
        public double Maximum { get; }
        public DateTime FirstDate { get; }
        public DateTime LastDate { get; }
        internal ProductObservationSummary(PriceSnapshot[] observations)
        {
            Count = observations.Length;
            SellerCount = observations.Select(p => p.seller.Trim()).Distinct(StringComparer.Ordinal).Count();
            Minimum = observations.Min(p => p.unitPriceKrw.Value);
            Maximum = observations.Max(p => p.unitPriceKrw.Value);
            FirstDate = observations.Min(p => ProductInsightCalculator.ObservationDate(p.observedAt).Value);
            LastDate = observations.Max(p => ProductInsightCalculator.ObservationDate(p.observedAt).Value);
        }
        public string PriceRange => Minimum == Maximum ? ProductInsightCalculator.Won(Minimum)
            : ProductInsightCalculator.WonNumber(Minimum) + "~" + ProductInsightCalculator.Won(Maximum);
    }

    // 표시용 계산만 담당한다. 규격 문자열을 파싱하거나 관측값을 추정하지 않는다.
    public static class ProductInsightCalculator
    {
        private static readonly CultureInfo Korean = CultureInfo.GetCultureInfo("ko-KR");
        private static bool Positive(double? value) => value.HasValue && value > 0 && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);
        private static bool Observed(PriceSnapshot price) => price != null && price.kind != "listed"
            && (price.kind == null || price.kind == "observed") && Positive(price.unitPriceKrw);
        private static double? Ratio(PriceSnapshot price, double? amount)
        {
            if (!Observed(price) || !Positive(amount)) return null;
            var value = price.unitPriceKrw.Value / amount.Value;
            return Positive(value) ? value : null;
        }
        public static double? PricePerContentUnit(ProductSnapshot product) => product?.contentUnit == "g" || product?.contentUnit == "ml"
            ? Ratio(product.price, product.contentAmount) : null;
        public static double? PricePerServing(ProductSnapshot product) => product == null || string.IsNullOrWhiteSpace(product.servingUnit)
            || string.IsNullOrWhiteSpace(product.servingCountSource) ? null : Ratio(product.price, product.servingCount);
        public static ProductObservationSummary ObservationSummary(ProductSnapshot product)
        {
            if (product == null) return null;
            // 빈 명시 목록도 그대로 존중한다. 확인되지 않은 판매처·날짜는 집계하지 않는다.
            var source = product.priceObservations ?? (product.price == null ? Array.Empty<PriceSnapshot>() : new[] { product.price });
            var observations = source.Where(p => Observed(p) && !string.IsNullOrWhiteSpace(p.seller) && ObservationDate(p.observedAt).HasValue).ToArray();
            return observations.Length == 0 ? null : new ProductObservationSummary(observations);
        }
        internal static DateTime? ObservationDate(string value) => value != null && value.Length >= 10
            && DateTime.TryParseExact(value.Substring(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : (DateTime?)null;
        public static string WonNumber(double value) => Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("N0", Korean);
        public static string Won(double value) => WonNumber(value) + "원";
        public static string Approximate(double? value) => Positive(value) ? "약 " + Won(value.Value) : null;
        public static string AnnotationText(ProductSnapshot product, string kind)
        {
            if (kind == "servingPrice")
            {
                var serving = Approximate(PricePerServing(product));
                if (serving == null) return null;
                var content = Approximate(PricePerContentUnit(product));
                return "1" + product.servingUnit + "당 " + serving + "\n"
                    + (content == null ? "" : "1" + product.contentUnit + "당 " + content + " · ") + "관측가 기준";
            }
            if (kind == "observationSummary")
            {
                var summary = ObservationSummary(product);
                return summary == null ? null : $"관측 판매처 {summary.SellerCount}곳 · {summary.PriceRange}\nPriceTrace 스냅샷";
            }
            return null;
        }
    }
}
