using Lean.Gui;
using UnityEngine;

namespace Kodimodi.UI.Profile
{
    [RequireComponent(typeof(LeanWindow))]
    public class SignIn : MonoBehaviour
    {
        private LeanWindow window;
        private SignUp signUp;

        private void Awake()
        {
            window = GetComponent<LeanWindow>();
            signUp = FindAnyObjectByType<SignUp>(
                FindObjectsInactive.Include);
        }

        public void Open()
        {
            transform.SetAsLastSibling();
            window.TurnOn();
        }

        public void Close()
        {
            window.TurnOff();
        }

        public void OpenSignUp()
        {
            if (signUp == null)
                return;

            Close();
            signUp.Open();
        }
    }
}