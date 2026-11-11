using System;
using Akila.FPSFramework;
using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kodimodi
{
    [DefaultExecutionOrder(-20000)]
    [RequireComponent(typeof(BattleClient))]
    public class BattleCameraGuard : MonoBehaviour
    {
        private const double StartupGraceSeconds = 5;
        private const double RecoverySeconds = 15;

        [Header("Окно восстановления камеры")]
        [SerializeField] private LeanWindow popup;
        [SerializeField] private TMP_Text message;

        [Header("Возврат")]
        [SerializeField] private string profileScene = "ProfileScene";

        private BattleClient battle;
        private bool watching;
        private bool blocked;
        private bool exiting;
        private bool hadReady;

        private bool previousInput;
        private CursorLockMode previousCursor;
        private bool previousCursorVisible;

        private string lastState = "network";
        private double graceDeadline;
        private double recoveryDeadline;

        private void Awake()
        {
            battle = GetComponent<BattleClient>();

            if (popup != null)
                popup.TurnOff();
        }

        public bool BeginWatch()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (watching)
                return true;

            if (popup == null || message == null)
            {
                Debug.LogError(
                    "Назначь Popup и Message в BattleCameraGuard.",
                    this);

                ExitToProfile();
                return false;
            }

            watching = true;
            exiting = false;
            hadReady = false;

            lastState = "network";

            graceDeadline =
                Time.realtimeSinceStartupAsDouble +
                StartupGraceSeconds;

            recoveryDeadline = 0;

            // Подключаемся сразу, но первые 5 секунд
            // не блокируем игрока и не показываем popup.
            popup.TurnOff();

            return true;
#else
            Debug.Log(
                "Проверка камеры Battle работает в WebGL-сборке.");

            return false;
#endif
        }

        // Обычное состояние: отсутствие трека
        // при первоначальном запуске ещё не ошибка.
        public void OnBattleCameraState(string state)
        {
            HandleState(state, false);
        }

        // Явная ошибка SDK не ждёт окончания первых 5 секунд.
        public void OnBattleVideoError(string state)
        {
            HandleState(state, true);
        }

        private bool CanWatch()
        {
            if (!watching || exiting)
                return false;

            if (battle == null ||
                battle.IsMatchEnded ||
                battle.IsDisconnected)
            {
                EndWatch();
                return false;
            }

            // Поздний ready и кнопка повтора
            // не отменяют истёкший срок.
            if (Expired())
            {
                ExitToProfile();
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

                popup.TurnOff();
                SetBlocked(false);

                return;
            }

            if (explicitError ||
                hadReady ||
                blocked ||
                Time.realtimeSinceStartupAsDouble >= graceDeadline)
            {
                ShowProblem();
            }
        }

        private void ShowProblem()
        {
            if (!blocked)
            {
                recoveryDeadline =
                    Time.realtimeSinceStartupAsDouble +
                    RecoverySeconds;

                SetBlocked(true);
                popup.TurnOn();
            }

            // Повторные ошибки и смена network/missing
            // не продлевают срок.
            UpdateMessage();
        }

        private bool Expired()
        {
            return blocked &&
                   recoveryDeadline > 0 &&
                   Time.realtimeSinceStartupAsDouble >=
                   recoveryDeadline;
        }

        private void Update()
        {
            if (!CanWatch())
                return;

            if (!blocked &&
                !hadReady &&
                Time.realtimeSinceStartupAsDouble >= graceDeadline)
            {
                ShowProblem();
            }

            if (!blocked)
                return;

            FPSFrameworkCore.IsInputActive = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            UpdateMessage();
        }

        private void UpdateMessage()
        {
            int seconds = Math.Max(
                0,
                (int)Math.Ceiling(
                    recoveryDeadline -
                    Time.realtimeSinceStartupAsDouble));

            message.text = lastState == "missing"
                ? "Видео с камеры недоступно.\n" +
                  "Движение заблокировано.\n\n" +
                  $"Включи камеру: осталось {seconds} сек."
                : "Нет связи с LiveKit.\n" +
                  "Движение заблокировано.\n\n" +
                  $"Восстанавливаем подключение: осталось {seconds} сек.";
        }

        private void SetBlocked(bool value)
        {
            if (blocked == value)
                return;

            if (value)
            {
                previousInput = FPSFrameworkCore.IsInputActive;
                previousCursor = Cursor.lockState;
                previousCursorVisible = Cursor.visible;
            }

            blocked = value;

            if (battle == null)
                return;

            battle.SetVideoMovementBlocked(value);

            if (value)
            {
                FPSFrameworkCore.IsInputActive = false;

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (battle.CanPlay && !FPSFrameworkCore.IsPaused)
            {
                // Сохраняем состояние открытой статистики или меню.
                FPSFrameworkCore.IsInputActive = previousInput;

                Cursor.lockState = previousCursor;
                Cursor.visible = previousCursorVisible;
            }
        }

        public void RetryCamera()
        {
            if (!CanWatch() || !blocked)
                return;

            // Повторяем подключение, если потеряна комната,
            // либо восстанавливаем саму камеру.
            GetComponent<Livekit>()?.RetryCamera();
        }

        public void ExitToProfile()
        {
            if (exiting)
                return;

            exiting = true;

            battle?.Leave();
            EndWatch();

            MatchSession.RoomId = null;
            MatchSession.PlayerId = 0;
            MatchSession.BattleToken = null;

            // Статические флаги FPS Framework
            // переживают смену сцены.
            FPSFrameworkCore.IsInputActive = true;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SceneManager.LoadSceneAsync(profileScene);
        }

        public void EndWatch()
        {
            if (!watching)
                return;

            watching = false;

            GetComponent<LivekitCameraMonitor>()?.Unbind();

            if (popup != null)
                popup.TurnOff();

            SetBlocked(false);
            recoveryDeadline = 0;
        }

        private void OnDisable()
        {
            EndWatch();
        }
    }
}