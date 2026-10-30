using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MidtermAR
{
    // Tick은 메인 스레드 전용이다. 완료되지 않은 Task를 기다리거나 카메라 루프를 막지 않는다.
    public sealed class LookupCoordinator : IDisposable
    {
        public const double TimeoutSeconds = 5;
        public const double SuccessCacheSeconds = 300, NotFoundCacheSeconds = 60, RetryCooldownSeconds = 15;
        private sealed class Pending
        {
            public Task<ProductLookupResult> Task;
            public CancellationTokenSource Cancellation;
            public long Generation;
            public double Started;
        }
        private sealed class Cached { public ProductLookupResult Result; public double Expires; }
        private readonly IProductLookup lookup;
        private readonly Dictionary<string, Pending> pending = new Dictionary<string, Pending>();
        private readonly Dictionary<string, Cached> cache = new Dictionary<string, Cached>();
        private readonly Dictionary<string, double> cooldown = new Dictionary<string, double>();
        private long attemptedGeneration = -1;
        public int CachedCount => cache.Count;
        public int PendingCount => pending.Count;
        public LookupCoordinator(IProductLookup lookup) { this.lookup = lookup; }

        public void Tick(ProductRecognitionState recognition, double now)
        {
            foreach (var barcode in new List<string>(pending.Keys))
            {
                var request = pending[barcode];
                if (!request.Task.IsCompleted && now - request.Started < TimeoutSeconds) continue;
                ProductLookupResult result;
                if (request.Task.IsCompleted)
                {
                    try { result = request.Task.GetAwaiter().GetResult(); } // 완료를 확인한 뒤에만 읽는다.
                    catch { result = new ProductLookupResult(barcode, LookupStatus.PtUnavailable); }
                }
                else
                {
                    request.Cancellation.Cancel();
                    result = new ProductLookupResult(barcode, LookupStatus.PtUnavailable);
                    // 취소를 무시하는 클라이언트의 예외도 관찰한다. 화면에는 반영하지 않는다.
                    _ = request.Task.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                }
                pending.Remove(barcode);
                request.Cancellation.Dispose();
                if (result == null || result.Barcode != barcode) result = new ProductLookupResult(barcode, LookupStatus.PtUnavailable);
                if (result.Cacheable)
                {
                    if (cache.Count >= 128) cache.Clear();
                    // 영양 부분 실패는 오류 쿨다운과 같은 15초만 재사용해 PT 식별은 유지하고 이후 재확정에서 다시 조회한다.
                    var ttl = result.PartialFailure ? RetryCooldownSeconds : result.Status == LookupStatus.NotFound ? NotFoundCacheSeconds : SuccessCacheSeconds;
                    cache[barcode] = new Cached { Result = result, Expires = now + ttl };
                    cooldown.Remove(barcode);
                }
                else
                {
                    if (cooldown.Count >= 128) cooldown.Clear();
                    cooldown[barcode] = now + RetryCooldownSeconds; // 오류는 미발견 캐시가 아니다. 재확정 폭주만 막는다.
                }
                recognition.ApplyLookup(result, request.Generation);
            }
            var current = recognition.ConfirmedBarcode;
            if (current == null || attemptedGeneration == recognition.LookupGeneration) return;
            var generation = recognition.LookupGeneration;
            if (cache.TryGetValue(current, out var entry) && entry.Expires > now)
            {
                attemptedGeneration = generation;
                recognition.ApplyLookup(entry.Result, generation); // 현재 3회 확정된 대상에만 캐시를 사용한다.
                return;
            }
            // 같은 번호의 진행 중 요청은 합친다. 이전 세대의 완료는 캐시에만 남긴다.
            if (pending.ContainsKey(current)) { recognition.BeginLookup(generation); return; }
            attemptedGeneration = generation;
            if (pending.Count >= 4 || cooldown.TryGetValue(current, out var until) && until > now)
            { recognition.ApplyLookup(new ProductLookupResult(current, LookupStatus.PtUnavailable), generation); return; }
            recognition.BeginLookup(generation);
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            Task<ProductLookupResult> task;
            try { task = lookup.Lookup(current, cancellation.Token); }
            catch { task = Task.FromResult(new ProductLookupResult(current, LookupStatus.PtUnavailable)); }
            pending.Add(current, new Pending { Task = task, Cancellation = cancellation, Generation = generation, Started = now });
        }

        public void Dispose()
        {
            foreach (var request in pending.Values)
            {
                request.Cancellation.Cancel(); request.Cancellation.Dispose();
                _ = request.Task.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            }
            pending.Clear();
        }
    }
}
