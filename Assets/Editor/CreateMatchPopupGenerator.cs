using Lean.Gui;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CreateMatchPopupGenerator
{
    private const string CanvasName = "MainCanvas";
    private const string PopupName = "CreateMatchPopup";

    private static TMP_FontAsset font;
    private static Sprite rounded;

    private static readonly Color Black = Color.black;

    private static readonly Color Gray =
        new Color32(105, 105, 105, 255);

    private static readonly Color Line =
        new Color32(205, 205, 205, 255);

    [MenuItem("Tools/Game UI/Create Match Popup")]
    private static void Generate()
    {
        Canvas canvas = FindCanvas();

        if (canvas == null)
            return;

        if (canvas.transform.Find(PopupName) != null)
        {
            EditorUtility.DisplayDialog(
                "Popup already exists",
                $"Внутри {CanvasName} уже есть {PopupName}.",
                "OK");
            return;
        }

        font = TMP_Settings.defaultFontAsset;

        if (font == null)
        {
            EditorUtility.DisplayDialog(
                "TextMeshPro",
                "Сначала импортируй TMP Essential Resources:\n" +
                "Window > TextMeshPro > Import TMP Essential Resources",
                "OK");
            return;
        }

        rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>(
            "UI/Skin/UISprite.psd");

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Match Popup");

        RectTransform root = CreateRect(PopupName, canvas.transform);
        Stretch(root);

        Undo.RegisterCreatedObjectUndo(
            root.gameObject,
            "Create Match Popup");

        // Root components.
        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;

        LeanWindow window = root.gameObject.AddComponent<LeanWindow>();

        // Configure the serialized fields if present in this Lean GUI version.
        // On = true, Turn Off Siblings = false.
        ConfigureWindow(window);

        // Background.
        Image background = CreateImage(
            "Background (LeanButton)",
            root,
            0, 0, 0, 0,
            new Color(0, 0, 0, 0.45f));

        Stretch(background.rectTransform);

        // Left = -5, Top = -5, Right = -25, Bottom = -25.
        background.rectTransform.offsetMin = new Vector2(-5, -25);
        background.rectTransform.offsetMax = new Vector2(25, 5);

        LeanButton backgroundButton =
            background.gameObject.AddComponent<LeanButton>();

        ConfigureButton(backgroundButton, background);

// Panel: 760 × 460, scale = 1.
Image panel = CreateImage(
    "Panel", root,
    0, 0, 760, 460,
    Color.white, true);

RectTransform panelRect = panel.rectTransform;
panelRect.anchorMin = panelRect.anchorMax =
    new Vector2(0.5f, 0.5f);
panelRect.pivot = new Vector2(0.5f, 0.5f);
panelRect.anchoredPosition = Vector2.zero;
panelRect.localScale = Vector3.one;

// Header.
CreateText(
    "Title", panel.transform,
    "Create match",
    32, 22, 610, 48,
    34, Black,
    bold: true);

LeanButton close = CreateButton(
    "CloseButton", panel.transform,
    "×",
    684, 22, 44, 44,
    Color.clear, Black,
    34);

Image headerDivider = CreateImage(
    "Divider", panel.transform,
    32, 84, 696, 1,
    Line);

headerDivider.raycastTarget = false;

// Game mode.
RectTransform mode = CreateRect(
    "ModeSection", panel.transform);

Place(mode, 32, 102, 696, 150);

CreateText(
    "Label", mode,
    "GAME MODE",
    0, 0, 696, 28,
    19, Gray,
    bold: true);

Image selectedMode = CreateImage(
    "SelectedMode", mode,
    0, 38, 696, 64,
    Black, true);

CreateText(
    "Name", selectedMode.transform,
    "Free-for-all",
    20, 0, 590, 64,
    28, Color.white,
    bold: true);

CreateCheckmark(selectedMode.transform);

CreateText(
    "Description", mode,
    "Every player for themselves.",
    0, 112, 696, 32,
    21, Gray);

Image modeDivider = CreateImage(
    "Divider", panel.transform,
    32, 264, 696, 1,
    Line);

modeDivider.raycastTarget = false;

// Players.
RectTransform players = CreateRect(
    "PlayersRow", panel.transform);

Place(players, 32, 282, 696, 72);

CreateText(
    "Title", players,
    "Players",
    0, 0, 420, 34,
    26, Black,
    bold: true);

CreateText(
    "Description", players,
    "Total players in the match",
    0, 36, 420, 30,
    20, Gray);

// Stepper.
Image stepper = CreateImage(
    "Stepper", players,
    468, 4, 228, 56,
    Color.white, true);

AddOutline(stepper.gameObject, Line);

CreateButton(
    "MinusButton", stepper.transform,
    "−",
    0, 0, 75, 56,
    Color.clear, Black,
    28);

CreateText(
    "CountText", stepper.transform,
    "2",
    76, 0, 76, 56,
    28, Black,
    bold: true,
    centered: true);

CreateButton(
    "PlusButton", stepper.transform,
    "+",
    153, 0, 75, 56,
    Color.clear, Black,
    28);

Image leftSeparator = CreateImage(
    "SeparatorLeft", stepper.transform,
    75, 0, 1, 56,
    Line);

leftSeparator.raycastTarget = false;

Image rightSeparator = CreateImage(
    "SeparatorRight", stepper.transform,
    152, 0, 1, 56,
    Line);

rightSeparator.raycastTarget = false;

// Footer.
RectTransform footer = CreateRect(
    "Footer", panel.transform);

Place(footer, 32, 372, 696, 60);

LeanButton cancel = CreateButton(
    "CancelButton", footer,
    "Cancel",
    0, 0, 240, 60,
    Color.white, Black,
    26, true);

AddOutline(cancel.gameObject, Black);

CreateButton(
    "CreateButton", footer,
    "Create match →",
    260, 0, 436, 60,
    Black, Color.white,
    26, true);

        // Connect closing if this Lean GUI version exposes
        // OnClick and TurnOff() through its public API.
        ConnectClose(backgroundButton, window);
        ConnectClose(close, window);
        ConnectClose(cancel, window);

        Selection.activeGameObject = root.gameObject;

        EditorUtility.SetDirty(window);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log(
            $"Создан {CanvasName}/{PopupName}.\n" +
            "Кнопки Minus, Plus и Create нужно подключить к игровой логике.\n" +
            "Переходы LeanWindow настраиваются отдельно.",
            root);
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
                    $"Найдено несколько Canvas с именем {CanvasName}.\n" +
                    "Используй уникальное имя.",
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

    private static void ConfigureWindow(LeanWindow window)
    {
        var serialized = new SerializedObject(window);

        SetBool(serialized, "on", true);
        SetBool(serialized, "turnOffSiblings", false);

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(
        SerializedObject target,
        string propertyName,
        bool value)
    {
        SerializedProperty property =
            target.FindProperty(propertyName);

        if (property != null &&
            property.propertyType == SerializedPropertyType.Boolean)
        {
            property.boolValue = value;
        }
        else
        {
            Debug.LogWarning(
                $"LeanWindow: поле '{propertyName}' не найдено. " +
                "Проверь его значение в Inspector.",
                target.targetObject);
        }
    }

    private static void ConnectClose(
        LeanButton button,
        LeanWindow window)
    {
        // Reflection keeps the generator independent of minor API changes.
        var flags =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public;

        object clickEvent = null;

        var property = button.GetType().GetProperty("OnClick", flags);

        if (property != null)
            clickEvent = property.GetValue(button, null);

        if (clickEvent == null)
        {
            var field = button.GetType().GetField("OnClick", flags);

            if (field != null)
                clickEvent = field.GetValue(button);
        }

        var method = window.GetType().GetMethod(
            "TurnOff",
            flags,
            null,
            System.Type.EmptyTypes,
            null);

        if (clickEvent is UnityEngine.Events.UnityEvent unityEvent &&
            method != null)
        {
            var action = (UnityEngine.Events.UnityAction)
                System.Delegate.CreateDelegate(
                    typeof(UnityEngine.Events.UnityAction),
                    window,
                    method);

            UnityEventTools.AddPersistentListener(unityEvent, action);
            EditorUtility.SetDirty(button);
        }
        else
        {
            Debug.LogWarning(
                $"Подключи {button.name}: OnClick → LeanWindow.TurnOff " +
                "вручную в Inspector.",
                button);
        }
    }

    private static RectTransform CreateRect(
        string name,
        Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));

        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        return go.GetComponent<RectTransform>();
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

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        float x, float y,
        float width, float height,
        Color color,
        bool useRounded = false)
    {
        RectTransform rect = CreateRect(name, parent);
        Place(rect, x, y, width, height);

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;

        if (useRounded && rounded != null)
        {
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
        }

        return image;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string value,
        float x, float y,
        float width, float height,
        float size,
        Color color,
        bool bold = false,
        bool centered = false)
    {
        RectTransform rect = CreateRect(name, parent);
        Place(rect, x, y, width, height);

        TextMeshProUGUI text =
            rect.gameObject.AddComponent<TextMeshProUGUI>();

        text.font = font;
        text.text = value;
        text.fontSize = size;
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
        string value,
        float x, float y,
        float width, float height,
        Color background,
        Color foreground,
        float fontSize,
        bool useRounded = false)
    {
        Image image = CreateImage(
            name,
            parent,
            x, y, width, height,
            background,
            useRounded);

        LeanButton button =
            image.gameObject.AddComponent<LeanButton>();

        ConfigureButton(button, image);

        CreateText(
            "Text",
            image.transform,
            value,
            0, 0, width, height,
            fontSize,
            foreground,
            bold: true,
            centered: true);

        return button;
    }

    private static void ConfigureButton(
        LeanButton button,
        Graphic target)
    {
        button.targetGraphic = target;
        button.interactable = true;
        button.transition = Selectable.Transition.None;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.Automatic;
        button.navigation = navigation;
    }

    private static void CreateDivider(
        Transform parent,
        float y)
    {
        Image divider = CreateImage(
            "Divider",
            parent,
            48, y, 944, 1,
            Line);

        divider.raycastTarget = false;
    }

    private static void AddOutline(
        GameObject target,
        Color color)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(1, -1);
        outline.useGraphicAlpha = true;
    }

    private static void CreateCheckmark(Transform parent)
    {
        RectTransform check = CreateRect("CheckIcon", parent);

        check.anchorMin = check.anchorMax = new Vector2(1f, 0.5f);
        check.pivot = new Vector2(0.5f, 0.5f);
        check.sizeDelta = new Vector2(40, 40);
        check.anchoredPosition = new Vector2(-44, 0);

        CreateStroke(
            check,
            new Vector2(-8, -3),
            new Vector2(16, 5),
            -45);

        CreateStroke(
            check,
            new Vector2(5, 2),
            new Vector2(29, 5),
            45);
    }

    private static void CreateStroke(
        Transform parent,
        Vector2 position,
        Vector2 size,
        float angle)
    {
        Image image = CreateImage(
            "Stroke",
            parent,
            0, 0, size.x, size.y,
            Color.white);

        RectTransform rect = image.rectTransform;

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.Euler(0, 0, angle);

        image.raycastTarget = false;
    }
}