using System.Collections;
using Lean.Gui;
using UnityEngine;

namespace Kodimodi.UI
{
    public class ProfileController : MonoBehaviour
    {
        [Header("Главный экран")]
        [SerializeField] private GameObject playButton;
        [SerializeField] private GameObject deviceSetupUI;
        [SerializeField] private GameObject matchLobbyUI;

        [Header("Создание матча")]
        [SerializeField] private LeanWindow createMatchModal;

        private MediaAccessController mediaAccess;
        private WebcamPreviewController webcamPreview;
        private PermissionHelpView permissionHelp;
        private LobbyClient lobbyClient;
        private DeviceSetupView deviceView;
        private ConsentPopupController consentController;

        private Coroutine cameraRoutine;

        private bool stopping;
        private bool playRequested;
        private bool devicesRequested;
        private bool mediaWasReady;

        public bool IsCameraReady =>
            webcamPreview != null &&
            webcamPreview.isActiveAndEnabled &&
            webcamPreview.IsReady;

        public bool MediaReady =>
            Livekit.SkipMediaChecks ||
            (mediaAccess != null &&
             mediaAccess.isActiveAndEnabled &&
             mediaAccess.CameraGranted &&
             mediaAccess.MicrophoneGranted &&
             IsCameraReady);

        public bool CameraStarting => cameraRoutine != null;

        public bool DeviceOperationPending =>
            CameraStarting ||
            (mediaAccess != null && mediaAccess.RequestPending);

        private bool CanUpdateUI =>
            !stopping &&
            isActiveAndEnabled &&
            mediaAccess != null &&
            mediaAccess.isActiveAndEnabled &&
            webcamPreview != null &&
            webcamPreview.isActiveAndEnabled &&
            lobbyClient != null &&
            lobbyClient.isActiveAndEnabled;

        private void Awake()
        {
            consentController = FindAnyObjectByType<ConsentPopupController>(
                FindObjectsInactive.Include);

            mediaAccess =
                FindAnyObjectByType<MediaAccessController>();

            webcamPreview =
                FindAnyObjectByType<WebcamPreviewController>();

            lobbyClient =
                FindAnyObjectByType<LobbyClient>();

            permissionHelp =
                FindAnyObjectByType<PermissionHelpView>(
                    FindObjectsInactive.Include);

            if (deviceSetupUI != null)
                deviceView = deviceSetupUI.GetComponent<DeviceSetupView>();

            if (mediaAccess == null ||
                webcamPreview == null ||
                lobbyClient == null ||
                deviceView == null)
            {
                Debug.LogError(
                    "ProfileController: проверь активные MediaAccess, " +
                    "WebcamPreview, Lobby и компонент DeviceSetupView " +
                    "на объекте DeviceSetupUI.",
                    this);

                enabled = false;
                return;
            }

            // Передаём зависимости, даже если DeviceSetupUI выключен.
            deviceView.Bind(this, mediaAccess, permissionHelp);

            if (createMatchModal != null)
                createMatchModal.TurnOff();

            // При обычном входе сначала показывается Play.
            SetVisible(deviceSetupUI, false);
            SetVisible(matchLobbyUI, false);
            SetVisible(playButton, true);
        }

        private void OnEnable()
        {
            if (mediaAccess == null ||
                webcamPreview == null ||
                lobbyClient == null ||
                deviceView == null)
            {
                return;
            }

            stopping = false;
            playRequested = false;
            devicesRequested = false;
            mediaWasReady = false;

            mediaAccess.Changed += OnMediaChanged;
            mediaAccess.RequestCompleted += OnRequestCompleted;

            OnMediaChanged();
        }

        private void Update()
        {
            if (!CanUpdateUI)
                return;

            if (webcamPreview.HasFrame && !webcamPreview.IsReady)
            {
                webcamPreview.StopPreview();

                if (!lobbyClient.IsLobbyVisible)
                    devicesRequested = true;

                SetMessage(
                    "The camera has stopped.\n\n" +
                    "Try enabling the camera and microphone again.");

                RefreshEntryUI();
            }
        }

        private void LateUpdate()
        {
            RefreshEntryUI();
        }

        private void OnApplicationQuit()
        {
            stopping = true;
        }

        private void OnDisable()
        {
            // Ставим до остановки корутины: её finally может выполниться.
            stopping = true;

            if (mediaAccess != null)
            {
                mediaAccess.Changed -= OnMediaChanged;
                mediaAccess.RequestCompleted -= OnRequestCompleted;
            }

            playRequested = false;
            devicesRequested = false;
            mediaWasReady = false;

            CancelCameraStartupAndStop();

            // Здесь не включаем панели и не обращаемся к popup.
        }

        // ---------- Состояние разрешений ----------

        private void OnMediaChanged()
        {
            if (!CanUpdateUI)
                return;

            if (permissionHelp != null)
                permissionHelp.SetBrowser(mediaAccess.BrowserName);

            if (!mediaAccess.CameraGranted)
            {
                CancelCameraStartupAndStop();
            }
            else if (mediaAccess.Initialized &&
                     !mediaAccess.RequestPending)
            {
                // Сохранённое разрешение — запускаем видео в фоне.
                StartCamera();
            }

            RefreshEntryUI();
        }

        private void OnRequestCompleted(string result)
        {
            if (!CanUpdateUI)
                return;

            if (result != "granted")
                SetMessage(mediaAccess.LastError);

            RefreshEntryUI();
        }

