namespace MidtermAR
{
    // Independent camera frames must agree. A valid EAN checksum alone is not confidence.
    public sealed class BarcodeConfirmation
    {
        public const int RequiredReads = 3;
        private const float MaxGapSeconds = 1.5f;
        public string Candidate { get; private set; }
        public int Count { get; private set; }
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
