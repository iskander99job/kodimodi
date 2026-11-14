using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CurrencyUIGenerator
{
    private const string RootName = "CurrencyUI";
    private const string AssetFolder = "Assets/GeneratedUI/Currency";

    // Размеры в единицах Canvas.
    private const float Height = 82f;
    private const float BalanceWidth = 406f;
    private const float Gap = 28f;
    private const float RootWidth = BalanceWidth + Gap + Height;

    [MenuItem("Tools/Game UI/Create Currency UI")]
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
            Selection.activeGameObject = existing.gameObject;

            EditorUtility.DisplayDialog(
                "CurrencyUI",
                "CurrencyUI уже существует. " +
                "Удали старый объект CurrencyUI из Hierarchy " +
                "и запусти генератор ещё раз.",
                "OK");

            return;
        }

        TMP_FontAsset font = FindFont(canvas);

        if (font == null)
        {
            EditorUtility.DisplayDialog(
                "TextMeshPro",
                "Назначь Default Font Asset в настройках TMP.",
                "OK");
            return;
        }

        Sprite panelSprite = RoundedSprite(
            "Panel",
            12f,
            new Color32(247, 247, 247, 255),
            Color.white,
            new Color32(175, 175, 175, 255));

        Sprite blackSprite = RoundedSprite(
            "BlackButton",
            6f,
            Color.black,
            Color.black,
            Color.black);

        Sprite emptySprite = EmptySprite();

        RectTransform root = Rect(
            RootName,
            canvas.transform,
            0, 0, RootWidth, Height);

        // Правый верхний угол.
        root.anchorMin = root.anchorMax = new Vector2(1, 1);
        root.pivot = new Vector2(1, 1);
        root.anchoredPosition = new Vector2(-34, -30);

        // Popup-окна остаются поверх этого блока.
        root.SetAsFirstSibling();

        // ---------- Баланс ----------

        Image balancePanel = Box(
            "BalancePanel", root,
            0, 0, BalanceWidth, Height,
            Color.white);

        balancePanel.sprite = panelSprite;
        balancePanel.type = Image.Type.Sliced;

        Image coinIcon = Box(
            "CoinIcon", balancePanel.transform,
            22, 17, 58, 48,
            Color.black);

        coinIcon.sprite = emptySprite;
        coinIcon.preserveAspect = true;

        Box(
            "Divider", balancePanel.transform,
            106, 16, 1.5f, Height - 32,
            new Color32(160, 160, 160, 255));

        CreateBalanceText(
            balancePanel.transform,
            font);

        // ---------- Чёрная кнопка + ----------

        Image addBackground = Box(
            "AddCurrencyButton", balancePanel.transform,
            318, 16, 64, 50,
            Color.white);

        addBackground.sprite = blackSprite;
        addBackground.type = Image.Type.Sliced;

        AddButton(addBackground);

        // Плюс из двух прямоугольников:
        // не зависит от наличия символа в шрифте.
        RectTransform plus = Rect(
            "Plus", addBackground.transform,
            20, 13, 24, 24);

        Box(
            "Horizontal", plus,
            0, 10, 24, 4,
            Color.white);

        Box(
            "Vertical", plus,
            10, 0, 4, 24,
            Color.white);

        // ---------- Настройки ----------

        Image settingsBackground = Box(
            "SettingsButton", root,
            BalanceWidth + Gap, 0,
            Height, Height,
            Color.white);

        settingsBackground.sprite = panelSprite;
        settingsBackground.type = Image.Type.Sliced;

        AddButton(settingsBackground);

        Image settingsIcon = Box(
            "SettingsIcon", settingsBackground.transform,
            19, 19, 44, 44,
            Color.black);

        settingsIcon.sprite = emptySprite;
        settingsIcon.preserveAspect = true;

        Undo.RegisterCreatedObjectUndo(
            root.gameObject,
            "Create Currency UI");

        Selection.activeGameObject = root.gameObject;

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        Debug.Log(
            "CurrencyUI создан. Назначь Source Image у CoinIcon " +
            "и SettingsIcon. Действия кнопок — через On Click().",
            root);
    }

    private static void CreateBalanceText(
        Transform parent,
        TMP_FontAsset font)
    {
        RectTransform rect = Rect(
            "BalanceText", parent,
            128, 0, 170, Height);

        TextMeshProUGUI text =
            rect.gameObject.AddComponent<TextMeshProUGUI>();

        text.font = font;
        text.text = "2 500";
        text.fontSize = 40;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.MidlineLeft;

        text.enableAutoSizing = true;
        text.fontSizeMin = 24;
        text.fontSizeMax = 40;

        text.enableWordWrapping = false;
        text.raycastTarget = false;
    }

    private static void AddButton(Image background)
    {
        background.raycastTarget = true;

        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.1f;
        button.colors = colors;

        button.navigation = new Navigation
        {
            mode = Navigation.Mode.None
        };
    }

    private static TMP_FontAsset FindFont(Canvas canvas)
    {
        foreach (TMP_Text text in
                 canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.font != null)
                return text.font;
        }

        return TMP_Settings.defaultFontAsset;
    }

    private static Canvas FindCanvas()
    {
        Canvas result = null;

        foreach (Canvas candidate in
                 Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (EditorUtility.IsPersistent(candidate))
                continue;

            var scene = candidate.gameObject.scene;

            if (!scene.IsValid() || !scene.isLoaded ||
                candidate.name != "MainCanvas")
                continue;

            if (result != null)
            {
                EditorUtility.DisplayDialog(
                    "Canvas",
                    "Найдено несколько MainCanvas.",
                    "OK");

                return null;
            }

            result = candidate;
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
        string name,
        Transform parent,
        float x,
        float y,
        float width,
        float height)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform));

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
        string name,
        Transform parent,
        float x,
        float y,
        float width,
        float height,
        Color color)
    {
        RectTransform rect = Rect(
            name, parent,
            x, y, width, height);

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    private static Sprite RoundedSprite(
        string name,
        float radius,
        Color bottom,
        Color top,
        Color border)
    {
        string path = $"{AssetFolder}/{name}.png";

        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (existing != null)
            return existing;

        const int size = 64;

        Texture2D texture = new Texture2D(
            size, size,
            TextureFormat.RGBA32,
            false);

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

                Color fill = Color.Lerp(
                    bottom, top, y / (size - 1f));

                Color color = Color.Lerp(border, fill, inner);
                color.a *= alpha;

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();

        Sprite result = SaveSprite(
            texture,
            path,
            new Vector4(14, 14, 14, 14));

        Object.DestroyImmediate(texture);

        return result;
    }

    private static Sprite EmptySprite()
    {
        string path = $"{AssetFolder}/EmptyIcon.png";

        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (existing != null)
            return existing;

        Texture2D texture = new Texture2D(
            2, 2,
            TextureFormat.RGBA32,
            false);

        texture.SetPixels(new[]
        {
            Color.clear, Color.clear,
            Color.clear, Color.clear
        });

        texture.Apply();

        Sprite result = SaveSprite(
            texture, path, Vector4.zero);

        Object.DestroyImmediate(texture);

        return result;
    }

    private static Sprite SaveSprite(
        Texture2D texture,
        string path,
        Vector4 border)
    {
        Directory.CreateDirectory(AssetFolder);
        File.WriteAllBytes(path, texture.EncodeToPNG());

        AssetDatabase.ImportAsset(
            path,
            ImportAssetOptions.ForceSynchronousImport);

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
        importer.textureCompression =
            TextureImporterCompression.Uncompressed;

        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}