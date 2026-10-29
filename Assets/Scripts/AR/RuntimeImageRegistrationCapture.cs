using System;
using UnityEngine;

namespace MidtermAR
{
    // CPU pixels have a top-left origin; Unity texture rows start at the bottom.
    // ARCore's image database conversion flips Unity texture rows internally.
    public static class RuntimeImageRegistrationCapture
    {
        public static bool TryImagePoint(BarcodeViewportTransform transform, Vector2 viewport, out Vector2 point)
        {
            point = default;
            if (!transform.IsValid || !Finite(viewport)) return false;
            var origin = transform.Map(Vector2.zero);
            var xAxis = transform.Map(Vector2.right) - origin;
            var yAxis = transform.Map(Vector2.up) - origin;
            var delta = viewport - origin;
            var determinant = xAxis.x * yAxis.y - xAxis.y * yAxis.x;
            point = new Vector2((delta.x * yAxis.y - delta.y * yAxis.x) / determinant,
                (xAxis.x * delta.y - xAxis.y * delta.x) / determinant);
            const float tolerance = .0001f;
            return Finite(point) && point.x >= -tolerance && point.x <= 1f + tolerance
                && point.y >= -tolerance && point.y <= 1f + tolerance;
        }

        public static byte[] CropRgb24(byte[] source, int sourceWidth, int sourceHeight,
            BarcodeViewportTransform transform, Rect guide, int outputWidth, int outputHeight)
        {
            if (source == null || sourceWidth <= 0 || sourceHeight <= 0
                || source.LongLength != (long)sourceWidth * sourceHeight * 3
                || outputWidth <= 0 || outputHeight <= 0 || outputWidth > 1600 || outputHeight > 1600
                || !Finite(guide.min) || !Finite(guide.max) || guide.width <= 0 || guide.height <= 0
                || guide.xMin < 0 || guide.yMin < 0 || guide.xMax > 1 || guide.yMax > 1)
                throw new ArgumentException("Invalid CPU image or capture guide.");
            foreach (var corner in new[] { guide.min, guide.max,
                         new Vector2(guide.xMin, guide.yMax), new Vector2(guide.xMax, guide.yMin) })
                if (!TryImagePoint(transform, corner, out _))
                    throw new ArgumentException("Capture guide falls outside the camera image.");

            var output = new byte[outputWidth * outputHeight * 3];
            for (var y = 0; y < outputHeight; y++)
            for (var x = 0; x < outputWidth; x++)
            {
                // Bottom-to-top output preserves the visible guide's orientation without mirroring.
                var viewport = new Vector2(guide.xMin + (x + .5f) / outputWidth * guide.width,
                    guide.yMin + (y + .5f) / outputHeight * guide.height);
                if (!TryImagePoint(transform, viewport, out var imagePoint))
                    throw new ArgumentException("Camera mapping changed during capture.");
                var sx = Mathf.Clamp(Mathf.FloorToInt(imagePoint.x * sourceWidth), 0, sourceWidth - 1);
                var sy = Mathf.Clamp(Mathf.FloorToInt(imagePoint.y * sourceHeight), 0, sourceHeight - 1);
                var inputOffset = (sy * sourceWidth + sx) * 3;
                var outputOffset = (y * outputWidth + x) * 3;
                output[outputOffset] = source[inputOffset];
                output[outputOffset + 1] = source[inputOffset + 1];
                output[outputOffset + 2] = source[inputOffset + 2];
            }
            return output;
        }

        private static bool Finite(Vector2 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y)
            && !float.IsInfinity(value.x) && !float.IsInfinity(value.y);
    }
}
