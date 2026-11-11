using Lean.Gui;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DeviceSetupUIGenerator
{
    private const string CanvasName = "MainCanvas";
    private const string RootName = "DeviceSetupUI";

    private static TMP_FontAsset font;
    private static Sprite rounded;

    private static readonly Color Ink = Color.black;
    private static readonly Color Muted =
        new Color32(90, 90, 90, 255);
    private static readonly Color RowColor =
        new Color32(244, 244, 244, 255);
    private static readonly Color BadgeColor =
        new Color32(225, 225, 225, 255);
    private static readonly Color BorderColor =
        new Color32(200, 200, 200, 255);

    [MenuItem("Tools/Game UI/Create Device Setup")]
    private static void Generate()
    {
        Canvas canvas = FindCanvas();

        if (canvas == null)
            return;

        if (canvas.transform.Find(RootName) != null)
        {
            EditorUtility.DisplayDialog(
                "UI already exists",
                $"Внутри {CanvasName} уже есть {RootName}.",
                "OK");
            return;
        }

        font = TMP_Settings.defaultFontAsset;

        if (font == null)
        {
            EditorUtility.DisplayDialog(
                "TextMeshPro",
                "Импортируй TMP Essential Resources и назначь " +
                "Default Font Asset в настройках TMP.",
                "OK");
            return;
        }

        rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>(
            "UI/Skin/UISprite.psd");

        RectTransform root = Rect(RootName, canvas.transform);
        Stretch(root);

        Undo.RegisterCreatedObjectUndo(
            root.gameObject,
            "Create Device Setup UI");

        root.gameObject.AddComponent<CanvasGroup>();

        // Right side of the screen.
        // Designed for Canvas Scaler reference resolution 1920 × 1080.
        RectTransform panel = Rect("Panel", root);
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 0.5f);
        panel.pivot = new Vector2(1f, 0.5f);
        panel.anchoredPosition = new Vector2(-80, 0);
        panel.sizeDelta = new Vector2(880, 820);
        panel.localScale = Vector3.one;

        // Thin border with a white backing over it.
        Image border = Box(
            "Border", panel,
            0, 0, 880, 820,
            BorderColor, true);

        border.raycastTarget = false;

        Image backing = Box(
            "Backing", panel,
            2, 2, 876, 816,
            Color.white, true);

        // Block clicks through the visible panel.
        backing.raycastTarget = true;

        // Header.
        Text(
            "Title", panel,
            "Подготовься к игре",
            48, 56, 784, 72,
            52, Ink,
            bold: true);

        Text(
            "Description", panel,
            "Камера покажет твоё лицо на персонаже. Микрофон\n" +
            "нужен для общения с игроками.",
            48, 148, 784, 80,
            28, Muted);

        // Device rows.
        CreateDeviceRow(
            panel,
            "CameraRow",
            "Камера",
            "Не включена",
            264);

        CreateDeviceRow(
            panel,
            "MicrophoneRow",
            "Микрофон",
            "Не включён",
            386);

        // Instructions.
        Text(
            "Instruction", panel,
            "Нажми кнопку и разреши доступ в окне браузера.",
            48, 522, 784, 54,
            25, Muted);

        // Main action.
        CreateButton(
            "EnableDevicesButton", panel,
            "Включить камеру и микрофон",
            44, 588, 792, 80,
            Color.black, Color.white,
            32, true);

        // Help link.
        LeanButton help = CreateButton(
            "HelpButton", panel,
            "У меня не получается",
            180, 682, 520, 56,
            Color.clear, Ink,
            27, false);

        TMP_Text helpText =
            help.GetComponentInChildren<TMP_Text>();

        helpText.fontStyle = FontStyles.Underline;

        // Footer, as in the supplied screenshot.
        Image divider = Box(
            "Divider", panel,
            44, 752, 792, 1,
            BorderColor);

        divider.raycastTarget = false;

        Text(
            "Footer", panel,
            "Проверка устройств — перед входом в матч",
            44, 766, 792, 40,
            23, Muted,
            centered: true);

        Selection.activeGameObject = root.gameObject;
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        Debug.Log(
            $"Создан {CanvasName}/{RootName}. " +
            "Назначь иконки и подключи события LeanButton.",
            root);
    }

    private static void CreateDeviceRow(
        Transform parent,
        string name,
        string title,
        string status,
        float y)
    {
        Image row = Box(
            name, parent,
            44, y, 792, 104,
            RowColor, true);

        row.raycastTarget = false;

        // Icon placeholder: assign a Sprite and enable Image.
        RectTransform iconRect = Rect("Icon", row.transform);
        Place(iconRect, 30, 26, 52, 52);

        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.color = Ink;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = false;

        Image separator = Box(
            "Separator", row.transform,
            112, 22, 1, 60,
            BorderColor);

        separator.raycastTarget = false;

        Text(
            "Title", row.transform,
            title,
            148, 13, 390, 40,
            32, Ink,
            bold: true);

        Text(
            "Description", row.transform,
            "Доступ ещё не предоставлен",
            148, 57, 400, 30,
            23, Muted);

        Image badge = Box(
            "StatusBadge", row.transform,
            588, 26, 188, 52,
            BadgeColor, true);

        badge.raycastTarget = false;

        Text(
            "StatusText", badge.transform,
            status,
            8, 0, 172, 52,
            23, Ink,
            centered: true);
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

            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            if (candidate.gameObject.name != CanvasName)
                continue;

            if (result != null)
            {
                EditorUtility.DisplayDialog(
                    "Several canvases found",
                    $"Найдено несколько Canvas с именем {CanvasName}.",
                    "OK");
                return null;
            }

            result = candidate;
        }

        if (result == null)
        {
            EditorUtility.DisplayDialog(
                "Canvas not found",
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
        float x, float y,
        float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static Image Box(
        string name,
        Transform parent,
        float x, float y,
        float width, float height,
        Color color,
        bool useRounded = false)
    {
        RectTransform rect = Rect(name, parent);
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

    private static TMP_Text Text(
        string name,
        Transform parent,
        string value,
        float x, float y,
        float width, float height,
        float fontSize,
        Color color,
        bool bold = false,
        bool centered = false)
    {
        RectTransform rect = Rect(name, parent);
        Place(rect, x, y, width, height);

        TextMeshProUGUI text =
            rect.gameObject.AddComponent<TextMeshProUGUI>();

        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = bold
            ? FontStyles.Bold
            : FontStyles.Normal;

        text.color = color;
        text.alignment = centered
            ? TextAlignmentOptions.Center
            : TextAlignmentOptions.MidlineLeft;

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
        float fontSize,
        bool bold)
    {
        Image image = Box(
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

        Text(
            "Text", image.transform,
            label,
            12, 0, width - 24, height,
            fontSize, foreground,
            bold: bold,
            centered: true);

        return button;
    }
}