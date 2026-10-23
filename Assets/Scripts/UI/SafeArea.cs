using UnityEngine;

namespace MidtermAR
{
    public sealed class SafeArea : MonoBehaviour
    {
        private Rect previous; // 화면 안전 영역이 바뀔 때만 다시 적용
        private RectTransform rect; // 노치·시스템 영역을 피해 배치할 UI 영역
        private void Awake() { rect = (RectTransform)transform; Apply(); }
        private void Update() { if (Screen.safeArea != previous) Apply(); }
        private void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            previous = Screen.safeArea;
            rect.anchorMin = new Vector2(previous.xMin / Screen.width, previous.yMin / Screen.height);
            rect.anchorMax = new Vector2(previous.xMax / Screen.width, previous.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
