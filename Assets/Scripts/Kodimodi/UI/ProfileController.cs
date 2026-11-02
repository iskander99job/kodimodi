using System.Collections;
using Lean.Gui;
using UnityEngine;
using UnityEngine.UI;

namespace Kodimodi.UI
{
    public class ProfileController : MonoBehaviour
    {
        public RawImage rawImage;
        public Image rawImageMask;
        public LeanWindow webcamWarningModal;
        public LeanWindow createMatchModal;
        
        // Сохраняем ссылку на текстуру, чтобы иметь к ней доступ
        private WebCamTexture webcamTexture;

        void Start()
        {
            //StartCoroutine(EnableWebCam(true));
        }
        
        public void OnPlayButtonClick()
        {
            createMatchModal.TurnOn();
            // Проверяем, идёт ли уже видео
            // if (webcamTexture != null && webcamTexture.isPlaying)
            // {
            //     return;
            // }
            //
            // webcamWarningModal.TurnOn();
            // StartCoroutine(EnableWebCam(false));
        }
    
        IEnumerator EnableWebCam(bool start)
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (Application.HasUserAuthorization(UserAuthorization.WebCam)) {
                if (!rawImage.enabled)
                {
                    rawImage.enabled = true;
                }
                rawImage.color = Color.white;
                rawImageMask.color = Color.white;
            
                // Если текстура ещё не создана, создаём её
                if (webcamTexture == null)
                {
                    webcamTexture = new WebCamTexture();
                    rawImage.texture = webcamTexture;
                }
            
                // Запускаем, если ещё не запущена
                if (!webcamTexture.isPlaying)
                {
                    webcamTexture.Play();
                }
            }
        }
    }
}
