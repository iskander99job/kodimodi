using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GunCustomizationTool.SciFi_Weapons
{
    public static class AttachmentInstantiator
    {
        private static readonly HashSet<AttachmentType> RailDevices = new()
        {
            AttachmentType.Laser,
            AttachmentType.Flashlight
        };

        private static readonly Dictionary<SlotType, Vector2> RailOutward = new()
        {
            { SlotType.BarLeft, new Vector2(0f, -1f) },
            { SlotType.BarRight, new Vector2(0f, 1f) },
            { SlotType.BarBottom, new Vector2(-1f, 0f) },
            { SlotType.BarTop, new Vector2(1f, 0f) }
        };

        public static GameObject InstantiateForPreview(GameObject prefab, Transform slot, AttachmentType type, SlotType slotType)
        {
            var instance = Object.Instantiate(prefab, slot);
            Align(instance, slot, type, slotType);
            return instance;
        }

        public static GameObject InstantiateForPrefab(GameObject prefab, Transform slot, AttachmentType type, SlotType slotType)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, slot);
            Align(instance, slot, type, slotType);
            return instance;
        }

        public static GameObject InstantiateForPreview(GameObject prefab, Transform slot)
        {
            var instance = Object.Instantiate(prefab, slot);
            instance.transform.localPosition = Vector3.zero;
            return instance;
        }

        public static GameObject InstantiateForPrefab(GameObject prefab, Transform slot)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, slot);
            instance.transform.localPosition = Vector3.zero;
            return instance;
        }

        private static void Align(GameObject instance, Transform slot, AttachmentType type, SlotType slotType)
        {
            if (!instance)
            {
                return;
            }

            // Half the Sci-Fi prefabs store a leftover position from the vendor's showcase layout
            // (shared sights sit near 7,9,0) and Unity keeps it when re-parenting. The Place_ node wins.
            instance.transform.localPosition = Vector3.zero;

            if (!RailDevices.Contains(type) || !RailOutward.TryGetValue(slotType, out var target))
            {
                return;
            }

            var natural = GetNaturalOutward(instance, slot);
            if (natural == Vector2.zero)
            {
                return;
            }

            instance.transform.localRotation = Quaternion.Euler(GetRailRoll(natural, target), 0f, 0f);
        }

        private static float GetRailRoll(Vector2 natural, Vector2 target)
        {
            var roll = Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg -
                       Mathf.Atan2(natural.y, natural.x) * Mathf.Rad2Deg;

            // Rails are orthogonal, so the real roll is a multiple of 90. The natural direction is
            // measured off geometry that isn't perfectly axis-aligned and lands ~1 degree out.
            return Mathf.Round(roll / 90f) * 90f;
        }

        private static Vector2 GetNaturalOutward(GameObject instance, Transform slot)
        {
            var bounds = new Bounds();
            var found = false;

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                // The beam runs down the barrel and would swamp the housing it is measuring.
                if (renderer.gameObject.name.EndsWith("_Ray", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!found)
            {
                return Vector2.zero;
            }

            var local = slot.InverseTransformPoint(bounds.center);
            var outward = new Vector2(local.y, local.z);
            return outward.sqrMagnitude < 1e-8f ? Vector2.zero : outward.normalized;
        }
    }
}
