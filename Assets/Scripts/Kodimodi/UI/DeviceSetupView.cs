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
                status = "On";
                description = "Video is working";
            }
            else if (profile.CameraStarting)
            {
                status = "Starting…";
                description = "Waiting for camera video";
            }
            else if (mediaAccess.RequestPending)
            {
                status = "Waiting";
                description = "Allow access in your browser";
            }
            else if (!mediaAccess.Initialized)
            {
                status = "Checking…";
                description = "Checking camera permission";
            }
            else
            {
                status = "Off";
                description = mediaAccess.CameraGranted
                    ? "Permission granted, video has not started yet"
                    : "Permission not granted yet";
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
                status = "Access granted";
                description = "Microphone permission granted";
            }
            else if (mediaAccess.RequestPending)
            {
                status = "Waiting";
                description = "Allow access in your browser";
            }
            else if (!mediaAccess.Initialized)
            {
                status = "Checking…";
                description = "Checking microphone permission";
            }
            else
            {
                status = "Off";
                description = "Permission not granted yet";
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
                label = "Allow access in your browser…";
            else if (profile.CameraStarting)
                label = "Starting camera…";
            else if (profile.MediaReady)
                label = "Devices ready";
            else
                label = "Enable camera and microphone";

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