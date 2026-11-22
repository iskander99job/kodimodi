using System;
using UnityEngine;

namespace Kodimodi.UI.Profile
{
    [CreateAssetMenu(menuName = "Kodimodi/Weapon Assets Catalog")]
    public sealed class WeaponAssetsCatalog : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public string code;
            public GameObject prefab;
            public Sprite icon;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public GameObject GetPrefab(string code) => Find(code).prefab;

        public Sprite GetIcon(string code) => Find(code).icon;

        private Entry Find(string code)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                foreach (var entry in entries)
                {
                    if (string.Equals(entry.code, code, StringComparison.OrdinalIgnoreCase))
                        return entry;
                }
            }

            return default;
        }
    }
}
