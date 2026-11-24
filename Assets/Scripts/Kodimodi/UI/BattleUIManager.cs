using System.Linq;
using Akila.FPSFramework;
using Kodimodi.Models;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kodimodi.UI
{
    public class BattleUIManager : MonoBehaviour
    {
        [Header("Возврат в меню")]
        [SerializeField] private string menuSceneName = "ProfileScene";

        [Header("Старый текст информации о матче — необязательно")]
        [SerializeField] private TMP_Text killsRemainingText;

        private BattleClient client;
        private BattleCameraGuard cameraGuard;
        private MatchNotifications notifications;

        private MatchStatsView matchStatsUI;
        private MatchResultsView matchResultsUI;
        private MatchErrorView matchErrorUI;
        private MatchCountdownView matchCountdownUI;
        private MatchLoadingView matchLoadingUI;
        private GameHUDView gameHUD;

        private GameObject exitMatchUI;

        private ScoreboardModel lastBoard;

        private bool statisticOpen;
        private bool exitMatchOpen;
        private bool wasCursorLocked;
        private bool matchEnded;
        private bool leaving;

        private bool CameraTimedOut =>
            cameraGuard != null && cameraGuard.HasTimedOut;

        private bool CameraBlocked =>
            client != null && client.VideoMovementBlocked;

        private void Awake()
        {
            notifications = GetComponent<MatchNotifications>();

            cameraGuard = FindAnyObjectByType<BattleCameraGuard>(
                FindObjectsInactive.Include);

            FindMatchStatsUI();
            FindExitMatchUI();
            FindMatchResultsUI();
            FindMatchErrorUI();

            matchCountdownUI = FindAnyObjectByType<MatchCountdownView>(
                FindObjectsInactive.Include);

            matchLoadingUI = FindAnyObjectByType<MatchLoadingView>(
                FindObjectsInactive.Include);

            gameHUD = FindAnyObjectByType<GameHUDView>(
                FindObjectsInactive.Include);

            HideStats();
            HideExitMatch();
            HideCountdown();

            if (matchResultsUI != null)
                matchResultsUI.SetVisible(false);

            if (matchErrorUI != null)
                matchErrorUI.SetVisible(false);

            if (gameHUD != null)
                gameHUD.SetVisible(false);

            if (matchLoadingUI != null)
            {
                matchLoadingUI.Show(
                    "Подключаемся к игровому серверу…");
            }
        }

        private void Start()
        {
            client = BattleClient.Instance;

            if (client == null)
            {
                HideLoading();

                Debug.LogError(
                    "BattleUIManager: BattleClient не найден.",
                    this);

                return;
            }

            client.ScoreboardUpdated += OnScoreboard;
            client.MatchEnded += OnMatchEnded;
            client.BattleDisconnected += OnBattleDisconnected;
            client.PlayerLeft += OnPlayerLeft;

            client.CountdownUpdated += OnCountdownUpdated;
            client.BattlePlayStarted += OnBattlePlayStarted;

            wasCursorLocked =
                Cursor.lockState == CursorLockMode.Locked;

            // Состояние могло измениться до подписки UI.
            if (client.IsDisconnected)
            {
                OnBattleDisconnected(client.DisconnectReason);
                return;
            }

            if (client.FinalResult != null)
            {
                OnMatchEnded(client.FinalResult);
                return;
            }

            if (CameraTimedOut)
            {
                OnCameraRecoveryTimedOut();
                return;
            }

            if (client.IsBattleStarted)
            {
                OnBattlePlayStarted();
            }
            else if (client.LastCountdown != null)
            {
                OnCountdownUpdated(client.LastCountdown);
            }
            else
            {
                RefreshMatchInput();
            }
        }

        private void OnDestroy()
        {
            if (client == null)
                return;

            client.ScoreboardUpdated -= OnScoreboard;
            client.MatchEnded -= OnMatchEnded;
            client.BattleDisconnected -= OnBattleDisconnected;
            client.PlayerLeft -= OnPlayerLeft;

            client.CountdownUpdated -= OnCountdownUpdated;
            client.BattlePlayStarted -= OnBattlePlayStarted;
        }

        private void Update()
        {
            if (leaving ||
                matchEnded ||
                client == null ||
                client.IsDisconnected ||
                !client.IsBattleStarted ||
                CameraTimedOut ||
                CameraBlocked)
            {
                return;
            }

            bool locked =
                Cursor.lockState == CursorLockMode.Locked;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SetExitMatchOpen(!exitMatchOpen);
            }
            else if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (!exitMatchOpen)
                    SetStatisticOpen(!statisticOpen);
            }
            else if (!exitMatchOpen &&
                     !statisticOpen &&
                     wasCursorLocked &&
                     !locked &&
                     !FPSFrameworkCore.IsPaused)
            {
                // Браузер может перехватить Esc,
                // освободив курсор без GetKeyDown.
                SetExitMatchOpen(true);
            }

            wasCursorLocked =
                Cursor.lockState == CursorLockMode.Locked;
        }

        private void LateUpdate()
        {
            UpdateLoading();
            UpdateGameHUD();

            if (leaving ||
                statisticOpen ||
                exitMatchOpen ||
                matchEnded ||
                CameraTimedOut ||
                CameraBlocked ||
                (client != null &&
                 (client.IsDisconnected || !client.IsBattleStarted)))
            {
                FPSFrameworkCore.IsInputActive = false;
            }

            if (CameraTimedOut || CameraBlocked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        // ---------- Поиск окон ----------

        private void FindMatchStatsUI()
        {
            if (matchStatsUI == null)
            {
                matchStatsUI = FindAnyObjectByType<MatchStatsView>(
                    FindObjectsInactive.Include);
            }
        }

        private void FindMatchResultsUI()
        {
            if (matchResultsUI == null)
            {
                matchResultsUI = FindAnyObjectByType<MatchResultsView>(
                    FindObjectsInactive.Include);
            }
        }

        private void FindMatchErrorUI()
        {
            if (matchErrorUI == null)
            {
                matchErrorUI = FindAnyObjectByType<MatchErrorView>(
                    FindObjectsInactive.Include);
            }
        }

        private void FindExitMatchUI()
        {
            if (exitMatchUI != null)
                return;

            FindMatchStatsUI();

            if (matchStatsUI == null)
                return;

            var canvasRoot = matchStatsUI.transform.parent;

            if (canvasRoot == null)
                return;

            var window = canvasRoot.Find("ExitMatchUI");

            if (window != null)
                exitMatchUI = window.gameObject;
        }

        // ---------- Управление и курсор ----------

        public void RefreshMatchInput()
        {
            bool terminal =
                leaving ||
                matchEnded ||
                client == null ||
                client.IsDisconnected ||
                CameraTimedOut;

            bool releaseCursor =
                terminal ||
                (client != null && !client.IsBattleStarted) ||
                statisticOpen ||
                exitMatchOpen ||
                CameraBlocked ||
                FPSFrameworkCore.IsPaused;

            FPSFrameworkCore.IsInputActive =
                !releaseCursor &&
                client != null &&
                client.CanPlay;

            Cursor.lockState = releaseCursor
                ? CursorLockMode.None
                : CursorLockMode.Locked;

            Cursor.visible = releaseCursor;

            wasCursorLocked =
                Cursor.lockState == CursorLockMode.Locked;
        }

        // ---------- Загрузка ----------

        private void HideLoading()
        {
            if (matchLoadingUI != null)
                matchLoadingUI.SetVisible(false);
        }

        private void UpdateLoading()
        {
            if (matchLoadingUI == null)
                return;

            bool countdownActive =
                client != null &&
                client.LastCountdown != null &&
                client.LastCountdown.StartsAtUnixMs > 0;

            bool visible =
                !leaving &&
                !matchEnded &&
                client != null &&
                !client.IsDisconnected &&
                !client.IsBattleStarted &&
                !CameraBlocked &&
                !CameraTimedOut &&
                !countdownActive;

            if (!visible)
            {
                HideLoading();
                return;
            }

            string status;

            if (!client.HasBattleConnection)
            {
                status = "Подключаемся к игровому серверу…";
            }
            else if (!client.HasLocalPlayer)
            {
                status =
                    "Ожидаем подключения игроков и подготовки матча…";
            }
            else if (!client.IsLocalCameraReady)
            {
                status = "Подключаем камеру…";
            }
            else
            {
                status = "Ожидаем готовности остальных игроков…";
            }

            matchLoadingUI.Show(status);

            FPSFrameworkCore.IsInputActive = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ---------- Отсчёт перед стартом ----------

        private void HideCountdown()
        {
            if (matchCountdownUI != null)
                matchCountdownUI.SetVisible(false);
        }

        private void OnCountdownUpdated(BattleCountdownModel model)
        {
            if (leaving ||
                matchEnded ||
                client == null ||
                client.IsDisconnected ||
                CameraTimedOut ||
                model == null)
            {
                return;
            }

            HideStats();
            HideExitMatch();

            if (model.StartsAtUnixMs <= 0)
            {
                HideCountdown();
                RefreshMatchInput();
                return;
            }

            HideLoading();

            if (matchCountdownUI != null)
            {
                matchCountdownUI.Show(client.CountdownDeadline);
            }
            else
            {
                Debug.LogError(
                    "Добавь MatchCountdownView на MatchCountdownUI.",
                    this);
            }

            RefreshMatchInput();
        }

        private void OnBattlePlayStarted()
        {
            HideLoading();
            HideCountdown();

            RefreshMatchInput();
        }

        // ---------- HUD ----------

        private void UpdateGameHUD()
        {
            if (gameHUD == null)
                return;

            bool visible =
                !leaving &&
                !matchEnded &&
                client != null &&
                !client.IsDisconnected &&
                client.IsBattleStarted &&
                client.LocalPlayer != null &&
                !CameraTimedOut;

            if (visible)
            {
                gameHUD.Render(
                    client.LocalPlayer,
                    lastBoard,
                    client.id);
            }

            gameHUD.SetVisible(visible);
        }

        // ---------- Статистика ----------

        private void SetStatisticOpen(bool open)
        {
            if (leaving ||
                matchEnded ||
                exitMatchOpen ||
                client == null ||
                client.IsDisconnected ||
                !client.IsBattleStarted ||
                CameraTimedOut ||
                CameraBlocked)
            {
                return;
            }

            FindMatchStatsUI();

            if (open && matchStatsUI == null)
            {
                Debug.LogError(
                    "Добавь MatchStatsView на MatchStatsUI.",
                    this);
                return;
            }

            statisticOpen = open;

            if (open)
                RenderStats();

            if (matchStatsUI != null)
                matchStatsUI.SetVisible(open);

            RefreshMatchInput();
        }

        private void HideStats()
        {
            statisticOpen = false;

            if (matchStatsUI != null)
                matchStatsUI.SetVisible(false);
        }

        private void RenderStats()
        {
            if (matchStatsUI == null || client == null)
                return;

            matchStatsUI.Render(
                lastBoard,
                client.id,
                client.DepartedPlayerIds,
                client.GetPlayerNickname);
        }

        private void OnScoreboard(ScoreboardModel board)
        {
            if (leaving ||
                matchEnded ||
                client == null ||
                client.IsDisconnected ||
                board == null)
            {
                return;
            }

            lastBoard = board;

            int leaderKills = board.Players?
                .Where(p =>
                    p != null &&
                    !client.DepartedPlayerIds.Contains(p.Id))
                .Select(p => p.Kills)
                .DefaultIfEmpty(0)
                .Max() ?? 0;

            int remaining = Mathf.Max(
                0,
                board.KillLimit - leaderKills);

            if (killsRemainingText != null)
            {
                killsRemainingText.text =
                    $"До конца матча: {remaining} убийств";
            }

            if (statisticOpen)
                RenderStats();
        }

        private void OnPlayerLeft(int playerId)
        {
            if (leaving ||
                matchEnded ||
                client == null ||
                client.IsDisconnected)
            {
                return;
            }

            notifications?.Show(
                $"{client.GetPlayerNickname(playerId)} вышел из матча");

            if (statisticOpen)
                RenderStats();
        }

        // ---------- Подтверждение выхода ----------

        private void SetExitVisible(bool visible)
        {
            if (exitMatchUI == null)
                return;

            var group = exitMatchUI.GetComponent<CanvasGroup>();

            if (group != null)
            {
                group.alpha = visible ? 1f : 0f;
                group.interactable = visible;
                group.blocksRaycasts = visible;
            }

            exitMatchUI.SetActive(visible);
        }

        private void SetExitMatchOpen(bool open)
        {
            if (leaving ||
                matchEnded ||
                client == null ||
                client.IsDisconnected ||
                !client.IsBattleStarted ||
                CameraTimedOut ||
                CameraBlocked)
            {
                return;
            }

            FindExitMatchUI();

            if (open && exitMatchUI == null)
            {
                Debug.LogError(
                    "ExitMatchUI не найден рядом с MatchStatsUI.",
                    this);
                return;
            }

            if (open)
                HideStats();

            exitMatchOpen = open;
            SetExitVisible(open);

            RefreshMatchInput();
        }

        private void HideExitMatch()
        {
            exitMatchOpen = false;
            SetExitVisible(false);
        }

        // ---------- Ошибка соединения ----------

        private void OnBattleDisconnected(string reason)
        {
            if (leaving || matchEnded)
                return;

            HideLoading();
            HideCountdown();
            HideStats();
            HideExitMatch();

            if (gameHUD != null)
                gameHUD.SetVisible(false);

            FPSFrameworkCore.IsInputActive = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            string message = reason switch
            {
                "invalid_session" => "Your session expired. Sign in and try joining again.",
                "arsenal_unavailable" => "Couldn't load match equipment. Try joining again.",
                "loading_timeout" =>
                    "Не все игроки успели подготовиться. Матч отменён.",

                "player_left_loading" =>
                    "Игрок вышел во время подготовки. Матч отменён.",

                "unauthorized" =>
                    "Не удалось подтвердить вход в матч.",

                "no_session" =>
                    "Нет данных матча. Вернитесь в меню.",

                "room_not_found" =>
                    "Матч не найден или уже закрыт.",

                "connection_timeout" =>
                    "Сервер не отвечает. Соединение потеряно.",

                "protocol_error" =>
                    "Ошибка обмена данными с игровым сервером.",

                _ =>
                    "Соединение с игровым сервером потеряно."
            };

            if (killsRemainingText != null)
                killsRemainingText.text = "Матч прерван";

            FindMatchErrorUI();

            if (matchErrorUI != null)
            {
                matchErrorUI.Show(message);
            }
            else
            {
                Debug.LogError(
                    "Добавь MatchErrorView на MatchErrorUI. " +
                    $"Причина отключения: {message}",
                    this);
            }
        }

        // ---------- Завершение матча ----------

        private void OnMatchEnded(MatchEndedModel result)
        {
            if (leaving ||
                matchEnded ||
                result == null ||
                client == null ||
                client.IsDisconnected)
            {
                return;
            }

            matchEnded = true;

            HideLoading();
            HideCountdown();
            HideStats();
            HideExitMatch();

            if (gameHUD != null)
                gameHUD.SetVisible(false);

            FPSFrameworkCore.IsInputActive = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (killsRemainingText != null)
                killsRemainingText.text = "Матч завершён";

            FindMatchResultsUI();

            if (matchResultsUI == null)
            {
                Debug.LogError(
                    "Добавь MatchResultsView на MatchResultsUI.",
                    this);
                return;
            }

            matchResultsUI.Render(
                result,
                client.id,
                client.DepartedPlayerIds,
                client.GetPlayerNickname);

            matchResultsUI.SetVisible(true);
        }

        // ---------- Таймаут камеры ----------

        public void OnCameraRecoveryTimedOut()
        {
            HideLoading();
            HideCountdown();
            HideStats();
            HideExitMatch();

            if (gameHUD != null)
                gameHUD.SetVisible(false);

            RefreshMatchInput();
        }

        // ---------- Кнопки ----------

        public void OnStatsCloseBtnClick()
        {
            SetStatisticOpen(false);
        }

        public void OnStatisticExitBtnClick()
        {
            SetExitMatchOpen(true);
        }

        public void OnExitMatchStayBtnClick()
        {
            SetExitMatchOpen(false);
        }

        public void OnExitMatchLeaveBtnClick()
        {
            if (!exitMatchOpen || leaving)
                return;

            LeaveToMenu();
        }

        public void OnStatisticFinishedHomeBtnClick()
        {
            LeaveToMenu();
        }

        public void OnConnectionLostHomeBtnClick()
        {
            LeaveToMenu();
        }

        // ---------- Выход в меню ----------

        private void LeaveToMenu()
        {
            if (leaving)
                return;

            leaving = true;

            HideLoading();
            HideCountdown();
            HideStats();
            HideExitMatch();

            if (gameHUD != null)
                gameHUD.SetVisible(false);

            BattleClient.Instance?.Leave();

            MatchSession.RoomId = null;
            MatchSession.PlayerId = 0;
            MatchSession.BattleToken = null;

            FPSFrameworkCore.IsInputActive = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SceneManager.LoadScene(menuSceneName);
        }
    }
}