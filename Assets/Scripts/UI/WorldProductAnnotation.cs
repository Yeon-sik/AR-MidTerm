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
            var origin = new Vector3(binding.offsetXMetres, 0, binding.offsetZMetres);
            var rect = labelPanel;
            rect.SetPositionAndRotation(image.TransformPoint(origin + layout.Center), image.rotation * Quaternion.Euler(90, 0, 0));
            rect.localScale = Vector3.one * (ProductAnnotationLayout.Width / rect.rect.width);
            leader.SetPositions(new[] { image.TransformPoint(origin + layout.Anchor), image.TransformPoint(origin + layout.LabelEdge) });
            hasPose = true;
        }
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible && hasPose);
            leader.enabled = visible && hasPose;
        }
        public void InvalidatePose() { hasPose = false; SetVisible(false); }
    }
}
