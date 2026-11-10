using System;
using System.Collections;
using System.Runtime.InteropServices;
using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kodimodi.UI
{
    public class ProfileController : MonoBehaviour
    {
        [Header("Видео головы")]
        [SerializeField] private RawImage rawImage;
        [SerializeField] private Image rawImageMask;
        [SerializeField] private bool mirrorPreview = true;

        [Header("Popup камеры")]
        [SerializeField] private LeanWindow cameraPopup;
        [SerializeField] private TMP_Text cameraPopupText;
        [SerializeField] private LeanButton enableCameraButton;

        [Header("Создание матча")]
        [SerializeField] private LeanWindow createMatchModal;

        [Header("Инструкция по разрешениям")]
        [SerializeField] private Image permissionHelpImage;
        [SerializeField] private Sprite chromePermissionSprite;
        [SerializeField] private Sprite safariPermissionSprite;
        [SerializeField] private Sprite firefoxPermissionSprite;
        [SerializeField] private Sprite edgePermissionSprite;
        [SerializeField] private Sprite fallbackPermissionSprite;

        private WebCamTexture webcamTexture;
        private Coroutine cameraRoutine;
        private Quaternion initialRotation;

        private bool cameraReady;
        private bool cameraPermissionGranted;
        private bool microphoneGranted;
        private bool mediaPermissionPending;
        private bool playPermissionCheckPending;
        private bool permissionHelpVisible;

        private string browserName = "unknown";

        public bool IsCameraReady =>
            cameraReady &&
            webcamTexture != null &&
            webcamTexture.isPlaying;

        private bool IsMediaReady =>
            cameraPermissionGranted &&
            microphoneGranted &&
            IsCameraReady;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void DetectMediaBrowser(string receiver);

        [DllImport("__Internal")]
        private static extern void CheckCameraPermission(string receiver);

        [DllImport("__Internal")]
        private static extern void RequestCameraAndMicrophone(
            string receiver,
            int requestVideo);

        [DllImport("__Internal")]
        private static extern void StopMediaPermissionWatch(string receiver);
#endif

        private void Awake()
        {
            if (rawImage == null)
            {
                Debug.LogError(
                    "Назначь RawImage головы в ProfileController.",
                    this);

                enabled = false;
                return;
            }

            initialRotation = rawImage.rectTransform.localRotation;

            HideVideo();
            HidePermissionHelp();
            UpdateButtons();
        }

        private void OnEnable()
        {
            if (rawImage == null)
                return;

            cameraPermissionGranted = false;
            microphoneGranted = false;
            mediaPermissionPending = false;
            playPermissionCheckPending = false;

#if UNITY_WEBGL && !UNITY_EDITOR
            DetectMediaBrowser(gameObject.name);
            CheckCameraPermission(gameObject.name);
#else
            // В редакторе достаточно камеры.
            microphoneGranted = true;

            if (Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                cameraPermissionGranted = true;
                StartCamera();
            }
#endif
        }

        public void OnBrowserDetected(string browser)
        {
            if (!isActiveAndEnabled)
                return;

            browserName = browser;

            if (permissionHelpVisible)
                UpdatePermissionHelpImage();
        }

        public void OnMediaPermissionsChecked(string result)
        {
            if (!isActiveAndEnabled)
                return;

            string[] states = result.Split('|');

            if (states.Length != 2)
                return;

            bool cameraUnavailable =
                states[0] == "denied" ||
                states[0] == "prompt";

            bool microphoneUnavailable =
                states[1] == "denied" ||
                states[1] == "prompt";

            bool cameraRevoked =
                (cameraPermissionGranted || IsCameraReady) &&
                cameraUnavailable;

            bool microphoneRevoked =
                microphoneGranted &&
                microphoneUnavailable;

            // unknown не означает запрет:
            // браузер мог не поддержать проверку.
            if (states[0] != "unknown")
                cameraPermissionGranted = states[0] == "granted";

            if (states[1] != "unknown")
                microphoneGranted = states[1] == "granted";

            // Запрещённую камеру останавливаем до проверки готовности.
            if (cameraUnavailable)
                CancelCameraStartupAndStop();

            // Пока открыт запрос устройств, не запускаем второй захват.
            if (mediaPermissionPending)
            {
                UpdateButtons();
                return;
            }

            if (cameraPermissionGranted)
                StartCamera();

            if (playPermissionCheckPending)
            {
                playPermissionCheckPending = false;
                ShowPlayResult();
            }
            else if (cameraRevoked || microphoneRevoked)
            {
                ShowMediaWarning();
            }
            else if (IsMediaReady)
            {
                cameraPopup.TurnOff();
            }

            UpdateButtons();
        }

        public void OnPlayButtonClick()
        {
            if (!isActiveAndEnabled)
                return;

            if (mediaPermissionPending)
            {
                ShowPendingPermissionMessage();
                cameraPopup.TurnOn();
                return;
            }

            if (playPermissionCheckPending)
                return;

#if UNITY_WEBGL && !UNITY_EDITOR
            playPermissionCheckPending = true;
            CheckCameraPermission(gameObject.name);
#else
            ShowPlayResult();
#endif

            UpdateButtons();
        }

        private void ShowPlayResult()
        {
            if (IsMediaReady)
            {
                cameraPopup.TurnOff();
                createMatchModal.TurnOn();
                return;
            }

            if (cameraPermissionGranted && microphoneGranted)
            {
                SetMessage(
                    cameraRoutine != null
                        ? "Ожидаем изображение с камеры…"
                        : "Не удалось запустить камеру.\n\n" +
                          "Нажми «Включить / проверить снова».");

                cameraPopup.TurnOn();
                return;
            }

            ShowMediaWarning();
        }

        public void EnableWebCam()
        {
            if (!isActiveAndEnabled)
                return;

            if (mediaPermissionPending)
            {
                ShowPendingPermissionMessage();
                return;
            }

            if (playPermissionCheckPending || cameraRoutine != null)
                return;

#if UNITY_WEBGL && !UNITY_EDITOR
            mediaPermissionPending = true;

            ShowPendingPermissionMessage();
            UpdateButtons();

            // Если видео уже работает, запрашиваем только микрофон.
            // Это позволяет не открывать камеру повторно.
            RequestCameraAndMicrophone(
                gameObject.name,
                IsCameraReady ? 0 : 1);
#else
            microphoneGranted = true;
            cameraPermissionGranted = true;
            StartCamera();
#endif
        }

        private void ShowPendingPermissionMessage()
        {
            SetMessage(
                "Разреши доступ к камере и микрофону " +
                "в окне браузера.\n\n" +
                "Если окно скрыто, нажми значок камеры " +
                "или настроек сайта слева от адреса.");
        }

        public void OnMediaPermissionRequested(string result)
        {
            if (!isActiveAndEnabled)
                return;

            mediaPermissionPending = false;

            if (result != "granted")
            {
                switch (result)
                {
                    case "NotAllowedError":
                    case "PermissionDeniedError":
                        SetMessage(
                            "Нет доступа к камере или микрофону.\n\n" +
                            "Если разрешения сайта уже включены, " +
                            "проверь доступ браузера к устройствам " +
                            "в настройках системы.");

                        ShowBrowserPermissionHelp();
                        break;

                    case "NotFoundError":
                    case "DevicesNotFoundError":
                        SetMessage(
                            "Камера или микрофон не найдены.\n\n" +
                            "Подключи оба устройства " +
                            "и повтори попытку.");
                        break;

                    case "NotReadableError":
                    case "TrackStartError":
                        SetMessage(
                            "Не удалось включить камеру или микрофон.\n\n" +
                            "Устройство может использоваться " +
                            "другим приложением. Закрой его " +
                            "и повтори попытку.");
                        break;

                    case "unavailable":
                        SetMessage(
                            "Браузер не предоставляет доступ " +
                            "к камере и микрофону.\n\n" +
                            "Открой игру по HTTPS или localhost " +
                            "в поддерживаемом браузере.");
                        break;

                    default:
                        SetMessage(
                            "Не удалось получить доступ " +
                            "к камере и микрофону.\n\n" +
                            "Проверь устройства и повтори попытку.");
                        break;
                }

                cameraPopup.TurnOn();
                UpdateButtons();
                return;
            }

            cameraPermissionGranted = true;
            microphoneGranted = true;

            if (IsMediaReady)
                cameraPopup.TurnOff();
            else
                StartCamera();

            UpdateButtons();
        }

        private void StartCamera()
        {
            if (!isActiveAndEnabled ||
                !cameraPermissionGranted ||
                cameraRoutine != null ||
                IsCameraReady)
            {
                return;
            }

            cameraRoutine = StartCoroutine(EnableWebCamRoutine());
            UpdateButtons();
        }

        private IEnumerator EnableWebCamRoutine()
        {
            // Даём StartCoroutine вернуть значение в cameraRoutine
            // до возможного завершения корутины.
            yield return null;

            StopCamera();

            try
            {
                if (!cameraPermissionGranted)
                    yield break;

                if (!Application.HasUserAuthorization(
                        UserAuthorization.WebCam))
                {
                    yield return Application.RequestUserAuthorization(
                        UserAuthorization.WebCam);
                }

                if (!cameraPermissionGranted)
                    yield break;

                if (!Application.HasUserAuthorization(
                        UserAuthorization.WebCam))
                {
                    cameraPermissionGranted = false;
                    ShowMediaWarning();
                    yield break;
                }

                string error = TryStartCamera();

                if (error != null)
                {
                    SetMessage(error);
                    cameraPopup.TurnOn();
                    yield break;
                }

                SetMessage(
                    microphoneGranted
                        ? "Ожидаем изображение с камеры…"
                        : "Запускаем камеру…\n\n" +
                          "Для игры также нужен доступ к микрофону.");

                float deadline = Time.realtimeSinceStartup + 15f;

                while (webcamTexture != null &&
                       (!webcamTexture.didUpdateThisFrame ||
                        webcamTexture.width <= 16 ||
                        webcamTexture.height <= 16))
                {
                    if (!cameraPermissionGranted)
                        yield break;

                    if (Time.realtimeSinceStartup >= deadline)
                    {
                        StopCamera();

                        SetMessage(
                            "Камера не начала передавать видео.\n\n" +
                            "Проверь устройство и попробуй закрыть " +
                            "другие приложения, использующие камеру.");

                        cameraPopup.TurnOn();
                        yield break;
                    }

                    yield return null;
                }

                if (webcamTexture == null || !cameraPermissionGranted)
                    yield break;

                rawImage.texture = webcamTexture;
                rawImage.color = Color.white;

                if (rawImageMask != null)
                    rawImageMask.color = Color.white;

                UpdateVideoLayout();

                rawImage.enabled = true;
                cameraReady = true;

                if (IsMediaReady)
                {
                    cameraPopup.TurnOff();
                }
                else
                {
                    // Обновляем текст, но не открываем popup
                    // автоматически при обычном входе в сцену.
                    SetMessage(
                        "Камера работает, но для игры " +
                        "также нужен доступ к микрофону.");

                    ShowBrowserPermissionHelp();
                }
            }
            finally
            {
                cameraRoutine = null;
                UpdateButtons();
            }
        }

        private string TryStartCamera()
        {
            try
            {
                webcamTexture = new WebCamTexture(640, 480, 20);
                webcamTexture.Play();
                return null;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                StopCamera();

                return
                    "Не удалось запустить камеру.\n\n" +
                    "Проверь устройство и разрешение браузера.";
            }
        }

        private void Update()
        {
            if (!cameraReady || webcamTexture == null)
                return;

            if (!webcamTexture.isPlaying)
            {
                StopCamera();
                UpdateButtons();

                SetMessage(
                    "Камера остановлена.\n\n" +
                    "Нажми «Включить / проверить снова».");

                cameraPopup.TurnOn();
                return;
            }

            if (webcamTexture.didUpdateThisFrame)
                UpdateVideoLayout();
        }

        private void UpdateVideoLayout()
        {
            float width = webcamTexture.width;
            float height = webcamTexture.height;

            if (width <= 16 || height <= 16)
                return;

            rawImage.rectTransform.localRotation =
                initialRotation *
                Quaternion.Euler(
                    0f, 0f, -webcamTexture.videoRotationAngle);

            float cropWidth = Mathf.Min(1f, height / width);
            float cropHeight = Mathf.Min(1f, width / height);

            float x = (1f - cropWidth) * 0.5f;
            float y = (1f - cropHeight) * 0.5f;

            if (mirrorPreview)
            {
                x += cropWidth;
                cropWidth = -cropWidth;
            }

            if (webcamTexture.videoVerticallyMirrored)
            {
                y += cropHeight;
                cropHeight = -cropHeight;
            }

            rawImage.uvRect =
                new Rect(x, y, cropWidth, cropHeight);
        }

        public void OnContinueButtonClick()
        {
            OnPlayButtonClick();
        }

        public void OnCancelCameraButtonClick()
        {
            // Закрытие popup не останавливает разрешённую камеру.
            cameraPopup.TurnOff();
        }

        private void ShowMediaWarning()
        {
            string message;

            if (!cameraPermissionGranted && !microphoneGranted)
            {
                message =
                    "Твоё лицо — твой персонаж!\n\n" +
                    "Для игры нужен доступ к камере и микрофону.";
            }
            else if (!cameraPermissionGranted)
            {
                message = "Для игры нужен доступ к камере.";
            }
            else
            {
                message =
                    "Для игры также нужен доступ к микрофону.";
            }

            SetMessage(message);
            ShowBrowserPermissionHelp();

            cameraPopup.TurnOn();
        }

        private string GetBrowserPermissionInstructions()
        {
            switch (browserName)
            {
                case "chrome":
                    return
                        "В Chrome нажми значок настроек сайта " +
                        "слева от адреса.\n" +
                        "Разреши «Камеру» и «Микрофон». " +
                        "Если этих пунктов нет — " +
                        "открой «Настройки сайтов».";

                case "edge":
                    return
                        "В Edge нажми значок сведений о сайте " +
                        "слева от адреса.\n" +
                        "Открой разрешения для этого сайта " +
                        "и разреши камеру и микрофон.";

                case "firefox":
                    return
                        "В Firefox нажми значок разрешений " +
                        "слева от адреса.\n" +
                        "Удали сохранённую блокировку камеры " +
                        "и микрофона, затем повтори попытку " +
                        "и разреши доступ.";

                case "safari":
                    return
                        "В Safari открой меню " +
                        "Safari → Настройки → Веб-сайты.\n" +
                        "В разделах «Камера» и «Микрофон» " +
                        "выбери «Разрешить» для сайта игры.";

                default:
                    return
                        "Открой настройки или разрешения сайта " +
                        "в своём браузере и разреши " +
                        "камеру и микрофон.";
            }
        }

        private void ShowBrowserPermissionHelp()
        {
#if !UNITY_EDITOR
    if (permissionHelpVisible)
        return;

    permissionHelpVisible = true;

    if (cameraPopupText != null)
    {
        cameraPopupText.text +=
            "\n\n" + GetBrowserPermissionInstructions() +
            "\n\nНажми «Включить / проверить снова»." +
            "\nЕсли браузер просит обновить страницу — обнови её.";
    }

    UpdatePermissionHelpImage();
#endif
        }

        private void UpdatePermissionHelpImage()
        {
            if (permissionHelpImage == null)
                return;

            Sprite sprite;

            switch (browserName)
            {
                case "chrome":
                    sprite = chromePermissionSprite;
                    break;
                case "safari":
                    sprite = safariPermissionSprite;
                    break;
                case "firefox":
                    sprite = firefoxPermissionSprite;
                    break;
                case "edge":
                    sprite = edgePermissionSprite;
                    break;
                default:
                    sprite = fallbackPermissionSprite;
                    break;
            }

            if (sprite == null)
                sprite = fallbackPermissionSprite;

            permissionHelpImage.sprite = sprite;
            permissionHelpImage.preserveAspect = true;
            permissionHelpImage.gameObject.SetActive(
                permissionHelpVisible && sprite != null);
        }

        private void HidePermissionHelp()
        {
            permissionHelpVisible = false;

            if (permissionHelpImage != null)
                permissionHelpImage.gameObject.SetActive(false);
        }

        private void SetMessage(string message)
        {
            HidePermissionHelp();

            if (cameraPopupText != null)
                cameraPopupText.text = message;
        }

        private void UpdateButtons()
        {
            if (enableCameraButton == null)
                return;

            enableCameraButton.gameObject.SetActive(
                !playPermissionCheckPending &&
                !mediaPermissionPending &&
                cameraRoutine == null &&
                !IsMediaReady);
        }

        private void HideVideo()
        {
            if (rawImage != null)
            {
                rawImage.enabled = false;
                rawImage.texture = null;
                rawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                rawImage.rectTransform.localRotation = initialRotation;
            }

            if (rawImageMask != null)
                rawImageMask.color = Color.black;
        }

        private void CancelCameraStartupAndStop()
        {
            if (cameraRoutine != null)
            {
                StopCoroutine(cameraRoutine);
                cameraRoutine = null;
            }

            StopCamera();
        }

        private void StopCamera()
        {
            cameraReady = false;
            HideVideo();

            if (webcamTexture == null)
                return;

            webcamTexture.Stop();
            Destroy(webcamTexture);
            webcamTexture = null;
        }

        private void OnDisable()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (rawImage != null)
                StopMediaPermissionWatch(gameObject.name);
#endif

            playPermissionCheckPending = false;
            mediaPermissionPending = false;
            cameraPermissionGranted = false;
            microphoneGranted = false;

            CancelCameraStartupAndStop();
            HidePermissionHelp();
            UpdateButtons();
        }
    }
}