using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GunCustomizationTool.SciFi_Weapons
{
    public class PreviewManager : System.IDisposable
    {
        private const int PreviewLayer = 31;

        // Sci-Fi weapons are modelled pointing down local -X, so they already face across the camera.
        // Cartoon pointed down +Z and needed -90 here.
        private const float PreviewYaw = 0f;

        private RenderTexture _renderTexture;
        private Camera _camera;
        private Transform _cameraPivot;
        private GameObject _previewRoot;
        private Vector2 _previewDrag;
        private Vector2 _panOffset;
        private float _zoom = 1f;
        private Texture2D _previewTexture;
        private readonly GUIStyle _previewBackgroundStyle;
        private GameObject _lightingRig;

        private float _defaultZoom;
        private Vector2 _defaultPanOffset;
        private Vector2 _defaultDrag;

        public bool IsViewDefault { get; private set; } = true;

        public PreviewManager()
        {
            CreateCamera();
            _previewBackgroundStyle = new GUIStyle();
        }

        public void Dispose()
        {
            if (_previewRoot)
            {
                Object.DestroyImmediate(_previewRoot);
            }

            if (_lightingRig)
            {
                Object.DestroyImmediate(_lightingRig);
            }

            if (_camera)
            {
                Object.DestroyImmediate(_camera.gameObject);
            }

            if (_cameraPivot)
            {
                Object.DestroyImmediate(_cameraPivot.gameObject);
            }

            if (_renderTexture)
            {
                _renderTexture.Release();
            }

            if (_previewTexture)
            {
                Object.DestroyImmediate(_previewTexture);
            }
        }

        private void CreateCamera()
        {
            _renderTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);

            var pivotGo = new GameObject("CameraPivot");
            pivotGo.hideFlags = HideFlags.HideAndDontSave;
            _cameraPivot = pivotGo.transform;

            var camObj = new GameObject("PreviewCamera");
            camObj.hideFlags = HideFlags.HideAndDontSave;

            _camera = camObj.AddComponent<Camera>();
            _camera.targetTexture = _renderTexture;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.cameraType = CameraType.Preview;
            _camera.fieldOfView = 30f;
            _camera.cullingMask = 1 << PreviewLayer;
            _camera.enabled = false;
            _camera.nearClipPlane = 0.01f;

            _camera.transform.SetParent(_cameraPivot);

            CreateLightingRig();
        }

        private void CreateLightingRig()
        {
            _lightingRig = new GameObject("LightingRig");
            _lightingRig.hideFlags = HideFlags.HideAndDontSave;

            const float intensity = .7f;

            CreateDirectionalLight("FrontLight", new Vector3(0, 2, -2), intensity);
            CreateDirectionalLight("BackLight", new Vector3(0, 2, 2), intensity);
            CreateDirectionalLight("LeftLight", new Vector3(-2, 2, 0), intensity);
            CreateDirectionalLight("RightLight", new Vector3(2, 2, 0), intensity);
        }

        private void CreateDirectionalLight(string name, Vector3 relativePosition, float intensity)
        {
            var lightObj = new GameObject(name);
            lightObj.transform.SetParent(_lightingRig.transform);
            lightObj.transform.localPosition = relativePosition;
            lightObj.transform.LookAt(_lightingRig.transform);

            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = intensity;
            light.cullingMask = 1 << PreviewLayer;
            light.shadows = LightShadows.None;
        }

        public void RenderPreview(Rect previewRect)
        {
            if (!_previewRoot)
            {
                return;
            }

            var allLights = Object.FindObjectsOfType<Light>();
            var originalMasks = new Dictionary<Light, int>();
            try
            {
                foreach (var light in allLights)
                {
                    if (light == null)
                    {
                        continue;
                    }

                    if (_lightingRig != null && light.transform.IsChildOf(_lightingRig.transform))
                    {
                        continue;
                    }

                    originalMasks[light] = light.cullingMask;
                    light.cullingMask &= ~(1 << PreviewLayer);
                }

                _cameraPivot.rotation = Quaternion.Euler(-_previewDrag.y, _previewDrag.x, 0);
                _camera.transform.localPosition = new Vector3(_panOffset.x, _panOffset.y, -2f * _zoom);
                _camera.Render();
            }
            finally
            {
                foreach (var kv in originalMasks)
                {
                    if (kv.Key != null)
                    {
                        kv.Key.cullingMask = kv.Value;
                    }
                }
            }

            if (_previewTexture == null || _previewTexture.width != _renderTexture.width || _previewTexture.height != _renderTexture.height)
            {
                _previewTexture = new Texture2D(_renderTexture.width, _renderTexture.height, TextureFormat.RGBA32, false);
            }

            var oldActive = RenderTexture.active;
            RenderTexture.active = _renderTexture;
            _previewTexture.ReadPixels(new Rect(0, 0, _renderTexture.width, _renderTexture.height), 0, 0);
            _previewTexture.Apply();
            RenderTexture.active = oldActive;

            _previewBackgroundStyle.normal.background = _previewTexture;
            GUI.Box(previewRect, GUIContent.none, _previewBackgroundStyle);
        }

        public void HandleInput(Rect rect, Event e, System.Action repaint)
        {
            if (!rect.Contains(e.mousePosition))
            {
                return;
            }

            var changed = false;

            if (e.type == EventType.MouseDrag)
            {
                if (e.button == 0)
                {
                    _previewDrag.x += e.delta.x * 0.5f;
                    _previewDrag.y -= e.delta.y * 0.5f;
                    _previewDrag.y = Mathf.Clamp(_previewDrag.y, -80, 80);
                    changed = true;
                    e.Use();
                    repaint();
                }
                else if (e.button == 2)
                {
                    _panOffset.x -= e.delta.x * 0.002f;
                    _panOffset.y += e.delta.y * 0.002f;
                    _panOffset.x = Mathf.Clamp(_panOffset.x, -0.8f, 0.8f);
                    _panOffset.y = Mathf.Clamp(_panOffset.y, -0.8f, 0.8f);
                    changed = true;
                    e.Use();
                    repaint();
                }
            }
            else if (e.type == EventType.ScrollWheel)
            {
                _zoom = Mathf.Clamp(_zoom + e.delta.y * 0.05f, 0.15f, 5f);
                changed = true;
                e.Use();
                repaint();
            }

            if (changed)
            {
                IsViewDefault = Mathf.Approximately(_zoom, _defaultZoom) &&
                                _panOffset == _defaultPanOffset &&
                                _previewDrag == _defaultDrag;
            }
        }

        public void ResetView()
        {
            CenterCameraOnGun();
            IsViewDefault = true;
        }

        public void CenterCameraOnGun()
        {
            if (_previewRoot == null)
            {
                return;
            }

            var renderers = _previewRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            _cameraPivot.position = bounds.center;

            var verticalFOV = _camera.fieldOfView * Mathf.Deg2Rad;
            var objectHeight = bounds.extents.y;
            var distanceY = objectHeight / Mathf.Tan(verticalFOV * 0.5f);

            var aspect = (float)_renderTexture.width / _renderTexture.height;
            var objectWidth = bounds.extents.x;
            var distanceX = objectWidth / (Mathf.Tan(verticalFOV * 0.5f) * aspect);

            var requiredDistance = Mathf.Max(distanceY, distanceX) * 1.1f;
            _zoom = Mathf.Clamp(requiredDistance / 2f, 0.5f, 5f);
            _panOffset = Vector2.zero;
            _previewDrag = Vector2.zero;

            _defaultZoom = _zoom;
            _defaultPanOffset = _panOffset;
            _defaultDrag = _previewDrag;
            IsViewDefault = true;
        }

        public void RebuildPreview(string basePrefabPath,
            Dictionary<AttachmentType, AttachmentEntry> activeAttachments,
            Dictionary<AttachmentType, bool> attachmentEnabled,
            Dictionary<AttachmentType, bool> rayEnabled,
            GunType gunType,
            bool preserveView = false,
            Material skinMaterial = null)
        {
            var savedRotation = _cameraPivot ? _cameraPivot.rotation : Quaternion.identity;
            var savedCamLocalPos = _camera ? _camera.transform.localPosition : new Vector3(0, 0, -2f);
            var savedZoom = _zoom;
            var savedPan = _panOffset;
            var savedDrag = _previewDrag;

            if (_previewRoot)
                Object.DestroyImmediate(_previewRoot);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePrefabPath);
            if (!prefab)
            {
                Debug.LogError($"Missing prefab: {basePrefabPath}");
                return;
            }

            _previewRoot = Object.Instantiate(prefab);
            _previewRoot.hideFlags = HideFlags.HideAndDontSave;
            _previewRoot.transform.rotation = Quaternion.Euler(0, PreviewYaw, 0);
            _previewRoot.transform.position = Vector3.one * 1000f;

            SetLayerRecursively(_previewRoot, PreviewLayer);

            if (_lightingRig != null)
                _lightingRig.transform.position = _previewRoot.transform.position;

            var enabledAttachments = new List<(AttachmentType type, AttachmentEntry entry)>();
            foreach (var kv in activeAttachments)
            {
                if (attachmentEnabled.TryGetValue(kv.Key, out var enabled) && enabled && kv.Value != null)
                    enabledAttachments.Add((kv.Key, kv.Value));
            }

            enabledAttachments.Sort((a, b) => a.type.CompareTo(b.type));

            var slotsToFill = new HashSet<Transform>();
            var slotToAttachmentMap = new Dictionary<Transform, (AttachmentType type, AttachmentEntry entry, AttachmentEntry.AttachmentElement element, SlotType slotType)>();

            foreach (var (type, entry) in enabledAttachments)
            {
                if (entry.Elements == null) continue;

                if (type == AttachmentType.Charms)
                {
                    var charmSlot = _previewRoot.GetComponentsInChildren<Transform>()
                        .FirstOrDefault(t => SlotTypeExtensions.IsPlaceholder(t.name) && SlotType.Charm.Matches(t.name));
                    if (charmSlot == null) continue;

                    if (!slotsToFill.Contains(charmSlot))
                    {
                        slotsToFill.Add(charmSlot);
                        slotToAttachmentMap[charmSlot] = (type, entry, null, SlotType.Charm);
                    }

                    continue;
                }

                foreach (var element in entry.Elements)
                {
                    if (element?.Prefab == null) continue;

                    var targetSlotType = element.SlotType;

                    if ((type == AttachmentType.Flashlight || type == AttachmentType.Laser) &&
                        (targetSlotType == SlotType.BarLeft || targetSlotType == SlotType.BarRight))
                    {
                        var placeholders = _previewRoot.GetComponentsInChildren<Transform>()
                            .Where(t => SlotTypeExtensions.IsPlaceholder(t.name))
                            .ToList();
                        var hasDeclaredSide = placeholders.Any(t => targetSlotType.Matches(t.name));
                        var hasBarBottom = placeholders.Any(t => SlotType.BarBottom.Matches(t.name));
                        if (!hasDeclaredSide && hasBarBottom)
                            targetSlotType = SlotType.BarBottom;
                    }

                    var targetSlots = _previewRoot.GetComponentsInChildren<Transform>()
                        .Where(t => SlotTypeExtensions.IsPlaceholder(t.name) && targetSlotType.Matches(t.name))
                        .ToList();

                    foreach (var slot in targetSlots)
                    {
                        if (!slotsToFill.Contains(slot))
                        {
                            slotsToFill.Add(slot);
                            slotToAttachmentMap[slot] = (type, entry, element, targetSlotType);
                        }
                    }
                }
            }

            var allSlots = _previewRoot.GetComponentsInChildren<Transform>()
                .Where(t => SlotTypeExtensions.IsPlaceholder(t.name)).ToList();
            foreach (var slot in allSlots)
            {
                for (var i = slot.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(slot.GetChild(i).gameObject);
            }

            foreach (var slot in slotsToFill)
            {
                if (!slotToAttachmentMap.TryGetValue(slot, out var map)) continue;

                if (map.type == AttachmentType.Charms)
                {
                    const string hookPrefabPath = GunCustomizationModel.CharmHookPrefabPath;
                    var hookPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(hookPrefabPath);
                    if (hookPrefab == null) continue;

                    var hookInstance = AttachmentInstantiator.InstantiateForPreview(hookPrefab, slot);
                    hookInstance.hideFlags = HideFlags.HideAndDontSave;
                    SetLayerRecursively(hookInstance, PreviewLayer);

                    var nestedCharmSlot = hookInstance.GetComponentsInChildren<Transform>(true)
                        .FirstOrDefault(t => t.name == "Place_Charms");
                    if (nestedCharmSlot != null && map.entry.Elements.Length > 0 && map.entry.Elements[0].Prefab != null)
                    {
                        var charmInstance = AttachmentInstantiator.InstantiateForPreview(map.entry.Elements[0].Prefab, nestedCharmSlot);
                        charmInstance.hideFlags = HideFlags.HideAndDontSave;
                        SetLayerRecursively(charmInstance, PreviewLayer);
                    }
                }
                else
                {
                    var instance = AttachmentInstantiator.InstantiateForPreview(map.element.Prefab, slot, map.type, map.slotType);
                    instance.hideFlags = HideFlags.HideAndDontSave;
                    SetLayerRecursively(instance, PreviewLayer);

                    if (map.type is AttachmentType.Laser or AttachmentType.AimingSystem)
                    {
                        var enableRay = rayEnabled.TryGetValue(map.type, out var rayOn) && rayOn;
                        foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                        {
                            if (child.name.EndsWith("_Ray", StringComparison.OrdinalIgnoreCase))
                                child.gameObject.SetActive(enableRay);
                        }
                    }
                }
            }

            if (skinMaterial != null)
                ApplySkinRecursively(_previewRoot, skinMaterial);

            _previewRoot.SetActive(false);
            _previewRoot.SetActive(true);

            if (preserveView && _cameraPivot != null && _camera != null)
            {
                _cameraPivot.rotation = savedRotation;
                _camera.transform.localPosition = savedCamLocalPos;
                _zoom = savedZoom;
                _panOffset = savedPan;
                _previewDrag = savedDrag;
                IsViewDefault = Mathf.Approximately(_zoom, _defaultZoom) &&
                                _panOffset == _defaultPanOffset &&
                                _previewDrag == _defaultDrag;
            }
            else
            {
                CenterCameraOnGun();
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

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform t in obj.transform)
                SetLayerRecursively(t.gameObject, layer);
        }
    }
}
