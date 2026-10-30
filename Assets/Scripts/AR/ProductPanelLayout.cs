using UnityEngine;

namespace MidtermAR
{
    // 표시 기하만 계산한다. 인식 상태나 카메라 방향으로 위치·회전을 보정하지 않는다.
    public readonly struct ProductPanelLayout
    {
        public const float GapMetres = 0.012f;
        public const float TiltDegrees = 15f;
        public readonly float Width;
        public readonly Vector3 Center, ConnectorStart, ConnectorEnd;
        public static Quaternion Rotation => Quaternion.AngleAxis(TiltDegrees, Vector3.forward) * Quaternion.Euler(90, 0, 0);

        private ProductPanelLayout(float width, Vector3 center, Vector3 start, Vector3 end)
        { Width = width; Center = center; ConnectorStart = start; ConnectorEnd = end; }

        public static bool TryCreate(ProductImageBinding binding, Vector2 cropSize, out ProductPanelLayout layout)
        {
            layout = default;
            if (binding == null || !Positive(cropSize.x) || !Positive(cropSize.y)) return false;
            // 사전 등록 포장은 실측한 공통 폭을 쓴다. 현장 등록은 ARCore 추정 폭을 허용한다.
            // 개발 카드의 기존 추적 폭 대체도 유지하되, 일반 포장의 실측 누락을 허용하지 않는다.
            var productWidth = binding.productWidthMetres > 0 ? binding.productWidthMetres
                : binding.runtimeRegistered || binding.developmentOnly ? cropSize.x : 0;
            if (!Positive(productWidth)) return false;
            // 58mm 포장은 92.8mm 창. 640단위 창의 30단위 본문이 약 4.4mm가 되도록 최소 90mm를 둔다.
            // 160mm 상한은 큰 개발용 마커의 UI 과대 확대를 막는 표시 기준이며 상품 실측값이 아니다.
            var width = Mathf.Clamp(productWidth * 1.6f, 0.09f, 0.16f);
            var origin = new Vector3(binding.offsetXMetres, 0, binding.offsetZMetres);
            var start = origin + new Vector3(productWidth * .5f, .001f, 0);
            var end = origin + new Vector3(productWidth * .5f + GapMetres, .008f, 0);
            var center = end + Quaternion.AngleAxis(TiltDegrees, Vector3.forward) * new Vector3(width * .5f, 0, 0);
            layout = new ProductPanelLayout(width, center, start, end);
            return true;
        }

        // 표시 윤곽은 추적 크롭과 분리한다. 등록한 공통 폭·높이가 모두 있으면 상품 전체 영역을,
        // 없으면(현장 등록·개발 카드) 실제 추적 크롭 경계를 이미지 평면 위 1mm에 그린다.
        public static bool OutlinesWholeProduct(ProductImageBinding binding)
            => binding != null && Positive(binding.productWidthMetres) && Positive(binding.productHeightMetres);

        public static Vector3[] OutlineCorners(ProductImageBinding binding, Vector2 cropSize)
        {
            var whole = OutlinesWholeProduct(binding);
            var center = whole ? new Vector3(binding.offsetXMetres, 0, binding.offsetZMetres) : Vector3.zero;
            var x = (whole ? binding.productWidthMetres : cropSize.x) * .5f;
            var z = (whole ? binding.productHeightMetres : cropSize.y) * .5f;
            return new[] { center + new Vector3(-x, .001f, -z), center + new Vector3(x, .001f, -z),
                center + new Vector3(x, .001f, z), center + new Vector3(-x, .001f, z) };
        }
        private static bool Positive(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
