using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace Kodimodi.UI.Profile
{
    public sealed class ProfileLoader : MonoBehaviour
    {
        private readonly HashSet<string> ownedWeapons =
            new HashSet<string>(StringComparer.Ordinal);

        private Transform canvas;
        private GameObject profileSpinner;
        private GameObject[] profileObjects;
        private CanvasGroup playCanvasGroup;
        private ProfileRankController profileRank;
        private GameObject signInButton;
        private GameObject exitButton;
        private GameObject balancePanel;
        private TMP_Text balanceText;
        private string sessionApiBaseUrl;
        private string sessionAccessToken;
        private bool isLoading;
        private static (string ApiUrl, string Token) shopSession;
        internal static bool UseRussianInventoryStats { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int Kodimodi_ClearAccessToken();
#endif

        [Serializable]
        private sealed class ProfileResponse
        {
            public long id;
            public string nickname;
            public int coins;
            public int xp;
        }

        private void Awake()
        {
            var mainCanvas = GameObject.Find("MainCanvas");

            if (mainCanvas == null)
            {
                throw new InvalidOperationException(
                    "ProfileLoader: MainCanvas was not found.");
            }

            canvas = mainCanvas.transform;

            profileSpinner = FindRequired<Transform>(canvas, "ProfileSpinner").gameObject;
            profileRank = FindRequired<ProfileRankController>(canvas, "ProfileRank");

            var play = FindRequired<Transform>(canvas, "Play");

            if (!play.TryGetComponent<CanvasGroup>(out playCanvasGroup))
                playCanvasGroup = play.gameObject.AddComponent<CanvasGroup>();

            var rightTopBlock = FindRequired<Transform>(canvas, "RightTopBlock");

            signInButton = FindRequired<Transform>(rightTopBlock, "SignInButton").gameObject;
            exitButton = FindRequired<Transform>(rightTopBlock, "ExitButton").gameObject;
            balancePanel = FindRequired<Transform>(rightTopBlock, "BalancePanel").gameObject;
            balanceText = FindRequired<TMP_Text>(rightTopBlock, "BalancePanel/BalanceText");

            profileObjects = new[]
            {
                profileRank.gameObject,
                rightTopBlock.gameObject,
                FindRequired<Transform>(canvas, "SideMenuUI").gameObject
            };

            profileSpinner.SetActive(false);
            SetCoins(0);
            SetSignedIn(false);
        }

        private void Start()
        {
            if (isLoading)
                return;

            var previousSession = shopSession;
            shopSession = default;
            if (!string.IsNullOrEmpty(previousSession.Token))
            {
                StartCoroutine(LoadProfile(previousSession.ApiUrl, previousSession.Token));
                return;
            }

#if UNITY_EDITOR
            if (EditorAuthSession.TryRead(out var apiBaseUrl, out var accessToken))
                StartCoroutine(LoadProfile(apiBaseUrl, accessToken));
#endif
        }

        public void OpenWeaponShop() => OpenInventoryScene("WeaponShopScene");

        public void OpenArsenal() => OpenInventoryScene("ArsenalScene");

        public void OpenCoinShop()
        {
            if (isLoading || string.IsNullOrEmpty(sessionAccessToken))
                return;

            RememberInventorySession();
            CoinShopScreen.Open();
        }

        private void OpenInventoryScene(string sceneName)
        {
            if (isLoading)
                return;

            RememberInventorySession();
            SceneManager.LoadScene(sceneName);
        }

        private void RememberInventorySession()
        {
            shopSession = (sessionApiBaseUrl, sessionAccessToken);
            UseRussianInventoryStats = profileRank.CurrentLanguage == ProfileRankController.Language.Russian;
        }

        internal static bool TryGetShopSession(out string apiBaseUrl, out string token)
        {
            (apiBaseUrl, token) = shopSession;
            if (!string.IsNullOrEmpty(token))
                return true;

#if UNITY_EDITOR
            return EditorAuthSession.TryRead(out apiBaseUrl, out token);
#else
            return false;
#endif
        }

        internal static void ClearInventorySession()
        {
            shopSession = default;
#if UNITY_EDITOR
            EditorAuthSession.Clear();
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
            Kodimodi_ClearAccessToken();
#endif
        }

        public void Load(string apiBaseUrl, string accessToken)
        {
            if (isLoading)
                return;

#if UNITY_EDITOR
            EditorAuthSession.Save(apiBaseUrl, accessToken);
#endif

            StartCoroutine(LoadProfile(apiBaseUrl, accessToken));
        }

        public bool TryGetSession(out string apiBaseUrl, out string accessToken)
        {
            apiBaseUrl = sessionApiBaseUrl;
            accessToken = sessionAccessToken;
            return !isLoading && !string.IsNullOrEmpty(accessToken);
        }

        public string GetAccessToken(string apiBaseUrl)
        {
            return string.Equals(sessionApiBaseUrl, apiBaseUrl, StringComparison.Ordinal)
                ? sessionAccessToken
                : null;
        }

        public bool OwnsWeapon(string code)
        {
            return ownedWeapons.Contains(code);
        }

        public void RememberOwnedWeapon(string code)
        {
            ownedWeapons.Add(code);
        }

        internal void SyncOwnedWeapons(ArsenalWeapon[] weapons)
        {
            ownedWeapons.Clear();

            foreach (var weapon in weapons)
            {
                if (weapon != null && !string.IsNullOrWhiteSpace(weapon.code))
                    ownedWeapons.Add(weapon.code);
            }
        }

        public void SetCoins(int coins)
        {
            string amount = coins
                .ToString("N0", CultureInfo.InvariantCulture)
                .Replace(',', ' ');

            balanceText.text = amount;
            SetScreenBalance("WeaponShopUI", amount);
            SetScreenBalance("WeaponDetailsUI", amount);
            SetScreenBalance("ArsenalUI", amount);
        }

        public void ResetProfile()
        {
#if UNITY_EDITOR
            EditorAuthSession.Clear();
#endif

            sessionApiBaseUrl = null;
            sessionAccessToken = null;
            ownedWeapons.Clear();

            profileRank.SetNickname("Guest");
            profileRank.SetXP(0);
            SetCoins(0);
            SetSignedIn(false);
        }

        public void ClearRejectedSession()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Kodimodi_ClearAccessToken();
#endif

            ResetProfile();
        }

        private IEnumerator LoadProfile(string apiBaseUrl, string accessToken)
        {
            using var request = UnityWebRequest.Get(apiBaseUrl + "/profile");
            request.SetRequestHeader("Authorization", "Bearer " + accessToken);
            request.timeout = 15;

            var wasActive = new bool[profileObjects.Length];
            float playAlpha = playCanvasGroup.alpha;
            bool playInteractable = playCanvasGroup.interactable;
            bool playBlocksRaycasts = playCanvasGroup.blocksRaycasts;

            isLoading = true;

            for (int i = 0; i < profileObjects.Length; i++)
            {
                wasActive[i] = profileObjects[i].activeSelf;
                profileObjects[i].SetActive(false);
            }

            playCanvasGroup.alpha = 0f;
            playCanvasGroup.interactable = false;
            playCanvasGroup.blocksRaycasts = false;
            profileSpinner.SetActive(true);

            try
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success ||
                    request.responseCode != 200)
                {
                    if (request.responseCode == 401 || request.responseCode == 404)
                    {
                        ClearRejectedSession();
                    }

                    string message = request.responseCode switch
                    {
                        401 => "Please sign in again.",
                        404 => "Profile not found.",
                        _ => "Couldn't load your profile. Please try again."
                    };

                    ErrorPopup.Show(message, "Profile Error");
                    yield break;
                }

                var profile = ReadProfile(request.downloadHandler.text);

                if (profile == null || profile.id <= 0 ||
                    string.IsNullOrWhiteSpace(profile.nickname))
                {
                    ErrorPopup.Show(
                        "The server returned an invalid profile.", "Profile Error");
                    yield break;
                }

                if (sessionAccessToken != accessToken || sessionApiBaseUrl != apiBaseUrl)
                    ownedWeapons.Clear();

                sessionApiBaseUrl = apiBaseUrl;
                sessionAccessToken = accessToken;

                profileRank.SetNickname(profile.nickname);
                profileRank.SetXP(profile.xp);
                SetCoins(profile.coins);
                SetSignedIn(true);

                yield return LoadOwnedWeapons(apiBaseUrl, accessToken);
            }
            finally
            {
                profileSpinner.SetActive(false);

                for (int i = 0; i < profileObjects.Length; i++)
                    profileObjects[i].SetActive(wasActive[i]);

                playCanvasGroup.alpha = playAlpha;
                playCanvasGroup.interactable = playInteractable;
                playCanvasGroup.blocksRaycasts = playBlocksRaycasts;

                isLoading = false;
            }
        }

        private IEnumerator LoadOwnedWeapons(string apiBaseUrl, string token)
        {
            using var request = ArsenalApi.CreateRequest(apiBaseUrl, token);

            yield return request.SendWebRequest();

            if (request.responseCode == 401)
            {
                ClearRejectedSession();
                ErrorPopup.Show(ArsenalApi.SignInRequired, "Arsenal Error");
                yield break;
            }

            var arsenal = ArsenalApi.ReadResponse(request);

            if (arsenal?.weapons == null)
            {
                ErrorPopup.Show(ArsenalApi.LoadFailed, "Arsenal Error");
                yield break;
            }

            SyncOwnedWeapons(arsenal.weapons);
        }

        private void SetScreenBalance(string screen, string amount)
        {
            var target = canvas.Find(screen + "/Header/BalancePanel/BalanceText");

            if (target != null && target.TryGetComponent<TMP_Text>(out var text))
                text.text = amount;
        }

        private void SetSignedIn(bool signedIn)
        {
            signInButton.SetActive(!signedIn);
            exitButton.SetActive(signedIn);
            balancePanel.SetActive(signedIn);
        }

        private static ProfileResponse ReadProfile(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonUtility.FromJson<ProfileResponse>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static T FindRequired<T>(Transform root, string path) where T : Component
        {
            var child = root.Find(path);

            if (child != null && child.TryGetComponent<T>(out var component))
                return component;

            throw new InvalidOperationException(
                $"ProfileLoader: expected {typeof(T).Name} at '{root.name}/{path}'.");
        }
    }
}