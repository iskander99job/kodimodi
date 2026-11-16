using UnityEngine;

namespace Kodimodi.UI.Profile
{
    public class CoinShop : MonoBehaviour
    {
        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}