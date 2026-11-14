using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SideMenuUIGenerator
{
    private const string RootName = "SideMenuUI";
    private const string AssetFolder = "Assets/GeneratedUI/SideMenu";
    private const float BadgeRadius = 4f;

    [MenuItem("Tools/Game UI/Create Side Menu")]
    private static void Generate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Canvas canvas = FindCanvas();
        if (canvas == null)
            return;

        Transform existing = canvas.transform.Find(RootName);

        if (existing != null)
        {
            UpdateExistingBadge(existing);
            return;
        }

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;

        foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.font == null)
                continue;

            font = text.font;
            break;
        }

        if (font == null)
        {
            EditorUtility.DisplayDialog(
                "TextMeshPro",
                "Назначь Default Font Asset в настройках TMP.",
                "OK");
            return;
        }

        Sprite normal = RoundedSprite(
            "Normal",
            new Color32(245, 245, 245, 255),
            Color.white,
            new Color32(170, 170, 170, 255));

        Sprite highlighted = RoundedSprite(
            "Highlighted",
            new Color32(249, 246, 235, 255),
            Color.white,
            new Color32(224, 158, 0, 255));

        Sprite badge = CreateBadgeSprite();
        Sprite emptyIcon = EmptySprite();

        RectTransform root = Rect(
            RootName, canvas.transform,
            56, 230, 540, 300);

        root.SetAsFirstSibling();

        CreateButton(
            "WeaponShopButton", root,
            "МАГАЗИН ОРУЖИЯ", 0,
            highlighted, emptyIcon, font, badge);

        CreateButton(
            "ArsenalButton", root,
            "АРСЕНАЛ", 106,
            normal, emptyIcon, font);

        CreateButton(
            "RatingButton", root,
            "РЕЙТИНГ", 212,
            normal, emptyIcon, font);

        Undo.RegisterCreatedObjectUndo(
            root.gameObject, "Create Side Menu");

        Selection.activeGameObject = root.gameObject;
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        Debug.Log("SideMenuUI создан.", root);
    }

    private static Sprite CreateBadgeSprite()
    {
        return RoundedSprite(
            "BadgeRadius4",
            new Color32(237, 171, 5, 255),
            new Color32(255, 219, 82, 255),
            new Color32(235, 174, 20, 255),
            BadgeRadius);
    }

    private static void UpdateExistingBadge(Transform root)
    {
        Transform badgeTransform = root.Find("WeaponShopButton/NewBadge");

        if (badgeTransform == null ||
            !badgeTransform.TryGetComponent<Image>(out var badgeImage))
        {
            Debug.LogWarning(
                "В SideMenuUI не найден WeaponShopButton/NewBadge.",
                root);
            return;
        }

        Sprite sprite = CreateBadgeSprite();

        Undo.RecordObject(badgeImage, "Update NewBadge");

        badgeImage.sprite = sprite;
        badgeImage.type = Image.Type.Sliced;

        PrefabUtility.RecordPrefabInstancePropertyModifications(badgeImage);
        EditorUtility.SetDirty(badgeImage);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        Selection.activeGameObject = badgeTransform.gameObject;

        Debug.Log("Скругление NewBadge обновлено.", badgeImage);
    }

    private static void CreateButton(
        string name,
        Transform parent,
        string title,
        float y,
        Sprite background,
        Sprite emptyIcon,
        TMP_FontAsset font,
        Sprite badge = null)
    {
        RectTransform rect = Rect(name, parent, 0, y, 540, 88);

        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = background;
        image.type = Image.Type.Sliced;
        image.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.97f, 0.97f, 0.97f);
        colors.pressedColor = new Color(0.88f, 0.88f, 0.88f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.1f;
        button.colors = colors;

        button.navigation = new Navigation
        {
            mode = Navigation.Mode.None
        };

        Image icon = Box(
            "Icon", rect,
            26, 20, 54, 48,
            Color.white);

        icon.sprite = emptyIcon;
        icon.preserveAspect = true;

        Box(
            "Divider", rect,
            106, 20, 1.5f, 48,
            new Color32(180, 180, 180, 255));

        TMP_Text label = Label(
            "Label", rect, font, title,
            134, 0, badge != null ? 284 : 380, 88,
            30, TextAlignmentOptions.MidlineLeft);

        label.enableAutoSizing = true;
        label.fontSizeMin = 22;
        label.fontSizeMax = 30;

        if (badge != null)
        {
            Image badgeImage = Box(
                "NewBadge", rect,
                430, 27, 90, 34,
                Color.white);

            badgeImage.sprite = badge;
            badgeImage.type = Image.Type.Sliced;

            Label(
                "Label", badgeImage.transform, font, "НОВОЕ",
                0, 0, 90, 34,
                20, TextAlignmentOptions.Center);
        }
    }

    private static Canvas FindCanvas()
    {
        Canvas result = null;

        foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (EditorUtility.IsPersistent(canvas))
                continue;

            var scene = canvas.gameObject.scene;

            if (!scene.IsValid() || !scene.isLoaded ||
                canvas.name != "MainCanvas")
                continue;

            if (result != null)
            {
                EditorUtility.DisplayDialog(
                    "Canvas",
                    "Найдено несколько MainCanvas.",
                    "OK");
                return null;
            }

            result = canvas;
        }

        if (result == null)
        {
            EditorUtility.DisplayDialog(
                "Canvas",
                "Не найден Canvas с именем MainCanvas.",
                "OK");
        }

        return result;
    }

    private static RectTransform Rect(
        string name, Transform parent,
        float x, float y, float width, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);

        return rect;
    }

    private static Image Box(
        string name, Transform parent,
        float x, float y, float width, float height,
        Color color)
    {
        RectTransform rect = Rect(
            name, parent, x, y, width, height);

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    private static TMP_Text Label(
        string name, Transform parent,
        TMP_FontAsset font, string value,
        float x, float y, float width, float height,
        float size, TextAlignmentOptions alignment)
    {
        RectTransform rect = Rect(
            name, parent, x, y, width, height);

        TextMeshProUGUI text =
            rect.gameObject.AddComponent<TextMeshProUGUI>();

        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.black;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.raycastTarget = false;

        return text;
    }

    private static Sprite RoundedSprite(
        string name,
        Color bottom,
        Color top,
        Color border,
        float radius = 12f)
    {
        string path = $"{AssetFolder}/{name}.png";

        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
            return existing;

        const int size = 64;

        Texture2D texture = new Texture2D(
            size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - size / 2f)
                           - (size / 2f - radius);

                float qy = Mathf.Abs(y + 0.5f - size / 2f)
                           - (size / 2f - radius);

                float distance =
                    new Vector2(
                        Mathf.Max(qx, 0),
                        Mathf.Max(qy, 0)).magnitude
                    + Mathf.Min(Mathf.Max(qx, qy), 0)
                    - radius;

                float alpha = Mathf.Clamp01(0.5f - distance);
                float inner = Mathf.Clamp01(-distance - 1f);

                Color fill = Color.Lerp(bottom, top, y / (size - 1f));
                Color color = Color.Lerp(border, fill, inner);
                color.a *= alpha;

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();

        Sprite sprite = SaveSprite(
            texture, path, new Vector4(14, 14, 14, 14));

        Object.DestroyImmediate(texture);
        return sprite;
    }

    private static Sprite EmptySprite()
    {
        string path = $"{AssetFolder}/EmptyIcon.png";

        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
            return existing;

        Texture2D texture = new Texture2D(
            2, 2, TextureFormat.RGBA32, false);

        texture.SetPixels(new[]
        {
            Color.clear, Color.clear,
            Color.clear, Color.clear
        });

        texture.Apply();

        Sprite sprite = SaveSprite(texture, path, Vector4.zero);
        Object.DestroyImmediate(texture);

        return sprite;
    }

    private static Sprite SaveSprite(
        Texture2D texture, string path, Vector4 border)
    {
        Directory.CreateDirectory(AssetFolder);
        File.WriteAllBytes(path, texture.EncodeToPNG());

        AssetDatabase.ImportAsset(
            path, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer =
            (TextureImporter)AssetImporter.GetAtPath(path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;
        importer.spritePixelsPerUnit = 100;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}