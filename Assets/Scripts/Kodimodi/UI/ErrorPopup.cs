using Lean.Gui;
using TMPro;
using UnityEngine;

namespace Kodimodi.UI
{
    [RequireComponent(typeof(LeanWindow))]
    public class ErrorPopup : MonoBehaviour
    {
        [Header("Текст")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;

        private LeanWindow window;

        public static void Show(
            string message,
            string title = "Ошибка")
        {
            ErrorPopup popup = FindAnyObjectByType<ErrorPopup>(
                FindObjectsInactive.Include);

            if (popup == null)
            {
                Debug.LogError(
                    $"ErrorPopup не найден. {title}: {message}");

                return;
            }

            popup.Open(message, title);
        }

        private void Open(string message, string title)
        {
            if (window == null)
                window = GetComponent<LeanWindow>();

            if (titleText != null)
                titleText.text = title;

            if (messageText != null)
            {
                messageText.richText = false;
                messageText.text = message;
            }

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            // Popup должен находиться поверх остальных элементов Canvas.
            transform.SetAsLastSibling();

            window.TurnOn();
        }

        public void Close()
        {
            if (window == null)
                window = GetComponent<LeanWindow>();

            if (window != null)
                window.TurnOff();
        }
    }
}