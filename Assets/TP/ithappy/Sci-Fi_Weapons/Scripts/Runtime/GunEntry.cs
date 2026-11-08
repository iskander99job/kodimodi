using UnityEngine;

namespace GunCustomizationTool.SciFi_Weapons
{
    public class GunEntry : ScriptableObject
    {
        [field: SerializeField]
        public GameObject BasePrefab { get; set; }

        [field: SerializeField]
        public GunType GunType { get; set; }

        [field: SerializeField]
        public AttachmentEntry[] Slots { get; set; }
    }
}
