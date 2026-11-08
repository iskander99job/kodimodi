using UnityEngine;

namespace GunCustomizationTool.SciFi_Weapons
{
    public class SkinEntry : ScriptableObject
    {
        [field: SerializeField]
        public Material Material { get; set; }
        [field: SerializeField]
        public Sprite PreviewIcon { get; set; }
        [field: SerializeField]
        public string DisplayName { get; set; }
    }
}   
