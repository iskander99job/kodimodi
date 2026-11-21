using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class WeaponShopUiGenerator
{
    private const string CanvasName = "MainCanvas";
    private const string RootName = "WeaponShopContent";
    private const float HeaderSpace = 0.10f;

    private static readonly Color Ink = new Color32(16, 16, 16, 255);
    private static readonly Color Muted = new Color32(88, 88, 92, 255);
    private static readonly Color Line = new Color32(204, 206, 212, 255);
    private static readonly Color Track = new Color32(238, 238, 240, 255);
    private static readonly Color Fill = new Color32(62, 62, 64, 255);
    private static readonly Color Yellow = new Color32(255, 198, 0, 255);

    private static readonly string[] Categories =
    {
        "AR", "SMG", "PST", "SG", "MG", "SR", "GL"
    };

    [MenuItem("Tools/Weapon Shop/Generate Available")]
    private static void GenerateAvailable()
    {
        Generate(false);
    }

    [MenuItem("Tools/Weapon Shop/Generate Rank Locked")]
    private static void GenerateRankLocked()
    {
        Generate(true);
    }

    private static void Generate(bool rankLocked)
    {
        RectTransform canvas = FindMainCanvas();
        if (canvas == null)
            return;

        if (canvas.Find(RootName) != null)
        {
            EditorUtility.DisplayDialog(
                "Weapon Shop",
                "В MainCanvas уже есть WeaponShopContent. Переименуй его, "
                + "если хочешь сгенерировать второй вариант рядом.",
                "OK");
            return;
        }

#if UNITY_2022_2_OR_NEWER
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif

        RectTransform root = Rect(canvas, RootName, 0, HeaderSpace, 1, 1);
        root.SetAsFirstSibling();
        Paint(root, Color.white);

        BuildTabs(root, font);
        BuildPreview(root, font);

        Paint(Rect(root, "Vertical Divider", .619f, .125f, .620f, .97f), Line);

        RectTransform details = Rect(root, "Details", .641f, .13f, .985f, .97f);
        BuildDetails(details, font, rankLocked);

        Undo.RegisterCreatedObjectUndo(root.gameObject, "Generate Weapon Shop UI");
        Selection.activeGameObject = root.gameObject;
    }

    private static RectTransform FindMainCanvas()
    {
        Scene scene = SceneManager.GetActiveScene();
        RectTransform result = null;

        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
        {
            foreach (Canvas canvas in sceneRoot.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.name != CanvasName)
                    continue;

                if (result != null)
                {
                    EditorUtility.DisplayDialog(
                        "Weapon Shop",
                        "В активной сцене найдено несколько объектов MainCanvas.",
                        "OK");
                    return null;
                }

                result = canvas.transform as RectTransform;
            }
        }

        if (result == null)
        {
            EditorUtility.DisplayDialog(
                "Weapon Shop",
                "В активной сцене не найден MainCanvas с компонентом Canvas.",
                "OK");
        }

        return result;
    }

    private static void BuildTabs(RectTransform root, Font font)
    {
        RectTransform tabs = Rect(root, "Categories", .018f, .025f, .985f, .105f);

        for (int i = 0; i < Categories.Length; i++)
        {
            float left = i / (float)Categories.Length;
            float right = (i + 1) / (float)Categories.Length;
            bool active = i == 0;

            RectTransform tab = Rect(tabs, Categories[i], left, 0, right, 1);
            MakeButton(tab, Color.white);

            Label(tab, "Label", Categories[i], font, 29,
                0, 0, 1, .85f, TextAnchor.MiddleCenter,
                active ? Ink : Muted, true);

            if (i > 0)
                Paint(Rect(tab, "Separator", 0, .07f, .006f, .76f), Line);
        }

        Paint(Rect(tabs, "Bottom Rule", 0, .96f, 1, 1), Line);

        for (int i = 0; i < Categories.Length; i++)
        {
            float left = i / (float)Categories.Length;
            float right = (i + 1) / (float)Categories.Length;
            RectTransform underline = Rect(
                tabs, Categories[i] + " Underline", left, .87f, right, 1);

            Paint(underline, Yellow);
            underline.gameObject.SetActive(i == 0);
        }
    }

    private static void BuildPreview(RectTransform root, Font font)
    {
        RectTransform stage = Rect(root, "Preview", .018f, .125f, .609f, .97f);
        RectTransform surface = Rect(stage, "Preview Render Texture", 0, 0, 1, 1);

        RawImage preview = surface.gameObject.AddComponent<RawImage>();
        preview.raycastTarget = false;
        preview.enabled = false;

        BuildArrow(stage, font, "Previous", "\u2039", .007f, .068f);
        BuildArrow(stage, font, "Next", "\u203A", .935f, .996f);

        RectTransform counter = Rect(stage, "Page Counter", .421f, .81f, .579f, .892f);
        Paint(counter, Track);
        Label(counter, "Position", "3 / 10", font, 27,
            0, 0, 1, 1, TextAnchor.MiddleCenter, Ink, true);
    }

    private static void BuildArrow(
        RectTransform stage, Font font, string name, string glyph,
        float left, float right)
    {
        RectTransform arrow = Rect(stage, name, left, .42f, right, .525f);
        MakeButton(arrow, Color.white);

        Outline outline = arrow.gameObject.AddComponent<Outline>();
        outline.effectColor = Line;
        outline.effectDistance = new Vector2(1, -1);

        Label(arrow, "Arrow", glyph, font, 40,
            0, 0, 1, 1, TextAnchor.MiddleCenter, Ink, true);
    }

    private static void BuildDetails(RectTransform panel, Font font, bool rankLocked)
    {
        Label(panel, "Weapon Name", "M4A1", font, 47,
            0, 0, 1, .115f, TextAnchor.MiddleLeft, Ink, true);

        Rule(panel, .137f);

        Label(panel, "Stats Heading", "STATS", font, 28,
            0, .156f, 1, .217f, TextAnchor.MiddleLeft, Ink, true);

        Stat(panel, font, "Damage", 32, .24f);
        Stat(panel, font, "Accuracy", 78, .312f);
        Stat(panel, font, "Fire rate", 82, .384f);
        Stat(panel, font, "Recoil control", 70, .456f);

        float magazineTop = rankLocked ? .55f : .66f;
        Rule(panel, magazineTop);

        Label(panel, "Magazine Label", "Magazine", font, 23,
            0, magazineTop + .015f, .55f, magazineTop + .085f,
            TextAnchor.MiddleLeft, Ink);

        Label(panel, "Magazine Value", "30 rounds", font, 23,
            .55f, magazineTop + .015f, 1, magazineTop + .085f,
            TextAnchor.MiddleRight, Ink);

        Rule(panel, magazineTop + .10f);

        RectTransform rank = Rect(panel, "Rank Requirement", 0, .665f, 1, .825f);
        BuildRank(rank, font);
        rank.gameObject.SetActive(rankLocked);

        Label(panel, "Price", "$ 1 800", font, 39,
            0, .845f, .53f, .981f, TextAnchor.MiddleLeft, Ink, true);

        RectTransform buyRect = Rect(panel, "Buy", .55f, .845f, 1, .981f);
        Button buy = MakeButton(buyRect, rankLocked ? Track : Ink);
        buy.interactable = !rankLocked;

        Label(buyRect, "Label", rankLocked ? "LOCKED" : "BUY", font, 32,
            0, 0, 1, 1, TextAnchor.MiddleCenter,
            rankLocked ? Muted : Color.white, true);
    }

    private static void BuildRank(RectTransform row, Font font)
    {
        RectTransform badge = Rect(row, "Rank Badge", .012f, .2f, .092f, .8f);

        Image customIcon = badge.gameObject.AddComponent<Image>();
        customIcon.raycastTarget = false;
        customIcon.preserveAspect = true;
        customIcon.enabled = false;

        RectTransform bars = Rect(badge, "Silver Bars", 0, 0, 1, 1);
        Color silver = new Color32(173, 178, 186, 255);

        Paint(Rect(bars, "Left Bar", .15f, .14f, .36f, .86f), silver);
        Paint(Rect(bars, "Right Bar", .64f, .14f, .85f, .86f), silver);
        Paint(Rect(bars, "Upper Bridge", .36f, .25f, .64f, .33f), silver);
        Paint(Rect(bars, "Lower Bridge", .36f, .67f, .64f, .75f), silver);

        Label(row, "Required Rank", "Required rank: Captain", font, 23,
            .12f, .06f, 1, .53f, TextAnchor.MiddleLeft, Ink, true);

        Label(row, "Player Rank", "Your rank: Private", font, 21,
            .12f, .51f, 1, .93f, TextAnchor.MiddleLeft, Muted);
    }

    private static void Stat(
        RectTransform panel, Font font, string name, int value, float top)
    {
        RectTransform row = Rect(panel, name, 0, top, 1, top + .066f);

        Label(row, "Label", name, font, 23,
            0, 0, .30f, 1, TextAnchor.MiddleLeft, Ink);
        Label(row, "Value", value.ToString(), font, 23,
            .91f, 0, 1, 1, TextAnchor.MiddleRight, Ink);

        RectTransform meter = Rect(row, "Segmented Meter", .30f, .23f, .90f, .77f);
        Paint(meter, Muted);

        const int segments = 10;
        for (int i = 0; i < segments; i++)
        {
            float start = i / (float)segments;
            float end = (i + 1) / (float)segments;
            RectTransform cell = Rect(
                meter, $"Cell {i + 1}", start + .002f, .075f, end - .002f, .925f);

            Paint(cell, Track);

            float fraction = Mathf.Clamp01(value * segments / 100f - i);
            RectTransform amount = Rect(cell, "Fill", 0, 0, fraction, 1);
            Paint(amount, Fill);
            amount.gameObject.SetActive(fraction > 0);
        }
    }

    private static void Rule(RectTransform parent, float top)
    {
        Paint(Rect(parent, "Divider", 0, top, 1, top + .0025f), Line);
    }

    private static Button MakeButton(RectTransform rect, Color face)
    {
        Image image = Paint(rect, face);
        image.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static Image Paint(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Label(
        RectTransform parent, string name, string value, Font font, int size,
        float left, float top, float right, float bottom,
        TextAnchor alignment, Color color, bool bold = false)
    {
        RectTransform rect = Rect(parent, name, left, top, right, bottom);
        Text text = rect.gameObject.AddComponent<Text>();

        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 10;
        text.resizeTextMaxSize = size;
    }

    // Нормализованные координаты от левого верхнего угла родителя.
    private static RectTransform Rect(
        RectTransform parent, string name,
        float left, float top, float right, float bottom)
    {
        RectTransform rect = (RectTransform)new GameObject(
            name, typeof(RectTransform)).transform;

        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(left, 1f - bottom);
        rect.anchorMax = new Vector2(right, 1f - top);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }
}