using Lean.Gui;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MatchStatsUIGenerator
{
    private const string CanvasName = "MainCanvas";
    private const string RootName = "MatchStatsUI";

    private const float TableWidth = 1200;
    private const float RowHeight = 72;

    private static TMP_FontAsset font;

    private static readonly Color White = Color.white;

    private static readonly Color Secondary =
        new Color32(180, 185, 180, 255);

    private static readonly Color LineColor =
        new Color(1, 1, 1, 0.5f);

    [MenuItem("Tools/Game UI/Create Match Stats")]
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

        RectTransform root = Rect(RootName, canvas.transform);
        Stretch(root);

        Undo.RegisterCreatedObjectUndo(
            root.gameObject,
            "Create Match Stats UI");

        root.gameObject.AddComponent<CanvasGroup>();

        Image dimmer = Box(
            "Dimmer", root,
            0, 0, 0, 0,
            new Color(0, 0, 0, 0.68f));

        Stretch(dimmer.rectTransform);
        dimmer.raycastTarget = true;

        CreateCloseHint(root);

        RectTransform content = Rect("Content", root);

        content.anchorMin = content.anchorMax =
            new Vector2(0.5f, 0.5f);

        content.pivot = new Vector2(0.5f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(1200, 960);

        CreateHeader(content);
        ScrollRect scroll = CreateTable(content);
        CreateExitButton(content);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);

        scroll.verticalNormalizedPosition = 1f;

        Selection.activeGameObject = root.gameObject;
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        Debug.Log(
            "MatchStatsUI создан. Назначь иконку кнопки выхода " +
            "и подключи игровую логику.",
            root);
    }

    // ---------- TAB hint ----------

    private static void CreateCloseHint(Transform parent)
    {
        RectTransform hint = Rect("CloseHint", parent);

        hint.anchorMin = hint.anchorMax = new Vector2(1, 1);
        hint.pivot = new Vector2(1, 1);
        hint.anchoredPosition = new Vector2(-56, -38);
        hint.sizeDelta = new Vector2(250, 44);

        Image key = Box(
            "Key", hint,
            0, 0, 90, 42,
            new Color(1, 1, 1, 0.035f));

        OutlineRect(key.transform, 90, 42, White);

        Text(
            "Text", key.transform,
            "TAB",
            0, 0, 90, 42,
            20, White,
            TextAlignmentOptions.Center,
            5);

        Text(
            "Label", hint,
            "Закрыть",
            116, 0, 134, 42,
            23, White,
            TextAlignmentOptions.MidlineLeft,
            3);
    }

    // ---------- Header ----------

    private static void CreateHeader(Transform parent)
    {
        Text(
            "ModeLabel", parent,
            "КАЖДЫЙ САМ ЗА СЕБЯ",
            0, 12, 1200, 36,
            21, Secondary,
            TextAlignmentOptions.Center,
            8);

        Text(
            "Title", parent,
            "СТАТИСТИКА МАТЧА",
            0, 92, 1200, 84,
            60, White,
            TextAlignmentOptions.Center,
            7);

        Text(
            "VictoryHint", parent,
            "До победы — 10 убийств",
            0, 188, 1200, 42,
            25, Secondary,
            TextAlignmentOptions.Center,
            4);
    }

    // ---------- Table ----------

    private static ScrollRect CreateTable(Transform parent)
    {
        RectTransform table = Rect("StatsTable", parent);
        Place(table, 0, 310, TableWidth, 366);

        RectTransform header = Rect("Header", table);
        Place(header, 0, 0, TableWidth, 44);

        Text(
            "Place", header,
            "МЕСТО",
            0, 0, 110, 34,
            19, White,
            TextAlignmentOptions.Center,
            5);

        Text(
            "Player", header,
            "ИГРОК",
            200, 0, 470, 34,
            19, White,
            TextAlignmentOptions.MidlineLeft,
            6);

        Text(
            "Kills", header,
            "УБИЙСТВА",
            740, 0, 180, 34,
            19, White,
            TextAlignmentOptions.Center,
            4);

        Text(
            "Status", header,
            "СТАТУС",
            1020, 0, 180, 34,
            19, White,
            TextAlignmentOptions.MidlineLeft,
            5);

        Divider(table, 44);

        RectTransform scrollRoot = Rect("Scroll View", table);
        Place(scrollRoot, 0, 46, TableWidth, 320);

        ScrollRect scroll =
            scrollRoot.gameObject.AddComponent<ScrollRect>();

        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.decelerationRate = 0.135f;
        scroll.scrollSensitivity = 32;

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

        CreateRow(
            rows, "Player_01",
            1, "nickname", 7,
            localPlayer: true,
            left: false);

        CreateRow(
            rows, "Player_02",
            2, "Alex", 5,
            localPlayer: false,
            left: false);

        CreateRow(
            rows, "Player_03",
            3, "Ghost", 3,
            localPlayer: false,
            left: false);

        CreateRow(
            rows, "Player_04",
            4, "Player_04", 2,
            localPlayer: false,
            left: true);

        return scroll;
    }

    private static void CreateRow(
        Transform parent,
        string objectName,
        int place,
        string nickname,
        int kills,
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
                new Color(1, 1, 1, 0.045f));

            Box(
                "Marker", row,
                0, 12, 4, RowHeight - 24,
                White);
        }

        Text(
            "Place", row,
            place.ToString(),
            0, 0, 110, RowHeight - 1,
            32, color,
            TextAlignmentOptions.Center);

        TMP_Text nameText = Text(
            "Name", row,
            nickname,
            200, 0, localPlayer ? 340 : 470, RowHeight - 1,
            30, color,
            TextAlignmentOptions.MidlineLeft,
            2);

        nameText.overflowMode = TextOverflowModes.Ellipsis;

        if (localPlayer)
        {
            Text(
                "YouLabel", row,
                "ВЫ",
                570, 0, 80, RowHeight - 1,
                19, White,
                TextAlignmentOptions.MidlineLeft,
                4);
        }

        Text(
            "Kills", row,
            kills.ToString(),
            740, 0, 180, RowHeight - 1,
            32, color,
            TextAlignmentOptions.Center);

        // Same position and alignment for every status.
        Text(
            "Status", row,
            left ? "Вышел" : "В игре",
            1020, 0, 180, RowHeight - 1,
            27, color,
            TextAlignmentOptions.MidlineLeft,
            2);

        Divider(row, RowHeight - 1);
    }

    // ---------- Exit button ----------

    private static void CreateExitButton(Transform parent)
    {
        Image backing = Box(
            "ExitMatchButton", parent,
            335, 868, 530, 76,
            new Color(0, 0, 0, 0.12f));

        backing.raycastTarget = true;

        LeanButton button =
            backing.gameObject.AddComponent<LeanButton>();

        button.targetGraphic = backing;
        button.interactable = true;
        button.transition = Selectable.Transition.None;

        OutlineRect(backing.transform, 530, 76, White);

        // Assign the exit sprite and enable this Image.
        Image icon = Box(
            "Icon", backing.transform,
            36, 23, 30, 30,
            White);

        icon.preserveAspect = true;
        icon.enabled = false;

        Text(
            "Text", backing.transform,
            "ВЫЙТИ ИЗ МАТЧА",
            92, 0, 410, 76,
            23, White,
            TextAlignmentOptions.Center,
            5);

        Text(
            "ExitHint", parent,
            "Матч продолжится без вас",
            0, 946, 1200, 32,
            21, Secondary,
            TextAlignmentOptions.Center,
            4);
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

    private static RectTransform Rect(string name, Transform parent)
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

    private static void Divider(Transform parent, float y)
    {
        Box(
            "Divider", parent,
            0, y, TableWidth, 1,
            LineColor);
    }

    private static void OutlineRect(
        Transform parent,
        float width,
        float height,
        Color color)
    {
        const float thickness = 1.5f;

        Box(
            "BorderTop", parent,
            0, 0, width, thickness,
            color);

        Box(
            "BorderBottom", parent,
            0, height - thickness, width, thickness,
            color);

        Box(
            "BorderLeft", parent,
            0, 0, thickness, height,
            color);

        Box(
            "BorderRight", parent,
            width - thickness, 0, thickness, height,
            color);
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