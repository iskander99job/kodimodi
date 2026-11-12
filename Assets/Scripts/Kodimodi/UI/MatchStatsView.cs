using System.Collections.Generic;
using System.Linq;
using Kodimodi.Models;
using TMPro;
using UnityEngine;

namespace Kodimodi.UI
{
    public sealed class MatchStatsView : MonoBehaviour
    {
        [SerializeField] private TMP_Text victoryHint;
        [SerializeField] private Transform rows;

        [SerializeField] private GameObject playerRowPrefab;
        [SerializeField] private GameObject localPlayerRowPrefab;

        [SerializeField, Range(0f, 1f)]
        private float departedAlpha = 0.45f;

        private readonly List<GameObject> generated = new();

        public void SetVisible(bool visible)
        {
            var group = GetComponent<CanvasGroup>();

            if (group != null)
            {
                group.alpha = visible ? 1f : 0f;
                group.interactable = visible;
                group.blocksRaycasts = visible;
            }

            gameObject.SetActive(visible);
        }

        public void Render(
            ScoreboardModel board,
            int localId,
            IReadOnlyCollection<int> departedIds)
        {
            foreach (var row in generated)
            {
                if (row == null) continue;

                row.SetActive(false);
                Destroy(row);
            }

            generated.Clear();

            if (victoryHint != null)
            {
                victoryHint.text = board != null
                    ? $"До победы — {board.KillLimit} убийств"
                    : "Ожидаем статистику…";
            }

            if (board?.Players == null) return;

            if (rows == null ||
                playerRowPrefab == null ||
                localPlayerRowPrefab == null)
            {
                Debug.LogError(
                    "MatchStatsView: назначь Rows и оба префаба строк.",
                    this);
                return;
            }

            var sorted = board.Players
                .Where(p => p != null)
                .OrderByDescending(p => p.Kills)
                .ThenBy(p => p.Deaths)
                .ThenBy(p => p.Id)
                .ToArray();

            for (int i = 0; i < sorted.Length; i++)
            {
                var player = sorted[i];

                bool mine = player.Id == localId;
                bool departed = departedIds != null &&
                    departedIds.Contains(player.Id);

                var prefab = mine
                    ? localPlayerRowPrefab
                    : playerRowPrefab;

                var row = Instantiate(prefab, rows);
                generated.Add(row);
                row.SetActive(true);

                SetText(row.transform, "Place", (i + 1).ToString());
                SetText(row.transform, "Name", $"Игрок {player.Id}");
                SetText(row.transform, "Kills", player.Kills.ToString());
                SetText(row.transform, "Status",
                    departed ? "Вышел" : "В игре");

                Toggle(row.transform, "Highlight", mine);
                Toggle(row.transform, "Marker", mine);
                Toggle(row.transform, "YouLabel", mine);

                var group = row.GetComponent<CanvasGroup>();

                if (group == null)
                    group = row.AddComponent<CanvasGroup>();

                group.alpha = departed ? departedAlpha : 1f;
            }
        }

        private static void SetText(
            Transform root, string name, string value)
        {
            var child = root.Find(name);
            var text = child != null
                ? child.GetComponent<TMP_Text>()
                : null;

            if (text != null)
                text.text = value;
        }

        private static void Toggle(
            Transform root, string name, bool visible)
        {
            var child = root.Find(name);

            if (child != null)
                child.gameObject.SetActive(visible);
        }
    }
}