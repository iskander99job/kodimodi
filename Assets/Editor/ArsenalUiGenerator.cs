using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ArsenalUiGenerator
{
    private const string SkinFolder = "Assets/WeaponUiGenerated";
    private const float DesignWidth = 1672f;
    private const float DesignHeight = 941f;

    private static readonly string[] Categories =
    {
        "PISTOL", "SMG", "SHOTGUN", "AR", "MG", "SR"
    };

    private static readonly Color Ink = new Color32(12, 12, 12, 255);
    private static readonly Color Muted = new Color32(105, 105, 105, 255);
    private static readonly Color Line = new Color32(199, 199, 199, 255);

    [MenuItem("Tools/Weapon UI/Generate Arsenal")]
    private static void Generate()
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
                    Debug.LogError("В активной сцене несколько Canvas с именем MainCanvas.");
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

        if (canvas.transform.Find("Arsenal UI") != null)
        {
            Debug.LogWarning("В MainCanvas уже есть Arsenal UI.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Generate Arsenal UI");

        try
        {
            RectTransform arsenal = new Builder().Build(canvas.transform);

            if (canvas.GetComponent<GraphicRaycaster>() == null)
                Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = arsenal.gameObject;
            Undo.CollapseUndoOperations(group);
        }
        catch (Exception error)
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
            panel = LoadSprite("Panel");
            card = LoadSprite("Card");
            selectedCard = LoadSprite("SelectedCard");
            selectedTab = LoadSprite("ActiveTab");
            badge = LoadSprite("Badge");
            primary = LoadSprite("Action");
            blocked = LoadSprite("BlockedAction");
        }

        public RectTransform Build(Transform canvas)
        {
            RectTransform arsenal = Box(canvas, "Arsenal UI", 0, 0, 1, 1);
            arsenal.SetAsLastSibling();

            ImageOf(Box(arsenal, "Background", 0, 0, 1, 1), null, Color.white);
            Art(Page(arsenal, "Floor Grid", 0, 215, 1672, 726), false);

            RectTransform back = Page(arsenal, "Back Button", 28, 41, 56, 56);
            ButtonOf(back, panel);
            Label(Box(back, "Arrow", 0, 0, 1, 1), "←", 32);

            ImageOf(Page(arsenal, "Header Divider", 108, 41, 1, 56), null, Line);
            Label(Page(arsenal, "Title", 135, 41, 500, 56),
                "ARSENAL", 38, TextAnchor.MiddleLeft, true);

            RectTransform balance = Page(arsenal, "Balance", 1412, 41, 232, 56);
            ImageOf(balance, panel, Color.white);
            Art(Box(balance, "Coin Icon", .07f, .21f, .14f, .57f));
            Rule(balance, "Divider", .31f, .20f, .80f);
            Label(Box(balance, "Amount", .38f, 0, .55f, 1),
                "650", 29, TextAnchor.MiddleLeft, true);

            BuildTabs(arsenal);

            RectTransform pages = Box(arsenal, "Category Pages", 0, 0, 1, 1);

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
                        "No weapons owned", 24, color: Muted);
                    page.gameObject.SetActive(false);
                }
            }

            Canvas.ForceUpdateCanvases();
            return arsenal;
        }

        private void BuildTabs(RectTransform screen)
        {
            RectTransform variants = Box(screen, "Categories", 0, 0, 1, 1);

            RectTransform full = Page(variants, "Full Width", 28, 130, 1616, 75);
            BuildTabStrip(full, compact: false);

            RectTransform compact = Page(variants, "Compact", 28, 215, 656, 64);
            BuildTabStrip(compact, compact: true);
            compact.gameObject.SetActive(false);
        }

        private void BuildTabStrip(RectTransform strip, bool compact)
        {
            ImageOf(strip, panel, Color.white);

            string[] categories = compact
                ? new[] { "PISTOL", "AR", "SHOTGUN", "SMG" }
                : Categories;

            // Ширины четырёх вкладок в компактной полосе на референсе.
            float[] compactWidths = { 173f, 128f, 204f, 151f };
            float left = 0f;

            for (int i = 0; i < categories.Length; i++)
            {
                string category = categories[i];
                float width = compact
                    ? compactWidths[i] / 656f
                    : 1f / categories.Length;

                RectTransform tab = Box(strip, category + " Tab", left, 0, width, 1);
                left += width;

                Button button = ButtonOf(tab, null);
                button.transition = Selectable.Transition.None;
                button.targetGraphic.color = new Color(1f, 1f, 1f, .005f);

                RectTransform highlight = Box(tab, "Selected", 0, 0, 1, 1);
                ImageOf(highlight, selectedTab, Color.white);
                highlight.gameObject.SetActive(i == 0);

                if (i > 0)
                    Rule(tab, "Divider", 0, .22f, .78f);

                Art(Box(tab, "Category Icon", .14f, .23f, .23f, .54f));
                Label(Box(tab, "Category Label", .41f, 0, .55f, 1),
                    category, compact ? 21 : 23, bold: true);
            }
        }

        private void BuildPistolPage(RectTransform page, RectTransform content)
        {
            WeaponRow(content, "P-12", false, true);
            WeaponRow(content, "P-07", true, false);

            Art(Page(page, "Weapon Preview", 898, 283, 475, 292));
            Label(Page(page, "Weapon Name", 769, 576, 750, 61),
                "P-07", 54, bold: true);

            Stat(page, 769, 645, "DAMAGE", "26");
            Stat(page, 1178, 645, "MAGAZINE", "12");
            Stat(page, 769, 720, "FIRE RATE", "360 RPM");
            Stat(page, 1178, 720, "RELOAD", "1.4 S");

            RectTransform states = Page(page, "Equip States", 769, 812, 803, 117);

            RectTransform canEquip = Box(states, "Can Equip", 0, 0, 1, 1);
            RectTransform equipButton = Box(canEquip, "Equip Button", 0, 0, 1, 80f / 117f);
            ButtonOf(equipButton, primary);
            Label(Box(equipButton, "Label", 0, 0, 1, 1),
                "EQUIP", 34, bold: true, color: Color.white);

            Label(Box(canEquip, "Replacement Hint", 0, 83f / 117f, 1, 34f / 117f),
                "Replaces P-12 in the PISTOL slot", 21, color: Muted);

            RectTransform equipped = Box(states, "Equipped", 0, 0, 1, 1);
            RectTransform equippedPanel = Box(equipped, "Equipped Panel", 0, 0, 1, 80f / 117f);
            ImageOf(equippedPanel, blocked, Color.white);
            Label(Box(equippedPanel, "Label", 0, 0, 1, 1),
                "✓  EQUIPPED", 34, bold: true);
            equipped.gameObject.SetActive(false);
        }

        private void WeaponRow(RectTransform content, string name,
            bool selected, bool equipped)
        {
            RectTransform row = Box(content, name + " Card", 0, 0, 1, 0);
            LayoutElement size = Undo.AddComponent<LayoutElement>(row.gameObject);
            size.minHeight = 146;
            size.preferredHeight = 146;
            ButtonOf(row, card);

            // Отдельные объекты для выбора карточки и статуса экипировки.
            RectTransform highlight = Box(row, "Selected", 0, 0, 1, 1);
            ImageOf(highlight, selectedCard, Color.white);
            highlight.gameObject.SetActive(selected);

            Art(Box(row, "Thumbnail", .055f, .20f, .20f, .60f));
            Rule(row, "Thumbnail Divider", .296f, .27f, .73f);
            Label(Box(row, "Weapon Label", .338f, .31f, .27f, .35f),
                name, 35, TextAnchor.MiddleLeft, true);

            RectTransform status = Box(row, "Equipped Badge", .655f, .31f, .30f, .37f);
            ImageOf(status, badge, Color.white);
            Label(Box(status, "Label", 0, 0, 1, 1), "✓  EQUIPPED", 21, bold: true);
            status.gameObject.SetActive(equipped);
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

    private static RectTransform BuildScrollView(RectTransform page)
    {
        RectTransform list = Page(page, "Weapon Scroll View", 26, 308, 681, 496);
        ScrollRect scroll = Undo.AddComponent<ScrollRect>(list.gameObject);
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 36;

        RectTransform viewport = Box(list, "Viewport", 0, 0, .975f, 1);
        ImageOf(viewport, null, new Color(1, 1, 1, .005f), true);
        Undo.AddComponent<RectMask2D>(viewport.gameObject);

        RectTransform content = Box(viewport, "Content", 0, 0, 1, 0);
        VerticalLayoutGroup layout = Undo.AddComponent<VerticalLayoutGroup>(content.gameObject);
        layout.padding = new RectOffset(2, 2, 2, 2);
        layout.spacing = 26;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = Undo.AddComponent<ContentSizeFitter>(content.gameObject);
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform track = Box(list, "Scrollbar", .989f, 0, .007f, 1);
        ImageOf(track, null, new Color(0, 0, 0, .035f), true);
        RectTransform area = Box(track, "Sliding Area", 0, 0, 1, 1);
        RectTransform handle = Box(area, "Handle", 0, 0, 1, 1);
        Image handleImage = ImageOf(handle, null, new Color(0, 0, 0, .22f), true);

        Scrollbar scrollbar = Undo.AddComponent<Scrollbar>(track.gameObject);
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.value = 1f;

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return content;
    }

    private static Button ButtonOf(RectTransform rect, Sprite sprite)
    {
        Image image = ImageOf(rect, sprite, Color.white, true);
        Button button = Undo.AddComponent<Button>(rect.gameObject);
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color32(245, 245, 245, 255);
        colors.pressedColor = new Color32(227, 227, 227, 255);
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

    private static Sprite LoadSprite(string name)
    {
        string path = $"{SkinFolder}/{name}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (sprite == null)
        {
            throw new InvalidOperationException(
                $"Не найден спрайт {path}. Сначала запусти Generate Shop, чтобы создать оформление.");
        }

        return sprite;
    }
}