using TMPro;
using UnityEngine;

namespace Kodimodi.UI
{
    public sealed class MatchErrorView : MonoBehaviour
    {
        private TMP_Text title;
        private TMP_Text description;

        public void Show(string message)
        {
            Debug.Log(
                $"[MatchErrorView] Show: {message}; " +
                $"parentActive={transform.parent == null || transform.parent.gameObject.activeInHierarchy}");
            if (title == null)
                title = FindText("Content/Title");

            if (description == null)
                description = FindText("Content/Description");

            if (title != null)
                title.text = "Матч прерван";

            if (description != null)
                description.text = message;

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

        private TMP_Text FindText(string path)
        {
            var child = transform.Find(path);

            return child != null
                ? child.GetComponent<TMP_Text>()
                : null;
        }
    }
}