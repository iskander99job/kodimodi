using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Kodimodi.UI
{
    public class MediaAccessController : MonoBehaviour
    {
        public event Action Changed;
        public event Action PermissionsChecked;
        public event Action<string> RequestCompleted;

        public bool CameraGranted { get; private set; }
        public bool MicrophoneGranted { get; private set; }
        public bool RequestPending { get; private set; }
        public bool Initialized { get; private set; }

        public bool UnityCameraAuthorized { get; private set; }

        public string BrowserName { get; private set; } = "unknown";
        public string LastError { get; private set; } = "";

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

        private void OnEnable()
        {
            CameraGranted = false;
            MicrophoneGranted = false;
            RequestPending = false;
            Initialized = false;
            UnityCameraAuthorized = false;

#if UNITY_WEBGL && !UNITY_EDITOR
            DetectMediaBrowser(gameObject.name);
#endif

            CheckPermissions();
        }

        public void CheckPermissions()
        {
            if (!isActiveAndEnabled)
                return;

#if UNITY_WEBGL && !UNITY_EDITOR
            CheckCameraPermission(gameObject.name);
#else
            CameraGranted = Application.HasUserAuthorization(
                UserAuthorization.WebCam);

            // Сохраняем прежнее поведение редактора.
            MicrophoneGranted = true;
            Initialized = true;

            Changed?.Invoke();
            PermissionsChecked?.Invoke();
#endif
        }

        public void RequestAccess(bool requestVideo)
        {
            if (!isActiveAndEnabled || RequestPending)
                return;

            LastError = "";
            RequestPending = true;

            Changed?.Invoke();

#if UNITY_WEBGL && !UNITY_EDITOR
            RequestCameraAndMicrophone(
                gameObject.name,
                requestVideo ? 1 : 0);
#else
            StartCoroutine(RequestEditorAccess());
#endif
        }

        // Дополнительная проверка Unity перед запуском WebCamTexture.
        public IEnumerator EnsureUnityCameraAccess()
        {
            UnityCameraAuthorized = false;

            if (!Application.HasUserAuthorization(
                    UserAuthorization.WebCam))
            {
                yield return Application.RequestUserAuthorization(
                    UserAuthorization.WebCam);
            }

            UnityCameraAuthorized = Application.HasUserAuthorization(
                UserAuthorization.WebCam);
        }

        private IEnumerator RequestEditorAccess()
        {
            yield return EnsureUnityCameraAccess();

            OnMediaPermissionRequested(
                UnityCameraAuthorized
                    ? "granted"
                    : "NotAllowedError");
        }

        // ---------- Callbacks из CameraPermission.jslib ----------

        public void OnBrowserDetected(string browser)
        {
            if (!isActiveAndEnabled)
                return;

            BrowserName = browser;
            Changed?.Invoke();
        }

        public void OnMediaPermissionsChecked(string result)
        {
            if (!isActiveAndEnabled)
                return;

            string[] states = result.Split('|');

            if (states.Length != 2)
                return;

            // unknown не означает отказ:
            // браузер мог не поддержать permissions.query.
            if (states[0] != "unknown")
                CameraGranted = states[0] == "granted";

            if (states[1] != "unknown")
                MicrophoneGranted = states[1] == "granted";

            Initialized = true;

            Changed?.Invoke();
            PermissionsChecked?.Invoke();
        }

        public void OnMediaPermissionRequested(string result)
        {
            if (!isActiveAndEnabled)
                return;

            RequestPending = false;

            if (result == "granted")
            {
                CameraGranted = true;
                MicrophoneGranted = true;
                Initialized = true;
                LastError = "";
            }
            else
            {
                LastError = GetErrorMessage(result);
            }

            Changed?.Invoke();
            RequestCompleted?.Invoke(result);
        }

        private static string GetErrorMessage(string error)
        {
            switch (error)
            {
                case "NotAllowedError":
                case "PermissionDeniedError":
                    return
                        "Нет доступа к камере или микрофону.\n\n" +
                        "Проверь разрешения сайта и доступ браузера " +
                        "к устройствам в настройках системы.";

                case "NotFoundError":
                case "DevicesNotFoundError":
                    return
                        "Камера или микрофон не найдены.\n\n" +
                        "Подключи оба устройства и повтори попытку.";

                case "NotReadableError":
                case "TrackStartError":
                    return
                        "Не удалось включить камеру или микрофон.\n\n" +
                        "Устройство может использоваться другим " +
                        "приложением. Закрой его и повтори попытку.";

                case "unavailable":
                    return
                        "Браузер не предоставляет доступ к устройствам.\n\n" +
                        "Открой игру по HTTPS или localhost " +
                        "в поддерживаемом браузере.";

                default:
                    return
                        "Не удалось получить доступ к устройствам.\n\n" +
                        "Проверь камеру и микрофон и повтори попытку.";
            }
        }

        private void OnDisable()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            StopMediaPermissionWatch(gameObject.name);
#endif

            StopAllCoroutines();

            CameraGranted = false;
            MicrophoneGranted = false;
            RequestPending = false;
            Initialized = false;
            UnityCameraAuthorized = false;

            Changed?.Invoke();
        }
    }
}