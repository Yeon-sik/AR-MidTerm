using System.Collections.Generic;
using UnityEngine;
using ZXing;
using ZXing.Common;

namespace MidtermAR
{
    public sealed class BarcodeReadResult // 작업 스레드에서 메인 스레드로 전달할 판독값
    {
        public readonly string Text; // 선행 0을 보존하는 바코드 문자열
        public readonly string Format; // 판독기가 확인한 바코드 형식
        public readonly bool HasPosition;
        public readonly Vector2 ImageStart; // CPU 영상 좌상단 기준 정규화된 판독 선분
        public readonly Vector2 ImageEnd;
        public BarcodeReadResult(string text, string format) { Text = text; Format = format; }
        public BarcodeReadResult(string text, string format, Vector2 start, Vector2 end)
            : this(text, format) { ImageStart = start; ImageEnd = end; HasPosition = true; }
    }

    // Owned by one scanner/worker. No Unity object or camera access on the worker thread.
    public sealed class BarcodeDecoder // 픽셀 배열을 ZXing 판독 결과로 변환
    {
        private readonly BarcodeReaderGeneric reader = new BarcodeReaderGeneric
        {
            // EAN 판독기 자체의 회전 처리는 결과 좌표도 원본 영상으로 돌려준다.
            AutoRotate = false,
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
            if (result == null) return null;
            var points = result.ResultPoints;
            if (points == null || points.Length < 2) return new BarcodeReadResult(result.Text, result.BarcodeFormat.ToString());
            return new BarcodeReadResult(result.Text, result.BarcodeFormat.ToString(),
                new Vector2(points[0].X / width, points[0].Y / height),
                new Vector2(points[1].X / width, points[1].Y / height));
        }
    }
}
