using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace MidtermAR.Editor
{
    // Replays genuine captured frames; results are diagnostics, not synthetic device proof.
    public static class BarcodeImageAnalysis
    {
        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            var input = args[Array.IndexOf(args, "-barcodeImagePath") + 1];
            var output = args[Array.IndexOf(args, "-barcodeAnalysisPath") + 1];
            var files = Directory.Exists(input) ? Directory.GetFiles(input).Where(p => p.EndsWith(".pgm") || p.EndsWith(".png")) : new[] { input };
            var records = new List<object>();
            foreach (var file in files)
            {
                int width, height;
                byte[] source;
                if (file.EndsWith(".pgm"))
                {
                    var bytes = File.ReadAllBytes(file);
                    var position = 0;
                    string Token()
                    {
                        while (bytes[position] <= 32) position++;
                        var start = position;
                        while (bytes[position] > 32) position++;
                        return System.Text.Encoding.ASCII.GetString(bytes, start, position - start);
                    }
                    if (Token() != "P5") throw new FormatException("Only binary PGM is supported.");
                    width = int.Parse(Token()); height = int.Parse(Token());
                    if (Token() != "255") throw new FormatException("Only Gray8 is supported.");
                    position++; // Exactly one separator; pixels may themselves be whitespace bytes.
                    source = new byte[width * height];
                    Buffer.BlockCopy(bytes, position, source, 0, source.Length);
                }
                else
                {
                    var texture = new Texture2D(2, 2);
                    if (!texture.LoadImage(File.ReadAllBytes(file))) throw new FormatException("Image could not be loaded.");
                    width = texture.width; height = texture.height;
                    source = texture.GetPixels32().Select(c => (byte)((c.r * 77 + c.g * 150 + c.b * 29) >> 8)).ToArray();
                    UnityEngine.Object.DestroyImmediate(texture);
                }
                foreach (var edge in new[] { 640, 960, 1280, 1920, Math.Max(width, height) }.Distinct())
                {
                    var scale = Math.Min(1f, (float)edge / Math.Max(width, height));
                    var w = Math.Max(1, (int)(width * scale)); var h = Math.Max(1, (int)(height * scale));
                    var pixels = new byte[w * h];
                    for (var y = 0; y < h; y++)
                        for (var x = 0; x < w; x++)
                        {
                            var sx = (x + 0.5f) / scale - 0.5f; var sy = (y + 0.5f) / scale - 0.5f;
                            var x0 = Mathf.Clamp((int)Math.Floor(sx), 0, width - 1); var y0 = Mathf.Clamp((int)Math.Floor(sy), 0, height - 1);
                            var x1 = Math.Min(x0 + 1, width - 1); var y1 = Math.Min(y0 + 1, height - 1);
                            var fx = Mathf.Clamp01(sx - x0); var fy = Mathf.Clamp01(sy - y0);
                            pixels[y * w + x] = (byte)Mathf.Lerp(Mathf.Lerp(source[y0 * width + x0], source[y0 * width + x1], fx), Mathf.Lerp(source[y1 * width + x0], source[y1 * width + x1], fx), fy);
                        }
                    var timer = Stopwatch.StartNew();
                    var result = new BarcodeDecoder().Decode(pixels, w, h);
                    timer.Stop();
                    records.Add(new { file = Path.GetFileName(file), width = w, height = h, barcode = result?.Text, milliseconds = timer.Elapsed.TotalMilliseconds });
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonConvert.SerializeObject(records, Formatting.Indented));
            UnityEngine.Debug.Log("[ProductAR Analysis] " + File.ReadAllText(output));
        }
    }
}
