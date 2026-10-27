using UnityEngine;

namespace MidtermAR
{
    public sealed class BarcodeFollowState
    {
        public const float MaxAgeSeconds = 0.65f;
        public string Barcode { get; private set; }
        public Vector2 ViewportPosition { get; private set; }
        private float lastSeen = float.NegativeInfinity;

        public void Select(string barcode) { Barcode = barcode; Invalidate(); }
        public void Invalidate() { lastSeen = float.NegativeInfinity; }
        public bool IsVisible(float now) => Barcode != null && now >= lastSeen && now - lastSeen <= MaxAgeSeconds;

        public void Observe(BarcodeReadResult result, BarcodeViewportTransform transform, float capturedAt, float now)
        {
            if (result == null) return; // 짧은 판독 공백은 허용하되 타임아웃 후 숨긴다.
            if (result.Text != Barcode || result.Format != "EAN_13") { Invalidate(); return; }
            if (!result.HasPosition || !transform.IsValid || capturedAt > now || now - capturedAt > MaxAgeSeconds)
            { Invalidate(); return; }
            var position = transform.Map((result.ImageStart + result.ImageEnd) * 0.5f);
            if (float.IsNaN(position.x) || float.IsNaN(position.y)
                || position.x < 0 || position.x > 1 || position.y < 0 || position.y > 1) { Invalidate(); return; }
            if (capturedAt < lastSeen) return;
            ViewportPosition = position;
            lastSeen = capturedAt;
        }
    }
}
