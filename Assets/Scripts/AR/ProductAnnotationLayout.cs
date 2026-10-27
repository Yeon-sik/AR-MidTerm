using UnityEngine;

namespace MidtermAR
{
    // 공통 상품 좌표 (X, Z), +Z는 사진 아래쪽이다. 카메라·화면 경계에 의존하지 않는다.
    public readonly struct ProductAnnotationLayout
    {
        public const float Width = .115f, Height = .026f, Gap = .012f;
        public readonly Rect Bounds;
        public readonly Vector3 Anchor, LabelEdge;
        public Vector3 Center => new Vector3(Bounds.center.x, .001f, Bounds.center.y);
        private ProductAnnotationLayout(Rect bounds, Vector3 anchor, Vector3 edge)
        { Bounds = bounds; Anchor = anchor; LabelEdge = edge; }

        public static bool TryCreate(ProductImageBinding product, ProductAnnotationBinding annotation, Rect basicPanel, out ProductAnnotationLayout layout)
        {
            layout = default;
            if (product == null || annotation == null || !Finite(product.productWidthMetres) || !Finite(product.productHeightMetres)
                || product.productWidthMetres <= 0 || product.productHeightMetres <= 0
                || !Finite(annotation.anchorXMetres) || !Finite(annotation.anchorZMetres)
                || Mathf.Abs(annotation.anchorXMetres) > product.productWidthMetres * .5f
                || Mathf.Abs(annotation.anchorZMetres) > product.productHeightMetres * .5f) return false;
            var x = annotation.anchorXMetres; var z = annotation.anchorZMetres;
            Rect bounds; Vector3 edge;
            if (annotation.side == "left")
            {
                bounds = new Rect(-product.productWidthMetres * .5f - Gap - Width, z - Height * .5f, Width, Height);
                edge = new Vector3(bounds.xMax, .001f, z);
            }
            else if (annotation.side == "top")
            {
                // 위 라벨의 오른쪽 끝을 기본 정보판 왼쪽보다 2mm 안쪽으로 제한한다.
                var centerX = Mathf.Min(x, basicPanel.xMin - .002f - Width * .5f);
                bounds = new Rect(centerX - Width * .5f, -product.productHeightMetres * .5f - Gap - Height, Width, Height);
                edge = new Vector3(Mathf.Clamp(x, bounds.xMin, bounds.xMax), .001f, bounds.yMax);
            }
            else return false;
            var package = new Rect(-product.productWidthMetres * .5f, -product.productHeightMetres * .5f, product.productWidthMetres, product.productHeightMetres);
            var basicConnector = new Rect(product.productWidthMetres * .5f, -.001f, Gap, .002f);
            if (bounds.Overlaps(package) || bounds.Overlaps(basicPanel) || bounds.Overlaps(basicConnector)) return false;
            layout = new ProductAnnotationLayout(bounds, new Vector3(x, .001f, z), edge);
            return true;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
