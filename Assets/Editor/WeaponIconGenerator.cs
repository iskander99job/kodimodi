
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class WeaponIconGenerator
{
    private const int IconSize = 512;
    private const string OutputFolder = "Assets/WeaponIcons";

    [MenuItem("Assets/Generate Weapon Icon", true)]
    private static bool ValidateGenerate()
    {
        return Selection.activeObject is GameObject go &&
               PrefabUtility.IsPartOfPrefabAsset(go);
    }

    [MenuItem("Assets/Generate Weapon Icon")]
    private static void Generate()
    {
        GameObject prefab = Selection.activeGameObject;

        if (prefab == null)
            return;

        Directory.CreateDirectory(OutputFolder);

        GameObject root = null;
        GameObject cameraObject = null;
        RenderTexture renderTexture = null;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            // Создаём временную копию префаба
            root = Object.Instantiate(prefab);
            root.name = "IconPreview_" + prefab.name;
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;

            // Удаляем компоненты, которые могут влиять на рендер
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;

            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
                animator.enabled = false;

            // Отдельный слой для рендера
            const int previewLayer = 31;

            SetLayerRecursively(root, previewLayer);

            // Находим границы модели
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

            Bounds bounds = new Bounds();
            bool hasBounds = false;

            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                Debug.LogError("Prefab не содержит видимых Renderer.");
                return;
            }

            // Создаём камеру
            cameraObject = new GameObject("WeaponIconCamera");
            Camera camera = cameraObject.AddComponent<Camera>();

            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0, 0, 0, 0);
            camera.cullingMask = 1 << previewLayer;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            // Настройка URP
            UniversalAdditionalCameraData cameraData =
                cameraObject.GetComponent<UniversalAdditionalCameraData>();

            if (cameraData != null)
            {
                cameraData.renderType = CameraRenderType.Base;
                cameraData.renderPostProcessing = false;
            }

            // Ракурс: смотрим вдоль локальной оси Z
            Quaternion rotation = Quaternion.LookRotation(
                Vector3.forward,
                Vector3.up
            );

            camera.transform.rotation = rotation;

            // Определяем необходимый размер кадра
            Vector3 size = bounds.size;

// Отступ от краёв: 3%
            float padding = 0.85f;

// Для квадратной иконки
            float requiredHeight = Mathf.Max(size.x, size.y);

            camera.orthographicSize =
                Mathf.Max(requiredHeight * padding * 0.5f, 0.01f);

            camera.transform.position =
                bounds.center - camera.transform.forward *
                (bounds.extents.magnitude + 10f);

            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = Mathf.Max(
                bounds.extents.magnitude * 4f + 20f,
                100f
            );

            // Render Texture с альфа-каналом
            renderTexture = new RenderTexture(
                IconSize,
                IconSize,
                24,
                RenderTextureFormat.ARGB32
            );

            renderTexture.Create();
            camera.targetTexture = renderTexture;

            // Рендерим
            camera.Render();

            RenderTexture.active = renderTexture;

            Texture2D texture = new Texture2D(
                IconSize,
                IconSize,
                TextureFormat.RGBA32,
                false
            );

            texture.ReadPixels(
                new Rect(0, 0, IconSize, IconSize),
                0,
                0
            );

            texture.Apply();

            string path = OutputFolder + "/" + prefab.name + ".png";

            File.WriteAllBytes(path, texture.EncodeToPNG());

            Object.DestroyImmediate(texture);

            camera.targetTexture = null;
            RenderTexture.active = previousActive;

            AssetDatabase.ImportAsset(path);

            TextureImporter importer =
                AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            Debug.Log("Weapon icon generated: " + path);

            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        finally
        {
            RenderTexture.active = previousActive;

            if (cameraObject != null)
                Object.DestroyImmediate(cameraObject);

            if (root != null)
                Object.DestroyImmediate(root);

            if (renderTexture != null)
            {
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }
        }
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
