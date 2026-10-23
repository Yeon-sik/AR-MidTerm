namespace MidtermAR
{
    // Independent camera frames must agree. A valid EAN checksum alone is not confidence.
    public sealed class BarcodeConfirmation
    {
        public const int RequiredReads = 3; // 같은 번호를 연속 확인할 횟수
        private const float MaxGapSeconds = 1.5f; // 연속 판독으로 인정할 최대 시간 간격
        public string Candidate { get; private set; } // 현재 연속 확인 중인 번호
        public int Count { get; private set; } // 현재 후보가 연속 일치한 횟수
        private float lastReadTime;

        public bool Observe(BarcodeReadResult result, float capturedAt)
        {
            if (result == null || result.Format != "EAN_13" || !BarcodeIdentity.TryNormalizeEan13(result.Text, out var normalized))
            { Reset(); return false; }
            if (Candidate != normalized || capturedAt < lastReadTime || capturedAt - lastReadTime > MaxGapSeconds)
            { Candidate = normalized; Count = 0; }
            lastReadTime = capturedAt;
            Count++;
            return Count >= RequiredReads;
        }

        public void Reset() { Candidate = null; Count = 0; lastReadTime = 0; }
    }
}
