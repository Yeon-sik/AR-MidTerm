#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace MidtermAR
{
    // Bounded local diagnostics: eight source frames, overwritten on each new scan.
    // No image upload, gallery permission, Unity object access or synthetic camera input.
    public static class BarcodeFrameDiagnostics // 개발 빌드의 판독 영상·결과를 제한된 슬롯에 저장
    {
        public static void Save(string directory, int slot, int scanGeneration, double sourceTimestamp, byte[] pixels, int width, int height, string barcode, double decodeMilliseconds)
        {
            if (slot < 1 || slot > 8) return;
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "sample-" + slot.ToString("00"));
            using (var stream = File.Create(path + ".pgm"))
            {
                var header = Encoding.ASCII.GetBytes($"P5\n{width} {height}\n255\n");
                stream.Write(header, 0, header.Length);
                stream.Write(pixels, 0, width * height);
            }
            File.WriteAllText(path + ".json", JsonConvert.SerializeObject(new
            { capturedUtc = DateTime.UtcNow.ToString("o"), scanGeneration, sourceTimestamp, width, height, barcode, decodeMilliseconds }));
        }
    }
}
#endif
