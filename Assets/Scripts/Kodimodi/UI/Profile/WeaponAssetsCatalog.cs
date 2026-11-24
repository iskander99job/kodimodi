using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Kodimodi.UI.Profile
{
    [CreateAssetMenu(menuName = "Kodimodi/Weapon Assets Catalog")]
    public sealed class WeaponAssetsCatalog : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public string code;
            [FormerlySerializedAs("prefab")]
            public GameObject localWeaponPrefab;
            public GameObject enemyWeaponPrefab;
            public Sprite icon;
            public Sprite preview;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public GameObject GetPrefab(string code) => GetLocalWeaponPrefab(code);

        public GameObject GetLocalWeaponPrefab(string code) => Find(code).localWeaponPrefab;

        public Sprite GetIcon(string code) => Find(code).icon;

        public Sprite GetPreview(string code)
        {
            var entry = Find(code);
            return entry.preview != null ? entry.preview : entry.icon;
        }

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
