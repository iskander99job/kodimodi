using System;
using System.Collections.Generic;
using System.Linq;

namespace GunCustomizationTool.SciFi_Weapons
{
    public static class SlotTypeExtensions
    {
        private static readonly Dictionary<SlotType, string[]> Aliases = new()
        {
            { SlotType.Buttstock, new[] { "Buttstock", "Gunstock" } },
            { SlotType.Charm, new[] { "Charm", "Charms" } },
            { SlotType.Handle, new[] { "Handle", "BarHandle" } }
        };

        private static string[] GetPlaceholderNames(this SlotType slotType)
        {
            if (Aliases.TryGetValue(slotType, out var aliases))
                return aliases;
            return new[] { slotType.ToString() };
        }

        // Ignores the gun prefix and any trailing index: "Place_Pistol4_Magazine_2" -> "Magazine".
        public static string GetPlaceholderToken(string placeholderName)
        {
            if (string.IsNullOrEmpty(placeholderName))
                return string.Empty;

            var parts = placeholderName.Split('_');
            for (var i = parts.Length - 1; i >= 0; i--)
            {
                var part = parts[i];
                if (part.Length == 0 || part.All(char.IsDigit))
                    continue;

                return part;
            }

            return string.Empty;
        }

        public static bool IsPlaceholder(string transformName) => transformName.StartsWith("Place_", StringComparison.Ordinal);

        public static bool Matches(this SlotType slotType, string placeholderName)
        {
            var token = GetPlaceholderToken(placeholderName);
            return slotType.GetPlaceholderNames()
                .Any(alias => string.Equals(alias, token, StringComparison.OrdinalIgnoreCase));
        }

        public static bool TryResolve(string placeholderName, out SlotType slotType)
        {
            var token = GetPlaceholderToken(placeholderName);
            if (!string.IsNullOrEmpty(token))
            {
                foreach (SlotType candidate in Enum.GetValues(typeof(SlotType)))
                {
                    if (candidate.GetPlaceholderNames()
                        .Any(alias => string.Equals(alias, token, StringComparison.OrdinalIgnoreCase)))
                    {
                        slotType = candidate;
                        return true;
                    }
                }
            }

            slotType = default;
            return false;
        }
    }
}
