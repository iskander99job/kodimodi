using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kodimodi.UI
{
    public class MatchLobbyView : MonoBehaviour
    {
        [Header("Тексты")]
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text linkText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private TMP_Text statusText;

        [Header("Список игроков")]
        [SerializeField] private Transform playersContent;
        [SerializeField] private GameObject playerRowTemplate;

        [Header("Кнопки")]
        [SerializeField] private LeanButton joinButton;
        [SerializeField] private LeanButton startButton;
        [SerializeField] private LeanButton leaveButton;

        [Header("Загрузка")]
        [SerializeField] private GameObject spinner;

        private TMP_Text leaveButtonText;

        private bool loading;
        private bool showJoin;
        private bool canJoin;
        private bool showStart;
        private bool canStart;
        private bool canLeave = true;

        private static readonly Color EmptyColor =
            new Color32(145, 145, 145, 255);

        public void SetLink(string link)
        {
            if (linkText != null)
                linkText.text = link;
        }

        public void SetSummary(int count, int capacity)
        {
            if (playerCountText != null)
            {
                playerCountText.text = capacity > 0
                    ? $"{count} / {capacity}"
                    : "…";
            }

            if (descriptionText != null)
            {
                descriptionText.text = capacity > 0
                    ? $"Free-for-all · Up to {capacity} players"
                    : "Free-for-all";
            }
        }

        public void SetStatus(string message)
        {
            if (statusText == null)
                return;

            statusText.text = message;

            SetVisible(
                statusText.gameObject,
                !string.IsNullOrWhiteSpace(message));
        }

        public void SetLoading(bool value)
        {
            loading = value;

            if (spinner != null)
                spinner.SetActive(value);

            RefreshButtons();
        }

        public void SetActions(
            bool showJoin,
            bool canJoin,
            bool showStart,
            bool canStart,
            bool canLeave = true)
        {
            this.showJoin = showJoin;
            this.canJoin = canJoin;
            this.showStart = showStart;
            this.canStart = canStart;
            this.canLeave = canLeave;

            // В текущем LobbyClient showStart включается для создателя.
            if (leaveButton != null)
            {
                if (leaveButtonText == null)
                {
                    leaveButtonText =
                        leaveButton.GetComponentInChildren<TMP_Text>(true);
                }

                if (leaveButtonText != null)
                {
                    leaveButtonText.text = showStart
                        ? "Cancel match"
                        : "Leave lobby";
                }
            }

            RefreshButtons();
        }

        private void RefreshButtons()
        {
            if (joinButton != null)
            {
                joinButton.interactable = !loading && canJoin;
                SetVisible(joinButton.gameObject, !loading && showJoin);
            }

            if (startButton != null)
            {
                startButton.interactable = !loading && canStart;
                SetVisible(startButton.gameObject, !loading && showStart);

                Image backing = startButton.GetComponent<Image>();

                if (backing != null)
                {
                    backing.color = canStart
                        ? Color.black
                        : new Color32(91, 88, 88, 255);
                }
            }

            if (leaveButton != null)
            {
                leaveButton.interactable = !loading && canLeave;
                SetVisible(leaveButton.gameObject, !loading);
            }
        }

        private static void SetVisible(
            GameObject target,
            bool visible)
        {
            if (target != null && target.activeSelf != visible)
                target.SetActive(visible);
        }

        public void ClearPlayers()
        {
            if (playersContent == null)
                return;

            foreach (Transform child in playersContent)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        public void RenderPlayers(
            int count,
            int capacity,
            int yourIndex = -1,
            int hostIndex = -1)
        {
            ClearPlayers();
            SetSummary(count, capacity);

            if (playersContent == null || playerRowTemplate == null)
                return;

            int rows = Mathf.Max(count, capacity);

            for (int i = 0; i < rows; i++)
            {
                GameObject item = Instantiate(
                    playerRowTemplate,
                    playersContent);

                item.SetActive(true);

                bool occupied = i < count;
                Color contentColor = occupied
                    ? Color.black
                    : EmptyColor;

                // Имя или подпись свободного места.
                Transform nameObject = item.transform.Find("Name");

                if (nameObject != null)
                {
                    TMP_Text label =
                        nameObject.GetComponent<TMP_Text>();

                    if (label != null)
                    {
                        label.text = occupied
                            ? $"Player {i + 1}"
                            : "Waiting for player…";

                        if (occupied && i == yourIndex)
                            label.text += " — You";

                        label.color = contentColor;
                        label.fontStyle = occupied
                            ? FontStyles.Bold
                            : FontStyles.Normal;
                    }
                }

                // Иконка игрока.
                Transform iconObject = item.transform.Find("Icon");

                if (iconObject != null)
                {
                    Image icon = iconObject.GetComponent<Image>();

                    if (icon != null)
                        icon.color = contentColor;
                }

                // Отметка создателя.
                Transform badge = item.transform.Find("HostBadge");

                if (badge != null)
                {
                    badge.gameObject.SetActive(
                        occupied && i == hostIndex);

                    TMP_Text badgeText =
                        badge.GetComponentInChildren<TMP_Text>(true);

                    if (badgeText != null)
                    {
                        badgeText.text = i == yourIndex
                            ? "YOU · HOST"
                            : "HOST";
                    }
                }

                Image rowImage = item.GetComponent<Image>();

                if (rowImage != null)
                {
                    rowImage.color = occupied
                        ? new Color32(244, 244, 244, 255)
                        : new Color32(250, 250, 250, 255);
                }
            }
        }
    }
}