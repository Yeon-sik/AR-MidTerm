using UnityEngine.XR.ARSubsystems;

namespace MidtermAR
{
    public static class ImageTrackingPolicy
    {
        public static bool CanDisplay(bool ready, bool paused, TrackingState state, int method, bool expectedImage)
            => ready && !paused && expectedImage && state == TrackingState.Tracking && method == 1;
    }
}
