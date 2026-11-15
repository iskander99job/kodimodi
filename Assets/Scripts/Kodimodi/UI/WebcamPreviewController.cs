using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kodimodi.UI
{
    public class WebcamPreviewController : MonoBehaviour
    {
        [Header("Видео головы")]
        [SerializeField] private RawImage rawImage;
        [SerializeField] private Image rawImageMask;
        [SerializeField] private bool mirrorPreview = true;

        [Header("Параметры камеры")]
        [SerializeField] private int requestedWidth = 640;
        [SerializeField] private int requestedHeight = 480;
        [SerializeField] private int requestedFPS = 20;
        [SerializeField] private float startupTimeout = 15f;

        private WebCamTexture webcamTexture;
        private Quaternion initialRotation;

        public bool HasFrame { get; private set; }
        public string LastError { get; private set; }

        public bool IsReady =>
            HasFrame &&
            webcamTexture != null &&
            webcamTexture.isPlaying;

        private void Awake()
        {
            if (rawImage == null)
            {
                Debug.LogError(
                    "Назначь RawImage головы в WebcamPreviewController.",
                    this);

                enabled = false;
                return;
            }

            initialRotation = rawImage.rectTransform.localRotation;
            HideVideo();
        }

        // Разрешение должно быть получено до вызова этого метода.
        public IEnumerator PlayRoutine()
        {
            LastError = null;

            if (IsReady)
                yield break;

            StopPreview();

            if (!isActiveAndEnabled || rawImage == null)
            {
                LastError = "Camera preview is unavailable.";
                yield break;
            }

            if (!TryStartCamera())
                yield break;

            float deadline =
                Time.realtimeSinceStartup + startupTimeout;

            while (webcamTexture != null &&
                   (!webcamTexture.didUpdateThisFrame ||
                    webcamTexture.width <= 16 ||
                    webcamTexture.height <= 16))
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    LastError =
                        "The camera did not start streaming video.\n\n" +
                        "Check your device and close other " +
                        "applications that use the camera.";

                    StopPreview();
                    yield break;
                }

                yield return null;
            }

            if (webcamTexture == null || !webcamTexture.isPlaying)
            {
                LastError = "Camera startup was interrupted.";
                StopPreview();
                yield break;
            }

            rawImage.texture = webcamTexture;
            rawImage.color = Color.white;

            if (rawImageMask != null)
                rawImageMask.color = Color.white;

            UpdateVideoLayout();

            rawImage.enabled = true;
            HasFrame = true;
        }

        private bool TryStartCamera()
        {
            try
            {
                webcamTexture = new WebCamTexture(
                    requestedWidth,
                    requestedHeight,
                    requestedFPS);

                webcamTexture.Play();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);

                LastError =
                    "Could not start the camera.\n\n" +
                    "Check your device and browser permission.";

                StopPreview();
                return false;
            }
        }

        private void Update()
        {
            if (IsReady && webcamTexture.didUpdateThisFrame)
                UpdateVideoLayout();
        }

        private void UpdateVideoLayout()
        {
            float width = webcamTexture.width;
            float height = webcamTexture.height;

            if (width <= 16 || height <= 16)
                return;

            rawImage.rectTransform.localRotation =
                initialRotation *
                Quaternion.Euler(
                    0f, 0f, -webcamTexture.videoRotationAngle);

            float cropWidth = Mathf.Min(1f, height / width);
            float cropHeight = Mathf.Min(1f, width / height);

            float x = (1f - cropWidth) * 0.5f;
            float y = (1f - cropHeight) * 0.5f;

            if (mirrorPreview)
            {
                x += cropWidth;
                cropWidth = -cropWidth;
            }

            if (webcamTexture.videoVerticallyMirrored)
            {
                y += cropHeight;
                cropHeight = -cropHeight;
            }

            rawImage.uvRect =
                new Rect(x, y, cropWidth, cropHeight);
        }

        public void StopPreview()
        {
            HasFrame = false;
            HideVideo();

            if (webcamTexture == null)
                return;

            webcamTexture.Stop();
            Destroy(webcamTexture);
            webcamTexture = null;
        }

        private void HideVideo()
        {
            if (rawImage != null)
            {
                rawImage.enabled = false;
                rawImage.texture = null;
                rawImage.uvRect = new Rect(0, 0, 1, 1);
                rawImage.rectTransform.localRotation = initialRotation;
            }

            if (rawImageMask != null)
                rawImageMask.color = Color.black;
        }

        private void OnDisable()
        {
            StopPreview();
        }
    }
}