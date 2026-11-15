using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace GunCustomizationTool.SciFi_Weapons
{
    public class GunCustomizationModel
    {
        public const string CharmHookPrefabPath = "Assets/ithappy/Sci-Fi_Weapons/Prefabs/Charms/Charm_Hook_Main.prefab";

        public GunEntry SelectedGun { get; private set; }
        public Dictionary<AttachmentType, List<AttachmentEntry>> AttachmentOptions { get; } = new();
        public Dictionary<AttachmentType, AttachmentEntry> ActiveAttachments { get; } = new();
        public Dictionary<AttachmentType, int> VariantIndices { get; } = new();
        public Dictionary<AttachmentType, bool> AttachmentEnabled { get; } = new();
        public Dictionary<AttachmentType, bool> RayEnabled { get; } = new();

        public List<GunEntry> AllGuns { get; private set; } = new();
        public List<GunEntry> FilteredGuns { get; private set; } = new();
        public int GunIndex { get; set; }
        public GunType SelectedType { get; set; }
        public HashSet<AttachmentType> ForceDisabledTypes { get; } = new();

        public string SelectedSkinColorName
        {
            get
            {
                if (SelectedSkinIndex < 0 || SelectedSkinIndex >= Skins.Count || !Skins[SelectedSkinIndex])
                {
                    return null;
                }

                var skin = Skins[SelectedSkinIndex];
                return !string.IsNullOrEmpty(skin.DisplayName)
                    ? skin.DisplayName
                    : skin.name.Replace("ColorSet_", string.Empty);
            }
        }

        public List<SkinEntry> Skins { get; private set; } = new();
        public List<bool> SkinEnabled { get; private set; } = new();
        public int SelectedSkinIndex { get; private set; } = -1;

        public PreviewManager PreviewManager { get; }
        public HistoryManager HistoryManager { get; }

        private GunEntry _gunSlotsCacheFor;
        private HashSet<SlotType> _gunSlotsCache = new();

        public GunCustomizationModel()
        {
            PreviewManager = new PreviewManager();
            HistoryManager = new HistoryManager();
        }

        public void NextSkin(bool skipHistory = false)
        {
            if (Skins.Count == 0) return;
            var newIndex = (SelectedSkinIndex + 1) % Skins.Count;
            SetSkin(newIndex, skipHistory);
        }

        public void PreviousSkin(bool skipHistory = false)
        {
            if (Skins.Count == 0) return;
            var newIndex = (SelectedSkinIndex - 1 + Skins.Count) % Skins.Count;
            SetSkin(newIndex, skipHistory);
        }

        public void LoadSkins(string skinsFolder = "Assets/ithappy/Sci-Fi_Weapons/Configs/Skins")
        {
            Skins.Clear();
            SkinEnabled.Clear();
            if (!AssetDatabase.IsValidFolder(skinsFolder)) return;

            var guids = AssetDatabase.FindAssets("t:SkinEntry", new[] { skinsFolder });
            var skins = new List<SkinEntry>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var skin = AssetDatabase.LoadAssetAtPath<SkinEntry>(path);
                if (skin != null) skins.Add(skin);
            }

            skins.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            Skins = skins;
            foreach (var _ in Skins) SkinEnabled.Add(true);

            if (Skins.Count > 0 && SelectedSkinIndex == -1)
                SelectedSkinIndex = 0;
        }

        public void SetSkin(int index, bool skipHistory = false)
        {
            if (index < 0 || index >= Skins.Count) return;
            if (!skipHistory) HistoryManager.PushState(CaptureState());
            SelectedSkinIndex = index;
            RebuildPreview(preserveView: true);
        }

        public void RefreshGunList(string gunEntriesPath)
        {
            AllGuns = AssetDatabase.FindAssets("t:GunEntry", new[] { gunEntriesPath })
                .Select(g => AssetDatabase.LoadAssetAtPath<GunEntry>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(g => g != null)
                .ToList();
        }

        public void FilterGuns()
        {
            FilteredGuns = AllGuns.Where(g => g.GunType == SelectedType).ToList();
            GunIndex = 0;
            if (FilteredGuns.Count > 0)
            {
                SelectGun(FilteredGuns[0], skipHistory: true);
            }
        }

        public void SelectGun(GunEntry gun, bool skipHistory = false)
        {
            if (!skipHistory)
            {
                HistoryManager.PushState(CaptureState());
            }

            ForceDisabledTypes.Clear();
            SelectedGun = gun;
            AttachmentOptions.Clear();
            ActiveAttachments.Clear();
            VariantIndices.Clear();
            AttachmentEnabled.Clear();
            ForceDisabledTypes.Clear();
            RayEnabled.Clear();

            foreach (var a in gun.Slots.Where(a => a != null))
            {
                if (!AttachmentOptions.ContainsKey(a.AttachmentType))
                {
                    AttachmentOptions[a.AttachmentType] = new List<AttachmentEntry>();
                }

                AttachmentOptions[a.AttachmentType].Add(a);
            }

            foreach (var kv in AttachmentOptions)
            {
                var type = kv.Key;
                const int index = 0;

                ActiveAttachments[type] = kv.Value[index];
                VariantIndices[type] = index;
                AttachmentEnabled[type] = true;
                RayEnabled[type] = true;
            }

            var enabledTypes = AttachmentEnabled
                .Where(kv => kv.Value && kv.Key != AttachmentType.Barrel)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var type in enabledTypes)
            {
                EnforceMutualExclusivity(type);
            }

            ApplySpecialBarrelDisables();

            RebuildPreview(preserveView: false);
        }

        public void SetAttachment(AttachmentType type, AttachmentEntry entry, int variant)
        {
            ActiveAttachments[type] = entry;
            VariantIndices[type] = variant;
            if (AttachmentEnabled.TryGetValue(type, out var enabled) && enabled)
            {
                EnforceMutualExclusivity(type);
                ApplySpecialBarrelDisables();
            }

            RebuildPreview(preserveView: true);
        }

        public void SetAttachmentEnabled(AttachmentType type, bool enabled)
        {
            if (type == AttachmentType.Barrel)
            {
                return;
            }

            AttachmentEnabled[type] = enabled;
            if (enabled)
            {
                EnforceMutualExclusivity(type);
                ApplySpecialBarrelDisables();
            }

            RebuildPreview(preserveView: true);
        }

        public void SetRayEnabled(AttachmentType type, bool enabled)
        {
            if (!RayEnabled.ContainsKey(type) || RayEnabled[type] == enabled)
            {
                return;
            }

            RayEnabled[type] = enabled;
            RebuildPreview(preserveView: true);
        }

        public void RandomizeAll(Dictionary<GunType, bool> typeToggles, bool skipHistory = false)
        {
            if (!skipHistory)
            {
                HistoryManager.PushState(CaptureState());
            }

            var enabledTypes = typeToggles.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
            if (enabledTypes.Count == 0)
            {
                foreach (var t in System.Enum.GetValues(typeof(GunType)).Cast<GunType>())
                    typeToggles[t] = true;
                enabledTypes = typeToggles.Keys.ToList();
            }

            var randomType = enabledTypes[Random.Range(0, enabledTypes.Count)];
            SelectedType = randomType;
            var gunsOfType = AllGuns.Where(g => g.GunType == randomType).ToList();
            if (gunsOfType.Count == 0)
            {
                var randGun = AllGuns[Random.Range(0, AllGuns.Count)];
                SelectedType = randGun.GunType;
                gunsOfType = AllGuns.Where(g => g.GunType == SelectedType).ToList();
            }

            var selectedGun = gunsOfType[Random.Range(0, gunsOfType.Count)];
            FilteredGuns = gunsOfType;
            GunIndex = FilteredGuns.IndexOf(selectedGun);
            SelectGun(selectedGun, skipHistory: true);

            if (Skins.Count > 0)
            {
                var enabledIndices = new List<int>();
                for (var i = 0; i < Skins.Count; i++)
                {
                    if (i < SkinEnabled.Count && SkinEnabled[i])
                        enabledIndices.Add(i);
                }

                if (enabledIndices.Count == 0)
                {
                    for (var i = 0; i < Skins.Count; i++)
                        SkinEnabled[i] = true;
                    enabledIndices = Enumerable.Range(0, Skins.Count).ToList();
                }

                SelectedSkinIndex = enabledIndices[Random.Range(0, enabledIndices.Count)];
            }

            foreach (var type in AttachmentOptions.Keys.ToList())
            {
                var list = AttachmentOptions[type];
                var index = Random.Range(0, list.Count);

                VariantIndices[type] = index;
                ActiveAttachments[type] = list[index];

                if (type == AttachmentType.AimingSystem)
                {
                    AttachmentEnabled[type] = Random.value > 0.8f;
                }
                else
                {
                    AttachmentEnabled[type] = type is AttachmentType.Barrel or AttachmentType.Magazine or AttachmentType.Buttstock || Random.value > 0.5f;
                }

                RayEnabled[type] = true;
            }

            var enabledAttachmentTypes = AttachmentEnabled
                .Where(kv => kv.Value)
                .Select(kv => kv.Key)
                .OrderBy(t => t)
                .ToList();

            var previouslyEnabled = new Dictionary<AttachmentType, bool>(AttachmentEnabled);
            foreach (var type in AttachmentOptions.Keys)
                AttachmentEnabled[type] = type is AttachmentType.Barrel or AttachmentType.Magazine or AttachmentType.Buttstock;

            foreach (var type in enabledAttachmentTypes)
            {
                if (type is AttachmentType.Barrel or AttachmentType.Magazine or AttachmentType.Buttstock)
                {
                    continue;
                }

                AttachmentEnabled[type] = true;
                EnforceMutualExclusivity(type);
            }

            ApplySpecialBarrelDisables();

            RebuildPreview(preserveView: false);
            PreviewManager.CenterCameraOnGun();
        }

        public void SavePrefab(string saveName, string savePath)
        {
            if (!SelectedGun?.BasePrefab)
                return;

            var basePrefab = SelectedGun.BasePrefab;
            saveName = string.IsNullOrEmpty(saveName.Trim())
                ? $"{SelectedGun.name.Replace("GunEntry_", "")}_Custom"
                : saveName.Trim();

            if (!System.IO.Directory.Exists(savePath))
                System.IO.Directory.CreateDirectory(savePath);

            var assetPath = System.IO.Path.Combine(savePath, $"{saveName}.prefab").Replace("\\", "/");
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            // Assemble in an isolated preview scene. Building in the open scene puts the temporary
            // gun in the Hierarchy while it is assembled, and SaveAsPrefabAssetAndConnect leaves that
            // scene flagged as modified even after the object is destroyed - the next Ctrl+S then
            // writes a scene the user never edited. Nothing here needs the instance to stay connected.
            var previewScene = EditorSceneManager.NewPreviewScene();
            GameObject instance = null;

            try
            {
                instance = Object.Instantiate(basePrefab);
                instance.name = saveName;
                instance.hideFlags = HideFlags.None;
                EditorSceneManager.MoveGameObjectToScene(instance, previewScene);

                ApplyAttachmentsToInstance(instance);

                if (SelectedSkinIndex >= 0 && SelectedSkinIndex < Skins.Count && Skins[SelectedSkinIndex] != null)
                    ApplySkinRecursively(instance, Skins[SelectedSkinIndex].Material);

                PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
            }
            finally
            {
                if (instance)
                {
                    Object.DestroyImmediate(instance);
                }

                EditorSceneManager.ClosePreviewScene(previewScene);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void ApplyAttachmentsToInstance(GameObject instance)
        {
            var enabledAttachments = ActiveAttachments
                .Where(kv => AttachmentEnabled.TryGetValue(kv.Key, out var en) && en && kv.Value != null)
                .OrderBy(kv => kv.Key)
                .ToList();

            var occupiedSlots = new HashSet<Transform>();
            var clearedSlots = new HashSet<Transform>();

            foreach (var (type, entry) in enabledAttachments)
            {
                if (entry.Elements == null)
                {
                    continue;
                }

                if (type == AttachmentType.Charms)
                {
                    ApplyCharmToInstance(instance, entry, occupiedSlots, clearedSlots);
                    continue;
                }

                foreach (var element in entry.Elements)
                {
                    if (!element?.Prefab)
                    {
                        continue;
                    }

                    var targetSlotType = ResolveEffectiveSlot(type, element.SlotType);

                    var slots = instance.GetComponentsInChildren<Transform>()
                        .Where(t => SlotTypeExtensions.IsPlaceholder(t.name) && targetSlotType.Matches(t.name))
                        .ToList();

                    foreach (var slot in slots)
                    {
                        if (occupiedSlots.Contains(slot))
                        {
                            continue;
                        }

                        if (!clearedSlots.Contains(slot))
                        {
                            for (var i = slot.childCount - 1; i >= 0; i--)
                                Object.DestroyImmediate(slot.GetChild(i).gameObject);
                            clearedSlots.Add(slot);
                        }

                        var inst = AttachmentInstantiator.InstantiateForPrefab(element.Prefab, slot, type, targetSlotType);
                        inst.hideFlags = HideFlags.None;

                        if (type is AttachmentType.Laser or AttachmentType.AimingSystem)
                        {
                            var rayOn = RayEnabled.TryGetValue(type, out var r) && r;
                            foreach (var child in inst.GetComponentsInChildren<Transform>(true))
                                if (child.name.EndsWith("_Ray", System.StringComparison.OrdinalIgnoreCase))
                                {
                                    child.gameObject.SetActive(rayOn);
                                }
                        }

                        occupiedSlots.Add(slot);
                    }
                }
            }
        }

        private void ApplyCharmToInstance(GameObject instance, AttachmentEntry entry, HashSet<Transform> occupied, HashSet<Transform> cleared)
        {
            var charmSlot = instance.GetComponentsInChildren<Transform>()
                .FirstOrDefault(t => SlotTypeExtensions.IsPlaceholder(t.name) && SlotType.Charm.Matches(t.name));
            if (!charmSlot)
            {
                return;
            }

            if (!cleared.Contains(charmSlot))
            {
                for (var i = charmSlot.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(charmSlot.GetChild(i).gameObject);
                cleared.Add(charmSlot);
            }

            const string hookPath = CharmHookPrefabPath;
            var hook = AssetDatabase.LoadAssetAtPath<GameObject>(hookPath);
            if (!hook)
            {
                Debug.LogWarning($"Charm hook prefab not found at {hookPath}");
                return;
            }

            var hookInstance = AttachmentInstantiator.InstantiateForPrefab(hook, charmSlot);
            hookInstance.hideFlags = HideFlags.None;

            var nestedCharmSlot = hookInstance.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Place_Charms");

            if (nestedCharmSlot == null)
            {
                Debug.LogWarning("Charm_Hook_Main does not contain a child named 'Place_Charms'.");
                return;
            }

            if (entry.Elements.Length > 0 && entry.Elements[0].Prefab)
            {
                var charmInstance = AttachmentInstantiator.InstantiateForPrefab(entry.Elements[0].Prefab, nestedCharmSlot);
                charmInstance.hideFlags = HideFlags.None;
            }

            occupied.Add(charmSlot);
        }

        private void RebuildPreview(bool preserveView)
        {
            if (!SelectedGun)
                return;

            var prefabPath = AssetDatabase.GetAssetPath(SelectedGun.BasePrefab);
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogError($"BasePrefab is missing or not assigned for {SelectedGun.name}");
                return;
            }

            var skinMat = (SelectedSkinIndex >= 0 && SelectedSkinIndex < Skins.Count && Skins[SelectedSkinIndex] != null) ? Skins[SelectedSkinIndex].Material : null;
            PreviewManager.RebuildPreview(
                prefabPath,
                ActiveAttachments,
                AttachmentEnabled,
                RayEnabled,
                SelectedGun.GunType,
                preserveView,
                skinMat
            );
        }

        public void RestoreState(HistoryManager.HistoryState state)
        {
            SelectedGun = state.Gun;
            SelectedType = SelectedGun.GunType;
            FilteredGuns = AllGuns.Where(g => g.GunType == SelectedType).ToList();
            GunIndex = FilteredGuns.IndexOf(SelectedGun);

            AttachmentOptions.Clear();
            foreach (var a in SelectedGun.Slots.Where(a => a != null))
            {
                if (!AttachmentOptions.ContainsKey(a.AttachmentType))
                {
                    AttachmentOptions[a.AttachmentType] = new List<AttachmentEntry>();
                }

                AttachmentOptions[a.AttachmentType].Add(a);
            }

            ActiveAttachments.Clear();
            foreach (var kv in state.ActiveAttachments) ActiveAttachments[kv.Key] = kv.Value;
            VariantIndices.Clear();
            foreach (var kv in state.VariantIndices) VariantIndices[kv.Key] = kv.Value;
            AttachmentEnabled.Clear();
            foreach (var kv in state.AttachmentEnabled) AttachmentEnabled[kv.Key] = kv.Value;
            RayEnabled.Clear();
            foreach (var kv in state.RayEnabled) RayEnabled[kv.Key] = kv.Value;
            SelectedSkinIndex = state.SkinIndex;

            AttachmentEnabled[AttachmentType.Barrel] = true;
            RebuildPreview(false);
            PreviewManager.CenterCameraOnGun();
        }

        private HistoryManager.HistoryState CaptureState() => new()
        {
            Gun = SelectedGun,
            ActiveAttachments = new Dictionary<AttachmentType, AttachmentEntry>(ActiveAttachments),
            VariantIndices = new Dictionary<AttachmentType, int>(VariantIndices),
            AttachmentEnabled = new Dictionary<AttachmentType, bool>(AttachmentEnabled),
            RayEnabled = new Dictionary<AttachmentType, bool>(RayEnabled),
            SkinIndex = SelectedSkinIndex
        };

        private void EnforceMutualExclusivity(AttachmentType type)
        {
            if (!AttachmentEnabled.TryGetValue(type, out var enabled) || !enabled)
            {
                return;
            }

            if (SelectedGun != null && SelectedGun.name.Contains("AR6", System.StringComparison.OrdinalIgnoreCase))
            {
                if (type == AttachmentType.UnderbarrelGl)
                {
                    if (AttachmentEnabled.TryGetValue(AttachmentType.Muzzle, out var muzzleEnabled) && muzzleEnabled)
                    {
                        AttachmentEnabled[AttachmentType.Muzzle] = false;
                    }
                }
                else if (type == AttachmentType.Muzzle)
                {
                    if (AttachmentEnabled.TryGetValue(AttachmentType.UnderbarrelGl, out var glEnabled) && glEnabled)
                    {
                        AttachmentEnabled[AttachmentType.UnderbarrelGl] = false;
                    }
                }
            }

            var currentSlotsNormal = GetUsedSlots(type, ActiveAttachments[type]);
            foreach (var kv in ActiveAttachments)
            {
                if (kv.Key == type)
                {
                    continue;
                }

                if (!AttachmentEnabled.TryGetValue(kv.Key, out var otherEnabled) || !otherEnabled)
                {
                    continue;
                }

                if (currentSlotsNormal.Overlaps(GetUsedSlots(kv.Key, kv.Value)))
                {
                    AttachmentEnabled[kv.Key] = false;
                }
            }
        }

        // Must mirror the re-routing done when parts are instantiated: a pistol flashlight declares
        // BarRight but lands on BarBottom. Compare declared slots instead and a clash goes undetected,
        // dropping the loser in silence while its card still reads Enabled.
        private HashSet<SlotType> GetUsedSlots(AttachmentType type, AttachmentEntry entry)
        {
            var slots = new HashSet<SlotType>();
            if (entry?.Elements == null)
            {
                return slots;
            }

            foreach (var el in entry.Elements)
                if (el != null)
                {
                    slots.Add(ResolveEffectiveSlot(type, el.SlotType));
                }

            return slots;
        }

        private SlotType ResolveEffectiveSlot(AttachmentType type, SlotType declared)
        {
            if (type != AttachmentType.Flashlight && type != AttachmentType.Laser)
            {
                return declared;
            }

            if (declared != SlotType.BarLeft && declared != SlotType.BarRight)
            {
                return declared;
            }

            var gunSlots = GetGunSlots();
            return !gunSlots.Contains(declared) && gunSlots.Contains(SlotType.BarBottom)
                ? SlotType.BarBottom
                : declared;
        }

        private HashSet<SlotType> GetGunSlots()
        {
            if (_gunSlotsCacheFor == SelectedGun)
            {
                return _gunSlotsCache;
            }

            _gunSlotsCache = new HashSet<SlotType>();
            _gunSlotsCacheFor = SelectedGun;

            if (SelectedGun && SelectedGun.BasePrefab)
            {
                foreach (var t in SelectedGun.BasePrefab.GetComponentsInChildren<Transform>(true))
                {
                    if (SlotTypeExtensions.IsPlaceholder(t.name) && SlotTypeExtensions.TryResolve(t.name, out var slot))
                    {
                        _gunSlotsCache.Add(slot);
                    }
                }
            }

            return _gunSlotsCache;
        }

        private void ApplySpecialBarrelDisables()
        {
            ForceDisabledTypes.Remove(AttachmentType.Laser);
            ForceDisabledTypes.Remove(AttachmentType.Flashlight);

            if (!SelectedGun)
            {
                return;
            }

            var gunName = SelectedGun.name;
            var isAR2or3 = gunName.IndexOf("AR2", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                           gunName.IndexOf("AR3", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isAR2or3)
            {
                return;
            }

            if (!ActiveAttachments.TryGetValue(AttachmentType.Barrel, out var barrelEntry) || !barrelEntry)
            {
                return;
            }

            if (barrelEntry.name.Contains("Barrel_Stock", System.StringComparison.OrdinalIgnoreCase))
            {
                if (AttachmentEnabled.TryGetValue(AttachmentType.Laser, out var laserOn) && laserOn)
                {
                    AttachmentEnabled[AttachmentType.Laser] = false;
                }

                if (AttachmentEnabled.TryGetValue(AttachmentType.Flashlight, out var flashOn) && flashOn)
                {
                    AttachmentEnabled[AttachmentType.Flashlight] = false;
                }

                ForceDisabledTypes.Add(AttachmentType.Laser);
                ForceDisabledTypes.Add(AttachmentType.Flashlight);
            }
        }

        private static void ApplySkinRecursively(GameObject root, Material skinMaterial)
        {
            if (skinMaterial == null) return;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null &&
                        materials[i].name.IndexOf("ColorSet", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        materials[i] = skinMaterial;
                        changed = true;
                    }
                }

                if (changed)
                    renderer.sharedMaterials = materials;
            }
        }
    }
}
