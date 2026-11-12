using Lean.Gui;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MatchErrorUIGenerator
{
    private const string CanvasName = "MainCanvas";
    private const string RootName = "MatchErrorUI";

    private static TMP_FontAsset font;

    private static readonly Color White = Color.white;

    private static readonly Color Secondary =
        new Color32(180, 185, 180, 255);

    [MenuItem("Tools/Game UI/Create Match Error")]
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

        RectTransform root = CreateRect(
            RootName,
            canvas.transform);

        Stretch(root);

        Undo.RegisterCreatedObjectUndo(
            root.gameObject,
            "Create Match Error UI");

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

        RectTransform content = CreateRect("Content", root);

        content.anchorMin = content.anchorMax =
            new Vector2(0.5f, 0.5f);

        content.pivot = new Vector2(0.5f, 0.5f);
        content.anchoredPosition = new Vector2(0, -30);
        content.sizeDelta = new Vector2(1000, 640);

        CreateIcon(content);
        CreateMessage(content);
        CreateDivider(content);
        CreateLobbyButton(content);

        Selection.activeGameObject = root.gameObject;

        EditorSceneManager.MarkSceneDirty(
            root.gameObject.scene);

        Debug.Log(
            "MatchErrorUI создан. Назначь иконку в ErrorIcon " +
            "и событие кнопки LobbyButton.",
            root);
    }

    // ---------- Icon ----------

    private static void CreateIcon(Transform parent)
    {
        Image icon = Box(
            "ErrorIcon", parent,
            468, 0, 64, 64,
            White);

        icon.preserveAspect = true;

        // Назначь спрайт и включи Image.
        icon.enabled = false;
    }

    // ---------- Message ----------

    private static void CreateMessage(Transform parent)
    {
        Text(
            "Eyebrow", parent,
            "МАТЧ ПРЕРВАН",
            0, 104, 1000, 38,
            23, White,
            spacing: 9);

        Text(
            "Title", parent,
            "СОЕДИНЕНИЕ\nПОТЕРЯНО",
            0, 160, 1000, 126,
            48, White,
            spacing: 7);

        TMP_Text description = Text(
            "Description", parent,
            "Не удалось сохранить связь с игровым сервером.\n" +
            "Вернитесь в лобби, чтобы начать новый матч.",
            20, 310, 960, 84,
            25, Secondary,
            spacing: 0.5f);

        description.lineSpacing = 10;
    }

    // ---------- Separator ----------

    private static void CreateDivider(Transform parent)
    {
        Box(
            "Divider", parent,
            437, 434, 126, 1.5f,
            new Color(1, 1, 1, 0.75f));
    }

    // ---------- Lobby button ----------

    private static void CreateLobbyButton(Transform parent)
    {
        const float width = 304;
        const float height = 76;

        Image backing = Box(
            "LobbyButton", parent,
            348, 510, width, height,
            new Color(0, 0, 0, 0.12f));

        backing.raycastTarget = true;

        LeanButton button =
            backing.gameObject.AddComponent<LeanButton>();

        button.targetGraphic = backing;
        button.interactable = true;
        button.transition = Selectable.Transition.None;

        CreateBorder(backing.transform, width, height);

        Text(
            "Text", backing.transform,
            "В ЛОББИ",
            0, 0, width, height,
            23, White,
            spacing: 5);

        // Геометрическая стрелка — отдельный спрайт не требуется.
        RectTransform arrow = CreateRect(
            "Arrow",
            backing.transform);

        Place(arrow, 250, 23, 30, 30);

        Stroke(
            arrow,
            "Shaft",
            new Vector2(0, 0),
            new Vector2(24, 1.5f),
            0);

        Stroke(
            arrow,
            "Upper",
            new Vector2(8, 3.3f),
            new Vector2(10, 1.5f),
            -45);

        Stroke(
            arrow,
            "Lower",
            new Vector2(8, -3.3f),
            new Vector2(10, 1.5f),
            45);
    }

    private static void Stroke(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        float angle)
    {
        Image image = Box(
            name, parent,
            0, 0, size.x, size.y,
            White);

        RectTransform rect = image.rectTransform;

        rect.anchorMin = rect.anchorMax =
            new Vector2(0.5f, 0.5f);

        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.Euler(0, 0, angle);
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
        float spacing = 0)
    {
        RectTransform rect = CreateRect(name, parent);
        Place(rect, x, y, width, height);

        TextMeshProUGUI text =
            rect.gameObject.AddComponent<TextMeshProUGUI>();

        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.characterSpacing = spacing;
        text.raycastTarget = false;

        return text;
    }
}