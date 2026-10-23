namespace MidtermAR
{
    public static class CameraResolutionPolicy // 지원 카메라 해상도의 판독용 우선순위 계산
    {
        // Preserve barcode modules up to 1080p; prefer 30 fps at an equal resolution.
        public static int Score(int width, int height, int? framesPerSecond)
        {
            if (width <= 0 || height <= 0 || width > 1920 || height > 1920 || (long)width * height > 1920 * 1080) return 0;
            return width * height * 100 + (framesPerSecond == 30 ? 2 : framesPerSecond == null ? 1 : 0);
        }
    }
}
