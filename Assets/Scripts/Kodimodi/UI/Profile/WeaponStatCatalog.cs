using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kodimodi.UI.Profile
{
    [CreateAssetMenu(menuName = "Kodimodi/Weapon Stat Catalog")]
    public sealed class WeaponStatCatalog : ScriptableObject
    {
        [Serializable]
        internal struct Entry
        {
            public string code;
            public string englishName;
            public string russianName;

            public string Name(bool russian) =>
                russian && !string.IsNullOrEmpty(russianName) ? russianName : englishName;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        internal IReadOnlyList<Entry> Entries => entries;
    }

    [Serializable]
    internal sealed class WeaponStatValue
    {
        public string statCode;
        public int value;
    }

    internal sealed class WeaponStatsView
    {
        private static readonly string[] SceneRows = { "DAMAGE", "MAGAZINE", "FIRE RATE", "RELOAD" };

        private readonly List<RectTransform> rows = new();
        private readonly Transform page;
        private readonly WeaponStatCatalog catalog;
        private readonly bool russian;
        private readonly Vector2[] min = new Vector2[SceneRows.Length];
        private readonly Vector2[] max = new Vector2[SceneRows.Length];

        public WeaponStatsView(Transform page, WeaponStatCatalog catalog, bool russian)
        {
            this.page = page;
            this.catalog = catalog;
            this.russian = russian;
            for (int i = 0; i < SceneRows.Length; i++)
            {
                var row = (RectTransform)page.Find(SceneRows[i]);
                rows.Add(row);
                min[i] = row.anchorMin;
                max[i] = row.anchorMax;
            }

            Render(null);
        }

        public void Render(WeaponStatValue[] stats)
        {
            var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (stats != null)
            {
                foreach (var stat in stats)
                {
                    if (stat != null && !string.IsNullOrEmpty(stat.statCode))
                        values[stat.statCode] = stat.value;
                }
            }

            var visible = new List<(string Name, int Value)>();
            if (catalog != null)
            {
                foreach (var entry in catalog.Entries)
                {
                    if (!string.IsNullOrEmpty(entry.code) && values.Remove(entry.code, out int value))
                        visible.Add((entry.Name(russian), value));
                }
            }

            while (rows.Count < visible.Count)
                rows.Add(UnityEngine.Object.Instantiate(rows[0], page, false));

            int levels = (visible.Count + 1) / 2;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                row.gameObject.SetActive(i < visible.Count);
                if (i >= visible.Count)
                    continue;

                var label = row.Find("Label").GetComponent<Text>();
                var value = row.Find("Value").GetComponent<Text>();
                label.text = visible[i].Name;
                value.text = visible[i].Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (levels <= 2)
                {
                    row.anchorMin = min[i];
                    row.anchorMax = max[i];
                }
                else
                {
                    int column = i % 2;
                    float bottom = min[2].y;
                    float top = max[0].y + (max[0].y - min[0].y);
                    float step = (top - bottom) / levels;
                    float upper = top - step * (i / 2);
                    row.anchorMin = new Vector2(min[column].x, upper - step * 0.9f);
                    row.anchorMax = new Vector2(max[column].x, upper);
                }

                Fit(label);
                Fit(value);
            }
        }

        private static void Fit(Text text)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 8;
            text.resizeTextMaxSize = text.fontSize;
        }
    }
}
