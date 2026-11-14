using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class ToggleLabelLink : MonoBehaviour
{
    private TMP_Text label;

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        Canvas canvas = label.canvas;

        if (canvas == null)
            return;

        Camera eventCamera =
            canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(
            label,
            Input.mousePosition,
            eventCamera
        );

        if (linkIndex < 0)
            return;

        string url = label.textInfo.linkInfo[linkIndex].GetLinkID();

        if (!string.IsNullOrEmpty(url))
            Application.OpenURL(url);
    }
}