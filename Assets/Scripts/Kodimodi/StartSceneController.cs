using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kodimodi
{
    public class StartSceneController : MonoBehaviour
    {
        public Button findMatchButton;
        public TMP_Text findMatchText;

        void Start()
        {

        }

        public void FindMatch()
        {
            findMatchButton.interactable = false;
            findMatchText.gameObject.SetActive(true);
        }
    }
}