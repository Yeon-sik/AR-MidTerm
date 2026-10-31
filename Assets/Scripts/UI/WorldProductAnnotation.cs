using TMPro;
using UnityEngine;

namespace MidtermAR
{
    public sealed class WorldProductAnnotation : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private RectTransform labelPanel;
        [SerializeField] private LineRenderer leader;
        private bool hasPose;
        public string Text => label.text;
        public bool IsVisible => gameObject.activeSelf && leader.enabled;
        public void SetContent(string text) { label.richText = false; label.text = text; SetVisible(false); }
        public void ApplyPose(Transform image, ProductImageBinding binding, ProductAnnotationLayout layout)
        {
            // 배치는 사진 좌표에서 계산하고, 이미지 로컬로 바꾼 뒤 월드 좌표에 놓는다.
            var origin = new Vector3(binding.offsetXMetres, 0, binding.offsetZMetres);
            var rect = labelPanel;
            rect.SetPositionAndRotation(World(layout.Center), image.rotation * Quaternion.Euler(90, 0, 0));
            rect.localScale = Vector3.one * (ProductAnnotationLayout.Width / rect.rect.width);
            leader.SetPositions(new[] { World(layout.Anchor), World(layout.LabelEdge) });
            hasPose = true;
            Vector3 World(Vector3 photo) => image.TransformPoint(ProductPanelLayout.ImageLocal(origin + photo));
        }
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible && hasPose);
            leader.enabled = visible && hasPose;
        }
        public void InvalidatePose() { hasPose = false; SetVisible(false); }
    }
}
