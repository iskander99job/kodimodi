using TMPro;
using UnityEngine;

namespace Kodimodi.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public class GameFpsCounter : MonoBehaviour
    {
        [SerializeField, Min(0.1f)]
        private float updateInterval = 0.5f;

        private TMP_Text label;
        private float elapsed;
        private int frames;

        private void Awake()
        {
            label = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            elapsed = 0f;
            frames = 0;

            label.SetText("FPS: ...");
        }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;

            if (delta <= 0f)
                return;

            elapsed += delta;
            frames++;

            if (elapsed < updateInterval)
                return;

            float fps = frames / elapsed;
            label.SetText("FPS: {0:0}", fps);

            elapsed = 0f;
            frames = 0;
        }
    }
}