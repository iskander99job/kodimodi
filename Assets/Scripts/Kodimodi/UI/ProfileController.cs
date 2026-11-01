using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Kodimodi.UI
{
    public class ProfileController : MonoBehaviour
    {
        public RawImage rawImage;
        public Image rawImageMask;
    
        // Сохраняем ссылку на текстуру, чтобы иметь к ней доступ
        private WebCamTexture webcamTexture;

        void Start()
        {
            StartCoroutine(EnableWebCam());
        }
        
        public void OnPlayButtonClick()
        {
            // Проверяем, идёт ли уже видео
            if (webcamTexture != null && webcamTexture.isPlaying)
            {
                Debug.Log("Видео уже идёт.");
                // Здесь можно, например, ничего не делать или переключить на паузу
                return;
            }
        
            StartCoroutine(EnableWebCam());
        }
    
        IEnumerator EnableWebCam()
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
            
                // Проверяем статус сразу после вызова Play()
                Debug.Log("Камера запущена: " + webcamTexture.isPlaying);
            }
        }
    }
}
