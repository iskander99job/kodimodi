using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GunCustomizationTool.SciFi_Weapons
{
    public static class AttachmentSpriteAssigner
    {
        private const string IconsRoot = "Assets/ithappy/Sci-Fi_Weapons/Icons";
        public const string SeparateAssetsRoot = IconsRoot + "/Separate_Assets";
        public const string PreferredColor = "Amber";

        public static readonly string[] ColorSetNames =
        {
            "Amber", "Copper", "Coral", "Frost", "Fusion", "Ion", "Nova", "Smoky", "Solar", "Toxic"
        };

        private const string ColorlessKey = "";

        private static Dictionary<string, string> _iconPaths;
        private static readonly Dictionary<string, Sprite> LoadedIcons = new(StringComparer.OrdinalIgnoreCase);

        // Call when a window opens, or the index gets built by whoever asks first, which is usually a
        // card mid-paint.
        public static void WarmUp() => GetIconPaths();

        public static Sprite GetIconForColor(Sprite icon, string colorName)
        {
            if (!icon || string.IsNullOrEmpty(colorName))
            {
                return icon;
            }

            var partName = StripAnyColorSuffix(icon.name, out var currentColor);
            if (string.IsNullOrEmpty(currentColor) || string.Equals(currentColor, colorName, StringComparison.OrdinalIgnoreCase))
            {
                return icon;
            }

            return Load(Key(partName, colorName)) ?? icon;
        }

        private static Sprite Load(string key)
        {
            if (LoadedIcons.TryGetValue(key, out var cached))
            {
                return cached;
            }

            Sprite sprite = null;
            if (GetIconPaths().TryGetValue(key, out var path))
            {
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            LoadedIcons[key] = sprite;
            return sprite;
        }

        // Indexes asset paths only and loads sprites on first use: the pack ships ~3000 icons and a
        // window shows a dozen at a time.
        private static Dictionary<string, string> GetIconPaths()
        {
            if (_iconPaths != null)
            {
                return _iconPaths;
            }

            _iconPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!AssetDatabase.IsValidFolder(SeparateAssetsRoot))
            {
                return _iconPaths;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { SeparateAssetsRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var partName = StripAnyColorSuffix(Path.GetFileNameWithoutExtension(path), out var color);
                var key = Key(partName, color);
                if (!_iconPaths.ContainsKey(key))
                {
                    _iconPaths[key] = path;
                }
            }

            return _iconPaths;
        }

        private static string Key(string partName, string color) => partName + "|" + color;

        private static string StripAnyColorSuffix(string fileName, out string color)
        {
            foreach (var candidate in ColorSetNames)
            {
                if (fileName.EndsWith("_" + candidate, StringComparison.OrdinalIgnoreCase))
                {
                    color = candidate;
                    return fileName.Substring(0, fileName.Length - candidate.Length - 1);
                }
            }

            color = ColorlessKey;
            return fileName;
        }
    }
}
