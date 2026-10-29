#if UNITY_EDITOR || DEVELOPMENT_BUILD
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MidtermAR
{
    // 안내 전용 화면. 입력은 probe의 Input System 경로 하나에서 처리한다.
    public sealed class RuntimeImageRegistrationProbeView : MonoBehaviour
    {
        private static readonly Color Ink = new Color(.08f, .15f, .19f, .96f);
        private static readonly Color Green = new Color(.11f, .43f, .32f);
        private static readonly Color Muted = new Color(.27f, .34f, .36f);
        private static readonly Color Paper = new Color(.98f, .97f, .93f);
        private TextMeshProUGUI title, instruction, progress, captureLabel, resetLabel, consequence;
        private Image captureSurface, resetSurface;
        private RawImage preview;
        private AspectRatioFitter previewAspect;
        private Image[] guideEdges;

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
            var header = Surface(root, "Instructions", new Vector2(.025f, .775f), new Vector2(.975f, .975f), Ink);
            title = Text(header.rectTransform, "Title", font, 46, Color.white, new Vector2(.035f, .64f), new Vector2(.965f, .96f));
            instruction = Text(header.rectTransform, "Instruction", font, 32, Color.white, new Vector2(.035f, .18f), new Vector2(.965f, .66f));
            progress = Text(header.rectTransform, "Progress", font, 26, new Color(.82f, .91f, .86f), new Vector2(.035f, .025f), new Vector2(.965f, .19f));

            var guide = RuntimeImageRegistrationProbe.GuideViewport;
            var guideRoot = Rect(root, "Front Guide", guide.min, guide.max);
            guideEdges = new[] {
                Surface(guideRoot, "Top", new Vector2(0, 1), Vector2.one, Color.yellow),
                Surface(guideRoot, "Bottom", Vector2.zero, Vector2.right, Color.yellow),
                Surface(guideRoot, "Left", Vector2.zero, Vector2.up, Color.yellow),
                Surface(guideRoot, "Right", Vector2.right, Vector2.one, Color.yellow)
            };
            guideEdges[0].rectTransform.sizeDelta = guideEdges[1].rectTransform.sizeDelta = new Vector2(0, 4);
            guideEdges[2].rectTransform.sizeDelta = guideEdges[3].rectTransform.sizeDelta = new Vector2(4, 0);

            var previewArea = Surface(root, "Captured Front", new Vector2(.025f, .045f), new Vector2(.235f, .22f), Ink);
            var previewFrame = Rect(previewArea.transform, "Preview Frame", new Vector2(.04f, .16f), new Vector2(.96f, .98f));
            var previewRect = Rect(previewFrame, "Preview", Vector2.zero, Vector2.one);
            preview = previewRect.gameObject.AddComponent<RawImage>();
            preview.raycastTarget = false;
            previewAspect = previewRect.gameObject.AddComponent<AspectRatioFitter>();
            previewAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            var previewText = Text(previewArea.rectTransform, "Preview Label", font, 24, Paper,
                new Vector2(.04f, .01f), new Vector2(.96f, .16f));
            previewText.text = "촬영한 앞면";
            previewText.alignment = TextAlignmentOptions.Center;

            captureSurface = Surface(root, "Capture", new Vector2(.27f, .125f), new Vector2(.96f, .2f), Green);
            captureLabel = Text(captureSurface.rectTransform, "Capture Label", font, 37, Color.white, new Vector2(.04f, .08f), new Vector2(.96f, .92f));
            captureLabel.alignment = TextAlignmentOptions.Center;
            resetSurface = Surface(root, "Reset", new Vector2(.27f, .045f), new Vector2(.96f, .11f), Ink);
            resetLabel = Text(resetSurface.rectTransform, "Reset Label", font, 33, Color.white, new Vector2(.04f, .08f), new Vector2(.96f, .92f));
            resetLabel.alignment = TextAlignmentOptions.Center;
            consequence = Text(root, "Reset Consequence", font, 21, Color.white,
                new Vector2(.27f, .004f), new Vector2(.96f, .041f));
            consequence.alignment = TextAlignmentOptions.Center;
        }

        public void Refresh(bool ready, bool adding, bool capturing, RuntimeImageRegistrationEvidence evidence,
            string fallback, Texture2D captured, bool canCapture, bool canReset, bool trialLocked)
        {
            var registered = !string.IsNullOrEmpty(evidence.ImageName);
            title.text = trialLocked && !registered ? "새 촬영은 앱 재시작 후에 해주세요" : Heading(ready, adding, capturing, evidence);
            instruction.text = trialLocked && !registered
                ? "앞면 등록 뒤 이번 확인이 종료됐어요. 중복 등록을 막기 위해 앱을 완전히 종료한 뒤 다시 실행해주세요."
                : Guidance(ready, adding, capturing, evidence, fallback);
            progress.text = trialLocked && !registered ? "이번 확인 종료 · 새 촬영은 앱 재시작 후 가능"
                : evidence.Reacquired ? "등록 완료 · 처음 인식 완료 · 재인식 완료"
                : evidence.FirstTracking ? "등록 완료 · 처음 인식 완료 · 재인식 대기"
                : registered ? "등록 완료 · 앞면 인식 대기" : "앞면 촬영 · 등록 · 다시 인식 순서로 확인합니다.";
            captureLabel.text = adding || capturing ? "등록하는 중" : trialLocked ? "앞면 등록됨" : "앞면 촬영";
            resetLabel.text = trialLocked ? "새 촬영은 앱 재시작 후" : "촬영 취소";
            consequence.text = trialLocked ? "중복 등록을 막기 위해 이번 실행에서는 한 번만 촬영합니다."
                : "촬영한 뒤에는 같은 앞면을 계속 비춰주세요.";
            captureSurface.color = canCapture ? Green : Muted;
            resetSurface.color = canReset ? Ink : Muted;
            foreach (var edge in guideEdges) edge.color = evidence.Visible ? new Color(.25f, .9f, .5f) : new Color(1f, .81f, .33f);
            preview.texture = captured;
            preview.enabled = captured != null;
            if (captured != null) previewAspect.aspectRatio = (float)captured.width / captured.height;
        }

        public static string Heading(bool ready, bool adding, bool capturing, RuntimeImageRegistrationEvidence evidence)
        {
            if (!ready) return "주변을 천천히 비춰주세요";
            if (adding || capturing) return "앞면을 등록하고 있어요";
            if (evidence.Reacquired) return "다시 인식했어요";
            if (evidence.ObservedLoss) return "같은 앞면을 다시 비춰주세요";
            if (evidence.FirstTracking) return "상품을 화면 밖으로 빼주세요";
            return string.IsNullOrEmpty(evidence.ImageName) ? "상품 앞면을 틀에 맞춰주세요" : "등록됐어요. 앞면을 찾고 있어요";
        }

        public static string Guidance(bool ready, bool adding, bool capturing, RuntimeImageRegistrationEvidence evidence, string fallback)
        {
            if (!ready) return string.IsNullOrEmpty(evidence.ImageName)
                ? "책상과 주변 무늬가 보이도록 휴대폰을 천천히 움직여주세요. 카메라 준비가 끝나면 촬영할 수 있어요."
                : "앞면 사진은 등록됐지만 공간 추적이 준비되지 않았어요. 다시 촬영하지 말고 책상과 주변을 천천히 비춰주세요.";
            if (adding || capturing) return "사진의 인식 가능 여부를 확인하고 있어요. 버튼을 다시 누르지 않아도 됩니다.";
            if (evidence.Reacquired) return "화면 밖으로 뺐던 같은 앞면을 다시 인식했어요. 크기와 위치 추적 기록도 남겼어요.";
            if (evidence.ObservedLoss) return "상품이 사라진 것을 확인했어요. 촬영 버튼을 누르지 말고 방금 등록한 앞면을 다시 보여주세요.";
            if (evidence.FirstTracking) return "앞면 인식과 크기 추정을 확인했어요. 상품을 완전히 화면 밖으로 뺀 뒤 다음 안내를 기다려주세요.";
            if (!string.IsNullOrEmpty(evidence.ImageName)) return "다시 촬영하지 말고 같은 앞면을 계속 비춰주세요. 휴대폰을 천천히 움직이면 위치와 크기를 추정해요.";
            return fallback;
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

        private static Image Surface(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var image = Rect(parent, name, min, max).gameObject.AddComponent<Image>();
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
#endif
