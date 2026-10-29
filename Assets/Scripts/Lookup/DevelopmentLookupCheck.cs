#if DEVELOPMENT_BUILD && UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MidtermAR
{
    // ADB Intent로 명시한 개발 시험에서만 실행한다. 카메라 인식 상태는 변경하지 않는다.
    public static class DevelopmentLookupCheck
    {
        public static async void RunIfRequested(RemoteLookupConfig config)
        {
            string barcode;
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
            {
                if (!intent.Call<bool>("getBooleanExtra", "productar.lookupProbe", false)) return;
                barcode = intent.Call<string>("getStringExtra", "productar.lookupBarcode");
                intent.Call("removeExtra", "productar.lookupProbe");
            }
            if (!BarcodeIdentity.TryNormalizeEan13(barcode, out barcode)) barcode = "8801104671507";
            if (!config.PtReady) { Debug.Log("[ProductAR LookupCheck] disabled-config"); return; }
            var http = new UnityLookupHttp();
            await Check("pt-barcode", async cancellation => (await new PtBarcodeClient(config, http).Lookup("8801104671507", cancellation)).Status);
            await Check("nutrition", async cancellation => (await new PublicNutritionClient(config, http).Lookup("1516c3eb-3dfb-4e11-8040-d23fadcdaad0", cancellation)) == null ? "empty" : "found");
            await Check("off-v3", async cancellation => (await new OffProductClient(config, http, Application.version,
                () => Time.realtimeSinceStartupAsDouble).Lookup(barcode, cancellation)) == null ? "not_found" : "found");
            // 연락처를 제삼자 echo 서버에 전송하지 않는다. 합성 UA로 Android 헤더 전달만 확인한다.
            await Check("android-user-agent", async cancellation =>
            {
                var syntheticAgent = "ProductAR/" + Application.version + " (probe@example.invalid)";
                var reply = await http.Send("https://httpbin.org/user-agent", null, null, syntheticAgent, cancellation);
                return reply.Status == 200 && (string)JObject.Parse(reply.Body)["user-agent"] == syntheticAgent ? "wire-match" : "wire-mismatch";
            });
            Debug.Log("[ProductAR LookupCheck] complete; physical-scan=NOT_RUN");
        }
        private static async Task Check(string name, Func<CancellationToken, Task<string>> run)
        {
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                try { Debug.Log("[ProductAR LookupCheck] " + name + "=" + await run(cancellation.Token)); }
                catch { Debug.Log("[ProductAR LookupCheck] " + name + "=unavailable"); }
            }
        }
    }
}
#endif
