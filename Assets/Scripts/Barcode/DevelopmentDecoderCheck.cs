#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEngine;
using ZXing;

namespace MidtermAR
{
    // Development builds only. This proves the managed decoder runs under IL2CPP,
    // not that the camera has read a physical product.
    public static class DevelopmentDecoderCheck
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static async void Run()
        {
            try
            {
                await Task.Run(CheckSyntheticEan13);
                Debug.Log("[ProductAR SelfCheck] PASS: synthetic EAN-13 and 90-degree rotation. Physical camera decode NOT VERIFIED.");
            }
            catch (Exception error) { Debug.LogError("[ProductAR SelfCheck] FAIL: " + error.Message); }
        }

        private static void CheckSyntheticEan13()
        {
            const int width = 960, height = 320;
            var matrix = new MultiFormatWriter().encode("8801104671507", BarcodeFormat.EAN_13, width, height);
            var reader = new BarcodeDecoder();
            foreach (var rotated in new[] { false, true })
            {
                var pixels = new byte[width * height];
                for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                        pixels[rotated ? x * height + height - y - 1 : y * width + x] = matrix[x, y] ? (byte)0 : (byte)255;
                var decoded = reader.Decode(pixels, rotated ? height : width, rotated ? width : height);
                if (decoded == null || decoded.Text != "8801104671507" || decoded.Format != "EAN_13")
                    throw new InvalidOperationException("Platform decoder returned an unexpected result.");
            }
        }
    }
}
#endif
