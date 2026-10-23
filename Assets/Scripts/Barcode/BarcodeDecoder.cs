using System.Collections.Generic;
using ZXing;
using ZXing.Common;

namespace MidtermAR
{
    public sealed class BarcodeReadResult // 작업 스레드에서 메인 스레드로 전달할 판독값
    {
        public readonly string Text; // 선행 0을 보존하는 바코드 문자열
        public readonly string Format; // 판독기가 확인한 바코드 형식
        public BarcodeReadResult(string text, string format) { Text = text; Format = format; }
    }

    // Owned by one scanner/worker. No Unity object or camera access on the worker thread.
    public sealed class BarcodeDecoder // 픽셀 배열을 ZXing 판독 결과로 변환
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
