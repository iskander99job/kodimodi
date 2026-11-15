using System;
using UnityEngine;

namespace GunCustomizationTool.SciFi_Weapons
{
    public class AttachmentEntry : ScriptableObject
    {
        [field: SerializeField]
        public AttachmentType AttachmentType { get; set; }

        [field: SerializeField]
        public AttachmentElement[] Elements { get; set; }

        [field: SerializeField]
        public Sprite Icon { get; set; }

        [Serializable]
        public class AttachmentElement
        {
            public SlotType SlotType;
            public GameObject Prefab;
        }
    }
}
