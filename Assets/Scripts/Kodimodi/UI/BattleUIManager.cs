using Lean.Gui;
using UnityEngine;

namespace Kodimodi.UI
{
    public class BattleUIManager : MonoBehaviour
    {
        public LeanWindow statisticPopup;
        public GameObject statisticScrollViewItemPrefab, statisticScrollViewLocalItemPrefab;
        
        public LeanWindow statisticFinishedPopup;
        
        //Вызывается когда матч закончился и игрок нажимает на кнопку чтобы перейти на главную сцену
        public void OnStatisticFinishedHomeBtnClick()
        {
            
        }
        
        //Вызывается когда игрок нажимает на кнопку чтобы покинуть матч
        public void OnStatisticExitBtnClick()
        {
            
        }
    }
}
