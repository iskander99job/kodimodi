using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class WeaponShopUIGenerator
{
    private const string RootName = "WeaponShopUI";
    private const string AssetFolder = "Assets/GeneratedUI/WeaponShop";

    private static TMP_FontAsset font;
    private static Sprite panelSprite;
    private static Sprite selectedSprite;
    private static Sprite blackSprite;
    private static Sprite emptySprite;

    private static readonly Color Gray =
        new Color32(105, 105, 110, 255);

    private static readonly Color Line =
        new Color32(205, 205, 210, 255);

    private static readonly Color Gold =
        new Color32(245, 185, 0, 255);

    [MenuItem("Tools/Game UI/Create Weapon Shop")]
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
            Debug.Log("WeaponShopUI уже существует.", existing);
            return;
        }

        font = TMP_Settings.defaultFontAsset;

        foreach (TMP_Text text in
                 canvas.GetComponentsInChildren<TMP_Text>(true))
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

        panelSprite = RoundedSprite(
            "Panel", 12,
            new Color32(250, 250, 252, 255),
            Color.white, Line);

        selectedSprite = RoundedSprite(
            "SelectedPanel", 12,
            new Color32(250, 250, 252, 255),
            Color.white, Gold);

        blackSprite = RoundedSprite(
            "BlackButton", 6,
            Color.black, Color.black, Color.black);

        emptySprite = EmptySprite();

        RectTransform root = CreateRect(RootName, canvas.transform);
        Stretch(root);

        Image background = root.gameObject.AddComponent<Image>();
        background.color = Color.white;
        background.raycastTarget = true;

        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        group.interactable = true;
        group.blocksRaycasts = true;

        CreateHeader(root);
        CreateCategories(root);
        CreateSectionHeader(root);
        CreateWeaponGrid(root);

        Undo.RegisterCreatedObjectUndo(
            root.gameObject, "Create Weapon Shop UI");

        Selection.activeGameObject = root.gameObject;
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        Debug.Log(
            "WeaponShopUI создан. Назначь картинки в WeaponImage, " +
            "иконки — в Icon, CoinIcon, SettingsIcon и LockIcon. " +
            "Действия кнопок назначаются через On Click().",
            root);
    }

    // ---------- Верхняя панель ----------

    private static void CreateHeader(RectTransform root)
    {
        RectTransform header = Rect(
            "Header", root, 28, 16, 1864, 72);

        Image back = Panel(
            "BackButton", header,
            0, 0, 80, 64, panelSprite);

        AddButton(back);

        // Стрелка назад из двух прямоугольников.
        Image upper = Box(
            "ArrowUpper", back.transform,
            27, 23, 24, 5, Color.black);

        upper.rectTransform.localRotation =
            Quaternion.Euler(0, 0, 45);

        Image lower = Box(
            "ArrowLower", back.transform,
            27, 23, 24, 5, Color.black);

        lower.rectTransform.localRotation =
            Quaternion.Euler(0, 0, -45);

        Box(
            "Divider", header,
            104, 0, 1.5f, 64, Gray);

        Label(
            "Title", header,
            "МАГАЗИН ОРУЖИЯ",
            136, 0, 1030, 64,
            54, Color.black);

        RectTransform currency = Rect(
            "CurrencyUI", header,
            0, 0, 478, 64);

        currency.anchorMin = currency.anchorMax = new Vector2(1, 1);
        currency.pivot = new Vector2(1, 1);
        currency.anchoredPosition = Vector2.zero;

        Image balance = Panel(
            "BalancePanel", currency,
            0, 0, 380, 64, panelSprite);

        Icon(
            "CoinIcon", balance.transform,
            20, 12, 48, 40);

        Box(
            "Divider", balance.transform,
            88, 12, 1.5f, 40, Gray);

        Label(
            "BalanceText", balance.transform,
            "2 500",
            116, 0, 160, 64,
            40, Color.black);

        Image add = Panel(
            "AddCurrencyButton", balance.transform,
            296, 12, 60, 40, blackSprite);

        AddButton(add);

        Box(
            "PlusHorizontal", add.transform,
            19, 18, 22, 4, Color.white);

        Box(
            "PlusVertical", add.transform,
            28, 9, 4, 22, Color.white);

        Image settings = Panel(
            "SettingsButton", currency,
            414, 0, 64, 64, panelSprite);

        AddButton(settings);

        Icon(
            "SettingsIcon", settings.transform,
            13, 13, 38, 38);
    }

    // ---------- Категории ----------

    private static void CreateCategories(RectTransform root)
    {
        RectTransform categories = Rect(
            "Categories", root,
            28, 108, 1864, 112);

        string[] names =
        {
            "AR", "SMG", "PISTOL", "SHOTGUN", "MG", "SR", "GL"
        };

        float width = 1864f / names.Length;

        for (int i = 0; i < names.Length; i++)
        {
            RectTransform tab = Rect(
                names[i] + "Button", categories,
                i * width, 0, width, 112);

            Image hitArea = tab.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;

            AddButton(hitArea);

            Icon(
                "Icon", tab,
                (width - 150) / 2, 0, 150, 50);

            TMP_Text label = Label(
                "Label", tab, names[i],
                0, 54, width, 42,
                30, i == 0 ? Color.black : Gray);

            label.alignment = TextAlignmentOptions.Center;

            Image underline = Box(
                "SelectedUnderline", tab,
                8, 106, width - 16, 6, Gold);

            underline.gameObject.SetActive(i == 0);

            if (i < names.Length - 1)
            {
                Box(
                    "Divider", tab,
                    width - 1, 0, 1, 96, Line);
            }
        }

        Box(
            "CategoriesDivider", root,
            28, 220, 1864, 1.5f, Line);
    }

    private static void CreateSectionHeader(RectTransform root)
    {
        RectTransform section = Rect(
            "SectionHeader", root,
            28, 242, 1864, 52);

        Label(
            "CategoryTitle", section,
            "ШТУРМОВЫЕ ВИНТОВКИ",
            0, 0, 600, 52,
            36, Color.black);

        Box(
            "Divider", section,
            618, 7, 1.5f, 38, Gray);

        Label(
            "WeaponCount", section,
            "6 моделей",
            646, 0, 240, 52,
            28, Gray);

        Box(
            "SectionDivider", root,
            28, 308, 1864, 1.5f, Line);
    }

    // ---------- Сетка оружия ----------

    private static void CreateWeaponGrid(RectTransform root)
    {
        RectTransform scrollRoot = CreateRect("WeaponsScroll", root);

        scrollRoot.anchorMin = Vector2.zero;
        scrollRoot.anchorMax = Vector2.one;
        scrollRoot.offsetMin = new Vector2(28, 20);
        scrollRoot.offsetMax = new Vector2(-28, -326);

        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35;

        RectTransform viewport = CreateRect("Viewport", scrollRoot);
        Stretch(viewport);

        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;

        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        Canvas.ForceUpdateCanvases();

        float availableWidth = viewport.rect.width;

        if (availableWidth <= 0)
            availableWidth = 1864;

        float cardWidth = (availableWidth - 40) / 3f;
        const float cardHeight = 354f;

        GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(cardWidth, cardHeight);
        grid.spacing = new Vector2(20, 20);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter fitter =
            content.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;

        CreateWeaponCard(content, "M4A1", 1800, null, true, cardWidth);
        CreateWeaponCard(content, "AK-12", 2200, null, false, cardWidth);
        CreateWeaponCard(content, "FAMAS", 2400, null, false, cardWidth);

        CreateWeaponCard(
            content, "SCAR-L", 3200,
            "Нужен Лейтенант", false, cardWidth);

        CreateWeaponCard(
            content, "HK416", 4000,
            "Нужен Капитан", false, cardWidth);

        CreateWeaponCard(
            content, "AUG A3", 4800,
            "Нужен Майор", false, cardWidth);
    }

    private static void CreateWeaponCard(
        Transform parent,
        string weaponName,
        int price,
        string rankRequirement,
        bool selected,
        float width)
    {
        Image card = Panel(
            weaponName.Replace(" ", "_") + "Card",
            parent,
            0, 0, width, 354,
            selected ? selectedSprite : panelSprite);

        RectTransform rect = card.rectTransform;

        Image weapon = Box(
            "WeaponImage", rect,
            22, 16, width - 44, 212,
            Color.white);

        weapon.sprite = emptySprite;
        weapon.preserveAspect = true;

        Label(
            "WeaponName", rect,
            weaponName,
            24, 236, 250, 44,
            36, Color.black);

        // Требование ранга отображается только для закрытого оружия.
        RectTransform requirement = Rect(
            "RankRequirement", rect,
            width - 280, 242, 256, 32);

        Icon(
            "LockIcon", requirement,
            0, 0, 26, 32);

        TMP_Text rankLabel = Label(
            "RequiredRank", requirement,
            rankRequirement ?? "",
            36, 0, 220, 32,
            23, Gray);

        rankLabel.alignment = TextAlignmentOptions.MidlineRight;

        requirement.gameObject.SetActive(
            !string.IsNullOrEmpty(rankRequirement));

        Box(
            "Divider", rect,
            24, 284, width - 48, 1.5f, Line);

        Icon(
            "CoinIcon", rect,
            24, 302, 44, 36);

        Label(
            "PriceText", rect,
            FormatPrice(price),
            80, 296, 185, 48,
            36, Color.black);

        Image details = Panel(
            "DetailsButton", rect,
            width - 252, 300, 228, 44,
            blackSprite);

        AddButton(details);

        TMP_Text detailsLabel = Label(
            "Label", details.transform,
            "ПОДРОБНЕЕ",
            0, 0, 228, 44,
            26, Color.white);

        detailsLabel.alignment = TextAlignmentOptions.Center;
    }

    // ---------- UI helpers ----------

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        return go.GetComponent<RectTransform>();
    }

    private static RectTransform Rect(
        string name, Transform parent,
        float x, float y, float width, float height)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);

        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Image Box(
        string name, Transform parent,
        float x, float y, float width, float height,
        Color color)
    {
        RectTransform rect = Rect(name, parent, x, y, width, height);

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    private static Image Panel(
        string name, Transform parent,
        float x, float y, float width, float height,
        Sprite sprite)
    {
        Image image = Box(
            name, parent, x, y, width, height, Color.white);

        image.sprite = sprite;
        image.type = Image.Type.Sliced;

        return image;
    }

    private static Image Icon(
        string name, Transform parent,
        float x, float y, float width, float height)
    {
        Image image = Box(
            name, parent, x, y, width, height, Color.black);

        image.sprite = emptySprite;
        image.preserveAspect = true;

        return image;
    }

    private static TMP_Text Label(
        string name, Transform parent,
        string value,
        float x, float y, float width, float height,
        float size, Color color)
    {
        RectTransform rect = Rect(name, parent, x, y, width, height);

        TextMeshProUGUI text =
            rect.gameObject.AddComponent<TextMeshProUGUI>();

        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * 0.7f;
        text.fontSizeMax = size;
        text.raycastTarget = false;

        return text;
    }

    private static void AddButton(Image image)
    {
        image.raycastTarget = true;

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        button.navigation = new Navigation
        {
            mode = Navigation.Mode.None
        };

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.93f, 0.93f, 0.93f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
    }

    private static string FormatPrice(int price)
    {
        return price.ToString(
            "N0",
            System.Globalization.CultureInfo.InvariantCulture
        ).Replace(",", " ");
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
                    "Canvas", "Найдено несколько MainCanvas.", "OK");

                return null;
            }

            result = canvas;
        }

        if (result == null)
        {
            EditorUtility.DisplayDialog(
                "Canvas", "Не найден Canvas MainCanvas.", "OK");
        }

        return result;
    }

    // ---------- Генерация спрайтов ----------

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
        Texture2D texture,
        string path,
        Vector4 border)
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
        importer.textureCompression =
            TextureImporterCompression.Uncompressed;

        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}