using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kodimodi.UI.Profile
{
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
        private readonly bool russian;
        private readonly Vector2[] min = new Vector2[SceneRows.Length];
        private readonly Vector2[] max = new Vector2[SceneRows.Length];

        public WeaponStatsView(Transform page, bool russian)
        {
            this.page = page;
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
            var visible = new List<(string Name, int Value)>();
            if (stats != null)
            {
                foreach (var stat in stats)
                {
                    if (stat != null && !string.IsNullOrWhiteSpace(stat.statCode))
                        visible.Add((GetLabel(stat.statCode), stat.value));
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

        private string GetLabel(string code)
        {
            string key = code.Trim().ToLowerInvariant();
            if (russian)
            {
                string translation = key switch
                {
                    "damage" => "Урон",
                    "magazine_size" => "Ёмкость магазина",
                    "fire_rate" => "Скорострельность",
                    "reload" => "Перезарядка",
                    "accuracy" => "Точность",
                    "recoil_control" => "Контроль отдачи",
                    _ => null
                };
                if (translation != null)
                    return translation;
            }

            string label = key.Replace('_', ' ');
            return char.ToUpperInvariant(label[0]) + label.Substring(1);
        }

        private static void Fit(Text text)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 8;
            text.resizeTextMaxSize = text.fontSize;
        }
    }
}
