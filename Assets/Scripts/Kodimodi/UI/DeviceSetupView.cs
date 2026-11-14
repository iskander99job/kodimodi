using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kodimodi.UI
{
    public class DeviceSetupView : MonoBehaviour
    {
        [Header("Камера")]
        [SerializeField] private TMP_Text cameraStatus;
        [SerializeField] private TMP_Text cameraDescription;
        [SerializeField] private Image cameraBadge;

        [Header("Микрофон")]
        [SerializeField] private TMP_Text microphoneStatus;
        [SerializeField] private TMP_Text microphoneDescription;
        [SerializeField] private Image microphoneBadge;

        [Header("Основная кнопка")]
        [SerializeField] private LeanButton enableButton;
        [SerializeField] private TMP_Text enableButtonText;

        private ProfileController profile;
        private MediaAccessController mediaAccess;
        private PermissionHelpView permissionHelp;
        private ConsentPopupController consentController;

        private static readonly Color InactiveBadge =
            new Color32(225, 225, 225, 255);

        private void Awake()
        {
            consentController = FindAnyObjectByType<ConsentPopupController>(
                FindObjectsInactive.Include);
        }
        
        public void Bind(
            ProfileController profileController,
            MediaAccessController accessController,
            PermissionHelpView helpView)
        {
            profile = profileController;
            mediaAccess = accessController;
            permissionHelp = helpView;

            if (isActiveAndEnabled)
                Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (profile == null ||
                !profile.isActiveAndEnabled ||
                mediaAccess == null ||
                !mediaAccess.isActiveAndEnabled)
            {
                return;
            }

            RefreshCamera();
            RefreshMicrophone();
            RefreshButton();
        }

        private void RefreshCamera()
        {
            bool ready = profile.IsCameraReady;

            string status;
            string description;

            if (ready)
            {
                status = "Включена";
                description = "Видео работает";
            }
            else if (profile.CameraStarting)
            {
                status = "Запуск…";
                description = "Ожидаем изображение с камеры";
            }
            else if (mediaAccess.RequestPending)
            {
                status = "Ожидание";
                description = "Разреши доступ в браузере";
            }
            else if (!mediaAccess.Initialized)
            {
                status = "Проверяем…";
                description = "Проверяем разрешение камеры";
            }
            else
            {
                status = "Не включена";
                description = mediaAccess.CameraGranted
                    ? "Доступ есть, видео ещё не запущено"
                    : "Доступ ещё не предоставлен";
            }

            SetText(cameraStatus, status);
            SetText(cameraDescription, description);
            SetBadge(cameraBadge, cameraStatus, ready);
        }

        private void RefreshMicrophone()
        {
            bool granted = mediaAccess.MicrophoneGranted;

            string status;
            string description;

            if (granted)
            {
                status = "Доступ есть";
                description = "Разрешение на микрофон получено";
            }
            else if (mediaAccess.RequestPending)
            {
                status = "Ожидание";
                description = "Разреши доступ в браузере";
            }
            else if (!mediaAccess.Initialized)
            {
                status = "Проверяем…";
                description = "Проверяем разрешение микрофона";
            }
            else
            {
                status = "Не включён";
                description = "Доступ ещё не предоставлен";
            }

            SetText(microphoneStatus, status);
            SetText(microphoneDescription, description);
            SetBadge(microphoneBadge, microphoneStatus, granted);
        }

        private void RefreshButton()
        {
            if (enableButton != null)
            {
                enableButton.interactable =
                    !profile.DeviceOperationPending &&
                    !profile.MediaReady;
            }

            string label;

            if (mediaAccess.RequestPending)
                label = "Разреши доступ в браузере…";
            else if (profile.CameraStarting)
                label = "Включаем камеру…";
            else if (profile.MediaReady)
                label = "Устройства готовы";
            else
                label = "Включить камеру и микрофон";

            SetText(enableButtonText, label);
        }

        public void OnEnableDevicesClick()
        {
            if (consentController == null || !consentController.CheckConsent())
                return;
            
            if (profile != null &&
                profile.isActiveAndEnabled &&
                !profile.DeviceOperationPending)
            {
                profile.EnableWebCam();
            }
        }

        public void OnHelpClick()
        {
            if (permissionHelp != null)
                permissionHelp.Show();
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null && target.text != value)
                target.text = value;
        }

        private static void SetBadge(
            Image background,
            TMP_Text label,
            bool ready)
        {
            if (background != null)
                background.color = ready ? Color.black : InactiveBadge;

            if (label != null)
                label.color = ready ? Color.white : Color.black;
        }
    }
}