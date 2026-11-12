using System.Linq;
using Akila.FPSFramework;
using Kodimodi.Models;
using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kodimodi.UI
{
    public class BattleUIManager : MonoBehaviour
    {
        private MatchStatsView matchStatsUI;
        
        private GameObject exitMatchUI;
        private bool exitMatchOpen;
        
        [Header("Возврат в меню")]
        [SerializeField] private string menuSceneName = "ProfileScene";

        [Header("Информация о матче")]
        [SerializeField] private TMP_Text killsRemainingText;

        [Header("Потеря соединения")]
        [SerializeField] private LeanWindow connectionLostPopup;
        [SerializeField] private TMP_Text connectionLostText;

        private ScoreboardModel lastBoard;

        private BattleClient client;
        private MatchNotifications notifications;
        private MatchResultsView matchResultsUI;

        private bool statisticOpen;
        private bool wasCursorLocked;
        private bool matchEnded;
        private bool leaving;

        private void Awake()
        {
            notifications = GetComponent<MatchNotifications>();

            FindMatchStatsUI();

            if (matchStatsUI != null)
                matchStatsUI.SetVisible(false);
            
            FindExitMatchUI();
            SetExitVisible(false);
            
            FindMatchResultsUI();

            if (matchResultsUI != null)
                matchResultsUI.SetVisible(false);
        }

        private void FindMatchResultsUI()
        {
            if (matchResultsUI != null)
                return;

            matchResultsUI = FindAnyObjectByType<MatchResultsView>(
                FindObjectsInactive.Include);
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
                client.IsDisconnected)
            {
                return;
            }

            FindExitMatchUI();

            if (open && exitMatchUI == null)
            {
                Debug.LogError(
                    "BattleUIManager: объект ExitMatchUI не найден " +
                    "рядом с MatchStatsUI.",
                    this);
                return;
            }

            if (open)
                HideStats();

            exitMatchOpen = open;
            SetExitVisible(open);

            FPSFrameworkCore.IsInputActive =
                !open &&
                client.CanPlay &&
                !FPSFrameworkCore.IsPaused;

            bool releaseCursor =
                open ||
                client.VideoMovementBlocked ||
                FPSFrameworkCore.IsPaused;

            Cursor.lockState = releaseCursor
                ? CursorLockMode.None
                : CursorLockMode.Locked;

            Cursor.visible = releaseCursor;

            wasCursorLocked =
                Cursor.lockState == CursorLockMode.Locked;
        }

        private void HideExitMatch()
        {
            exitMatchOpen = false;
            SetExitVisible(false);
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
        
        private void FindMatchStatsUI()
        {
            if (matchStatsUI != null)
                return;

            matchStatsUI = FindAnyObjectByType<MatchStatsView>(
                FindObjectsInactive.Include);
        }

        private void Start()
        {
            client = BattleClient.Instance;

            if (client == null)
            {
                Debug.LogError(
                    "BattleUIManager: BattleClient не найден.",
                    this);
                return;
            }

            client.ScoreboardUpdated += OnScoreboard;
            client.MatchEnded += OnMatchEnded;
            client.BattleDisconnected += OnBattleDisconnected;
            client.PlayerLeft += OnPlayerLeft;

            wasCursorLocked =
                Cursor.lockState == CursorLockMode.Locked;

            if (client.IsDisconnected)
                OnBattleDisconnected(client.DisconnectReason);

            // Результат мог прийти раньше подписки UI.
            if (client.FinalResult != null)
                OnMatchEnded(client.FinalResult);
        }

        private void OnDestroy()
        {
            if (client != null)
            {
                client.ScoreboardUpdated -= OnScoreboard;
                client.MatchEnded -= OnMatchEnded;
                client.BattleDisconnected -= OnBattleDisconnected;
                client.PlayerLeft -= OnPlayerLeft;
            }
        }

        private void Update()
        {
            if (leaving ||
                matchEnded ||
                client == null ||
                client.IsDisconnected)
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
                     !client.VideoMovementBlocked &&
                     !FPSFrameworkCore.IsPaused)
            {
                // Браузер может перехватить Esc и только освободить курсор.
                SetExitMatchOpen(true);
            }

            wasCursorLocked =
                Cursor.lockState == CursorLockMode.Locked;
        }

        private void LateUpdate()
        {
            if (leaving ||
                statisticOpen ||
                exitMatchOpen ||
                matchEnded ||
                (client != null && client.IsDisconnected))
            {
                FPSFrameworkCore.IsInputActive = false;
            }
        }

        // ---------- Статистика матча ----------

        private void SetStatisticOpen(bool open)
        {
            if (exitMatchOpen)
                return;
            
            if (leaving ||
                matchEnded ||
                client == null ||
                client.IsDisconnected)
            {
                return;
            }

            FindMatchStatsUI();

            if (open && matchStatsUI == null)
            {
                Debug.LogError(
                    "BattleUIManager: добавь MatchStatsView " +
                    "на объект MatchStatsUI.",
                    this);
                return;
            }

            statisticOpen = open;

            if (open)
                RenderStats();

            if (matchStatsUI != null)
                matchStatsUI.SetVisible(open);

            FPSFrameworkCore.IsInputActive =
                !open &&
                client.CanPlay &&
                !FPSFrameworkCore.IsPaused;

            bool releaseCursor =
                open ||
                client.VideoMovementBlocked ||
                FPSFrameworkCore.IsPaused;

            Cursor.lockState = releaseCursor
                ? CursorLockMode.None
                : CursorLockMode.Locked;

            Cursor.visible = releaseCursor;

            wasCursorLocked =
                Cursor.lockState == CursorLockMode.Locked;
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
                client.DepartedPlayerIds);
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
                $"Игрок {playerId} вышел из матча");

            if (statisticOpen)
                RenderStats();
        }

        // ---------- Потеря соединения ----------
        // Пока используется старое окно Connection.

        private void OnBattleDisconnected(string reason)
        {
            if (leaving || matchEnded)
                return;

            HideStats();
            HideExitMatch();

            FPSFrameworkCore.IsInputActive = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            string message = reason switch
            {
                "loading_timeout" =>
                    "Не все игроки загрузились. Матч отменён.",

                "player_left_loading" =>
                    "Игрок вышел во время загрузки. Матч отменён.",

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

            if (connectionLostText != null)
                connectionLostText.text = message;

            if (connectionLostPopup != null)
                connectionLostPopup.TurnOn();
            else
                Debug.LogError(message);
        }

        // ---------- Завершение матча ----------
        // Старые окна сохраняем до следующего этапа.

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

            HideStats();
            HideExitMatch();

            FPSFrameworkCore.IsInputActive = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (killsRemainingText != null)
                killsRemainingText.text = "Матч завершён";

            FindMatchResultsUI();

            if (matchResultsUI == null)
            {
                Debug.LogError(
                    "BattleUIManager: добавь MatchResultsView " +
                    "на объект MatchResultsUI.",
                    this);
                return;
            }

            matchResultsUI.Render(
                result,
                client.id,
                client.DepartedPlayerIds);

            matchResultsUI.SetVisible(true);
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

        public void OnStatisticFinishedHomeBtnClick()
        {
            LeaveToMenu();
        }

        public void OnConnectionLostHomeBtnClick()
        {
            LeaveToMenu();
        }

        private void LeaveToMenu()
        {
            if (leaving)
                return;

            leaving = true;
            HideStats();
            HideExitMatch();

            BattleClient.Instance?.Leave();

            MatchSession.RoomId = null;
            MatchSession.PlayerId = 0;
            MatchSession.BattleToken = null;

            // Статическое состояние переживает смену сцены.
            FPSFrameworkCore.IsInputActive = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SceneManager.LoadScene(menuSceneName);
        }
    }
}