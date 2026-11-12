using Lean.Gui;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MatchResultsUIGenerator
{
    private const string CanvasName = "MainCanvas";
    private const string RootName = "MatchResultsUI";

    private const float TableWidth = 1080;
    private const float RowHeight = 64;

    private static TMP_FontAsset font;

    private static readonly Color White = Color.white;

    private static readonly Color Secondary =
        new Color32(185, 190, 185, 255);

    private static readonly Color LineColor =
        new Color(1f, 1f, 1f, 0.45f);

    [MenuItem("Tools/Game UI/Create Match Results")]
    private static void Generate()
    {
        Canvas canvas = FindCanvas();

        if (canvas == null)
            return;

        if (canvas.transform.Find(RootName) != null)
        {
            EditorUtility.DisplayDialog(
                "UI already exists",
                $"Удали старый {RootName} и запусти генератор снова.",
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

        RectTransform root = Rect(RootName, canvas.transform);
        Stretch(root);

        Undo.RegisterCreatedObjectUndo(
            root.gameObject,
            "Create Match Results UI");

        root.gameObject.AddComponent<CanvasGroup>();

        Image dimmer = Box(
            "Dimmer", root,
            0, 0, 0, 0,
            new Color(0, 0, 0, 0.68f));

        Stretch(dimmer.rectTransform);
        dimmer.raycastTarget = true;

        RectTransform content = Rect("Content", root);

        content.anchorMin = content.anchorMax =
            new Vector2(0.5f, 0.5f);

        content.pivot = new Vector2(0.5f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(1080, 960);

        CreateHeader(content);
        CreatePlacement(content);
        ScrollRect scroll = CreateTable(content);
        CreateLobbyButton(content);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);

        scroll.verticalNormalizedPosition = 1f;

        Selection.activeGameObject = root.gameObject;

        EditorSceneManager.MarkSceneDirty(
            root.gameObject.scene);

        Debug.Log(
            "MatchResultsUI создан. Rows поддерживает любое количество " +
            "строк с вертикальной прокруткой.",
            root);
    }

    // ---------- Header ----------

    private static void CreateHeader(Transform parent)
    {
        Text(
            "ModeLabel", parent,
            "КАЖДЫЙ САМ ЗА СЕБЯ",
            0, 12, 1080, 34,
            21, Secondary,
            TextAlignmentOptions.Center,
            9);

        Text(
            "Title", parent,
            "МАТЧ ЗАВЕРШЁН",
            0, 80, 1080, 84,
            64, White,
            TextAlignmentOptions.Center,
            7);
    }

    // ---------- Placement ----------

    private static void CreatePlacement(Transform parent)
    {
        RectTransform placement = Rect("Placement", parent);
        Place(placement, 350, 200, 380, 118);

        Text(
            "Number", placement,
            "1",
            0, 0, 110, 118,
            112, White,
            TextAlignmentOptions.Center);

        Text(
            "Label", placement,
            "МЕСТО",
            130, 34, 250, 68,
            36, White,
            TextAlignmentOptions.MidlineLeft,
            9);

        Text(
            "ResultCaption", parent,
            "Ваш результат",
            0, 322, 1080, 36,
            23, Secondary,
            TextAlignmentOptions.Center,
            5);
    }

    // ---------- Scrollable table ----------
    
private static ScrollRect CreateTable(Transform parent)
{
    RectTransform table = Rect("ResultsTable", parent);
    Place(table, 0, 400, TableWidth, 308);

    RectTransform header = Rect("Header", table);
    Place(header, 0, 0, TableWidth, 44);

    Text(
        "Place", header,
        "МЕСТО",
        0, 0, 90, 34,
        17, White,
        TextAlignmentOptions.Center,
        3);

    Text(
        "Player", header,
        "ИГРОК",
        120, 0, 290, 34,
        17, White,
        TextAlignmentOptions.MidlineLeft,
        4);

    Text(
        "Kills", header,
        "УБИЙСТВА",
        430, 0, 130, 34,
        17, White,
        TextAlignmentOptions.Center,
        2);

    Text(
        "Coins", header,
        "МОНЕТЫ",
        575, 0, 125, 34,
        17, White,
        TextAlignmentOptions.Center,
        3);

    Text(
        "XP", header,
        "XP",
        725, 0, 100, 34,
        17, White,
        TextAlignmentOptions.Center,
        3);

    Text(
        "Status", header,
        "СТАТУС",
        870, 0, 210, 34,
        17, White,
        TextAlignmentOptions.MidlineLeft,
        4);

    Divider(table, 44);

    RectTransform scrollRoot = Rect("Scroll View", table);
    Place(scrollRoot, 0, 46, TableWidth, 256);

    ScrollRect scroll =
        scrollRoot.gameObject.AddComponent<ScrollRect>();

    scroll.horizontal = false;
    scroll.vertical = true;
    scroll.movementType = ScrollRect.MovementType.Clamped;
    scroll.inertia = true;
    scroll.decelerationRate = 0.135f;
    scroll.scrollSensitivity = 30f;

    RectTransform viewport = Rect("Viewport", scrollRoot);
    Stretch(viewport);

    Image viewportImage =
        viewport.gameObject.AddComponent<Image>();

    viewportImage.color = Color.clear;
    viewportImage.raycastTarget = true;

    viewport.gameObject.AddComponent<RectMask2D>();

    RectTransform rows = Rect("Rows", viewport);

    rows.anchorMin = new Vector2(0, 1);
    rows.anchorMax = new Vector2(1, 1);
    rows.pivot = new Vector2(0.5f, 1);
    rows.anchoredPosition = Vector2.zero;
    rows.sizeDelta = Vector2.zero;

    VerticalLayoutGroup layout =
        rows.gameObject.AddComponent<VerticalLayoutGroup>();

    layout.padding = new RectOffset();
    layout.spacing = 0;
    layout.childAlignment = TextAnchor.UpperLeft;
    layout.childControlWidth = true;
    layout.childControlHeight = true;
    layout.childForceExpandWidth = true;
    layout.childForceExpandHeight = false;

    ContentSizeFitter fitter =
        rows.gameObject.AddComponent<ContentSizeFitter>();

    fitter.horizontalFit =
        ContentSizeFitter.FitMode.Unconstrained;

    fitter.verticalFit =
        ContentSizeFitter.FitMode.PreferredSize;

    scroll.viewport = viewport;
    scroll.content = rows;

    // Демонстрационные данные:
    // место, имя, убийства, монеты, XP, свой игрок, вышел.
    CreateRow(
        rows, "Player_01",
        1, "nickname", 10, 150, 250,
        true, false);

    CreateRow(
        rows, "Player_02",
        2, "Alex", 7, 100, 180,
        false, false);

    CreateRow(
        rows, "Player_03",
        3, "Ghost", 5, 60, 120,
        false, true);

    CreateRow(
        rows, "Player_04",
        4, "Player_04", 2, 30, 70,
        false, true);

    return scroll;
}

private static void CreateRow(
    Transform parent,
    string objectName,
    int place,
    string nickname,
    int kills,
    int coins,
    int xp,
    bool localPlayer,
    bool left)
{
    RectTransform row = Rect(objectName, parent);
    Place(row, 0, 0, TableWidth, RowHeight);

    LayoutElement element =
        row.gameObject.AddComponent<LayoutElement>();

    element.minHeight = RowHeight;
    element.preferredHeight = RowHeight;
    element.flexibleHeight = 0;

    Color color = left ? Secondary : White;

    if (localPlayer)
    {
        Box(
            "Highlight", row,
            0, 0, TableWidth, RowHeight - 1,
            new Color(1, 1, 1, 0.035f));

        Box(
            "Marker", row,
            0, 12, 4, 40,
            White);
    }

    Text(
        "Place", row,
        place.ToString(),
        0, 0, 90, RowHeight - 1,
        28, color,
        TextAlignmentOptions.Center);

    TMP_Text nameText = Text(
        "Name", row,
        nickname,
        120, 0, localPlayer ? 205 : 290, RowHeight - 1,
        26, color,
        TextAlignmentOptions.MidlineLeft,
        1);

    nameText.overflowMode = TextOverflowModes.Ellipsis;

    if (localPlayer)
    {
        Text(
            "YouLabel", row,
            "ВЫ",
            340, 0, 65, RowHeight - 1,
            17, White,
            TextAlignmentOptions.Center,
            3);
    }

    Text(
        "Kills", row,
        kills.ToString(),
        430, 0, 130, RowHeight - 1,
        28, color,
        TextAlignmentOptions.Center);

    Text(
        "Coins", row,
        "+" + coins,
        575, 0, 125, RowHeight - 1,
        26, color,
        TextAlignmentOptions.Center);

    Text(
        "XP", row,
        "+" + xp,
        725, 0, 100, RowHeight - 1,
        26, color,
        TextAlignmentOptions.Center);

    RectTransform status = Rect("Status", row);
    Place(status, 870, 0, 210, RowHeight - 1);

    Icon(
        "Icon", status,
        0, 19, 26, 26,
        color);

    Text(
        "Text", status,
        left ? "Вышел" : "Завершил",
        left ? 42 : 0,
        0,
        left ? 168 : 210,
        RowHeight - 1,
        24, color,
        TextAlignmentOptions.MidlineLeft);

    Divider(row, RowHeight - 1);
}
    
    
    // ---------- Rewards ----------

    private static void CreateRewards(Transform parent)
    {
        RectTransform rewards = Rect("Rewards", parent);
        Place(rewards, 250, 750, 580, 56);

        Icon(
            "CoinsIcon", rewards,
            0, 8, 40, 40,
            White);

        Text(
            "CoinsText", rewards,
            "+150 МОНЕТ",
            64, 0, 280, 56,
            30, White,
            TextAlignmentOptions.MidlineLeft,
            4);

        Text(
            "Separator", rewards,
            "•",
            344, 0, 42, 56,
            28, White,
            TextAlignmentOptions.Center);

        Text(
            "ExperienceText", rewards,
            "+250 XP",
            404, 0, 176, 56,
            30, White,
            TextAlignmentOptions.MidlineLeft,
            4);
    }

    // ---------- Lobby button ----------

    private static void CreateLobbyButton(Transform parent)
    {
        Image backing = Box(
            "LobbyButton", parent,
            346, 868, 388, 76,
            new Color(0, 0, 0, 0.12f));

        backing.raycastTarget = true;

        LeanButton button =
            backing.gameObject.AddComponent<LeanButton>();

        button.targetGraphic = backing;
        button.interactable = true;
        button.transition = Selectable.Transition.None;

        Box(
            "BorderTop", backing.transform,
            0, 0, 388, 1.5f, White);

        Box(
            "BorderBottom", backing.transform,
            0, 74.5f, 388, 1.5f, White);

        Box(
            "BorderLeft", backing.transform,
            0, 0, 1.5f, 76, White);

        Box(
            "BorderRight", backing.transform,
            386.5f, 0, 1.5f, 76, White);

        Text(
            "Text", backing.transform,
            "В ЛОББИ",
            48, 0, 224, 76,
            23, White,
            TextAlignmentOptions.Center,
            7);

        Icon(
            "ArrowIcon", backing.transform,
            292, 23, 32, 30,
            White);
    }

    // ---------- Helpers ----------

    private static Canvas FindCanvas()
    {
        Canvas result = null;

        foreach (Canvas candidate in
                 Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (EditorUtility.IsPersistent(candidate))
                continue;

            var scene = candidate.gameObject.scene;

            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            if (candidate.gameObject.name != CanvasName)
                continue;

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

    private static RectTransform Rect(
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
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Place(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
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
        RectTransform rect = Rect(name, parent);
        Place(rect, x, y, width, height);

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    private static void Icon(
        string name,
        Transform parent,
        float x,
        float y,
        float width,
        float height,
        Color color)
    {
        Image image = Box(
            name, parent,
            x, y, width, height,
            color);

        image.preserveAspect = true;
        image.enabled = false;
    }

    private static void Divider(Transform parent, float y)
    {
        Box(
            "Divider", parent,
            0, y, TableWidth, 1,
            LineColor);
    }

    private static TMP_Text Text(
        string name,
        Transform parent,
        string value,
        float x,
        float y,
        float width,
        float height,
        float size,
        Color color,
        TextAlignmentOptions alignment,
        float spacing = 0)
    {
        RectTransform rect = Rect(name, parent);
        Place(rect, x, y, width, height);

        TextMeshProUGUI text =
            rect.gameObject.AddComponent<TextMeshProUGUI>();

        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.characterSpacing = spacing;
        text.raycastTarget = false;

        return text;
    }
}