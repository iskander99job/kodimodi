using Lean.Gui;
using UnityEngine;
using UnityEngine.UI;

namespace Kodimodi.UI
{
    public class ConsentPopupController : MonoBehaviour
    {
        private const string ConsentKey = "ConsentAccepted_v1";

        [Header("Окно согласия")]
        [SerializeField] private LeanWindow consentPopup;
        [SerializeField] private Toggle firstToggle;
        [SerializeField] private Toggle secondToggle;
        [SerializeField] private GameObject doneButton;

        private ProfileController profileController;

        private bool waitingForConsent;
        private bool initialized;

        private bool HasSavedConsent =>
            PlayerPrefs.GetInt(ConsentKey, 0) == 1;

        private void Awake()
        {
            profileController = FindAnyObjectByType<ProfileController>();

            if (profileController == null ||
                consentPopup == null ||
                firstToggle == null ||
                secondToggle == null ||
                doneButton == null)
            {
                Debug.LogError(
                    "ConsentPopupController: проверь поля в Inspector " +
                    "и наличие активного ProfileController в сцене.",
                    this);

                enabled = false;
                return;
            }

            firstToggle.SetIsOnWithoutNotify(false);
            secondToggle.SetIsOnWithoutNotify(false);

            firstToggle.onValueChanged.AddListener(OnToggleChanged);
            secondToggle.onValueChanged.AddListener(OnToggleChanged);

            initialized = true;

            UpdateDoneButton();
            consentPopup.TurnOff();
        }

        private void OnDestroy()
        {
            if (firstToggle != null)
                firstToggle.onValueChanged.RemoveListener(OnToggleChanged);

            if (secondToggle != null)
                secondToggle.onValueChanged.RemoveListener(OnToggleChanged);
        }

        public bool CheckConsent()
        {
            if (HasSavedConsent)
                return true;

            if (!initialized || !isActiveAndEnabled)
                return false;

            if (!waitingForConsent)
            {
                waitingForConsent = true;

                firstToggle.SetIsOnWithoutNotify(false);
                secondToggle.SetIsOnWithoutNotify(false);

                UpdateDoneButton();
                consentPopup.TurnOn();
            }

            return false;
        }

        // Кнопка Done.
        public void OnDoneClicked()
        {
            if (!initialized ||
                !isActiveAndEnabled ||
                !waitingForConsent ||
                !firstToggle.isOn ||
                !secondToggle.isOn)
            {
                return;
            }

            PlayerPrefs.SetInt(ConsentKey, 1);
            PlayerPrefs.Save();

            waitingForConsent = false;
            consentPopup.TurnOff();

            ContinuePlay();
        }

        // Кнопка Close или закрытие по фону.
        public void OnCloseClicked()
        {
            if (!initialized)
                return;

            waitingForConsent = false;
            consentPopup.TurnOff();
        }

        private void OnToggleChanged(bool value)
        {
            UpdateDoneButton();
        }

        private void UpdateDoneButton()
        {
            doneButton.SetActive(
                firstToggle.isOn && secondToggle.isOn);
        }

        private void ContinuePlay()
        {
            if (profileController != null &&
                profileController.isActiveAndEnabled)
            {
                profileController.OnPlayButtonClick();
            }
        }

        [ContextMenu("Reset Saved Consent")]
        private void ResetSavedConsent()
        {
            PlayerPrefs.DeleteKey(ConsentKey);
            PlayerPrefs.Save();
        }
    }
}