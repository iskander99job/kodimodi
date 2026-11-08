using System.Collections;
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
        public LeanWindow statisticPopup;
        public GameObject statisticScrollViewItemPrefab, statisticScrollViewLocalItemPrefab;
        public Transform statisticContent;   // Content внутри Scroll View окна Statistic

        public LeanWindow statisticFinishedPopup;
        public LeanWindow finishedTextPopup;

        private ScoreboardModel lastBoard;
        private bool statisticOpen;
        private bool wasCursorLocked;
        
        [Header("Завершение матча")]
        [SerializeField] private TMP_Text finishedResultText;
        [SerializeField] private Transform finishedStatisticContent;

        private bool matchEnded;
        private Coroutine finishRoutine;
        private BattleClient client;
        private MatchNotifications notifications;
        
        [SerializeField] private string menuSceneName = "ProfileScene";
        [SerializeField] private TMP_Text killsRemainingText;
        
        [Header("Потеря соединения")]
        [SerializeField] private LeanWindow connectionLostPopup;
        [SerializeField] private TMP_Text connectionLostText;

        private void Awake()
        {
            notifications = GetComponent<MatchNotifications>();
        }
        
        private void OnPlayerLeft(int playerId)
        {
            if (matchEnded || client == null || client.IsDisconnected)
                return;

            notifications?.Show($"Игрок {playerId} вышел из матча");
        }
        
        private void Start()
        {
            client = BattleClient.Instance;
            if (client == null) return;

            client.ScoreboardUpdated += OnScoreboard;
            client.MatchEnded += OnMatchEnded;
            client.BattleDisconnected += OnBattleDisconnected;
            client.PlayerLeft += OnPlayerLeft;

            if (client.IsDisconnected)
                OnBattleDisconnected(client.DisconnectReason);

            // Если результат пришёл до подписки UI.
            if (client.FinalResult != null)
                OnMatchEnded(client.FinalResult);
        }

        private void OnBattleDisconnected(string reason)
        {
            if (matchEnded) return;

            if (finishRoutine != null)
                StopCoroutine(finishRoutine);

            statisticOpen = false;
            statisticPopup.TurnOff();

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

        public void OnConnectionLostHomeBtnClick()
        {
            LeaveToMenu();
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

            if (finishRoutine != null)
                StopCoroutine(finishRoutine);
        }
        
        private void OnMatchEnded(MatchEndedModel result)
        {
            if (matchEnded || (client != null && client.IsDisconnected))
                return;
            matchEnded = true;
            killsRemainingText.text = "match Ended";

            lastBoard = new ScoreboardModel
            {
                Players = result.Players
            };

            statisticOpen = false;
            statisticPopup.TurnOff();

            FPSFrameworkCore.IsInputActive = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            finishedResultText.text = result.WinnerId == client.id
                ? "You won!"
                : "You lost";

            finishRoutine = StartCoroutine(ShowFinishedRoutine());
        }

        private IEnumerator ShowFinishedRoutine()
        {
            finishedTextPopup.TurnOn();

            yield return new WaitForSecondsRealtime(3f);

            finishedTextPopup.TurnOff();

            Render(lastBoard, finishedStatisticContent);
            statisticFinishedPopup.TurnOn();

            finishRoutine = null;
        }

        private void Update()
        {
            if (matchEnded || (client != null && client.IsDisconnected))
                return;
            
            bool locked = Cursor.lockState == CursorLockMode.Locked;

            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape))
                SetStatisticOpen(!statisticOpen);
            else if (!statisticOpen && wasCursorLocked && !locked)
                SetStatisticOpen(true);   // в браузере Esc часто только освобождает курсор

            wasCursorLocked = locked;
        }

        private void LateUpdate()
        {
            if (statisticOpen
                || matchEnded
                || (client != null && client.IsDisconnected))
            {
                FPSFrameworkCore.IsInputActive = false;
            }
        }

        // ---------- Окно статистики ----------

        private void SetStatisticOpen(bool open)
        {
            if (matchEnded || (client != null && client.IsDisconnected))
                return;
            
            statisticOpen = open;

            if (open)
            {
                Render(lastBoard);   // при открытии всегда свежие данные
                statisticPopup.TurnOn();
            }
            else
            {
                statisticPopup.TurnOff();
            }

            FPSFrameworkCore.IsInputActive =
                !open && client != null && client.CanPlay;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        private void OnScoreboard(ScoreboardModel board)
        {
            if (matchEnded || (client != null && client.IsDisconnected))
                return;

            lastBoard = board;

            int leaderKills = board.Players
                .Where(p => !client.DepartedPlayerIds.Contains(p.Id))
                .Select(p => p.Kills)
                .DefaultIfEmpty(0)
                .Max();

            int remaining = Mathf.Max(
                0,
                board.KillLimit - leaderKills);

            if (killsRemainingText != null)
            {
                killsRemainingText.text =
                    $"До конца матча: {remaining} убийств";
            }

            if (statisticOpen)
                Render(board);
        }

        // ---------- Таблица ----------

        private void Render(ScoreboardModel board, Transform content = null)
        {
            if (content == null)
                content = statisticContent;

            foreach (Transform child in content)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            if (board == null) return;

            int myId = BattleClient.Instance != null ? BattleClient.Instance.id : 0;

            var sorted = board.Players
                .OrderByDescending(p => p.Kills)
                .ThenBy(p => p.Deaths)
                .ThenBy(p => p.Id)
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                var p = sorted[i];

                bool mine = p.Id == myId;
                var prefab = mine ? statisticScrollViewLocalItemPrefab : statisticScrollViewItemPrefab;
                var row = Instantiate(prefab, content);

                SetText(row.transform, "Place", (i + 1).ToString());
                SetText(row.transform, "Name", mine ? $"Игрок {p.Id} - Вы" : $"Игрок {p.Id}");
                SetText(row.transform, "Kills", p.Kills.ToString());
            }
        }

        private static void SetText(Transform row, string childName, string value)
        {
            var child = row.Find(childName);
            var text = child != null ? child.GetComponent<TMP_Text>() : null;

            if (text == null)
            {
                Debug.LogWarning($"В префабе строки нет текста '{childName}'");
                return;
            }

            text.text = value;
        }

        // ---------- Кнопки (пока заготовки) ----------

// Игрок нажал «Выйти» посреди матча
        public void OnStatisticExitBtnClick() => LeaveToMenu();

// Матч закончился, игрок нажал «На главную»
        public void OnStatisticFinishedHomeBtnClick() => LeaveToMenu();

        private void LeaveToMenu()
        {
            // это состояние статическое и переживёт смену сцены, поэтому возвращаем как было
            statisticOpen = false;
            FPSFrameworkCore.IsInputActive = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            MatchSession.RoomId = null;
            MatchSession.PlayerId = 0;
            MatchSession.BattleToken = null;

            BattleClient.Instance?.Leave();
            SceneManager.LoadScene(menuSceneName);
        }
    }
}