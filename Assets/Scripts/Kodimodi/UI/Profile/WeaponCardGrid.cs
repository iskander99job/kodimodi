using UnityEngine;
using UnityEngine.UI;

namespace Kodimodi.UI.Profile
{
    [AddComponentMenu("Layout/Weapon Card Grid")]
    public sealed class WeaponCardGrid : GridLayoutGroup
    {
        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();

            m_ChildAlignment = rectChildren.Count < 3
                ? TextAnchor.MiddleLeft
                : TextAnchor.MiddleCenter;
        }
    }
}