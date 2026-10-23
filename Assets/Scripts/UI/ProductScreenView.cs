using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MidtermAR
{
    public sealed class ProductScreenView : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusTitle;
        [SerializeField] private TMP_Text statusMessage;
        [SerializeField] private TMP_Text productSummary;
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private Button primaryButton;
        [SerializeField] private Button moveButton;
        [SerializeField] private Button deleteButton;

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
