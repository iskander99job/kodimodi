using TMPro;
using UnityEngine;

namespace Kodimodi.UI
{
    public class CreateMatch : MonoBehaviour
    {
        public TMP_Text matchInfoTextUI;
        private int playersCount = 2;
        public GameObject searchMatchScrollViewItemPrefabUI;
        public GameObject searchMatchScrollViewUI;
        public TMP_InputField matchLinkUI;
        public GameObject cancelMatchBtnUI, startMatchBtnUI;
        public GameObject createMatchModal, searchMatchModal;
        
        void Start()
        {
            SwitchModal("create");
        }

        private void SwitchModal(string name)
        {
            if (name == "create")
            {
                createMatchModal.SetActive(true);
                searchMatchModal.SetActive(false);
            } else if (name == "search")
            {
                createMatchModal.SetActive(false);
                searchMatchModal.SetActive(true);
            }
        }
        
        public void OnCreateMatch()
        {
            if (playersCount >= 2)
            {
                SwitchModal("search");
            }
        }

        public void OnMinus()
        {
            if (playersCount > 2)
            {
                playersCount -= 1;
                UpdateMatchInfoText();
            }
        }

        public void OnPlus()
        {
            if (playersCount < 6)
            {
                playersCount += 1;
                UpdateMatchInfoText();
            }
        }

        private void UpdateMatchInfoText()
        {
            matchInfoTextUI.text = 
                "<b>Режим:</b> каждый сам за себя\n" +
                $"<b>Кол-во игроков:</b> {playersCount}";
        }

        public void OnCopyMatchLink()
        {
            FindAnyObjectByType<ClipboardManager>().CopyToClipboard(matchLinkUI.text);
        }

        public void OnCancelMatchSearch()
        {
            
        }

        public void OnStartMatch()
        {
            
        }

        private void HideButtons()
        {
            startMatchBtnUI.SetActive(false);
            cancelMatchBtnUI.SetActive(false);
        }
    }
}
