using Lean.Gui;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CameraTimeoutUIGenerator
{
    private const string CanvasName = "MainCanvas";
    private const string RootName = "CameraTimeoutUI";

    private static TMP_FontAsset font;

    private static readonly Color White = Color.white;

    private static readonly Color Secondary =
        new Color32(185, 190, 185, 255);

    [MenuItem("Tools/Game UI/Create Camera Timeout")]
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
            "Create Camera Timeout UI");

        CanvasGroup group =
            root.gameObject.AddComponent<CanvasGroup>();

        group.alpha = 1;
        group.interactable = true;
        group.blocksRaycasts = true;

        // Full-screen overlay.
        Image dimmer = Box(
            "Dimmer", root,
            0, 0, 0, 0,
            new Color(0, 0, 0, 0.78f));

        Stretch(dimmer.rectTransform);
        dimmer.raycastTarget = true;

        // Centered content.
        RectTransform content = CreateRect("Content", root);

        content.anchorMin = content.anchorMax =
            new Vector2(0.5f, 0.5f);

        content.pivot = new Vector2(0.5f, 0.5f);
        content.anchoredPosition = new Vector2(0, 30);
        content.sizeDelta = new Vector2(1100, 760);

        CreateMessage(content);
        CreateLobbyButton(content);

        Selection.activeGameObject = root.gameObject;

        EditorSceneManager.MarkSceneDirty(
            root.gameObject.scene);

        Debug.Log(
            "CameraTimeoutUI создан. Назначь иконку и событие LobbyButton.",
            root);
    }

    // ---------- Message ----------

    private static void CreateMessage(Transform parent)
    {
        Image icon = Box(
            "CameraOffIcon", parent,
            510, 0, 80, 80,
            White);

        icon.preserveAspect = true;
        icon.enabled = false;

        Text(
            "Eyebrow", parent,
            "ВРЕМЯ ИСТЕКЛО",
            0, 128, 1100, 44,
            28, White,
            spacing: 6);

        TMP_Text title = Text(
            "Title", parent,
            "ВЫ ОТКЛЮЧЕНЫ\nОТ МАТЧА",
            0, 190, 1100, 150,
            52, White,
            spacing: 7);

        title.lineSpacing = 8;

        TMP_Text description = Text(
            "Description", parent,
            "Камера не была включена за 15 секунд.\n" +
            "Для участия в матче камера должна работать.",
            20, 382, 1060, 92,
            29, Secondary);

        description.lineSpacing = 10;

        Box(
            "Divider", parent,
            504, 520, 92, 1.5f,
            new Color(1, 1, 1, 0.8f));
    }

    // ---------- Lobby button ----------

    private static void CreateLobbyButton(Transform parent)
    {
        const float width = 420;
        const float height = 84;

        Image backing = Box(
            "LobbyButton", parent,
            340, 574, width, height,
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
            20, 0, width - 40, height,
            30, White,
            spacing: 6);
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