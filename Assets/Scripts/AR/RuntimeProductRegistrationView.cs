using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MidtermAR
{
    // This canvas only draws. All touch and mouse input goes through the controller.
    public sealed class RuntimeProductRegistrationView : MonoBehaviour
    {
        public static Rect CaptureArea => new Rect(.27f, .125f, .69f, .075f);
        public static Rect CancelArea => new Rect(.27f, .045f, .69f, .065f);
        public const string SessionNotice = "앱을 종료하면 등록이 사라져요. 크기는 카메라가 추정해요.";
        public const string ReadyInstruction = "평평하고 반사가 적은 앞면을 틀에 채운 뒤 촬영해주세요.";
        private static readonly Color Ink = new Color(.08f, .15f, .19f, .96f);
        private static readonly Color Green = new Color(.11f, .43f, .32f);
        private static readonly Color Muted = new Color(.27f, .34f, .36f);
        private TextMeshProUGUI heading, product, instruction, captureLabel;
        private Image captureSurface;

        public void Initialize(TMP_FontAsset font)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2340);
            scaler.matchWidthOrHeight = .5f;
            var root = (RectTransform)transform;
            var header = Surface(root, "Instructions", new Rect(.025f, .775f, .95f, .2f), Ink);
            heading = Text(header.rectTransform, "Title", font, 44, Color.white, new Vector2(.035f, .72f), new Vector2(.965f, .97f));
            product = Text(header.rectTransform, "Selected Product", font, 30, new Color(.82f, .91f, .86f), new Vector2(.035f, .43f), new Vector2(.965f, .74f));
            product.maxVisibleLines = 2;
            product.overflowMode = TextOverflowModes.Ellipsis;
            instruction = Text(header.rectTransform, "Instruction", font, 32, Color.white, new Vector2(.035f, .025f), new Vector2(.965f, .43f));

            var guide = RuntimeProductRegistrationController.GuideViewport;
            var guideRoot = Rect(root, "Front Guide", guide.min, guide.max);
            var color = new Color(1f, .81f, .33f);
            var top = Surface(guideRoot, "Top", new Rect(0, 1, 1, 0), color);
            var bottom = Surface(guideRoot, "Bottom", new Rect(0, 0, 1, 0), color);
            var left = Surface(guideRoot, "Left", new Rect(0, 0, 0, 1), color);
            var right = Surface(guideRoot, "Right", new Rect(1, 0, 0, 1), color);
            top.rectTransform.sizeDelta = bottom.rectTransform.sizeDelta = new Vector2(0, 4);
            left.rectTransform.sizeDelta = right.rectTransform.sizeDelta = new Vector2(4, 0);

            var noticeBackground = Surface(root, "Session Notice", new Rect(.025f, .045f, .21f, .175f), Ink);
            var notice = Text(noticeBackground.rectTransform, "Session Limits", font, 25, Color.white,
                new Vector2(.07f, .035f), new Vector2(.93f, .965f));
            notice.text = SessionNotice;
            captureSurface = Surface(root, "Capture", CaptureArea, Green);
            captureLabel = Text(captureSurface.rectTransform, "Capture Label", font, 37, Color.white,
                new Vector2(.04f, .08f), new Vector2(.96f, .92f));
            captureLabel.alignment = TextAlignmentOptions.Center;
            var cancel = Surface(root, "Cancel", CancelArea, Ink);
            var cancelText = Text(cancel.rectTransform, "Cancel Label", font, 33, Color.white,
                new Vector2(.04f, .08f), new Vector2(.96f, .92f));
            cancelText.text = "취소";
            cancelText.alignment = TextAlignmentOptions.Center;
        }

        public void Refresh(string productLabel, bool ready, bool busy, string status)
        {
            product.text = productLabel ?? "";
            heading.text = busy ? "앞면을 등록하고 있어요" : ready ? "상품 앞면 등록" : "주변을 천천히 비춰주세요";
            instruction.text = busy ? "사진의 특징을 확인하고 있어요. 잠시만 기다려주세요."
                : ready ? status : "책상과 주변 무늬가 보이도록 휴대폰을 천천히 움직여주세요.";
            captureLabel.text = busy ? "등록하는 중" : "앞면 촬영";
            captureSurface.color = ready && !busy ? Green : Muted;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image Surface(Transform parent, string name, Rect area, Color color)
        {
            var image = Rect(parent, name, area.min, area.max).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, float size,
            Color color, Vector2 min, Vector2 max)
        {
            var text = Rect(parent, name, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = size * .85f;
            text.fontSizeMax = size;
            text.color = color;
            text.richText = false;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }
    }
}
