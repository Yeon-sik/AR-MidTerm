using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MidtermAR
{
    public sealed class ProductScreenView : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusStep;
        [SerializeField] private TMP_Text statusTitle; // 현재 단계와 다음 행동 안내
        [SerializeField] private TMP_Text statusMessage;
        [SerializeField] private TMP_Text productSummary; // 조회된 상품명·규격 요약
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private Button primaryButton; // 재스캔·권한 설정 등 기본 동작
        [SerializeField] private Button moveButton; // 기존 정보판 재배치
        [SerializeField] private Button deleteButton; // 배치된 정보판 삭제
        [SerializeField] private TMP_Text moveLabel;
        [SerializeField] private RectTransform instructionPanel;
        [SerializeField] private RectTransform buttons;

        public void Show(string step, string title, string message, string summary, string action,
            bool primaryEnabled, bool moveEnabled, bool deleteEnabled, bool error, bool attention = false, string moveAction = "재배치", bool secondaryAction = false)
        {
            statusStep.text = step;
            statusStep.color = error ? new Color(1f, 0.65f, 0.58f)
                : attention ? new Color(1f, 0.82f, 0.43f) : new Color(0.48f, 0.9f, 0.72f);
            statusTitle.text = title;
            statusTitle.color = Color.white;
            statusMessage.text = message;
            productSummary.text = summary;
            primaryLabel.text = action;
            moveLabel.text = moveAction;
            // 사용 가능한 동작만 표시한다. 숨긴 버튼 자리도 레이아웃에서 제외된다.
            primaryButton.gameObject.SetActive(primaryEnabled);
            moveButton.gameObject.SetActive(moveEnabled);
            deleteButton.gameObject.SetActive(deleteEnabled);
            primaryButton.interactable = primaryEnabled;
            moveButton.interactable = moveEnabled;
            deleteButton.interactable = deleteEnabled;
            var hasProduct = !string.IsNullOrEmpty(summary);
            primaryButton.targetGraphic.color = (hasProduct || secondaryAction) && !error ? new Color(0.2f, 0.3f, 0.34f) : (Color)new Color32(37, 126, 92, 255);
            productSummary.gameObject.SetActive(hasProduct);
            var cursor = 20f;
            if (hasProduct)
            {
                Position((RectTransform)productSummary.transform, cursor, 44);
                cursor += 54;
            }
            var width = Mathf.Max(1f, instructionPanel.rect.width - 48);
            var hasMessage = !string.IsNullOrEmpty(message);
            statusMessage.gameObject.SetActive(hasMessage);
            var messageHeight = hasMessage ? Mathf.Max(44f, statusMessage.GetPreferredValues(message, width, Mathf.Infinity).y) : 0;
            Position((RectTransform)statusMessage.transform, cursor, messageHeight);
            cursor += messageHeight;
            var hasActions = primaryEnabled || moveEnabled || deleteEnabled;
            buttons.gameObject.SetActive(hasActions);
            if (hasActions)
            {
                if (hasMessage || hasProduct) cursor += 18;
                Position(buttons, cursor, 132);
                cursor += 132;
            }
            instructionPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cursor + 20);
        }

        private static void Position(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(24, -top - height);
            rect.offsetMax = new Vector2(-24, -top);
        }
    }
}
