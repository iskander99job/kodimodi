using UnityEngine;
using UnityEngine.UI;

namespace Kodimodi.UI.Profile
{
    [CreateAssetMenu(menuName = "Kodimodi/Weapon Category Icons")]
    public sealed class WeaponCategoryIcons : ScriptableObject
    {
        internal static readonly (string Tab, string Code)[] Tabs =
        {
            ("PISTOL", "pistol"), ("AR", "ar"),
            ("SMG", "smg"), ("SHOTGUN", "shotgun")
        };

        [SerializeField] private Sprite pistol;
        [SerializeField] private Sprite ar;
        [SerializeField] private Sprite smg;
        [SerializeField] private Sprite shotgun;

        internal void Apply(Transform tab, string code)
        {
            Sprite sprite = code switch
            {
                "pistol" => pistol,
                "ar" => ar,
                "smg" => smg,
                "shotgun" => shotgun,
                _ => null
            };

            if (sprite == null)
                return;

            var image = tab.Find("Category Icon").GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.enabled = true;
            image.gameObject.SetActive(true);
        }
    }
}
