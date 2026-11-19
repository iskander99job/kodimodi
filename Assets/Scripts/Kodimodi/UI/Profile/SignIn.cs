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
    public class SignIn : MonoBehaviour
    {
        [SerializeField] private string apiBaseUrl = "http://localhost:5028";

        private LeanWindow window;
        private SignUp signUp;
        private ProfileLoader profileLoader;
        private TMP_InputField nicknameInput;
        private TMP_InputField passwordInput;
        private TMP_Text errorText;
        private GameObject spinner;
        private LeanButton signInButton;
        private Button closeButton;
        private Button createAccountButton;
        private bool isSubmitting;

        [Serializable]
        private sealed class LoginRequest
        {
            public string nickname;
            public string password;
        }

        [Serializable]
        private sealed class LoginResponse
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
            signUp = FindAnyObjectByType<SignUp>(FindObjectsInactive.Include);
            profileLoader = FindAnyObjectByType<ProfileLoader>();

            nicknameInput = FindRequired<TMP_InputField>("Panel/NicknameInput");
            passwordInput = FindRequired<TMP_InputField>("Panel/PasswordInput");
            errorText = FindRequired<TMP_Text>("Panel/ErrorText");
            spinner = FindRequired<Transform>("Panel/Spinner").gameObject;
            signInButton = FindRequired<LeanButton>("Panel/SignInButtonSlot/SignInButton");
            closeButton = FindRequired<Button>("Panel/Close");
            createAccountButton = FindRequired<Button>("Panel/Footer/CreateAccountButton");

            nicknameInput.contentType = TMP_InputField.ContentType.Standard;
            nicknameInput.characterLimit = 15;
            passwordInput.contentType = TMP_InputField.ContentType.Password;
            passwordInput.characterLimit = 30;

            SetSubmitting(false);
            ShowError(null);
            signInButton.OnClick.AddListener(Submit);
        }

        private void OnDestroy()
        {
            if (signInButton != null)
                signInButton.OnClick.RemoveListener(Submit);
        }

        public void Open()
        {
            nicknameInput.SetTextWithoutNotify(string.Empty);
            passwordInput.SetTextWithoutNotify(string.Empty);
            ShowError(null);

            transform.SetAsLastSibling();
            window.TurnOn();
        }

        public void Close()
        {
            window.TurnOff();
        }

        public void OpenSignUp()
        {
            if (isSubmitting || signUp == null)
                return;

            Close();
            signUp.Open();
        }

        private void Submit()
        {
            if (isSubmitting)
                return;

            ShowError(null);

            if (profileLoader == null)
            {
                Debug.LogError("Add ProfileLoader to the Profile object.", this);
                ShowError(SignInErrors.SignInFailed);
                return;
            }

            string nickname = nicknameInput.text;
            string password = passwordInput.text;

            if (!Regex.IsMatch(nickname, @"\A[A-Za-z0-9_]{4,15}\z") ||
                password.Length < 6 || password.Length > 30)
            {
                ShowError(SignInErrors.InvalidCredentials);
                return;
            }

            string baseUrl = apiBaseUrl.Trim().TrimEnd('/');

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                ShowError(SignInErrors.InvalidApiUrl);
                return;
            }

            StartCoroutine(Login(baseUrl, nickname, password));
        }

        private IEnumerator Login(string baseUrl, string nickname, string password)
        {
            string json = JsonUtility.ToJson(new LoginRequest
            {
                nickname = nickname,
                password = password
            });

            using var request = new UnityWebRequest(
                baseUrl + "/auth/login", UnityWebRequest.kHttpVerbPOST);

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
                ShowError(SignInErrors.ConnectionFailed);
                yield break;
            }

            var response = ReadResponse(request.downloadHandler.text);

            if (request.responseCode != 200 ||
                request.result != UnityWebRequest.Result.Success)
            {
                ShowError(SignInErrors.GetMessage(response?.error));
                yield break;
            }

            if (string.IsNullOrWhiteSpace(response?.accessToken))
            {
                ShowError(SignInErrors.MissingAccessToken);
                yield break;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            if (Kodimodi_SaveAccessToken(response.accessToken) != 1)
            {
                ShowError(SignInErrors.CookieNotSaved);
                yield break;
            }
#endif

            passwordInput.SetTextWithoutNotify(string.Empty);
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

        private static LoginResponse ReadResponse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonUtility.FromJson<LoginResponse>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private void SetSubmitting(bool value)
        {
            isSubmitting = value;
            signInButton.gameObject.SetActive(!value);
            spinner.SetActive(value);

            nicknameInput.interactable = !value;
            passwordInput.interactable = !value;
            closeButton.interactable = !value;
            createAccountButton.interactable = !value;
        }

        private void ShowError(string message)
        {
            errorText.text = message ?? string.Empty;
            errorText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
    }
}