using System;
using Newtonsoft.Json;

namespace MidtermAR
{
    // 로컬 파일만 읽는다. 키·연락처·원문 응답은 로그나 검증 문서에 출력하지 않는다.
    public sealed class RemoteLookupConfig
    {
        public bool enabled;
        public string ptUrl;
        public string ptPublishableKey;
        public string nutritionUrl;
        public string nutritionPublishableKey;
        public string offContact;

        public bool PtReady => enabled && Endpoint(ptUrl) && Publishable(ptPublishableKey);
        public bool NutritionReady => Endpoint(nutritionUrl) && Publishable(nutritionPublishableKey);
        public bool OffReady => !string.IsNullOrWhiteSpace(offContact) && offContact.Length <= 200
            && offContact.IndexOfAny(new[] { '\r', '\n', '(', ')' }) < 0;
        public string UserAgent(string version) => "ProductAR/" + version + " (" + offContact + ")";

        public static RemoteLookupConfig Parse(string json)
        {
            try { return string.IsNullOrWhiteSpace(json) ? new RemoteLookupConfig()
                : JsonConvert.DeserializeObject<RemoteLookupConfig>(json) ?? new RemoteLookupConfig(); }
            catch (JsonException) { return new RemoteLookupConfig(); }
        }

        private static bool Endpoint(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == "https" && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo)
            && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
        private static bool Publishable(string value) => value != null && value.StartsWith("sb_publishable_", StringComparison.Ordinal)
            && value.Length > 20 && value.IndexOfAny(new[] { '\r', '\n' }) < 0;
    }
}