        // ---------- Play ----------

        public void OnPlayButtonClick()
        {
            if (!CanUpdateUI)
                return;

            if (lobbyClient.IsLobbyVisible)
                return;

            playRequested = true;

            if (MediaReady)
            {
                OpenCreateMatch();
                RefreshEntryUI();
                return;
            }

            // Без работающих устройств создание не открываем.
            devicesRequested = true;
            RefreshEntryUI();

            // Проверка разрешений сама не запрашивает доступ.
            if (!mediaAccess.RequestPending)
                mediaAccess.CheckPermissions();
        }

        private void OpenCreateMatch()
        {
            if (!CanUpdateUI)
                return;

            // Повторная защита перед открытием.
            if (!MediaReady)
            {
                devicesRequested = true;
                return;
            }

            if (lobbyClient.IsLobbyVisible)
            {
                playRequested = false;
                return;
            }

            playRequested = false;
            devicesRequested = false;

            if (createMatchModal == null)
            {
                Debug.LogError(
                    "Назначь CreateMatchPopup → LeanWindow " +
                    "в поле Create Match Modal.",
                    this);

                return;
            }

            createMatchModal.TurnOn();
        }

        public void OnContinueButtonClick()
        {
            OnPlayButtonClick();
        }

        // ---------- Включение устройств ----------

        public void EnableWebCam()
        {
            if (!CanUpdateUI || DeviceOperationPending)
                return;

            SetMessage(
                "Allow access to your camera and microphone " +
                "in your browser.");

            mediaAccess.RequestAccess(
                requestVideo: !IsCameraReady);
        }

        private void StartCamera()
        {
            if (!CanUpdateUI ||
                !mediaAccess.CameraGranted ||
                mediaAccess.RequestPending ||
                CameraStarting ||
                IsCameraReady)
            {
                return;
            }

            cameraRoutine = StartCoroutine(StartCameraRoutine());
        }

        private IEnumerator StartCameraRoutine()
        {
            yield return null;

            try
            {
                if (!CanUpdateUI || !mediaAccess.CameraGranted)
                    yield break;

                webcamPreview.StopPreview();

                yield return mediaAccess.EnsureUnityCameraAccess();

                if (!CanUpdateUI || !mediaAccess.CameraGranted)
                    yield break;

                if (!mediaAccess.UnityCameraAuthorized)
                {
                    SetMessage(
                        "Could not access the camera.\n\n" +
                        "Check your permissions and try again.");

                    yield break;
                }

                SetMessage("Waiting for camera video…");

                yield return webcamPreview.PlayRoutine();

                if (!CanUpdateUI)
                    yield break;

                if (!IsCameraReady)
                {
                    SetMessage(
                        webcamPreview.LastError ??
                        "Could not start the camera.");

                    yield break;
                }

                SetMessage(
                    mediaAccess.MicrophoneGranted
                        ? "Video is working and microphone access is granted."
                        : "Microphone access is also required to play.");
            }
            finally
            {
                cameraRoutine = null;

                if (CanUpdateUI)
                    RefreshEntryUI();
            }
        }

        private void CancelCameraStartupAndStop()
        {
            if (cameraRoutine != null)
            {
                StopCoroutine(cameraRoutine);
                cameraRoutine = null;
            }

            if (webcamPreview != null)
                webcamPreview.StopPreview();
        }

        public void ShowDeviceSetupForLobby()
        {
            if (!CanUpdateUI || !lobbyClient.IsLobbyVisible)
                return;

            playRequested = false;
            devicesRequested = !MediaReady;

            RefreshEntryUI();
        }

        // ---------- Экраны ----------

        private void RefreshEntryUI()
        {
            if (!CanUpdateUI)
                return;

            bool ready = MediaReady;
            bool hasRoom = lobbyClient.IsLobbyVisible;

            if (ready)
                devicesRequested = false;

            if (hasRoom)
            {
                playRequested = false;
                mediaWasReady = ready;

                // В лобби устройства показываем только по явному запросу.
                bool showLobbyDevices = devicesRequested && !ready;

                SetVisible(deviceSetupUI, showLobbyDevices);
                SetVisible(matchLobbyUI, !showLobbyDevices);
                SetVisible(playButton, false);
                return;
            }

            if (mediaWasReady && !ready)
            {
                devicesRequested = true;

                if (createMatchModal != null)
                    createMatchModal.TurnOff();
            }

            mediaWasReady = ready;

            if (playRequested &&
                ready &&
                !mediaAccess.RequestPending)
            {
                //OpenCreateMatch();
            }

            bool showDevices = !ready && devicesRequested;

            SetVisible(deviceSetupUI, showDevices);
            SetVisible(matchLobbyUI, false);
            SetVisible(playButton, !showDevices);
        }

        private static void SetVisible(GameObject target, bool visible)
        {
            if (target != null && target.activeSelf != visible)
                target.SetActive(visible);
        }

        // ---------- Помощь ----------

        private void SetMessage(string message)
        {
            if (permissionHelp != null)
                permissionHelp.SetMessage(message);
        }

        public void ShowBrowserPermissionHelp()
        {
            if (permissionHelp != null)
                permissionHelp.Show();
        }

        public void OnCancelCameraButtonClick()
        {
            if (permissionHelp != null)
                permissionHelp.Hide();
        }
    }
}