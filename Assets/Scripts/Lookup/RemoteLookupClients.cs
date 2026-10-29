using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace MidtermAR
{
    public sealed class LookupHttpReply
    {
        public long Status;
        public string Body;
    }
    public interface ILookupHttp
    {
        Task<LookupHttpReply> Send(string url, string body, string publishableKey, string userAgent, CancellationToken cancellation);
    }

    public sealed class UnityLookupHttp : ILookupHttp
    {
        // Unity API 호출과 await 복귀는 Unity의 메인 SynchronizationContext에서 실행한다.
        public async Task<LookupHttpReply> Send(string url, string body, string publishableKey, string userAgent, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            using (var request = new UnityWebRequest(url, body == null ? "GET" : "POST"))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 5;
                request.redirectLimit = 0;
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                if (publishableKey != null) request.SetRequestHeader("apikey", publishableKey);
                if (userAgent != null) request.SetRequestHeader("User-Agent", userAgent);
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (cancellation.IsCancellationRequested) { request.Abort(); cancellation.ThrowIfCancellationRequested(); }
                    await Task.Yield();
                }
                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.DataProcessingError)
                    throw new IOException("Lookup transport unavailable");
                if (request.downloadedBytes > 1024 * 1024) throw new FormatException("Lookup response too large");
                return new LookupHttpReply { Status = request.responseCode, Body = request.downloadHandler.text };
            }
        }
    }

    // 같은 앱에서 생성한 OFF 클라이언트 모두가 하나의 분당 예산을 공유한다.
    public sealed class LookupRateLimit
    {
        private readonly Queue<double> requests = new Queue<double>();
        private readonly int limit;
        public LookupRateLimit(int limit = 15) { this.limit = limit; }
        public bool TryAcquire(double now)
        {
            while (requests.Count > 0 && now - requests.Peek() >= 60) requests.Dequeue();
            if (requests.Count >= limit) return false;
            requests.Enqueue(now); return true;
        }
    }

    public sealed class PtBarcodeClient : IPtBarcodeClient
    {
        public const string Contract = "pricetrace-barcode-lookup.v1";
        private readonly RemoteLookupConfig config;
        private readonly ILookupHttp http;
        public PtBarcodeClient(RemoteLookupConfig config, ILookupHttp http) { this.config = config; this.http = http; }
        public async Task<PtBarcodeReply> Lookup(string barcode, CancellationToken cancellation)
        {
            var response = await http.Send(config.ptUrl.TrimEnd('/') + "/rest/v1/rpc/get_public_product_by_barcode_v1",
                new JObject { ["p_barcode"] = barcode }.ToString(Formatting.None), config.ptPublishableKey, null, cancellation);
            LookupJson.RequireSuccess(response);
            return Parse(response.Body, barcode);
        }

        public static PtBarcodeReply Parse(string json, string barcode)
        {
            var root = LookupJson.Object(json);
            if (LookupJson.Text(root, "contractVersion") != Contract || LookupJson.Text(root, "barcode") != barcode)
                throw new FormatException("PT contract/identity mismatch");
            LookupJson.Timestamp(root, "checkedAt");
            var status = LookupJson.Text(root, "status");
            var matches = root["matches"] as JArray ?? throw new FormatException("PT matches missing");
            if (status == "not_linked" || status == "invalid")
            {
                if (matches.Count != 0) throw new FormatException("PT status/count mismatch");
                return new PtBarcodeReply { Status = status };
            }
            // PT v1은 비공개/비활성 연결이 섞인 충돌의 후보를 모두 가린다.
            if (status == "conflict" && matches.Count == 0) return new PtBarcodeReply { Status = status };
            if (status != "matched" && status != "conflict" || status == "matched" && matches.Count != 1
                || status == "conflict" && matches.Count < 2) throw new FormatException("PT status/count mismatch");
            var ids = new HashSet<string>();
            ProductSnapshot product = null;
            foreach (var token in matches)
            {
                var row = token as JObject ?? throw new FormatException("PT product missing");
                var id = LookupJson.Uuid(row, "catalogProductId");
                if (!ids.Add(id)) throw new FormatException("PT duplicate identity");
                LookupJson.Uuid(row, "standardProductId");
                var name = LookupJson.Text(row, "name");
                var brand = LookupJson.Text(row, "brand", true);
                var contentAmount = LookupJson.Number(row, "contentAmount", true, true);
                var contentUnit = LookupJson.Text(row, "contentUnit", true);
                var count = LookupJson.Number(row, "packageCount", true, true);
                if (count.HasValue && count != Math.Truncate(count.Value)) throw new FormatException("PT package count");
                var specification = LookupJson.Text(row, "specificationLabel", true);
                LookupJson.Text(row, "categoryName", true);
                var price = row["latestPublicPrice"];
                if (price == null) throw new FormatException("PT price missing");
                PriceSnapshot observation = null;
                if (price.Type != JTokenType.Null)
                {
                    var p = price as JObject ?? throw new FormatException("PT price invalid");
                    var kind = LookupJson.Text(p, "kind");
                    if (kind != "observed" && kind != "listed") throw new FormatException("PT price kind");
                    observation = new PriceSnapshot
                    {
                        unitPriceKrw = LookupJson.Number(p, "amountKrw"), seller = LookupJson.Text(p, "seller"),
                        observedAt = LookupJson.Timestamp(p, "observedAt"), displaySource = LookupJson.Text(p, "sourceLabel"),
                        source = "PT public barcode v1", kind = kind
                    };
                    // 유효한 게시 가격도 계약상 matched다. 관측가만 표시하므로 가격만 제외한다.
                    if (kind == "listed") observation = null;
                }
                product = new ProductSnapshot { catalogProductId = id, name = name, brand = brand, specification = specification,
                    barcode = barcode, barcodeFormat = "EAN_13", price = observation, contentAmount = contentAmount, contentUnit = contentUnit };
                ProductCatalog.ValidateQuantities(product);
            }
            return new PtBarcodeReply { Status = status, Product = status == "matched" ? product : null };
        }
    }

    public sealed class OffProductClient : IOffProductClient
    {
        public const string SourceName = "Open Food Facts";
        private static readonly LookupRateLimit AppLimit = new LookupRateLimit();
        private readonly RemoteLookupConfig config;
        private readonly ILookupHttp http;
        private readonly string version;
        private readonly Func<double> clock;
        private readonly LookupRateLimit rateLimit;
        public OffProductClient(RemoteLookupConfig config, ILookupHttp http, string version, Func<double> clock, LookupRateLimit rateLimit = null)
        { this.config = config; this.http = http; this.version = version; this.clock = clock; this.rateLimit = rateLimit ?? AppLimit; }

        public async Task<ProductSnapshot> Lookup(string barcode, CancellationToken cancellation)
        {
            if (!config.OffReady || !rateLimit.TryAcquire(clock())) throw new IOException("OFF contact/limit unavailable");
            var response = await http.Send("https://world.openfoodfacts.org/api/v3/product/" + barcode
                + "?fields=product_name_ko,product_name,brands,quantity,nutriments,no_nutrition_data,product_quantity_unit,serving_quantity_unit,last_modified_t",
                null, null, config.UserAgent(version), cancellation);
            if (response.Status != 200 && response.Status != 404) throw new IOException("OFF unavailable");
            return Parse(response.Body, barcode, response.Status);
        }

        public static ProductSnapshot Parse(string json, string barcode, long httpStatus = 200)
        {
            var root = LookupJson.Object(json);
            if (LookupJson.Text(root, "code") != barcode) throw new FormatException("OFF identity mismatch");
            var status = LookupJson.Text(root, "status");
            var result = root["result"] as JObject ?? throw new FormatException("OFF result missing");
            var resultId = LookupJson.Text(result, "id");
            var errors = root["errors"] as JArray ?? throw new FormatException("OFF errors missing");
            if (status == "failure" && resultId == "product_not_found" && httpStatus == 404 && errors.Count > 0)
            {
                foreach (var error in errors)
                    if ((string)error["message"]?["id"] != "product_not_found") throw new FormatException("OFF failure is not a miss");
                return null;
            }
            if (httpStatus != 200 || resultId != "product_found" || errors.Count != 0
                || status != "success" && status != "success_with_warnings") throw new FormatException("OFF failure");
            var product = root["product"] as JObject ?? throw new FormatException("OFF product missing");
            var name = LookupJson.OptionalText(product, "product_name_ko") ?? LookupJson.OptionalText(product, "product_name");
            return name == null ? null : new ProductSnapshot { name = name, brand = LookupJson.OptionalText(product, "brands"),
                specification = LookupJson.OptionalText(product, "quantity"), barcode = barcode, barcodeFormat = "EAN_13",
                nutrition = ParseNutrition(product, barcode) };
        }

        private static NutritionSnapshot ParseNutrition(JObject product, string barcode)
        {
            if (product["no_nutrition_data"]?.Type == JTokenType.String && (string)product["no_nutrition_data"] == "on"
                || !(product["nutriments"] is JObject values)) return null;
            // OFF product_nutrition schema: *_100g는 판매 상태 100g/액체 100ml의 표준값이다.
            // *_unit는 입력 단위이므로 쓰지 않는다. sodium은 g, energy/energy-kj는 kJ다.
            // https://openfoodfacts.github.io/documentation/docs/Product-Opener/schemas/schemas/product_nutrition/
            var kcal = Nutrient(values, "energy-kcal_100g");
            var kj = Nutrient(values, "energy-kj_100g") ?? Nutrient(values, "energy_100g");
            var sodiumGrams = Nutrient(values, "sodium_100g");
            var sodiumMg = sodiumGrams * 1000;
            if (sodiumMg.HasValue && double.IsInfinity(sodiumMg.Value)) sodiumMg = null;
            var snapshot = new NutritionSnapshot
            {
                basisAmount = 100, basisUnit = NutritionBasisUnit(product), prepState = "as_sold",
                caloriesKcal = kcal ?? kj / 4.184,
                carbsGrams = Nutrient(values, "carbohydrates_100g"), proteinGrams = Nutrient(values, "proteins_100g"),
                fatGrams = Nutrient(values, "fat_100g"), sugarsGrams = Nutrient(values, "sugars_100g"),
                saturatedFatGrams = Nutrient(values, "saturated-fat_100g"), sodiumMg = sodiumMg,
                fiberGrams = Nutrient(values, "fiber_100g"), provider = SourceName,
                source = "https://world.openfoodfacts.org/product/" + barcode, displaySource = SourceName,
                sourceUpdatedAt = ProductUpdatedAt(product)
            };
            return ProductInfoFormatter.HasNutrition(new ProductSnapshot { nutrition = snapshot }) ? snapshot : null;
        }

        private static double? Nutrient(JObject values, string key)
        {
            var value = values[key];
            if (value == null || value.Type == JTokenType.Null) return null;
            if (value.Type != JTokenType.Integer && value.Type != JTokenType.Float && value.Type != JTokenType.String) return null;
            double number;
            if (value.Type == JTokenType.String)
            {
                if (!double.TryParse((string)value, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return null;
            }
            else
            {
                try { number = (double)value; }
                catch (OverflowException) { return null; }
            }
            if (double.IsNaN(number) || double.IsInfinity(number) || number < 0) return null;
            return number; // 잘못된 필드만 버린다. 나머지 영양값과 상품 식별은 유지한다.
        }

        private static string NutritionBasisUnit(JObject product)
        {
            var pack = NormalizedQuantityUnit(product["product_quantity_unit"]);
            var serving = NormalizedQuantityUnit(product["serving_quantity_unit"]);
            // 원문이 g/ml를 확정하지 않으면 질량과 부피를 임의로 바꾸지 않는다.
            return pack != null && serving != null && pack != serving ? "g/100ml" : pack ?? serving ?? "g/100ml";
        }

        private static string NormalizedQuantityUnit(JToken token)
        {
            var unit = token?.Type == JTokenType.String ? ((string)token).Trim().ToLowerInvariant() : null;
            return unit == "g" || unit == "ml" ? unit : null;
        }

        private static string ProductUpdatedAt(JObject product)
        {
            var value = Nutrient(product, "last_modified_t");
            if (!value.HasValue || value != Math.Truncate(value.Value) || value > 253402300799) return null;
            return DateTimeOffset.FromUnixTimeSeconds((long)value.Value).ToString("o", CultureInfo.InvariantCulture);
        }
    }

    public sealed class PublicNutritionClient : IPublicNutritionClient
    {
        private readonly RemoteLookupConfig config;
        private readonly ILookupHttp http;
        public PublicNutritionClient(RemoteLookupConfig config, ILookupHttp http) { this.config = config; this.http = http; }
        public async Task<NutritionSnapshot> Lookup(string catalogProductId, CancellationToken cancellation)
        {
            if (!config.NutritionReady) throw new IOException("Nutrition config unavailable");
            var response = await http.Send(config.nutritionUrl.TrimEnd('/') + "/rest/v1/rpc/get_public_product_nutrition_v1",
                new JObject { ["p_namespace"] = "pricetrace", ["p_catalog_product_id"] = catalogProductId }.ToString(Formatting.None),
                config.nutritionPublishableKey, null, cancellation);
            LookupJson.RequireSuccess(response);
            return Parse(response.Body, catalogProductId);
        }
        public static NutritionSnapshot Parse(string json, string id)
        {
            var token = LookupJson.Read(json);
            var rows = token is JArray array ? array : token is JObject obj ? new JArray(obj) : throw new FormatException("Nutrition rows missing");
            NutritionSnapshot selected = null;
            foreach (var row in rows)
            {
                if (LookupJson.Text(row, "contract_version") != "nutrition-read.v1" || LookupJson.Uuid(row, "catalog_product_id") != id)
                    throw new FormatException("Nutrition contract/approved catalog ID mismatch");
                var values = row["nutrition_values"] as JObject ?? throw new FormatException("Nutrition values missing");
                var snapshot = new NutritionSnapshot
                {
                    sourceCatalogProductId = id, nutritionFoodId = LookupJson.Text(row, "nutrition_food_id"),
                    basisAmount = LookupJson.Number(row, "basis_amount", false, true), basisUnit = LookupJson.Text(row, "basis_unit"),
                    prepState = LookupJson.Text(row, "prep_state"), caloriesKcal = LookupJson.Number(values, "calories_kcal", true),
                    proteinGrams = LookupJson.Number(values, "protein_grams", true), carbsGrams = LookupJson.Number(values, "carbs_grams", true),
                    fatGrams = LookupJson.Number(values, "fat_grams", true), sodiumMg = LookupJson.Number(values, "sodium_mg", true),
                    saturatedFatGrams = LookupJson.Number(values, "saturated_fat_grams", true), sugarsGrams = LookupJson.Number(values, "sugars_grams", true),
                    fiberGrams = LookupJson.Number(values, "fiber_grams", true), source = LookupJson.Text(row, "source_type"),
                    provider = "Fitness Nutrition", displaySource = "Fitness Nutrition · PT 승인"
                };
                if (selected != null) throw new FormatException("Multiple public nutrition rows require review");
                selected = snapshot;
            }
            return selected;
        }
    }

    internal static class LookupJson
    {
        public static JToken Read(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, MaxDepth = 32 })
                return JToken.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        }
        public static JObject Object(string json) => Read(json) as JObject ?? throw new FormatException("Object expected");
        public static string Text(JToken row, string key, bool nullable = false)
        {
            var value = row[key];
            if (nullable && value?.Type == JTokenType.Null) return null;
            if (value?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)value) || ((string)value).Length > 500)
                throw new FormatException("Text field invalid");
            return ((string)value).Trim();
        }
        public static string OptionalText(JToken row, string key) => row[key] == null || row[key].Type == JTokenType.Null
            || row[key].Type == JTokenType.String && string.IsNullOrWhiteSpace((string)row[key]) ? null : Text(row, key);
        public static string Uuid(JToken row, string key)
        { var text = Text(row, key); if (!Guid.TryParseExact(text, "D", out _)) throw new FormatException("UUID invalid"); return text; }
        public static string Timestamp(JToken row, string key)
        {
            var text = Text(row, key);
            if (!text.Contains("T") || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                throw new FormatException("Timestamp invalid");
            return text;
        }
        public static double? Number(JToken row, string key, bool nullable = false, bool positive = false)
        {
            var value = row[key];
            if (nullable && value?.Type == JTokenType.Null) return null;
            if (value == null || value.Type != JTokenType.Integer && value.Type != JTokenType.Float)
                throw new FormatException("Number invalid");
            var number = (double)value;
            if (double.IsNaN(number) || double.IsInfinity(number) || number < 0 || positive && number == 0) throw new FormatException("Number invalid");
            return number;
        }
        public static void RequireSuccess(LookupHttpReply reply)
        { if (reply.Status != 200) throw new IOException("Public lookup unavailable"); }
    }
}
