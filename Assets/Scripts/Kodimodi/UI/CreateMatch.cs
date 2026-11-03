using Lean.Gui;
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
        public LeanWindow modal;
        
        // Для проверки в редакторе, например: https://site.com/game/?room=A7K2QX
        [SerializeField] private string editorTestUrl = "";
        // Код комнаты из ссылки, null если игрок пришёл не по ссылке
        public string RoomCode { get; private set; }
        
        void Start()
        {
            RoomCode = RoomLinkChecker.GetRoomCode(editorTestUrl);

            if (RoomCode != null)
            {
                SetMatchLink(RoomCode);
                SwitchModal("search");
                HideButtons();
                modal.TurnOn();
            }
            else
            {
                SwitchModal("create");
            }
        }

// Показывает ссылку на матч. Вызывайте и после ответа сервера на POST /rooms
        public void SetMatchLink(string code)
        {
            matchLinkUI.text = RoomLinkChecker.BuildLink(code, editorTestUrl);
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
            //FindAnyObjectByType<ClipboardManager>().CopyToClipboard(matchLinkUI.text);
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
