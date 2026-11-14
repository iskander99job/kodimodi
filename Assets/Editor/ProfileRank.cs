using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ProfileRank
{
    private const string CanvasName = "MainCanvas";
    private const string RootName = "ProfileRank";

    [MenuItem("Tools/Game UI/Create Player Profile")]
    private static void Generate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

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

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;

        // Берём шрифт из существующего UI, если есть.
        foreach (TMP_Text existing in
                 canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            if (existing.font == null)
                continue;

            font = existing.font;
            break;
        }

        if (font == null)
        {
            EditorUtility.DisplayDialog(
                "TextMeshPro",
                "Назначь Default Font Asset в настройках TMP.",
                "OK");
            return;
        }

        // Можно выделить спрайт в Project перед генерацией.
        Sprite rankSprite = Selection.activeObject as Sprite;

        RectTransform root = Rect(
            RootName, canvas.transform,
            30, 25, 500, 140);

        // Существующие popup-окна будут отображаться поверх профиля.
        root.SetAsFirstSibling();

        Image icon = Box(
            "RankIcon", root,
            0, 0, 120, 120,
            Color.white);

        icon.sprite = rankSprite;
        icon.preserveAspect = true;

        Box(
            "Divider", root,
            140, 5, 2, 110,
            new Color32(160, 160, 160, 255));

        RectTransform info = Rect(
            "Info", root,
            165, 0, 330, 130);

        Text(
            "Nickname", info, font,
            "nickname",
            0, 0, 330, 38,
            32, FontStyles.Bold);

        Text(
            "RankName", info, font,
            "Легенда",
            0, 39, 330, 30,
            25, FontStyles.Normal);

        CreateXPBar(info);

        Text(
            "XPText", info, font,
            "350 / 1 000 XP",
            0, 97, 330, 32,
            25, FontStyles.Normal);

        Undo.RegisterCreatedObjectUndo(
            root.gameObject,
            "Create Player Profile UI");

        Selection.activeGameObject = root.gameObject;

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        Debug.Log(
            "PlayerProfile создан. Назначь спрайт в RankIcon, " +
            "измени тексты и Value у XPBar.",
            root);
    }

    private static void CreateXPBar(Transform parent)
    {
        Image border = Box(
            "XPBar", parent,
            0, 76, 290, 16,
            new Color32(100, 100, 100, 255));

        Image track = Box(
            "Track", border.transform,
            1, 1, 288, 14,
            new Color32(245, 245, 245, 255));

        Image fill = Box(
            "Fill", track.transform,
            0, 0, 288, 14,
            Color.black);

        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Slider slider = border.gameObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.interactable = false;
        slider.navigation = new Navigation
        {
            mode = Navigation.Mode.None
        };

        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fillRect;
        slider.minValue = 0;
        slider.maxValue = 1000;
        slider.wholeNumbers = true;
        slider.value = 350;

        // Разделители поверх заливки: шесть секций.
        for (int i = 1; i < 6; i++)
        {
            RectTransform tick = Box(
                "Tick_" + i, track.transform,
                0, 0, 1, 14,
                new Color32(125, 125, 125, 255)
            ).rectTransform;

            float fraction = i / 6f;
            tick.anchorMin = new Vector2(fraction, 0);
            tick.anchorMax = new Vector2(fraction, 1);
            tick.pivot = new Vector2(0.5f, 0.5f);
            tick.anchoredPosition = Vector2.zero;
            tick.sizeDelta = new Vector2(1, 0);
        }
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
                candidate.name != CanvasName)
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
        string name, Transform parent,
        float x, float y, float width, float height)
    {
        GameObject go = new GameObject(
            name, typeof(RectTransform));

        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();

        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);

        return rect;
    }

    private static Image Box(
        string name, Transform parent,
        float x, float y, float width, float height,
        Color color)
    {
        RectTransform rect = Rect(
            name, parent, x, y, width, height);

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    private static TMP_Text Text(
        string name, Transform parent, TMP_FontAsset font,
        string value,
        float x, float y, float width, float height,
        float size, FontStyles style)
    {
        RectTransform rect = Rect(
            name, parent, x, y, width, height);

        TextMeshProUGUI text =
            rect.gameObject.AddComponent<TextMeshProUGUI>();

        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;

        return text;
    }
}