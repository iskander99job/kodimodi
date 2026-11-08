using System.Collections;
using TMPro;
using UnityEngine;

namespace Kodimodi.UI
{
    public class MatchNotifications : MonoBehaviour
    {
        [SerializeField] private Transform container;
        [SerializeField] private GameObject notificationPrefab;
        [SerializeField] private float lifetime = 4f;
        [SerializeField] private int maxVisible = 5;

        public void Show(string message)
        {
            // Ограничиваем количество видимых сообщений.
            while (container.childCount >= Mathf.Max(1, maxVisible))
            {
                Transform oldest = container.GetChild(0);
                oldest.SetParent(null);
                Destroy(oldest.gameObject);
            }

            GameObject item = Instantiate(notificationPrefab, container);
            TMP_Text label = item.GetComponentInChildren<TMP_Text>();
            label.richText = false;
            label.text = message;

            StartCoroutine(RemoveAfterDelay(item));
        }

        private IEnumerator RemoveAfterDelay(GameObject item)
        {
            yield return new WaitForSecondsRealtime(lifetime);

            if (item != null)
                Destroy(item);
        }
    }
}