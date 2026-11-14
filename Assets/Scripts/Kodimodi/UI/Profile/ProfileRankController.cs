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

        [Serializable]
        public class Rank
        {
            public string russianName;
            public string englishName;

            [Min(0)]
            public int requiredXP;

            public Sprite icon;

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
                new Rank("Новобранец", "Recruit", 0),
                new Rank("Рядовой", "Private", 100),
                new Rank("Ефрейтор", "Lance Corporal", 250),
                new Rank("Капрал", "Corporal", 450),
                new Rank("Сержант", "Sergeant", 700),

                new Rank("Старший сержант", "Staff Sergeant", 1000),
                new Rank("Сержант первого класса", "Sergeant First Class", 1400),
                new Rank("Мастер-сержант", "Master Sergeant", 1900),
                new Rank("Первый сержант", "First Sergeant", 2500),
                new Rank("Сержант-майор", "Sergeant Major", 3200),

                new Rank("Младший лейтенант", "Second Lieutenant", 4000),
                new Rank("Лейтенант", "First Lieutenant", 5000),
                new Rank("Капитан", "Captain", 6200),
                new Rank("Майор", "Major", 7600),
                new Rank("Подполковник", "Lieutenant Colonel", 9200),

                new Rank("Полковник", "Colonel", 11000),
                new Rank("Бригадный генерал", "Brigadier General", 13500),
                new Rank("Генерал-майор", "Major General", 16500),
                new Rank("Генерал-лейтенант", "Lieutenant General", 20000),
                new Rank("Генерал", "General", 24000),

                new Rank("Маршал", "Marshal", 29000),
                new Rank("Ветеран", "Veteran", 35000),
                new Rank("Элита", "Elite", 42000),
                new Rank("Мастер", "Master", 50000),
                new Rank("Грандмастер", "Grandmaster", 60000),

                new Rank("Чемпион", "Champion", 72000),
                new Rank("Герой", "Hero", 86000),
                new Rank("Титан", "Titan", 102000),
                new Rank("Легенда", "Legend", 120000),
                new Rank("Бессмертный", "Immortal", 150000)
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

        // Можно подключить к кнопкам через On Click().
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
                xpText.text =
                    $"{FormatXP(earned)} / {FormatXP(needed)} XP";
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

        // Обновляет названия старого списка, сохраняя иконки и XP.
        [ContextMenu("Apply Bilingual Rank Names")]
        private void ApplyBilingualRankNames()
        {
            List<Rank> defaults = CreateDefaultRanks();

            if (ranks == null || ranks.Count != defaults.Count)
            {
                Debug.LogWarning(
                    "Для обновления названий в списке должно быть 30 рангов.",
                    this);
                return;
            }

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