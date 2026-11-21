using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class WeaponUiGenerator
{
    private const string SpriteFolder = "Assets/WeaponUiGenerated";
    private const float DesignWidth = 1672f;
    private const float DesignHeight = 941f;

    private static readonly string[] Categories =
    {
        "PISTOL", "SMG", "SHOTGUN", "AR", "MG", "SR"
    };

    private static readonly Color Ink = new Color32(12, 12, 12, 255);
    private static readonly Color Muted = new Color32(105, 105, 105, 255);
    private static readonly Color Line = new Color32(199, 199, 199, 255);
    private static readonly Color Gold = new Color32(245, 169, 12, 255);

    [MenuItem("Tools/Weapon UI/Generate Shop")]
    private static void GenerateShop()
    {
        Scene scene = SceneManager.GetActiveScene();
        Canvas canvas = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Canvas candidate in root.GetComponentsInChildren<Canvas>(true))
            {
                if (candidate.gameObject.name != "MainCanvas")
                    continue;

                if (canvas != null)
                {
                    Debug.LogError("В активной сцене найдено несколько MainCanvas.");
                    return;
                }

                canvas = candidate;
            }
        }

        if (canvas == null)
        {
            Debug.LogError("В активной сцене нужен MainCanvas с компонентом Canvas.");
            return;
        }

        if (canvas.transform.Find("Shop UI") != null)
        {
            Debug.LogWarning("Shop UI уже существует. Переименуй его перед генерацией нового.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Generate Shop UI");

        try
        {
            RectTransform shop = new Builder().Build(canvas.transform);

            if (canvas.GetComponent<GraphicRaycaster>() == null)
                Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = shop.gameObject;
            Undo.CollapseUndoOperations(group);
        }
        catch (System.Exception error)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogException(error);
        }
    }

    private sealed class Builder
    {
        private readonly Font font;
        private readonly Sprite panel;
        private readonly Sprite card;
        private readonly Sprite selectedCard;
        private readonly Sprite selectedTab;
        private readonly Sprite badge;
        private readonly Sprite primary;
        private readonly Sprite blocked;

        public Builder()
        {
#if UNITY_2022_2_OR_NEWER
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif

            if (!AssetDatabase.IsValidFolder(SpriteFolder))
                AssetDatabase.CreateFolder("Assets", "WeaponUiGenerated");

            Color warmWhite = new Color32(255, 253, 249, 255);
            Color warmGray = new Color32(250, 246, 236, 255);

            panel = MakeSprite("Panel", 12, 1.2f, Line, Color.white, Tone(245));
            card = MakeSprite("Card", 18, 1.4f, Line, Color.white, Tone(245));
            selectedCard = MakeSprite("SelectedCard", 18, 1.8f, Gold, warmWhite, warmGray);
            selectedTab = MakeSprite("ActiveTab", 10, 1.5f, Gold, warmWhite, warmGray);
            badge = MakeSprite("Badge", 10, 1f, Line, Tone(250), Tone(241));
            primary = MakeSprite("Action", 14, 0f, Ink, Ink, Ink);
            blocked = MakeSprite("BlockedAction", 14, 1.2f, Line, Tone(238), Tone(221));
        }

        public RectTransform Build(Transform canvas)
        {
            RectTransform shop = Box(canvas, "Shop UI", 0, 0, 1, 1);
            shop.SetAsLastSibling();

            ImageOf(Box(shop, "Background", 0, 0, 1, 1), null, Color.white);
            Art(Page(shop, "Floor Grid", 0, 215, 1672, 726), false);

            RectTransform back = Page(shop, "Back Button", 28, 41, 56, 56);
            ButtonOf(back, panel);
            Label(Box(back, "Arrow", 0, 0, 1, 1), "←", 32);

            ImageOf(Page(shop, "Header Divider", 108, 41, 1, 56), null, Line);
            Label(Page(shop, "Title", 135, 41, 500, 56),
                "SHOP", 38, TextAnchor.MiddleLeft, true);

            RectTransform balance = Page(shop, "Balance", 1412, 41, 232, 56);
            ImageOf(balance, panel, Color.white);
            Art(Box(balance, "Coin Icon", .07f, .21f, .14f, .57f));
            Rule(balance, "Divider", .31f, .20f, .80f);
            Label(Box(balance, "Amount", .38f, 0, .55f, 1),
                "1 250", 29, TextAnchor.MiddleLeft, true);

            BuildTabs(shop);

            RectTransform pages = Box(shop, "Category Pages", 0, 0, 1, 1);

            for (int i = 0; i < Categories.Length; i++)
            {
                RectTransform page = Box(pages, Categories[i] + " Page", 0, 0, 1, 1);
                RectTransform content = BuildScrollView(page);

                if (i == 0)
                {
                    BuildPistolPage(page, content);
                }
                else
                {
                    Label(Page(page, "Empty Message", 28, 309, 660, 70),
                        "No weapons available", 24, color: Muted);
                    page.gameObject.SetActive(false);
                }
            }

            Canvas.ForceUpdateCanvases();
            return shop;
        }

        private void BuildTabs(RectTransform shop)
        {
            RectTransform strip = Page(shop, "Categories", 28, 130, 1616, 75);
            ImageOf(strip, panel, Color.white);

            float width = 1f / Categories.Length;

            for (int i = 0; i < Categories.Length; i++)
            {
                RectTransform tab = Box(strip, Categories[i] + " Tab", i * width, 0, width, 1);
                Button button = ButtonOf(tab, null);
                button.transition = Selectable.Transition.None;
                button.targetGraphic.color = new Color(1, 1, 1, .005f);

                RectTransform highlight = Box(tab, "Selected", 0, 0, 1, 1);
                ImageOf(highlight, selectedTab, Color.white);
                highlight.gameObject.SetActive(i == 0);

                if (i > 0)
                    Rule(tab, "Divider", 0, .18f, .82f);

                Art(Box(tab, "Category Icon", .17f, .23f, .22f, .54f));
                Label(Box(tab, "Category Label", .40f, 0, .55f, 1),
                    Categories[i], 23, bold: true);
            }
        }

        private void BuildPistolPage(RectTransform page, RectTransform content)
        {
            Row(content, "P-07", "600", "", true);
            Row(content, "P-12", "OWNED", "", false);
            Row(content, "P-19", "950", "RANK 3", false);
            Row(content, "P-22", "1 100", "RANK 4", false);
            Row(content, "P-28", "1 400", "RANK 5", false);

            Art(Page(page, "Weapon Preview", 898, 283, 475, 292));
            Label(Page(page, "Weapon Name", 769, 576, 750, 61),
                "P-07", 54, bold: true);

            Stat(page, 769, 645, "DAMAGE", "26");
            Stat(page, 1178, 645, "MAGAZINE", "12");
            Stat(page, 769, 720, "FIRE RATE", "360 RPM");
            Stat(page, 1178, 720, "RELOAD", "1.4 S");

            BuildPurchaseStates(page);
        }

        private void BuildPurchaseStates(RectTransform page)
        {
            RectTransform states = Page(page, "Purchase States", 769, 812, 803, 80);

            RectTransform available = Box(states, "Available", 0, 0, 1, 1);
            ButtonOf(available, primary);
            Art(Box(available, "Coin Icon", .325f, .25f, .055f, .50f));
            Label(Box(available, "Label", .39f, 0, .55f, 1),
                "BUY · 600", 34, TextAnchor.MiddleLeft, true, Color.white);

            RectTransform rankLocked = Box(states, "Rank Locked", 0, 0, 1, 1);
            ButtonOf(rankLocked, blocked).interactable = false;
            Art(Box(rankLocked, "Rank Icon", .24f, .15f, .07f, .70f));
            Label(Box(rankLocked, "Label", .34f, 0, .62f, 1),
                "REQUIRES RANK 3", 34, TextAnchor.MiddleLeft, true);
            rankLocked.gameObject.SetActive(false);

            RectTransform owned = Box(states, "Owned", 0, 0, 1, 1);
            ButtonOf(owned, primary);
            Label(Box(owned, "Label", 0, 0, 1, 1),
                "VIEW IN ARSENAL  →", 34, bold: true, color: Color.white);
            owned.gameObject.SetActive(false);
        }

        private static RectTransform BuildScrollView(RectTransform page)
        {
            RectTransform list = Page(page, "Weapon Scroll View", 26, 308, 681, 496);
            ScrollRect scroll = Undo.AddComponent<ScrollRect>(list.gameObject);
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;

            RectTransform viewport = Box(list, "Viewport", 0, 0, .975f, 1);
            ImageOf(viewport, null, new Color(1, 1, 1, .005f), true);
            Undo.AddComponent<RectMask2D>(viewport.gameObject);

            RectTransform content = Box(viewport, "Content", 0, 0, 1, 0);
            VerticalLayoutGroup rows = Undo.AddComponent<VerticalLayoutGroup>(content.gameObject);
            rows.padding = new RectOffset(2, 2, 2, 2);
            rows.spacing = 26;
            rows.childControlWidth = true;
            rows.childControlHeight = true;
            rows.childForceExpandWidth = true;
            rows.childForceExpandHeight = false;

            ContentSizeFitter fitter = Undo.AddComponent<ContentSizeFitter>(content.gameObject);
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform track = Box(list, "Scrollbar", .989f, 0, .007f, 1);
            ImageOf(track, null, new Color(0, 0, 0, .035f), true);

            RectTransform area = Box(track, "Sliding Area", 0, 0, 1, 1);
            RectTransform handle = Box(area, "Handle", 0, 0, 1, 1);
            Image handleGraphic = ImageOf(handle, null, new Color(0, 0, 0, .22f), true);

            Scrollbar scrollbar = Undo.AddComponent<Scrollbar>(track.gameObject);
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleGraphic;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.value = 1f;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        private void Row(RectTransform content, string name, string status,
            string rank, bool selected)
        {
            RectTransform row = Box(content, name + " Card", 0, 0, 1, 0);
            LayoutElement size = Undo.AddComponent<LayoutElement>(row.gameObject);
            size.minHeight = 146;
            size.preferredHeight = 146;
            ButtonOf(row, selected ? selectedCard : card);

            Art(Box(row, "Thumbnail", .055f, .20f, .20f, .60f));
            Rule(row, "Thumbnail Divider", .296f, .27f, .73f);

            bool hasRank = rank.Length > 0;
            Label(Box(row, "Weapon Label", .338f, hasRank ? .18f : .31f, .27f, .35f),
                name, 35, TextAnchor.MiddleLeft, true);

            if (hasRank)
            {
                Art(Box(row, "Lock Icon", .345f, .56f, .036f, .185f));
                Label(Box(row, "Required Rank", .406f, .53f, .22f, .27f),
                    rank, 23, TextAnchor.MiddleLeft, true, Muted);
            }

            bool price = char.IsDigit(status[0]);
            RectTransform pill = Box(row, "Status Badge", .735f, .31f, .22f, .37f);
            ImageOf(pill, badge, Color.white);

            if (price)
                Art(Box(pill, "Coin Icon", .08f, .20f, .22f, .60f));

            Label(Box(pill, "Status Label", price ? .32f : 0, 0, price ? .66f : 1, 1),
                status, price ? 27 : 21, bold: true);
        }

        private void Stat(RectTransform page, float x, float y, string name, string value)
        {
            RectTransform box = Page(page, name, x, y, 394, 60);
            ImageOf(box, panel, Color.white);
            Rule(box, "Divider", .513f, .20f, .80f);

            Label(Box(box, "Label", .074f, 0, .43f, 1),
                name, 22, TextAnchor.MiddleLeft, true);
            Label(Box(box, "Value", .571f, 0, .40f, 1),
                value, 26, TextAnchor.MiddleLeft, true);
        }

        private void Label(RectTransform rect, string value, int size,
            TextAnchor alignment = TextAnchor.MiddleCenter, bool bold = false,
            Color? color = null)
        {
            Text label = Undo.AddComponent<Text>(rect.gameObject);
            label.font = font;
            label.fontSize = size;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.alignment = alignment;
            label.color = color ?? Ink;
            label.text = value;
            label.raycastTarget = false;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(10, size * 2 / 3);
            label.resizeTextMaxSize = size;
        }
    }

    private static Button ButtonOf(RectTransform rect, Sprite sprite)
    {
        Image image = ImageOf(rect, sprite, Color.white, true);
        Button button = Undo.AddComponent<Button>(rect.gameObject);
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.highlightedColor = Tone(245);
        colors.pressedColor = Tone(227);
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        button.colors = colors;
        return button;
    }

    private static Image ImageOf(RectTransform rect, Sprite sprite, Color color,
        bool raycast = false)
    {
        Image image = Undo.AddComponent<Image>(rect.gameObject);
        image.sprite = sprite;
        image.type = sprite == null ? Image.Type.Simple : Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    private static void Art(RectTransform rect, bool preserveAspect = true)
    {
        Image image = ImageOf(rect, null, Color.white);
        image.preserveAspect = preserveAspect;
        image.enabled = false;
    }

    private static void Rule(Transform parent, string name,
        float x, float top, float bottom)
    {
        RectTransform line = Box(parent, name, x, top, 0, bottom - top);
        line.offsetMax = new Vector2(1, 0);
        ImageOf(line, null, Line);
    }

    private static RectTransform Page(Transform parent, string name,
        float x, float y, float width, float height)
    {
        return Box(parent, name, x / DesignWidth, y / DesignHeight,
            width / DesignWidth, height / DesignHeight);
    }

    private static RectTransform Box(Transform parent, string name,
        float x, float y, float width, float height)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        Undo.SetTransformParent(obj.transform, parent, "Parent " + name);
        obj.layer = parent.gameObject.layer;

        var rect = (RectTransform)obj.transform;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.pivot = new Vector2(0, 1);
        rect.anchorMin = new Vector2(x, 1 - y - height);
        rect.anchorMax = new Vector2(x + width, 1 - y);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition3D = Vector3.zero;
        return rect;
    }

    private static Color Tone(byte value) => new Color32(value, value, value, 255);

    private static Sprite MakeSprite(string name, int radius, float stroke,
        Color border, Color top, Color bottom)
    {
        string path = $"{SpriteFolder}/{name}.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
            return existing;

        const int size = 64;
        const float half = size / 2f;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

        try
        {
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                Color fill = Color.Lerp(bottom, top, y / (size - 1f));

                for (int x = 0; x < size; x++)
                {
                    var point = new Vector2(
                        Mathf.Abs(x + .5f - half),
                        Mathf.Abs(y + .5f - half));

                    Vector2 q = point - Vector2.one * (half - radius);
                    float distance = Vector2.Max(q, Vector2.zero).magnitude
                        + Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;

                    Color pixel = stroke > 0
                        ? Color.Lerp(fill, border, Mathf.Clamp01(distance + stroke + .5f))
                        : fill;

                    pixel.a = Mathf.Clamp01(.5f - distance);
                    pixels[y * size + x] = pixel;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.spriteBorder = Vector4.one * (radius + 2);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}