using Lean.Gui;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MatchLobbyUIGenerator
{
    private const string CanvasName = "MainCanvas";
    private const string RootName = "MatchLobbyUI";

    private static TMP_FontAsset font;
    private static Sprite rounded;

    private static readonly Color Ink = Color.black;
    private static readonly Color Muted =
        new Color32(100, 100, 100, 255);
    private static readonly Color Surface =
        new Color32(244, 244, 244, 255);
    private static readonly Color BadgeColor =
        new Color32(225, 225, 225, 255);
    private static readonly Color BorderColor =
        new Color32(200, 200, 200, 255);

    [MenuItem("Tools/Game UI/Create Match Lobby")]
    private static void Generate()
    {
        Canvas canvas = FindCanvas();

        if (canvas == null)
            return;

        if (canvas.transform.Find(RootName) != null)
        {
            EditorUtility.DisplayDialog(
                "UI already exists",
                $"{RootName} уже существует внутри {CanvasName}.",
                "OK");
            return;
        }

        font = TMP_Settings.defaultFontAsset;

        if (font == null)
        {
            EditorUtility.DisplayDialog(
                "TextMeshPro",
                "Назначь Default Font Asset в настройках TMP.",
                "OK");
            return;
        }

        rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>(
            "UI/Skin/UISprite.psd");

        RectTransform root = CreateRect(RootName, canvas.transform);
        Stretch(root);

        Undo.RegisterCreatedObjectUndo(
            root.gameObject,
            "Create Match Lobby UI");

        root.gameObject.AddComponent<CanvasGroup>();

        // Same dimensions and placement as DeviceSetupUI.
        RectTransform panel = CreateRect("Panel", root);
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 0.5f);
        panel.pivot = new Vector2(1f, 0.5f);
        panel.anchoredPosition = new Vector2(-80, 0);
        panel.sizeDelta = new Vector2(880, 820);
        panel.localScale = Vector3.one;

        // Border and backing.
        Image border = CreateImage(
            "Border", panel,
            0, 0, 880, 820,
            BorderColor, true);

        border.raycastTarget = false;

        Image backing = CreateImage(
            "Backing", panel,
            2, 2, 876, 816,
            Color.white, true);

        backing.raycastTarget = true;

        // Header.
        CreateText(
            "Title", panel,
            "Пригласи друзей",
            48, 40, 784, 72,
            50, Ink,
            bold: true);

        CreateText(
            "MatchDescription", panel,
            "Каждый сам за себя · До 4 игроков",
            48, 118, 784, 42,
            27, Muted);

        // Invitation link. No section heading.
        RectTransform invitation = CreateRect(
            "InvitationSection", panel);

        Place(invitation, 44, 202, 792, 110);

        Image linkField = CreateImage(
            "LinkField", invitation,
            0, 0, 570, 62,
            Surface, true);

        linkField.raycastTarget = false;

        TMP_Text linkText = CreateText(
            "LinkText", linkField.transform,
            "kodimodi.com/b/kdj4",
            18, 0, 534, 62,
            27, Ink);

        linkText.overflowMode = TextOverflowModes.Ellipsis;

        CreateButton(
            "CopyLinkButton", invitation,
            "Копировать",
            586, 0, 206, 62,
            Ink, Color.white,
            25);

        CreateText(
            "Hint", invitation,
            "Отправь ссылку друзьям, чтобы они присоединились.",
            2, 72, 788, 34,
            22, Muted);

        Image divider = CreateImage(
            "Divider", panel,
            44, 326, 792, 1,
            BorderColor);

        divider.raycastTarget = false;

        // Players.
        RectTransform players = CreateRect(
            "PlayersSection", panel);

        Place(players, 44, 344, 792, 300);

        CreateText(
            "Title", players,
            "Игроки",
            2, 0, 590, 44,
            32, Ink,
            bold: true);

        CreateText(
            "PlayerCount", players,
            "2 / 4",
            650, 0, 140, 44,
            28, Muted,
            alignment: TextAlignmentOptions.MidlineRight);

        // Scrollable list, ready for more player rows.
        RectTransform scrollRect = CreateRect("PlayersScroll", players);
        Place(scrollRect, 0, 60, 792, 236);

        Image scrollBackground =
            scrollRect.gameObject.AddComponent<Image>();

        scrollBackground.color = Color.clear;
        scrollBackground.raycastTarget = true;

        ScrollRect scroll =
            scrollRect.gameObject.AddComponent<ScrollRect>();

        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        RectTransform viewport = CreateRect("Viewport", scrollRect);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0, 236);

        VerticalLayoutGroup layout =
            content.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.spacing = 8;
        layout.padding = new RectOffset();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter =
            content.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;

        CreatePlayerRow(
            content, "Player_01",
            "Игрок 1", true, true);

        CreatePlayerRow(
            content, "Player_02",
            "Игрок 2", true, false);

        CreatePlayerRow(
            content, "EmptySlot_01",
            "Ожидание игрока…", false, false);

        CreatePlayerRow(
            content, "EmptySlot_02",
            "Ожидание игрока…", false, false);

        // Footer.
        RectTransform footer = CreateRect("Footer", panel);
        Place(footer, 44, 664, 792, 132);

        CreateButton(
            "StartMatchButton", footer,
            "Начать матч",
            0, 0, 792, 72,
            Ink, Color.white,
            32);

        CreateText(
            "StartHint", footer,
            "Можно начать, не дожидаясь заполнения комнаты.",
            0, 80, 792, 28,
            21, Muted,
            alignment: TextAlignmentOptions.Center);

        // Leave link stays inside the panel.
        LeanButton leave = CreateButton(
            "LeaveLobbyButton", panel,
            "Покинуть лобби",
            240, 754, 400, 48,
            Color.clear, Ink,
            25, false);

        leave.GetComponentInChildren<TMP_Text>().fontStyle =
            FontStyles.Underline;

        // Move hint up slightly to leave room for the link.
        RectTransform hintRect = footer.Find("StartHint")
            .GetComponent<RectTransform>();

        Place(hintRect, 0, 76, 792, 26);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        Selection.activeGameObject = root.gameObject;
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        Debug.Log(
            $"Создан {CanvasName}/{RootName}. " +
            "Подключи события LeanButton и замени демонстрационные данные.",
            root);
    }

    private static void CreatePlayerRow(
        Transform parent,
        string name,
        string playerName,
        bool occupied,
        bool host)
    {
        Image row = CreateImage(
            name, parent,
            0, 0, 792, 50,
            occupied
                ? Surface
                : new Color32(249, 249, 249, 255),
            true);

        row.raycastTarget = false;

        LayoutElement element =
            row.gameObject.AddComponent<LayoutElement>();

        element.minHeight = 50;
        element.preferredHeight = 50;
        element.flexibleHeight = 0;

        // Icon placeholder.
        RectTransform iconRect = CreateRect("Icon", row.transform);
        Place(iconRect, 16, 7, 36, 36);

        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.color = occupied ? Ink : Muted;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = false;

        // Name stretches with the row.
        TMP_Text nameText = CreateText(
            "Name", row.transform,
            playerName,
            72, 0, host ? 450 : 700, 50,
            25, occupied ? Ink : Muted,
            bold: occupied);

        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = Vector2.zero;
        nameRect.anchorMax = Vector2.one;
        nameRect.offsetMin = new Vector2(72, 0);
        nameRect.offsetMax = new Vector2(host ? -244 : -16, 0);

        nameText.overflowMode = TextOverflowModes.Ellipsis;

        if (!host)
            return;

        Image badge = CreateImage(
            "HostBadge", row.transform,
            0, 0, 220, 32,
            BadgeColor, true);

        badge.raycastTarget = false;

        RectTransform badgeRect = badge.rectTransform;
        badgeRect.anchorMin = badgeRect.anchorMax =
            new Vector2(1, 0.5f);
        badgeRect.pivot = new Vector2(1, 0.5f);
        badgeRect.anchoredPosition = new Vector2(-16, 0);

        CreateText(
            "Text", badge.transform,
            "ТЫ · СОЗДАТЕЛЬ",
            8, 0, 204, 32,
            17, Ink,
            alignment: TextAlignmentOptions.Center);
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
                candidate.gameObject.name != CanvasName)
            {
                continue;
            }

            if (result != null)
            {
                EditorUtility.DisplayDialog(
                    "Canvas",
                    $"Найдено несколько Canvas с именем {CanvasName}.",
                    "OK");
                return null;
            }

            result = candidate;
        }

        if (result == null)
        {
            EditorUtility.DisplayDialog(
                "Canvas",
                $"Не найден Canvas с именем {CanvasName}.",
                "OK");
        }

        return result;
    }

    private static RectTransform CreateRect(
        string name,
        Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        return go.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Place(
        RectTransform rect,
        float x, float y,
        float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        float x, float y,
        float width, float height,
        Color color,
        bool useRounded = false)
    {
        RectTransform rect = CreateRect(name, parent);
        Place(rect, x, y, width, height);

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;

        if (useRounded && rounded != null)
        {
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
        }

        return image;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string value,
        float x, float y,
        float width, float height,
        float size,
        Color color,
        bool bold = false,
        TextAlignmentOptions alignment =
            TextAlignmentOptions.MidlineLeft)
    {
        RectTransform rect = CreateRect(name, parent);
        Place(rect, x, y, width, height);

        TextMeshProUGUI text =
            rect.gameObject.AddComponent<TextMeshProUGUI>();

        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = bold
            ? FontStyles.Bold
            : FontStyles.Normal;
        text.alignment = alignment;
        text.raycastTarget = false;

        return text;
    }

    private static LeanButton CreateButton(
        string name,
        Transform parent,
        string label,
        float x, float y,
        float width, float height,
        Color background,
        Color foreground,
        float size,
        bool bold = true)
    {
        Image image = CreateImage(
            name, parent,
            x, y, width, height,
            background,
            background.a > 0);

        image.raycastTarget = true;

        LeanButton button =
            image.gameObject.AddComponent<LeanButton>();

        button.targetGraphic = image;
        button.interactable = true;
        button.transition = Selectable.Transition.None;

        CreateText(
            "Text", image.transform,
            label,
            10, 0, width - 20, height,
            size, foreground,
            bold,
            TextAlignmentOptions.Center);

        return button;
    }
}