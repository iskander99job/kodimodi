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
        
        [SerializeField] private string menuSceneName = "Menu";

        private void Start()
        {
            if (BattleClient.Instance != null)
                BattleClient.Instance.ScoreboardUpdated += OnScoreboard;
        }

        private void OnDestroy()
        {
            if (BattleClient.Instance != null)
                BattleClient.Instance.ScoreboardUpdated -= OnScoreboard;
        }

        private void Update()
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;

            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape))
                SetStatisticOpen(!statisticOpen);
            else if (!statisticOpen && wasCursorLocked && !locked)
                SetStatisticOpen(true);   // в браузере Esc часто только освобождает курсор

            wasCursorLocked = locked;
        }

        private void LateUpdate()
        {
            // при возрождении NetworkRespawn включает ввод, пока окно открыто выключаем обратно
            if (statisticOpen && FPSFrameworkCore.IsInputActive)
                FPSFrameworkCore.IsInputActive = false;
        }

        // ---------- Окно статистики ----------

        private void SetStatisticOpen(bool open)
        {
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

            FPSFrameworkCore.IsInputActive = !open;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        private void OnScoreboard(ScoreboardModel board)
        {
            lastBoard = board;
            if (statisticOpen) Render(board);   // обновляем на лету, если окно уже открыто
        }

        // ---------- Таблица ----------

        private void Render(ScoreboardModel board)
        {
            // убираем старые строки: прячем сразу, потому что Destroy срабатывает в конце кадра
            foreach (Transform child in statisticContent)
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
                var row = Instantiate(prefab, statisticContent);

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

            BattleClient.Instance?.Leave();
            SceneManager.LoadScene(menuSceneName);
        }
    }
}