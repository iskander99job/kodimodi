using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class GameHUDGenerator
{
    private const string CanvasName = "MainCanvas";
    private const string RootName = "GameHUD";

    private static TMP_FontAsset font;

    private static readonly Color White = Color.white;

    private static readonly Color Secondary =
        new Color32(205, 205, 205, 255);

    [MenuItem("Tools/Game UI/Create Game HUD")]
    private static void Generate()
    {
        Canvas canvas = FindCanvas();

        if (canvas == null)
            return;

        if (canvas.transform.Find(RootName) != null)
        {
            EditorUtility.DisplayDialog(
                "HUD already exists",
                $"Внутри {CanvasName} уже есть {RootName}.",
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

        RectTransform root = CreateRect(RootName, canvas.transform);
        Stretch(root);

        Undo.RegisterCreatedObjectUndo(
            root.gameObject,
            "Create Game HUD");

        CreateScore(root);
        CreateTopHints(root);
        CreateHealth(root);
        CreateAmmo(root);

        Selection.activeGameObject = root.gameObject;

        EditorSceneManager.MarkSceneDirty(
            root.gameObject.scene);

        Debug.Log(
            "GameHUD создан. Назначь спрайты в объектах Icon " +
            "и включи их компоненты Image.",
            root);
    }

    // ---------- Верх: счёт ----------

    private static void CreateScore(Transform parent)
    {
        RectTransform score = CreateRect("Score", parent);

        Anchor(
            score,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0, -28),
            new Vector2(340, 92));

        Text(
            "Label",
            score,
            "ДО ПОБЕДЫ",
            0, 0, 340, 28,
            18,
            Secondary,
            TextAlignmentOptions.Center,
            spacing: 7);

        Text(
            "Value",
            score,
            "3 <color=#CDCDCD>/ 10</color>",
            0, 30, 340, 54,
            42,
            White,
            TextAlignmentOptions.Center);
    }

    // ---------- Верх справа: TAB / ESC ----------

    private static void CreateTopHints(Transform parent)
    {
        RectTransform hints = CreateRect("TopHints", parent);

        Anchor(
            hints,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-58, -38),
            new Vector2(252, 40));

        RectTransform scoreboard = CreateRect(
            "ScoreboardHint", hints);

        Place(scoreboard, 0, 0, 112, 40);

        Icon(
            "Icon",
            scoreboard,
            0, 6, 28, 28);

        Text(
            "Key",
            scoreboard,
            "TAB",
            44, 0, 68, 40,
            20,
            White,
            TextAlignmentOptions.MidlineLeft,
            spacing: 3);

        RectTransform exit = CreateRect(
            "MenuHint", hints);

        Place(exit, 140, 0, 112, 40);

        Icon(
            "Icon",
            exit,
            0, 6, 28, 28);

        Text(
            "Key",
            exit,
            "ESC",
            44, 0, 68, 40,
            20,
            White,
            TextAlignmentOptions.MidlineLeft,
            spacing: 3);
    }

    // ---------- Низ слева: здоровье ----------

    private static void CreateHealth(Transform parent)
    {
        RectTransform health = CreateRect("Health", parent);

        Anchor(
            health,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(68, 56),
            new Vector2(420, 112));

        Icon(
            "Icon",
            health,
            0, 8, 22, 22);

        Text(
            "Label",
            health,
            "ЗДОРОВЬЕ",
            42, 0, 260, 38,
            18,
            Secondary,
            TextAlignmentOptions.MidlineLeft,
            spacing: 5);

        Text(
            "Current",
            health,
            "85",
            0, 36, 86, 68,
            52,
            White,
            TextAlignmentOptions.MidlineLeft);

        Text(
            "Maximum",
            health,
            "/ 100",
            88, 48, 88, 48,
            25,
            Secondary,
            TextAlignmentOptions.MidlineLeft);

        Image track = Box(
            "BarBackground",
            health,
            190, 74, 200, 4,
            new Color(1, 1, 1, 0.3f));

        Image fill = Box(
            "BarFill",
            track.transform,
            0, 0, 200, 4,
            White);

        // Белый спрайт нужен для работы Image.fillAmount.
        fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
            "UI/Skin/Background.psd");

        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0.85f;
    }

    // ---------- Низ справа: оружие и патроны ----------

    private static void CreateAmmo(Transform parent)
    {
        RectTransform ammo = CreateRect("Ammo", parent);

        Anchor(
            ammo,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-68, 56),
            new Vector2(230, 140));

        Icon(
            "WeaponIcon",
            ammo,
            0, 4, 52, 30);

        Text(
            "WeaponName",
            ammo,
            "USP",
            76, 0, 154, 38,
            19,
            White,
            TextAlignmentOptions.MidlineLeft,
            spacing: 4);

        Text(
            "Current",
            ammo,
            "12",
            0, 36, 86, 66,
            52,
            White,
            TextAlignmentOptions.MidlineLeft);

        Text(
            "Reserve",
            ammo,
            "/ 200",
            92, 49, 130, 48,
            28,
            Secondary,
            TextAlignmentOptions.MidlineLeft);

        Text(
            "ReloadHint",
            ammo,
            "R  ПЕРЕЗАРЯДКА",
            0, 108, 230, 28,
            15,
            White,
            TextAlignmentOptions.MidlineLeft,
            spacing: 4);
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

    private static void Anchor(
        RectTransform rect,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
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

    private static void Icon(
        string name,
        Transform parent,
        float x,
        float y,
        float width,
        float height)
    {
        Image image = Box(
            name,
            parent,
            x, y, width, height,
            White);

        image.preserveAspect = true;

        // Включишь после назначения спрайта.
        image.enabled = false;
    }

    private static TMP_Text Text(
        string name,
        Transform parent,
        string value,
        float x,
        float y,
        float width,
        float height,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment,
        float spacing = 0)
    {
        RectTransform rect = CreateRect(name, parent);

        Place(rect, x, y, width, height);

        TextMeshProUGUI text =
            rect.gameObject.AddComponent<TextMeshProUGUI>();

        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.characterSpacing = spacing;
        text.raycastTarget = false;

        return text;
    }
}