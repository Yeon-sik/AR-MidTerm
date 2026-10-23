using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MidtermAR
{
    public sealed class ProductScreenView : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusTitle; // 현재 단계와 다음 행동 안내
        [SerializeField] private TMP_Text statusMessage;
        [SerializeField] private TMP_Text productSummary; // 조회된 상품명·규격 요약
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private Button primaryButton; // 재스캔·권한 설정 등 기본 동작
        [SerializeField] private Button moveButton; // 기존 정보판 재배치
        [SerializeField] private Button deleteButton; // 배치된 정보판 삭제

        public void Show(string title, string message, string summary, string action, bool primaryEnabled, bool moveEnabled, bool deleteEnabled, bool error)
        {
            statusTitle.text = title;
            statusTitle.color = error ? new Color(1f, 0.65f, 0.58f) : Color.white;
            statusMessage.text = message;
            productSummary.text = summary;
            primaryLabel.text = action;
            primaryButton.interactable = primaryEnabled;
            moveButton.interactable = moveEnabled;
            deleteButton.interactable = deleteEnabled;
        }
    }
}
