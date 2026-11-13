using System;
using TMPro;
using UnityEngine;

namespace Kodimodi.UI
{
    public sealed class MatchCountdownView : MonoBehaviour
    {
        private TMP_Text timerText;
        private double deadline;

        public void Show(double localDeadline)
        {
            if (timerText == null)
            {
                var timer = transform.Find("Content/TimerText");

                if (timer != null)
                    timerText = timer.GetComponent<TMP_Text>();
            }

            deadline = localDeadline;

            SetVisible(true);
            transform.SetAsLastSibling();

            UpdateTimer();
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

        private void Update()
        {
            UpdateTimer();
        }

        private void UpdateTimer()
        {
            if (timerText == null)
                return;

            double remaining = Math.Max(
                0,
                deadline - Time.realtimeSinceStartupAsDouble);

            string value = remaining > 0
                ? ((int)Math.Ceiling(remaining)).ToString()
                : "Старт…";

            if (timerText.text != value)
                timerText.text = value;
        }
    }
}