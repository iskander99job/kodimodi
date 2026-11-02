using TMPro;
using UnityEngine;

namespace Kodimodi.UI
{
    public class CreateMatch : MonoBehaviour
    {
        public TMP_Text matchInfoTextUI;
        private int playersCount = 2;
        
        void Start()
        {
        
        }

        public void OnCreateMatch()
        {
            if (playersCount >= 2)
            {
                
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
    }
}
