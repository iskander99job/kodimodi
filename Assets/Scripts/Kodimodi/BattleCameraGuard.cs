using System;
using Akila.FPSFramework;
using Kodimodi.UI;
using TMPro;
using UnityEngine;

namespace Kodimodi
{
    [DefaultExecutionOrder(-20000)]
    [RequireComponent(typeof(BattleClient))]
    public class BattleCameraGuard : MonoBehaviour
    {
        private const double StartupGraceSeconds = 5;
        private const double RecoverySeconds = 15;

        private BattleClient battle;
        private BattleUIManager ui;

        private CanvasGroup recoveryWindow;
        private CanvasGroup timeoutWindow;

        private TMP_Text description;
        private TMP_Text timerText;
        private RectTransform progressFill;
        private Vector3 progressInitialScale;

        private bool watching;
        private bool blocked;
        private bool hadReady;
        private bool timedOut;

        private string lastState = "network";
        private double graceDeadline;
        private double recoveryDeadline;

        public bool HasTimedOut => timedOut;

        private void Awake()
        {
            battle = GetComponent<BattleClient>();

            ui = FindAnyObjectByType<BattleUIManager>(
                FindObjectsInactive.Include);

            FindWindows();

            SetVisible(recoveryWindow, false);
            SetVisible(timeoutWindow, false);
        }

        private void FindWindows()
        {
            var groups = FindObjectsByType<CanvasGroup>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (var group in groups)
            {
                if (group.gameObject.name == "CameraRecoveryUI")
                    recoveryWindow = group;
                else if (group.gameObject.name == "CameraTimeoutUI")
                    timeoutWindow = group;
            }

            if (recoveryWindow == null)
                return;

            description = FindText(
                recoveryWindow.transform,
                "Content/Description");

            timerText = FindText(
                recoveryWindow.transform,
                "Content/Countdown/TimerText");

            var fill = recoveryWindow.transform.Find(
                "Content/Countdown/ProgressTrack/ProgressFill");

            if (fill != null)
            {
                progressFill = fill.GetComponent<RectTransform>();

                if (progressFill != null)
                    progressInitialScale = progressFill.localScale;
            }
        }

        public bool BeginWatch()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (timedOut)
                return false;

            if (watching)
                return true;

            if (ui == null ||
                recoveryWindow == null ||
                timeoutWindow == null ||
                timerText == null ||
                description == null)
            {
                Debug.LogError(
                    "BattleCameraGuard: проверь BattleUIManager, " +
                    "CameraRecoveryUI, CameraTimeoutUI и их структуру.",
                    this);

                battle.Leave();
                return false;
            }

            watching = true;
            hadReady = false;
            lastState = "network";

            graceDeadline =
                Time.realtimeSinceStartupAsDouble +
                StartupGraceSeconds;

            recoveryDeadline = 0;

            SetVisible(recoveryWindow, false);
            SetVisible(timeoutWindow, false);

            return true;
#else
            Debug.Log(
                "Проверка камеры Battle работает в WebGL-сборке.");

            return false;
#endif
        }

        public void OnBattleCameraState(string state)
        {
            HandleState(state, false);
        }

        public void OnBattleVideoError(string state)
        {
            HandleState(state, true);
        }

        private bool CanWatch()
        {
            if (!watching || timedOut)
                return false;

            if (battle == null ||
                battle.IsMatchEnded ||
                battle.IsDisconnected)
            {
                EndWatch();
                return false;
            }

            // Поздний ready не отменяет истёкший срок.
            if (blocked &&
                recoveryDeadline > 0 &&
                Time.realtimeSinceStartupAsDouble >= recoveryDeadline)
            {
                ShowTimeout();
                return false;
            }

            return true;
        }

        private void HandleState(string state, bool explicitError)
        {
            if (!CanWatch())
                return;

            lastState = state;

            if (!explicitError && state == "ready")
            {
                hadReady = true;
                recoveryDeadline = 0;

                SetVisible(recoveryWindow, false);
                SetBlocked(false);
                battle.SetBattleReady(true);

                return;
            }

            if (explicitError ||
                hadReady ||
                blocked ||
                Time.realtimeSinceStartupAsDouble >= graceDeadline)
            {
                ShowRecovery();
            }
        }

