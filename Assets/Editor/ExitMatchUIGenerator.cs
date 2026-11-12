using Lean.Gui;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ExitMatchUIGenerator
{
    private const string CanvasName = "MainCanvas";
    private const string RootName = "ExitMatchUI";

    private static TMP_FontAsset font;

    private static readonly Color White = Color.white;

    private static readonly Color Secondary =
        new Color32(185, 190, 185, 255);

    [MenuItem("Tools/Game UI/Create Exit Match")]
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

        RectTransform root = CreateRect(
            RootName,
            canvas.transform);

        Stretch(root);

        Undo.RegisterCreatedObjectUndo(
            root.gameObject,
            "Create Exit Match UI");

        CanvasGroup group =
            root.gameObject.AddComponent<CanvasGroup>();

        group.alpha = 1;
        group.interactable = true;
        group.blocksRaycasts = true;

        Image dimmer = Box(
            "Dimmer", root,
            0, 0, 0, 0,
            new Color(0, 0, 0, 0.78f));

        Stretch(dimmer.rectTransform);
        dimmer.raycastTarget = true;

        CreateEscapeHint(root);

        RectTransform content = CreateRect("Content", root);

        content.anchorMin = content.anchorMax =
            new Vector2(0.5f, 0.5f);

        content.pivot = new Vector2(0.5f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(1280, 520);

        Text(
            "Title", content,
            "ВЫ ДЕЙСТВИТЕЛЬНО ХОТИТЕ\nВЫЙТИ ИЗ МАТЧА?",
            0, 20, 1280, 160,
            46, White,
            spacing: 6);

        Text(
            "Description", content,
            "Матч продолжится без вас.",
            0, 210, 1280, 56,
            32, Secondary,
            spacing: 1);

        CreateButton(
            "StayButton", content,
            "ОСТАТЬСЯ",
            60, 330, 550, 92,
            Color.white,
            Color.black,
            outlined: false);

        CreateButton(
            "LeaveButton", content,
            "ВЫЙТИ ИЗ МАТЧА",
            670, 330, 550, 92,
            new Color(0, 0, 0, 0.12f),
            Color.white,
            outlined: true);

        Selection.activeGameObject = root.gameObject;

        EditorSceneManager.MarkSceneDirty(
            root.gameObject.scene);

        Debug.Log(
            "ExitMatchUI создан. Подключи события StayButton " +
            "и LeaveButton к игровой логике.",
            root);
    }

    // ---------- ESC hint ----------

    private static void CreateEscapeHint(Transform parent)
    {
        RectTransform hint = CreateRect("EscapeHint", parent);

        hint.anchorMin = hint.anchorMax = new Vector2(1, 1);
        hint.pivot = new Vector2(1, 1);
        hint.anchoredPosition = new Vector2(-56, -38);
        hint.sizeDelta = new Vector2(400, 44);

        Image key = Box(
            "Key", hint,
            0, 0, 80, 42,
            new Color(1, 1, 1, 0.035f));

        CreateBorder(key.transform, 80, 42);

        Text(
            "Text", key.transform,
            "ESC",
            0, 0, 80, 42,
            21, White,
            spacing: 3);

        Text(
            "Label", hint,
            "Вернуться в матч",
            108, 0, 292, 42,
            24, Secondary,
            alignment: TextAlignmentOptions.MidlineLeft);
    }

    // ---------- Buttons ----------

    private static void CreateButton(
        string name,
        Transform parent,
        string label,
        float x,
        float y,
        float width,
        float height,
        Color background,
        Color foreground,
        bool outlined)
    {
        Image backing = Box(
            name, parent,
            x, y, width, height,
            background);

        backing.raycastTarget = true;

        LeanButton button =
            backing.gameObject.AddComponent<LeanButton>();

        button.targetGraphic = backing;
        button.interactable = true;
        button.transition = Selectable.Transition.None;

        if (outlined)
            CreateBorder(backing.transform, width, height);

        Text(
            "Text", backing.transform,
            label,
            20, 0, width - 40, height,
            28, foreground,
            spacing: 5);
    }

    private static void CreateBorder(
        Transform parent,
        float width,
        float height)
    {
        const float thickness = 1.5f;

        Box(
            "BorderTop", parent,
            0, 0, width, thickness,
            White);

        Box(
            "BorderBottom", parent,
            0, height - thickness, width, thickness,
            White);

        Box(
            "BorderLeft", parent,
            0, 0, thickness, height,
            White);

        Box(
            "BorderRight", parent,
            width - thickness, 0, thickness, height,
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
        var go = new GameObject(
            name,
            typeof(RectTransform));

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
        RectTransform rect = CreateRect(name, parent);
        Place(rect, x, y, width, height);

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return image;
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
        float spacing = 0,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        RectTransform rect = CreateRect(name, parent);
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