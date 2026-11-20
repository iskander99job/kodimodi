using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kodimodi.UI.Profile
{
    public class ProfileRankController : MonoBehaviour
    {
        public enum Language
        {
            Russian,
            English
        }

        private const float DefaultIconSize = 120f;
        private const int LegacyRankCount = 30;

        [Serializable]
        public class Rank
        {
            public string russianName;
            public string englishName;

            [Min(0)]
            public int requiredXP;

            public Sprite icon;

            [Min(1)]
            public float iconWidth = DefaultIconSize;

            [Min(1)]
            public float iconHeight = DefaultIconSize;

            public float iconX = 0f;
            public float iconY = 0f;

            public Rank(string russian, string english, int xp)
            {
                russianName = russian;
                englishName = english;
                requiredXP = xp;
            }

            public string GetName(Language language)
            {
                return language == Language.Russian
                    ? russianName
                    : englishName;
            }
        }

        [Header("Язык")]
        [SerializeField] private Language language = Language.Russian;

        [Header("Игрок")]
        [SerializeField] private string nickname = "nickname";
        [SerializeField, Min(0)] private int playerXP;

        [Header("Ранги")]
        [SerializeField] private List<Rank> ranks = CreateDefaultRanks();

        [Header("UI — найдётся автоматически внутри ProfileRank")]
        [SerializeField] private Image rankIcon;
        [SerializeField] private TMP_Text nicknameText;
        [SerializeField] private TMP_Text rankNameText;
        [SerializeField] private TMP_Text xpText;
        [SerializeField] private Slider xpBar;

        public int PlayerXP => playerXP;
        public Rank CurrentRank { get; private set; }
        public Language CurrentLanguage => language;

        private static List<Rank> CreateDefaultRanks()
        {
            return new List<Rank>
            {
                new Rank("Рядовой", "Private", 0),
                new Rank("Ефрейтор", "Corporal", 100),
                new Rank("Младший сержант", "Junior Sergeant", 250),
                new Rank("Сержант", "Sergeant", 450),
                new Rank("Старший сержант", "Senior Sergeant", 700),
                new Rank("Старшина", "Sergeant Major", 1000),
                new Rank("Прапорщик", "Warrant Officer", 1400),
                new Rank("Старший прапорщик", "Senior Warrant Officer", 1900),
                new Rank("Младший лейтенант", "Junior Lieutenant", 2500),
                new Rank("Лейтенант", "Lieutenant", 3200),
                new Rank("Старший лейтенант", "Senior Lieutenant", 4000),
                new Rank("Капитан", "Captain", 5000),
                new Rank("Майор", "Major", 6200),
                new Rank("Подполковник", "Lieutenant Colonel", 7600),
                new Rank("Полковник", "Colonel", 9200),
                new Rank("Генерал-майор", "Major General", 11000),
                new Rank("Генерал-лейтенант", "Lieutenant General", 13500),
                new Rank("Ас", "Ace", 16500),
                new Rank("Бог", "God", 20000)
            };
        }

        private void Awake()
        {
            RefreshUI();
        }

        [ContextMenu("Find UI References")]
        private void FindUI()
        {
            if (rankIcon == null)
                rankIcon = FindComponent<Image>("RankIcon");

            if (nicknameText == null)
                nicknameText = FindComponent<TMP_Text>("Info/Nickname");

            if (rankNameText == null)
                rankNameText = FindComponent<TMP_Text>("Info/RankName");

            if (xpText == null)
                xpText = FindComponent<TMP_Text>("Info/XPText");

            if (xpBar == null)
                xpBar = FindComponent<Slider>("Info/XPBar");
        }

        public void SetLanguage(Language value)
        {
            language = value;
            RefreshUI();
        }

        public void UseRussian()
        {
            SetLanguage(Language.Russian);
        }

        public void UseEnglish()
        {
            SetLanguage(Language.English);
        }

        public void SetXP(int xp)
        {
            playerXP = Mathf.Max(0, xp);
            RefreshUI();
        }

        public void AddXP(int amount)
        {
            long result = (long)playerXP + amount;
            SetXP((int)Math.Max(0L, Math.Min(int.MaxValue, result)));
        }

        public void SetNickname(string value)
        {
            nickname = value;
            FindUI();

            if (nicknameText != null)
                nicknameText.text = nickname;
        }

        [ContextMenu("Refresh UI")]
        public void RefreshUI()
        {
            FindUI();
            playerXP = Mathf.Max(0, playerXP);

            Rank current = null;
            Rank next = null;

            if (ranks != null)
            {
                foreach (Rank rank in ranks)
                {
                    if (rank == null)
                        continue;

                    if (rank.requiredXP <= playerXP)
                    {
                        if (current == null ||
                            rank.requiredXP > current.requiredXP)
                        {
                            current = rank;
                        }
                    }
                    else
                    {
                        if (next == null ||
                            rank.requiredXP < next.requiredXP)
                        {
                            next = rank;
                        }
                    }
                }
            }

            CurrentRank = current;

            if (nicknameText != null)
                nicknameText.text = nickname;

            if (current == null)
            {
                if (rankNameText != null)
                    rankNameText.text = "";

                if (rankIcon != null)
                    rankIcon.enabled = false;

                if (xpText != null)
                    xpText.text = "";

                SetBar(0, 1);

                Debug.LogWarning(
                    "Не найден ранг для текущего XP. " +
                    "Добавь начальный ранг с Required XP = 0.",
                    this);
                return;
            }

            if (rankNameText != null)
                rankNameText.text = current.GetName(language);

            if (rankIcon != null)
            {
                rankIcon.sprite = current.icon;
                rankIcon.enabled = current.icon != null;

                RectTransform iconRect = rankIcon.rectTransform;
                iconRect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    current.iconWidth > 0f ? current.iconWidth : DefaultIconSize);
                iconRect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    current.iconHeight > 0f ? current.iconHeight : DefaultIconSize);
                iconRect.anchoredPosition = new Vector2(current.iconX, current.iconY);
            }

            if (next == null)
            {
                SetBar(1, 1);

                string maxLabel = language == Language.Russian
                    ? "МАКС."
                    : "MAX";

                if (xpText != null)
                {
                    xpText.text =
                        $"{FormatXP(playerXP)} XP • {maxLabel}";
                }

                return;
            }

            int earned = playerXP - current.requiredXP;
            int needed = next.requiredXP - current.requiredXP;

            SetBar(earned, needed);

            if (xpText != null)
            {
                xpText.text = $"{FormatXP(playerXP)} / {FormatXP(next.requiredXP)} XP";
            }
        }

        private void SetBar(int value, int maximum)
        {
            if (xpBar == null)
                return;

            xpBar.minValue = 0;
            xpBar.maxValue = maximum;
            xpBar.wholeNumbers = true;
            xpBar.value = value;
        }

        private T FindComponent<T>(string path) where T : Component
        {
            Transform child = transform.Find(path);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static string FormatXP(int value)
        {
            return value.ToString(
                "N0",
                System.Globalization.CultureInfo.InvariantCulture
            ).Replace(",", " ");
        }

        // Переносит первые 19 записей старого списка с их иконками и XP.
        [ContextMenu("Apply Bilingual Rank Names")]
        private void ApplyBilingualRankNames()
        {
            List<Rank> defaults = CreateDefaultRanks();

            if (ranks == null ||
                (ranks.Count != defaults.Count && ranks.Count != LegacyRankCount))
            {
                Debug.LogWarning(
                    $"Для обновления нужен список из {defaults.Count} рангов " +
                    $"или старый список из {LegacyRankCount} рангов.",
                    this);
                return;
            }

#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Apply Bilingual Rank Names");
#endif

            if (ranks.Count > defaults.Count)
                ranks.RemoveRange(defaults.Count, ranks.Count - defaults.Count);

            for (int i = 0; i < ranks.Count; i++)
            {
                if (ranks[i] == null)
                    ranks[i] = defaults[i];
                else
                {
                    ranks[i].russianName = defaults[i].russianName;
                    ranks[i].englishName = defaults[i].englishName;
                }
            }

            RefreshUI();

#if UNITY_EDITOR
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            UnityEditor.EditorUtility.SetDirty(this);

            if (!Application.isPlaying && gameObject.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    gameObject.scene);
            }
#endif
        }
    }
}