        private void ShowRecovery()
        {
            battle.SetBattleReady(false);
            if (!blocked)
            {
                recoveryDeadline =
                    Time.realtimeSinceStartupAsDouble +
                    RecoverySeconds;

                SetBlocked(true);
                SetVisible(recoveryWindow, true);

                // Поверх открытых статистики/меню.
                recoveryWindow.transform.SetAsLastSibling();
            }

            UpdateRecovery();
        }

        private void Update()
        {
            if (!CanWatch())
                return;

            if (!blocked &&
                !hadReady &&
                Time.realtimeSinceStartupAsDouble >= graceDeadline)
            {
                ShowRecovery();
            }

            if (!blocked)
                return;

            FPSFrameworkCore.IsInputActive = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            UpdateRecovery();
        }

        private void UpdateRecovery()
        {
            double remaining = Math.Max(
                0,
                recoveryDeadline -
                Time.realtimeSinceStartupAsDouble);

            if (timerText != null)
                timerText.text = ((int)Math.Ceiling(remaining)).ToString();

            if (description != null)
            {
                description.text = lastState == "missing"
                    ? "Видео с камеры недоступно. " +
                      "Включите камеру, чтобы продолжить игру."
                    : "Нет связи с LiveKit. " +
                      "Восстанавливаем подключение.";
            }

            if (progressFill != null)
            {
                float fraction = Mathf.Clamp01(
                    (float)(remaining / RecoverySeconds));

                progressFill.localScale = new Vector3(
                    progressInitialScale.x * fraction,
                    progressInitialScale.y,
                    progressInitialScale.z);
            }
        }

        private void SetBlocked(bool value)
        {
            if (blocked == value)
                return;

            blocked = value;
            battle?.SetVideoMovementBlocked(value);

            if (value)
            {
                FPSFrameworkCore.IsInputActive = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (!timedOut)
            {
                // Менеджер учитывает открытую статистику и меню.
                ui?.RefreshMatchInput();
            }
        }

        private void ShowTimeout()
        {
            if (timedOut)
                return;

            // До Leave: он вызывает EndWatch().
            timedOut = true;

            battle.Leave();

            SetVisible(recoveryWindow, false);

            ui?.OnCameraRecoveryTimedOut();

            var timeoutDescription = FindText(
                timeoutWindow.transform,
                "Content/Description");

            if (timeoutDescription != null)
            {
                timeoutDescription.text =
                    "Не удалось восстановить видео за отведённое время. " +
                    "Вы отключены от матча.";
            }

            SetVisible(timeoutWindow, true);
            timeoutWindow.transform.SetAsLastSibling();

            FPSFrameworkCore.IsInputActive = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void RetryCamera()
        {
            if (!CanWatch() || !blocked)
                return;

            // Повтор не продлевает срок восстановления.
            GetComponent<Livekit>()?.RetryCamera();
        }

        // Оставляем публичный метод для совместимости.
        public void ExitToProfile()
        {
            ui?.OnConnectionLostHomeBtnClick();
        }

        public void EndWatch()
        {
            watching = false;

            GetComponent<LivekitCameraMonitor>()?.Unbind();

            SetVisible(recoveryWindow, false);

            SetBlocked(false);
            recoveryDeadline = 0;
        }

        private void OnDisable()
        {
            EndWatch();
        }

        private static TMP_Text FindText(
            Transform root, string path)
        {
            var child = root.Find(path);

            return child != null
                ? child.GetComponent<TMP_Text>()
                : null;
        }

        private static void SetVisible(
            CanvasGroup window, bool visible)
        {
            if (window == null)
                return;

            window.alpha = visible ? 1f : 0f;
            window.interactable = visible;
            window.blocksRaycasts = visible;

            window.gameObject.SetActive(visible);
        }
    }
}