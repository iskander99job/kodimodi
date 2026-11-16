using Lean.Gui;
using UnityEngine;

namespace Kodimodi.UI.Profile
{
    [RequireComponent(typeof(LeanWindow))]
    public class SignUp : MonoBehaviour
    {
        private LeanWindow window;
        private SignIn signIn;

        private void Awake()
        {
            window = GetComponent<LeanWindow>();
            signIn = FindAnyObjectByType<SignIn>(
                FindObjectsInactive.Include);
        }

        public void Open()
        {
            if (signIn != null)
                signIn.Close();

            transform.SetAsLastSibling();
            window.TurnOn();
        }

        public void Close()
        {
            window.TurnOff();
        }

        public void Back()
        {
            Close();

            if (signIn != null)
                signIn.Open();
        }
    }
}