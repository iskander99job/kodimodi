using System;
using UnityEngine;

namespace Kodimodi.UI.Profile
{
    [CreateAssetMenu(menuName = "Kodimodi/Shop Rank Icons")]
    public sealed class ShopRankIcons : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public string code;
            public Sprite icon;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public Sprite Find(string code)
        {
            if (!string.IsNullOrEmpty(code))
            {
                foreach (var entry in entries)
                {
                    if (string.Equals(entry.code, code, StringComparison.OrdinalIgnoreCase))
                        return entry.icon;
                }
            }

            return null;
        }
    }
}
