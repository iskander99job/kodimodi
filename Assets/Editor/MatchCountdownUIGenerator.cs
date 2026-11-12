using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MatchCountdownUIGenerator
{
    private const string CanvasName = "MainCanvas";
    private const string RootName = "MatchCountdownUI";

    private static TMP_FontAsset font;

    private static readonly Color White = Color.white;

    private static readonly Color Secondary =
        new Color32(185, 190, 185, 255);

    [MenuItem("Tools/Game UI/Create Match Countdown")]
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
            "Create Match Countdown UI");

        CanvasGroup group =
            root.gameObject.AddComponent<CanvasGroup>();

        group.alpha = 1;
        group.interactable = false;
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
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(1100, 600);

        Text(
            "ModeLabel", content,
            "КАЖДЫЙ САМ ЗА СЕБЯ",
            0, 20, 1100, 42,
            24, Secondary,
            spacing: 7);

        Text(
            "Title", content,
            "МАТЧ СКОРО\nНАЧНЁТСЯ",
            0, 100, 1100, 166,
            62, White,
            spacing: 8);

        Text(
            "TimerText", content,
            "00:05",
            0, 296, 1100, 142,
            120, White,
            spacing: 3);

        Box(
            "Divider", content,
            497, 484, 106, 2,
            White);

        Text(
            "Hint", content,
            "Приготовьтесь к бою",
            0, 522, 1100, 56,
            32, Secondary);

        Selection.activeGameObject = root.gameObject;

        EditorSceneManager.MarkSceneDirty(
            root.gameObject.scene);

        Debug.Log(
            "MatchCountdownUI создан. TimerText пока содержит " +
            "демонстрационное значение 00:05.",
            root);
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