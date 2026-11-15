using System.Linq;
using Lean.Gui;
using TMPro;
using UnityEngine;

namespace Kodimodi.UI.Profile
{
    [RequireComponent(typeof(LeanWindow))]
    [RequireComponent(typeof(CanvasGroup))]
    public class SuccessPopup : MonoBehaviour
    {
        [Header("Текст")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;

        private LeanWindow window;

        private void Awake()
        {
            Initialize();
            window.TurnOff();
        }

        private void Initialize()
        {
            if (window == null)
                window = GetComponent<LeanWindow>();

            if (titleText == null)
                titleText = FindPanelText("Title");

            if (messageText == null)
                messageText = FindPanelText("Message");
        }

        private TMP_Text FindPanelText(string objectName)
        {
            var panel = transform.Find("Panel");

            if (panel == null)
                return null;

            var candidates = panel
                .GetComponentsInChildren<TMP_Text>(true)
                .Where(text =>
                    text.transform.parent == panel &&
                    text.gameObject.name == objectName)
                .ToArray();

            // При наличии дублей предпочитаем включённый текст.
            return candidates.FirstOrDefault(
                       text => text.gameObject.activeSelf)
                   ?? candidates.FirstOrDefault();
        }

        public static void Show(
            string message,
            string title = "Success")
        {
            var popup = FindAnyObjectByType<SuccessPopup>(
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
            // Активация вызывает Awake, если объект
            // изначально был выключен.
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            Initialize();

            SetText(titleText, title);
            SetText(messageText, message);

            if (titleText == null || messageText == null)
            {
                Debug.LogError(
                    "ErrorPopup: не найдены Title или Message.",
                    this);
            }

            transform.SetAsLastSibling();
            window.TurnOn();
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target == null)
                return;

            target.gameObject.SetActive(true);
            target.richText = false;
            target.text = value ?? "";
        }

        public void Close()
        {
            Initialize();
            window.TurnOff();
        }
    }
}