#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace MidtermAR
{
    // Registration alone is not proof that a physical package can be recognized again.
    public sealed class RuntimeImageRegistrationEvidence
    {
        public string ImageName { get; private set; }
        public bool FirstTracking { get; private set; }
        public bool ObservedLoss { get; private set; }
        public bool Reacquired { get; private set; }
        public bool Visible { get; private set; }
        public Vector2 EstimatedSize { get; private set; }

        public void Registered(string name)
        {
            Reset();
            ImageName = name;
        }

        public void Observe(string name, bool ready, TrackingState state, int method, Vector2 size)
        {
            if (string.IsNullOrEmpty(ImageName) || name != ImageName) return;
            Visible = ImageTrackingPolicy.CanDisplay(ready, false, state, method, true) && ValidSize(size);
            if (!ready) return; // Pause/session loss is not a physical package-loss test.
            if (Visible)
            {
                if (FirstTracking && ObservedLoss) Reacquired = true;
                FirstTracking = true;
                EstimatedSize = size;
            }
            else if (FirstTracking && (state != TrackingState.Tracking || method == 0 || method == 2))
                ObservedLoss = true;
        }

        public void Hide() => Visible = false;

        public void Reset()
        {
            ImageName = null;
            FirstTracking = ObservedLoss = Reacquired = Visible = false;
            EstimatedSize = default;
        }

        private static bool ValidSize(Vector2 size) => size.x > 0 && size.y > 0
            && !float.IsNaN(size.x) && !float.IsNaN(size.y)
            && !float.IsInfinity(size.x) && !float.IsInfinity(size.y);
    }
}
#endif
