using System.Collections.Generic;
using ZXing;
using ZXing.Common;

namespace MidtermAR
{
    public sealed class BarcodeReadResult
    {
        public readonly string Text;
        public readonly string Format;
        public BarcodeReadResult(string text, string format) { Text = text; Format = format; }
    }

    // Owned by one scanner/worker. No Unity object or camera access on the worker thread.
    public sealed class BarcodeDecoder
    {
        private readonly BarcodeReaderGeneric reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.EAN_13 },
                TryHarder = true,
                TryInverted = true
            }
        };

        public BarcodeReadResult Decode(byte[] pixels, int width, int height, bool rgba = false)
        {
            var result = reader.Decode(pixels, width, height,
                rgba ? RGBLuminanceSource.BitmapFormat.RGBA32 : RGBLuminanceSource.BitmapFormat.Gray8);
            return result == null ? null : new BarcodeReadResult(result.Text, result.BarcodeFormat.ToString());
        }
    }
}
