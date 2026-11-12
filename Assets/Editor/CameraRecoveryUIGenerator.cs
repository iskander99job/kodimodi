using Lean.Gui;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CameraRecoveryUIGenerator
{
    private const string CanvasName = "MainCanvas";
    private const string RootName = "CameraRecoveryUI";

    private static TMP_FontAsset font;

    private static readonly Color White = Color.white;

    private static readonly Color Secondary =
        new Color32(185, 190, 185, 255);

    [MenuItem("Tools/Game UI/Create Camera Recovery")]
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
            "Create Camera Recovery UI");

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
        content.anchoredPosition = new Vector2(0, -40);
        content.sizeDelta = new Vector2(1000, 800);

        CreateMessage(content);
        CreateCountdown(content);
        CreateButtons(content);

        Selection.activeGameObject = root.gameObject;

        EditorSceneManager.MarkSceneDirty(
            root.gameObject.scene);

        Debug.Log(
            "CameraRecoveryUI создан. Назначь иконку и события кнопок. " +
            "Таймер пока демонстрационный.",
            root);
    }

    // ---------- Message ----------

    private static void CreateMessage(Transform parent)
    {
        Image icon = Box(
            "CameraOffIcon", parent,
            468, 24, 64, 64,
            White);

        icon.preserveAspect = true;
        icon.enabled = false;

        Text(
            "Eyebrow", parent,
            "КАМЕРА ОТКЛЮЧЕНА",
            0, 112, 1000, 38,
            23, White,
            spacing: 7);

        Text(
            "Title", parent,
            "ВКЛЮЧИТЕ КАМЕРУ,\nЧТОБЫ ПРОДОЛЖИТЬ",
            0, 164, 1000, 122,
            46, White,
            spacing: 5);

        Text(
            "Description", parent,
            "Без камеры вы будете отключены от матча.",
            20, 294, 960, 52,
            26, Secondary);
    }

    // ---------- Countdown ----------

    private static void CreateCountdown(Transform parent)
    {
        RectTransform countdown = CreateRect("Countdown", parent);
        Place(countdown, 250, 366, 500, 158);

        Text(
            "TimerText", countdown,
            "00:15",
            0, 0, 500, 96,
            80, White,
            spacing: 2);

        Text(
            "Label", countdown,
            "До отключения от матча",
            0, 98, 500, 38,
            23, Secondary);

        Image track = Box(
            "ProgressTrack", countdown,
            40, 146, 420, 4,
            new Color(1, 1, 1, 0.18f));

        Image fill = Box(
            "ProgressFill", track.transform,
            0, 0, 420, 4,
            White);

        // Sprite is required for Image.fillAmount.
        fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
            "UI/Skin/UISprite.psd");

        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
    }

    // ---------- Buttons ----------

    private static void CreateButtons(Transform parent)
    {
        const float width = 540;
        const float height = 80;

        Image backing = Box(
            "EnableCameraButton", parent,
            230, 580, width, height,
            new Color(0, 0, 0, 0.12f));

        backing.raycastTarget = true;

        LeanButton enableButton =
            backing.gameObject.AddComponent<LeanButton>();

        enableButton.targetGraphic = backing;
        enableButton.interactable = true;
        enableButton.transition = Selectable.Transition.None;

        CreateBorder(backing.transform, width, height);

        Text(
            "Text", backing.transform,
            "ВКЛЮЧИТЬ КАМЕРУ",
            16, 0, width - 32, height,
            25, White,
            spacing: 5);

        Image helpBacking = Box(
            "HelpButton", parent,
            260, 680, 480, 52,
            Color.clear);

        helpBacking.raycastTarget = true;

        LeanButton helpButton =
            helpBacking.gameObject.AddComponent<LeanButton>();

        helpButton.targetGraphic = helpBacking;
        helpButton.interactable = true;
        helpButton.transition = Selectable.Transition.None;

        TMP_Text helpText = Text(
            "Text", helpBacking.transform,
            "У меня не получается",
            0, 0, 480, 52,
            25, Secondary);

        helpText.fontStyle = FontStyles.Underline;
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