using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Kodimodi.Errors;
using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Kodimodi.UI.Profile
{
    [RequireComponent(typeof(LeanWindow))]
    public class SignUp : MonoBehaviour
    {
        [SerializeField] private string apiBaseUrl = "http://localhost:5028";

        private LeanWindow window;
        private SignIn signIn;
        private ProfileLoader profileLoader;
        private TMP_InputField nicknameInput;
        private TMP_InputField passwordInput;
        private Toggle termsToggle;
        private Toggle personalDataToggle;
        private TMP_Text errorText;
        private GameObject spinner;
        private LeanButton createAccountButton;
        private Button closeButton;
        private Button backButton;
        private bool isSubmitting;

        [Serializable]
        private sealed class RegisterRequest
        {
            public string nickname;
            public string password;
        }

        [Serializable]
        private sealed class RegisterResponse
        {
            public string accessToken;
            public string error;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int Kodimodi_SaveAccessToken(string token);
#endif

        private void Awake()
        {
            window = GetComponent<LeanWindow>();
            signIn = FindAnyObjectByType<SignIn>(FindObjectsInactive.Include);
            profileLoader = FindAnyObjectByType<ProfileLoader>();

            nicknameInput = FindRequired<TMP_InputField>("Panel/NicknameInput");
            passwordInput = FindRequired<TMP_InputField>("Panel/PasswordInput");
            termsToggle = FindRequired<Toggle>("Panel/TermsToggle");
            personalDataToggle = FindRequired<Toggle>("Panel/PersonalDataToggle");
            errorText = FindRequired<TMP_Text>("Panel/ErrorText");
            spinner = FindRequired<Transform>("Panel/Spinner").gameObject;
            createAccountButton = FindRequired<LeanButton>("Panel/CreateAccountButton");
            closeButton = FindRequired<Button>("Panel/Close");
            backButton = FindRequired<Button>("Panel/Back");

            nicknameInput.contentType = TMP_InputField.ContentType.Standard;
            nicknameInput.characterLimit = 15;
            passwordInput.contentType = TMP_InputField.ContentType.Password;
            passwordInput.characterLimit = 30;
            nicknameInput.onValidateInput = AuthInputValidation.Nickname;
            passwordInput.onValidateInput = AuthInputValidation.Password;

            SetSubmitting(false);
            ShowError(null);
            createAccountButton.OnClick.AddListener(Submit);
        }

        private void OnDestroy()
        {
            if (createAccountButton != null)
                createAccountButton.OnClick.RemoveListener(Submit);
        }

        public void Open()
        {
            if (signIn != null)
                signIn.Close();

            nicknameInput.SetTextWithoutNotify(string.Empty);
            passwordInput.SetTextWithoutNotify(string.Empty);
            termsToggle.SetIsOnWithoutNotify(false);
            personalDataToggle.SetIsOnWithoutNotify(false);
            ShowError(null);

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

        public void Submit()
        {
            if (isSubmitting)
                return;

            ShowError(null);

            if (profileLoader == null)
            {
                Debug.LogError("Add ProfileLoader to the Profile object.", this);
                ShowError(RegistrationErrors.RegistrationFailed);
                return;
            }

            string nickname = nicknameInput.text;
            string password = passwordInput.text;

            if (!Regex.IsMatch(nickname, @"\A[A-Za-z0-9_]{4,15}\z"))
            {
                ShowError(RegistrationErrors.InvalidNickname);
                return;
            }

            if (password.Length < 6 || password.Length > 30)
            {
                ShowError(RegistrationErrors.InvalidPassword);
                return;
            }

            if (!termsToggle.isOn || !personalDataToggle.isOn)
            {
                ShowError(RegistrationErrors.ConsentRequired);
                return;
            }

            string baseUrl = apiBaseUrl.Trim().TrimEnd('/');

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                ShowError(RegistrationErrors.InvalidApiUrl);
                return;
            }

            StartCoroutine(Register(baseUrl, nickname, password));
        }

        private IEnumerator Register(string baseUrl, string nickname, string password)
        {
            string json = JsonUtility.ToJson(new RegisterRequest
            {
                nickname = nickname,
                password = password
            });

            using var request = new UnityWebRequest(
                baseUrl + "/auth/register", UnityWebRequest.kHttpVerbPOST);

            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 15;

            SetSubmitting(true);

            try
            {
                yield return request.SendWebRequest();
            }
            finally
            {
                SetSubmitting(false);
            }

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                ShowError(RegistrationErrors.ConnectionFailed);
                yield break;
            }

            var response = ReadResponse(request.downloadHandler.text);

            if (request.responseCode != 201 ||
                request.result != UnityWebRequest.Result.Success)
            {
                ShowError(RegistrationErrors.GetMessage(response?.error));
                yield break;
            }

            passwordInput.SetTextWithoutNotify(string.Empty);

            if (string.IsNullOrWhiteSpace(response?.accessToken))
            {
                ShowError(RegistrationErrors.MissingAccessToken);
                yield break;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            if (Kodimodi_SaveAccessToken(response.accessToken) != 1)
            {
                ShowError(RegistrationErrors.CookieNotSaved);
                yield break;
            }
#endif

            Close();
            profileLoader.Load(baseUrl, response.accessToken);
        }

        private T FindRequired<T>(string path) where T : Component
        {
            var child = transform.Find(path);

            if (child != null && child.TryGetComponent<T>(out var component))
                return component;

            throw new InvalidOperationException(
                $"{name}: expected {typeof(T).Name} at '{path}'.");
        }

        private static RegisterResponse ReadResponse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonUtility.FromJson<RegisterResponse>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private void SetSubmitting(bool value)
        {
            isSubmitting = value;
            createAccountButton.gameObject.SetActive(!value);
            spinner.SetActive(value);

            nicknameInput.interactable = !value;
            passwordInput.interactable = !value;
            termsToggle.interactable = !value;
            personalDataToggle.interactable = !value;
            closeButton.interactable = !value;
            backButton.interactable = !value;
        }

        private void ShowError(string message)
        {
            errorText.text = message ?? string.Empty;
            errorText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
    }
}