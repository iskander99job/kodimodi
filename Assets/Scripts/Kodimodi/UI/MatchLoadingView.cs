using TMPro;
using UnityEngine;

namespace Kodimodi.UI
{
    public sealed class MatchLoadingView : MonoBehaviour
    {
        private TMP_Text statusText;

        public void Show(string status)
        {
            if (statusText == null)
            {
                var child = transform.Find("Content/StatusText");

                if (child != null)
                    statusText = child.GetComponent<TMP_Text>();
            }

            if (statusText != null && statusText.text != status)
                statusText.text = status;

            SetVisible(true);
        }

        public void SetVisible(bool visible)
        {
            var group = GetComponent<CanvasGroup>();

            if (group != null)
            {
                group.alpha = visible ? 1f : 0f;
                group.interactable = visible;
                group.blocksRaycasts = visible;
            }

            gameObject.SetActive(visible);
        }
    }
}