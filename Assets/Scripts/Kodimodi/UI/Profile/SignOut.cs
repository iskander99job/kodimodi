using System.Runtime.InteropServices;
using Lean.Gui;
using UnityEngine;

namespace Kodimodi.UI.Profile
{
    [RequireComponent(typeof(LeanButton))]
    public sealed class SignOut : MonoBehaviour
    {
        private LeanButton exitButton;
        private ProfileLoader profileLoader;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int Kodimodi_ClearAccessToken();
#endif

        private void Awake()
        {
            exitButton = GetComponent<LeanButton>();
            profileLoader = FindAnyObjectByType<ProfileLoader>();
            exitButton.OnClick.AddListener(ExitAccount);
        }

        private void OnDestroy()
        {
            if (exitButton != null)
                exitButton.OnClick.RemoveListener(ExitAccount);
        }

        private void ExitAccount()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Kodimodi_ClearAccessToken() != 1)
            {
                ErrorPopup.Show("Couldn't clear your session.", "Sign-out Error");
                return;
            }
#endif

            if (profileLoader == null)
            {
                ErrorPopup.Show("Couldn't reset your profile.", "Sign-out Error");
                return;
            }

            profileLoader.ResetProfile();
        }
    }
}