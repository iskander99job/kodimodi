using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GunCustomizationTool.SciFi_Weapons
{
    public static class AttachmentIconRenderer
    {
        private const int PreviewLayer = 31;
        private const int IconSize = 512;
        private const int Supersample = 2;

        // Recovered by rendering four parts that do have icons and searching for the angle that best
        // reproduces them. Silhouette matching alone can't see a 180 degree flip on symmetric parts,
        // so the final angle came from per-pixel colour comparison once the render used Amber too.
        private const float CameraYaw = 153f;
        private const float CameraPitch = 8f;

        private const float ContentFill = 0.85f;

        private const float FieldOfView = 30f;
        private const float LightIntensity = 0.7f;
        private const int FitIterations = 4;
        private const int RefinePasses = 4;
        private const int ProbeSize = 128;
        private const float FillTolerance = 0.01f;

        private static readonly Dictionary<string, Texture2D> Cache = new();
        private static readonly Dictionary<string, GameObject> Pending = new();
        private static readonly List<string> PendingOrder = new();
        private static bool _draining;

        // Icons are rendered on the editor update tick, never inline. This is called from OnGUI, and
        // driving a camera plus ReadPixels there swaps the render target out from under the repaint
        // in progress: the icon sticks on whatever was drawn first and never follows the skin.
        public static event System.Action IconReady;

        public static Texture2D GetIcon(GameObject prefab, string colorName = null)
        {
            if (!prefab)
            {
                return null;
            }

            if (string.IsNullOrEmpty(colorName))
            {
                colorName = AttachmentSpriteAssigner.PreferredColor;
            }

            var key = prefab.GetInstanceID() + "|" + colorName;

            // Present-but-null means a render already ran and produced nothing. Treating that as a
            // miss re-queues it every OnGUI pass and never settles.
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            Enqueue(key, prefab);
            return null;
        }

        public static void ClearCache()
        {
            foreach (var texture in Cache.Values)
            {
                if (texture)
                {
                    Object.DestroyImmediate(texture);
                }
            }

            Cache.Clear();
            Pending.Clear();
            PendingOrder.Clear();
            SetDraining(false);
        }

        private static void Enqueue(string key, GameObject prefab)
        {
            if (Pending.ContainsKey(key))
            {
                return;
            }

            Pending[key] = prefab;
            PendingOrder.Add(key);
            SetDraining(true);
        }

        private static void SetDraining(bool active)
        {
            if (active == _draining)
            {
                return;
            }

            _draining = active;
            if (active)
            {
                EditorApplication.update += DrainQueue;
            }
            else
            {
                EditorApplication.update -= DrainQueue;
            }
        }

        private static void DrainQueue()
        {
            if (PendingOrder.Count == 0)
            {
                SetDraining(false);
                return;
            }

            var key = PendingOrder[0];
            PendingOrder.RemoveAt(0);
            Pending.TryGetValue(key, out var prefab);
            Pending.Remove(key);

            var colorName = key.Substring(key.IndexOf('|') + 1);
            Cache[key] = prefab ? Render(prefab, colorName) : null;

            if (PendingOrder.Count == 0)
            {
                SetDraining(false);
            }

            IconReady?.Invoke();
        }

        private static Texture2D Render(GameObject prefab, string colorName)
        {
            var instance = Object.Instantiate(prefab);
            RenderTexture renderTexture = null;
            GameObject cameraObject = null;
            GameObject lightingRig = null;
            Dictionary<Light, int> suppressed = null;

            try
            {
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.transform.position = Vector3.one * 5000f;
                instance.transform.rotation = Quaternion.identity;
                SetLayerRecursively(instance, PreviewLayer);
                ApplyIconColorSet(instance, colorName);

                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    return null;
                }

                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                var resolution = IconSize * Supersample;
                renderTexture = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32);

                cameraObject = new GameObject("AttachmentIconCamera") { hideFlags = HideFlags.HideAndDontSave };
                var camera = cameraObject.AddComponent<Camera>();
                camera.targetTexture = renderTexture;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.cameraType = CameraType.Preview;
                camera.cullingMask = 1 << PreviewLayer;
                camera.enabled = false;
                camera.fieldOfView = FieldOfView;
                camera.nearClipPlane = 0.001f;

                var direction = Quaternion.Euler(CameraPitch, CameraYaw, 0f) * Vector3.back;
                var distance = FitToBounds(camera, bounds, direction);

                lightingRig = CreateLightingRig(bounds.center);
                suppressed = SuppressSceneLights();

                distance = RefineToSilhouette(camera, bounds, direction, distance);
                PlaceCamera(camera, bounds, direction, distance);
                camera.Render();

                return Downsample(renderTexture);
            }
            finally
            {
                if (suppressed != null)
                {
                    RestoreSceneLights(suppressed);
                }

                if (lightingRig)
                {
                    Object.DestroyImmediate(lightingRig);
                }

                if (cameraObject)
                {
                    Object.DestroyImmediate(cameraObject);
                }

                Object.DestroyImmediate(instance);

                if (renderTexture)
                {
                    renderTexture.Release();
                    Object.DestroyImmediate(renderTexture);
                }
            }
        }

        private static float FitToBounds(Camera camera, Bounds bounds, Vector3 direction)
        {
            var distance = bounds.extents.magnitude / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad);

            for (var iteration = 0; iteration < FitIterations; iteration++)
            {
                PlaceCamera(camera, bounds, direction, distance);

                var occupied = 0f;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z);

                    var viewport = camera.WorldToViewportPoint(point);
                    occupied = Mathf.Max(occupied, Mathf.Abs(viewport.x - 0.5f) * 2f);
                    occupied = Mathf.Max(occupied, Mathf.Abs(viewport.y - 0.5f) * 2f);
                }

                if (occupied <= Mathf.Epsilon)
                {
                    break;
                }

                distance *= occupied / ContentFill;
            }

            PlaceCamera(camera, bounds, direction, distance);
            return distance;
        }

        private static float RefineToSilhouette(Camera camera, Bounds bounds, Vector3 direction, float distance)
        {
            var target = camera.targetTexture;
            var probe = new RenderTexture(ProbeSize, ProbeSize, 16, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(ProbeSize, ProbeSize, TextureFormat.RGBA32, false);
            camera.targetTexture = probe;

            try
            {
                for (var pass = 0; pass < RefinePasses; pass++)
                {
                    PlaceCamera(camera, bounds, direction, distance);
                    camera.Render();

                    var previous = RenderTexture.active;
                    RenderTexture.active = probe;
                    readback.ReadPixels(new Rect(0, 0, ProbeSize, ProbeSize), 0, 0);
                    readback.Apply();
                    RenderTexture.active = previous;

                    var fill = MeasureFill(readback);
                    if (fill <= Mathf.Epsilon || Mathf.Abs(fill - ContentFill) < FillTolerance)
                    {
                        break;
                    }

                    distance *= fill / ContentFill;
                }
            }
            finally
            {
                camera.targetTexture = target;
                Object.DestroyImmediate(readback);
                probe.Release();
                Object.DestroyImmediate(probe);
            }

            return distance;
        }

        private static float MeasureFill(Texture2D frame)
        {
            var pixels = frame.GetPixels();
            int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;

            for (var y = 0; y < frame.height; y++)
            {
                for (var x = 0; x < frame.width; x++)
                {
                    if (pixels[y * frame.width + x].a < 0.05f)
                    {
                        continue;
                    }

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0)
            {
                return 0f;
            }

            return Mathf.Max((maxX - minX + 1) / (float)frame.width, (maxY - minY + 1) / (float)frame.height);
        }

        private static void PlaceCamera(Camera camera, Bounds bounds, Vector3 direction, float distance)
        {
            camera.transform.position = bounds.center + direction * distance;
            camera.transform.LookAt(bounds.center);
        }

        private static Texture2D Downsample(RenderTexture source)
        {
            var full = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            RenderTexture.active = source;
            full.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            full.Apply();
            RenderTexture.active = previous;

            var sourcePixels = full.GetPixels();
            var result = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var resultPixels = new Color[IconSize * IconSize];
            var samples = Supersample * Supersample;

            for (var y = 0; y < IconSize; y++)
            {
                for (var x = 0; x < IconSize; x++)
                {
                    var accumulated = Color.clear;
                    for (var sy = 0; sy < Supersample; sy++)
                    {
                        for (var sx = 0; sx < Supersample; sx++)
                        {
                            accumulated += sourcePixels[(y * Supersample + sy) * source.width + (x * Supersample + sx)];
                        }
                    }

                    resultPixels[y * IconSize + x] = accumulated / samples;
                }
            }

            result.SetPixels(resultPixels);
            result.Apply();
            Object.DestroyImmediate(full);
            return result;
        }

        private static void ApplyIconColorSet(GameObject instance, string colorName)
        {
            var path = $"Assets/ithappy/Sci-Fi_Weapons/Materials/ColorSet_{colorName}.mat";
            var colorSet = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!colorSet)
            {
                return;
            }

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (materials[i] && materials[i].name.IndexOf("ColorSet", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        materials[i] = colorSet;
                        changed = true;
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private static GameObject CreateLightingRig(Vector3 center)
        {
            var rig = new GameObject("AttachmentIconLighting") { hideFlags = HideFlags.HideAndDontSave };
            rig.transform.position = center;

            CreateLight(rig, "Front", new Vector3(0, 2, -2));
            CreateLight(rig, "Back", new Vector3(0, 2, 2));
            CreateLight(rig, "Left", new Vector3(-2, 2, 0));
            CreateLight(rig, "Right", new Vector3(2, 2, 0));
            return rig;
        }

        private static void CreateLight(GameObject rig, string name, Vector3 relativePosition)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(rig.transform);
            lightObject.transform.localPosition = relativePosition;
            lightObject.transform.LookAt(rig.transform);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = LightIntensity;
            light.cullingMask = 1 << PreviewLayer;
            light.shadows = LightShadows.None;
        }

        private static Dictionary<Light, int> SuppressSceneLights()
        {
            var suppressed = new Dictionary<Light, int>();
            foreach (var light in Object.FindObjectsOfType<Light>())
            {
                if (!light || light.cullingMask == 1 << PreviewLayer)
                {
                    continue;
                }

                suppressed[light] = light.cullingMask;
                light.cullingMask &= ~(1 << PreviewLayer);
            }

            return suppressed;
        }

        private static void RestoreSceneLights(Dictionary<Light, int> suppressed)
        {
            foreach (var pair in suppressed)
            {
                if (pair.Key)
                {
                    pair.Key.cullingMask = pair.Value;
                }
            }
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
