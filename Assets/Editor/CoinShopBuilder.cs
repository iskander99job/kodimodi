// Assets/Editor/CoinShopBuilder.cs
#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class CoinShopBuilder
{
    // Отступ под существующий header, в единицах Canvas.
    const float HeaderSpace = 120;

    static readonly Color Ink = new Color32(10, 10, 10, 255);
    static readonly Color Gray = new Color32(202, 205, 213, 255);
    static readonly Color Gold = new Color32(255, 192, 0, 255);

    static Font font;
    static Sprite rounded;

    [MenuItem("Tools/Coin Shop/Create UI (no header)")]
    public static void Create()
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Coin Shop");

        font = Resources.GetBuiltinResource<Font>(
#if UNITY_2022_2_OR_NEWER
            "LegacyRuntime.ttf"
#else
            "Arial.ttf"
#endif
        );

        rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>(
            "UI/Skin/UISprite.psd"
        );

        Canvas canvas = Selection.activeGameObject
            ? Selection.activeGameObject.GetComponentInParent<Canvas>()
            : null;

        if (!canvas)
        {
            var go = new GameObject(
                "Coin Shop Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );

            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");

            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        CreateEventSystem();

        var root = Rect("CoinShopPage", canvas.transform);
        Stretch(root, 0, HeaderSpace, 0, 0);
        Fill(root, Color.white).raycastTarget = true;

        CreateHeading(root);

        var scrollArea = Rect("PacksScrollView", root);
        Stretch(scrollArea, 28, 100, 28, 64);

        var scroll = scrollArea.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45;

        var viewport = Rect("Viewport", scrollArea);
        Stretch(viewport);
        Fill(viewport, Color.white).raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll.viewport = viewport;

        var content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = Vector2.zero;
        scroll.content = content;

        var layout =
            content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18;
        layout.padding = new RectOffset(2, 2, 2, 2);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter =
            content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        string[] amounts =
        {
            "500", "1 200", "2 500",
            "6 500", "14 000", "37 500"
        };

        string[] bonuses =
        {
            "", "+200 BONUS", "+500 BONUS",
            "+1 500 BONUS", "+4 000 BONUS", "+12 500 BONUS"
        };

        string[] prices =
        {
            "$0.99", "$1.99", "$3.99",
            "$9.99", "$19.99", "$49.99"
        };

        for (int row = 0; row < 2; row++)
        {
            var rowRect = Rect("Row_" + (row + 1), content);
            rowRect.gameObject
                .AddComponent<LayoutElement>().preferredHeight = 330;

            var rowLayout =
                rowRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 18;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = true;

            for (int col = 0; col < 3; col++)
            {
                int i = row * 3 + col;

                CreateCard(
                    rowRect, i + 1,
                    amounts[i], bonuses[i], prices[i],
                    i == 2
                );
            }
        }

        Label(
            "Footer", root,
            "Coins are added to your balance after purchase.",
            20, FontStyle.Normal, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(1, 0),
            new Vector2(28, 8), new Vector2(-28, 52)
        );

        Selection.activeGameObject = root.gameObject;
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
    }

    static void CreateEventSystem()
    {
        if (UnityEngine.Object.FindObjectOfType<EventSystem>())
            return;

        var go = new GameObject("EventSystem", typeof(EventSystem));
        Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");

#if ENABLE_INPUT_SYSTEM
        var moduleType = Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, " +
            "Unity.InputSystem"
        );

        if (moduleType != null)
            go.AddComponent(moduleType);
        else
            Debug.LogWarning(
                "Add InputSystemUIInputModule to EventSystem."
            );
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    static void CreateHeading(RectTransform root)
    {
        var heading = Rect("SectionHeading", root);

        Box(
            heading,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(28, -72), new Vector2(-28, -8)
        );

        Label(
            "Title", heading, "COIN PACKS",
            32, FontStyle.Bold, TextAnchor.MiddleLeft,
            Vector2.zero, new Vector2(0, 1),
            Vector2.zero, new Vector2(232, 0)
        );

        var separator = Rect("TitleSeparator", heading);

        Box(
            separator,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(242, -20), new Vector2(244, 20)
        );
        Fill(separator, Ink);

        Label(
            "Subtitle", heading, "Choose your amount",
            23, FontStyle.Normal, TextAnchor.MiddleLeft,
            Vector2.zero, Vector2.one,
            new Vector2(264, 0), Vector2.zero
        );

        var line = Rect("SectionLine", root);

        Box(
            line,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(28, -84), new Vector2(-28, -82)
        );
        Fill(line, Gray);
    }

    static void CreateCard(
        Transform parent, int index,
        string amount, string bonus, string price,
        bool popular)
    {
        var card = Rect("Pack_0" + index, parent);

        var sizing = card.gameObject.AddComponent<LayoutElement>();
        sizing.minWidth = 0;
        sizing.preferredWidth = 0;
        sizing.flexibleWidth = 1;

        Fill(card, popular ? Gold : Gray, true);

        var body = Rect("Body", card);
        Stretch(body, 1.5f, 1.5f, 1.5f, 1.5f);
        Fill(body, Color.white, true);

        // Сюда назначить изображение набора монет.
        var art = Rect("PackArtwork_ASSIGN_SPRITE", body);
        Stretch(art, 36, 24, 36, 130);
        Fill(art, Color.white).preserveAspect = true;

        // Сюда назначить маленькую иконку валюты.
        var icon = Rect("CurrencyIcon_ASSIGN_SPRITE", body);

        Box(
            icon, Vector2.zero, Vector2.zero,
            new Vector2(22, 96), new Vector2(56, 130)
        );
        Fill(icon, Color.white).preserveAspect = true;

        Label(
            "Amount", body, amount,
            34, FontStyle.Bold, TextAnchor.MiddleLeft,
            Vector2.zero, new Vector2(0.5f, 0),
            new Vector2(66, 90), new Vector2(12, 138)
        );

        Label(
            "Bonus", body, bonus,
            18, FontStyle.Normal, TextAnchor.MiddleRight,
            new Vector2(0.5f, 0), new Vector2(1, 0),
            new Vector2(12, 90), new Vector2(-22, 138)
        );

        var divider = Rect("Divider", body);

        Box(
            divider, Vector2.zero, new Vector2(1, 0),
            new Vector2(22, 80), new Vector2(-22, 81)
        );
        Fill(divider, Gray);

        Label(
            "Price", body, price,
            30, FontStyle.Normal, TextAnchor.MiddleLeft,
            Vector2.zero, new Vector2(0.5f, 0),
            new Vector2(22, 19), new Vector2(0, 67)
        );

        var buy = Rect("BuyButton", body);

        Box(
            buy, new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-166, 20), new Vector2(-22, 66)
        );

        var background = Fill(buy, popular ? Gold : Ink, true);
        background.raycastTarget = true;

        var button = buy.gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        var caption = Label(
            "Label", buy, "BUY",
            25, FontStyle.Normal, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero
        );
        caption.color = popular ? Ink : Color.white;

        if (popular)
        {
            var badge = Rect("PopularBadge", body);

            Box(
                badge, Vector2.one, Vector2.one,
                new Vector2(-152, -47), new Vector2(-16, -14)
            );
            Fill(badge, Gold, true);

            Label(
                "Label", badge, "POPULAR",
                20, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero
            );
        }
    }

    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);

        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;

        return (RectTransform)go.transform;
    }

    static void Stretch(
        RectTransform rect,
        float left = 0, float top = 0,
        float right = 0, float bottom = 0)
    {
        Box(
            rect, Vector2.zero, Vector2.one,
            new Vector2(left, bottom),
            new Vector2(-right, -top)
        );
    }

    static void Box(
        RectTransform rect,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    static Image Fill(
        RectTransform rect, Color color, bool round = false)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        if (round && rounded)
        {
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
        }

        return image;
    }

    static Text Label(
        string name, Transform parent, string value,
        int size, FontStyle style, TextAnchor alignment,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var rect = Rect(name, parent);
        Box(rect, anchorMin, anchorMax, offsetMin, offsetMax);

        var text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Ink;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        return text;
    }
}
#endif