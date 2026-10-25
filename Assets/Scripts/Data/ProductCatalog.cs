using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace MidtermAR
{
    [Preserve, Serializable]
    public sealed class CatalogDocument
    {
        public int schemaVersion;
        public ProductSnapshot[] products;
    }

    [Preserve, Serializable]
    public sealed class ProductSnapshot
    {
        public string catalogProductId; // 가격·영양정보 연결의 기준 상품 ID
        public string name;
        public string brand;
        public string specification;
        public string barcode; // 숫자로 변환하지 않는 정확한 식별 문자열
        public string barcodeFormat;
        public MappingEvidence mapping; // 시연용 연결의 범위와 근거
        public PriceSnapshot price;
        public NutritionSnapshot nutrition;
    }

    [Preserve, Serializable]
    public sealed class MappingEvidence
    {
        public string scope;
        public string source;
        public string physicalDecodeVerification;
    }

    [Preserve, Serializable]
    public sealed class PriceSnapshot
    {
        public double? unitPriceKrw; // 관측한 개당 원화 가격, null은 정보 없음
        public string seller;
        public string observedAt;
        public string source;
        public string displaySource;
        public string confidence;
        public string observationId;
    }

    [Preserve, Serializable]
    public sealed class NutritionSnapshot
    {
        public double? basisAmount; // 영양 수치가 적용되는 기준량
        public string basisUnit;
        public string prepState;
        public double? caloriesKcal;
        public double? carbsGrams;
        public double? proteinGrams;
        public double? fatGrams;
        public double? sugarsGrams;
        public double? saturatedFatGrams;
        public double? sodiumMg;
        public double? fiberGrams;
        public string source;
        public string displaySource;
        public string nutritionFoodId;
        public string sourceCatalogProductId; // 영양정보의 연결 상품 일치 검증에 사용
    }

    public sealed class ProductCatalog
    {
        private readonly Dictionary<string, ProductSnapshot> products = new Dictionary<string, ProductSnapshot>(StringComparer.Ordinal); // 정확한 바코드 문자열만 키로 사용
        public int Count => products.Count;

        public ProductCatalog(string json)
        {
            var document = JsonConvert.DeserializeObject<CatalogDocument>(json,
                new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error, DateParseHandling = DateParseHandling.None });
            if (document == null || document.schemaVersion != 1 || document.products == null || document.products.Length == 0)
                throw new FormatException("시연 상품 데이터 형식이 올바르지 않습니다.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var product in document.products)
            {
                Validate(product);
                if (products.ContainsKey(product.barcode) || !ids.Add(product.catalogProductId))
                    throw new FormatException("시연 상품 데이터에 중복 식별자가 있습니다.");
                products.Add(product.barcode, product);
            }
        }

        public bool TryFind(string barcode, out ProductSnapshot product)
        {
            product = null;
            return BarcodeIdentity.TryNormalizeEan13(barcode, out var normalized) && products.TryGetValue(normalized, out product);
        }

        private static void Validate(ProductSnapshot product)
        {
            if (product == null || !Guid.TryParse(product.catalogProductId, out _) || string.IsNullOrWhiteSpace(product.name)
                || string.IsNullOrWhiteSpace(product.brand) || string.IsNullOrWhiteSpace(product.specification)
                || product.barcodeFormat != "EAN_13" || !BarcodeIdentity.TryNormalizeEan13(product.barcode, out var normalized)
                || normalized != product.barcode || product.mapping == null || product.mapping.scope != "demo-only"
                || string.IsNullOrWhiteSpace(product.mapping.source) || string.IsNullOrWhiteSpace(product.mapping.physicalDecodeVerification))
                throw new FormatException("상품 식별 근거가 부족합니다.");
            if (product.price != null)
            {
                if (product.price.unitPriceKrw.HasValue && (!IsValidNumber(product.price.unitPriceKrw) || string.IsNullOrWhiteSpace(product.price.seller)
                    || !DateTime.TryParseExact(product.price.observedAt, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                    || string.IsNullOrWhiteSpace(product.price.source)))
                    throw new FormatException("가격 관측의 값·출처·날짜를 확인할 수 없습니다.");
            }
            if (product.nutrition != null)
            {
                var n = product.nutrition;
                if (n.sourceCatalogProductId != product.catalogProductId || string.IsNullOrWhiteSpace(n.source)
                    || !n.basisAmount.HasValue || n.basisAmount <= 0 || !IsValidNumber(n.basisAmount) || string.IsNullOrWhiteSpace(n.basisUnit)
                    || new[] { n.caloriesKcal, n.carbsGrams, n.proteinGrams, n.fatGrams, n.sugarsGrams, n.saturatedFatGrams, n.sodiumMg, n.fiberGrams }.Any(v => !IsValidNumber(v)))
                    throw new FormatException("영양정보의 상품 연결·기준량·값이 올바르지 않습니다.");
            }
        }

        private static bool IsValidNumber(double? value) => !value.HasValue || (!double.IsNaN(value.Value) && !double.IsInfinity(value.Value) && value.Value >= 0);
    }

    public static class BarcodeIdentity
    {
        public static bool TryNormalizeEan13(string raw, out string normalized)
        {
            normalized = raw == null ? string.Empty : new string(raw.Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (normalized.Length != 13 || normalized.Any(c => c < '0' || c > '9')) return false;
            var sum = 0;
            for (var i = 0; i < 12; i++) sum += (normalized[i] - '0') * (i % 2 == 0 ? 1 : 3);
            return (10 - sum % 10) % 10 == normalized[12] - '0';
        }
    }
}
