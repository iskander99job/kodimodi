using System.Collections.Generic;
using System.Linq;
using Kodimodi.Models;
using TMPro;
using UnityEngine;

namespace Kodimodi.UI
{
    public sealed class MatchResultsView : MonoBehaviour
    {
        [Header("Префабы строк")]
        [SerializeField] private GameObject playerRowPrefab;
        [SerializeField] private GameObject localPlayerRowPrefab;
        [SerializeField] private GameObject departedPlayerRowPrefab;

        [SerializeField, Range(0f, 1f)]
        private float departedAlpha = 0.45f;

        private Transform rows;
        private TMP_Text placementNumber;
        private TMP_Text resultCaption;

        private readonly List<GameObject> generated = new();

        private bool Initialize()
        {
            if (rows == null)
            {
                rows = transform.Find(
                    "Content/ResultsTable/Scroll View/Viewport/Rows");
            }

            if (placementNumber == null)
            {
                placementNumber = FindText(
                    transform, "Content/Placement/Number");
            }

            if (resultCaption == null)
            {
                resultCaption = FindText(
                    transform, "Content/ResultCaption");
            }

            if (rows == null ||
                playerRowPrefab == null ||
                localPlayerRowPrefab == null ||
                departedPlayerRowPrefab == null)
            {
                Debug.LogError(
                    "MatchResultsView: проверь Rows и назначь три префаба строк.",
                    this);
                return false;
            }

            return true;
        }

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
            MatchEndedModel result,
            int localId,
            IReadOnlyCollection<int> departedIds,
            System.Func<int, string> getNickname)
        {
            if (result == null || !Initialize())
                return;

            foreach (var row in generated)
            {
                if (row == null)
                    continue;

                row.SetActive(false);
                Destroy(row);
            }

            generated.Clear();

            if (resultCaption != null)
            {
                resultCaption.text = result.WinnerId == localId
                    ? "Вы победили!"
                    : "Ваш результат";
            }

            if (placementNumber != null)
                placementNumber.text = "—";

            if (result.Players == null)
                return;

            var sorted = result.Players
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

                var prefab = departed
                    ? departedPlayerRowPrefab
                    : mine
                        ? localPlayerRowPrefab
                        : playerRowPrefab;

                var row = Instantiate(prefab, rows);
                row.name = $"ResultPlayer_{player.Id}";

                generated.Add(row);

                SetText(row.transform, "Place", (i + 1).ToString());
                SetText(row.transform, "Name", getNickname(player.Id));
                SetText(row.transform, "Kills", player.Kills.ToString());

                var status = FindText(row.transform, "Status/Text");

                if (status == null)
                    status = FindText(row.transform, "Status");

                if (status != null)
                {
                    status.text = departed
                        ? "Вышел"
                        : "Завершил";
                }

                Toggle(row.transform, "Status/Icon", departed);
                Toggle(row.transform, "Highlight", mine);
                Toggle(row.transform, "Marker", mine);
                Toggle(row.transform, "YouLabel", mine);

                var group = row.GetComponent<CanvasGroup>();

                if (group == null)
                    group = row.AddComponent<CanvasGroup>();

                group.alpha = departed ? departedAlpha : 1f;

                row.SetActive(true);

                if (mine && placementNumber != null)
                    placementNumber.text = (i + 1).ToString();
            }
        }

        private static TMP_Text FindText(
            Transform root, string path)
        {
            var child = root.Find(path);

            return child != null
                ? child.GetComponent<TMP_Text>()
                : null;
        }

        private static void SetText(
            Transform root, string path, string value)
        {
            var text = FindText(root, path);

            if (text != null)
                text.text = value;
        }

        private static void Toggle(
            Transform root, string path, bool visible)
        {
            var child = root.Find(path);

            if (child != null)
                child.gameObject.SetActive(visible);
        }
    }
}