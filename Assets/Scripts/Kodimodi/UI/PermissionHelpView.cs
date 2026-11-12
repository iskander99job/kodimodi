using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kodimodi.UI
{
    [RequireComponent(typeof(LeanWindow))]
    public class PermissionHelpView : MonoBehaviour
    {
        [Header("Содержимое")]
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Image instructionImage;

        [Header("Инструкции браузеров")]
        [SerializeField] private Sprite chromeSprite;
        [SerializeField] private Sprite safariSprite;
        [SerializeField] private Sprite firefoxSprite;
        [SerializeField] private Sprite edgeSprite;
        [SerializeField] private Sprite fallbackSprite;

        private LeanWindow window;
        private string browserName = "unknown";
        private string statusMessage = "";
        private bool visible;

        private LeanWindow Window
        {
            get
            {
                if (window == null)
                    window = GetComponent<LeanWindow>();

                return window;
            }
        }

        public void SetBrowser(string browser)
        {
            browserName = browser;

            if (visible)
                Refresh();
        }

        public void SetMessage(string message)
        {
            statusMessage = message;

            // Обновляем содержимое, но не открываем popup.
            if (visible)
                Refresh();
        }

        public void Show()
        {
            visible = true;
            Refresh();
            Window.TurnOn();
        }

        public void Hide()
        {
            visible = false;
            Window.TurnOff();
        }

        private void Refresh()
        {
            if (messageText != null)
            {
                string instructions = GetInstructions();

                messageText.text =
                    string.IsNullOrEmpty(statusMessage)
                        ? instructions
                        : statusMessage + "\n\n" + instructions;
            }

            if (instructionImage == null)
                return;

            Sprite sprite = GetBrowserSprite();

            instructionImage.sprite = sprite;
            instructionImage.preserveAspect = true;
            instructionImage.gameObject.SetActive(sprite != null);
        }

        private Sprite GetBrowserSprite()
        {
            Sprite sprite;

            switch (browserName)
            {
                case "chrome":
                    sprite = chromeSprite;
                    break;

                case "safari":
                    sprite = safariSprite;
                    break;

                case "firefox":
                    sprite = firefoxSprite;
                    break;

                case "edge":
                    sprite = edgeSprite;
                    break;

                default:
                    sprite = fallbackSprite;
                    break;
            }

            return sprite != null ? sprite : fallbackSprite;
        }

        private string GetInstructions()
        {
            switch (browserName)
            {
                case "chrome":
                    return
                        "В Chrome нажми значок настроек сайта " +
                        "слева от адреса.\n\n" +
                        "Разреши «Камеру» и «Микрофон». " +
                        "Если этих пунктов нет — открой «Настройки сайтов».\n\n" +
                        "Затем повтори включение устройств.";

                case "edge":
                    return
                        "В Edge нажми значок сведений о сайте " +
                        "слева от адреса.\n\n" +
                        "Открой разрешения для сайта и разреши " +
                        "камеру и микрофон.\n\n" +
                        "Затем повтори включение устройств.";

                case "firefox":
                    return
                        "В Firefox нажми значок разрешений " +
                        "слева от адреса.\n\n" +
                        "Удали сохранённую блокировку камеры и микрофона.\n\n" +
                        "Повтори включение устройств и разреши доступ.";

                case "safari":
                    return
                        "В Safari открой:\n" +
                        "Safari → Настройки → Веб-сайты.\n\n" +
                        "В разделах «Камера» и «Микрофон» выбери " +
                        "«Разрешить» для сайта игры.\n\n" +
                        "Затем повтори включение устройств.";

                default:
                    return
                        "Открой настройки или разрешения сайта " +
                        "в браузере и разреши камеру и микрофон.\n\n" +
                        "Затем повтори включение устройств.";
            }
        }
    }
}