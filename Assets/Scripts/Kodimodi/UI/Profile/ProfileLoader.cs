using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace Kodimodi.UI.Profile
{
    public sealed class ProfileLoader : MonoBehaviour
    {
        private GameObject profileSpinner;
        private GameObject[] profileObjects;
        private CanvasGroup playCanvasGroup;
        private ProfileRankController profileRank;
        private GameObject signInButton;
        private GameObject exitButton;
        private GameObject balancePanel;
        private TMP_Text balanceText;
        private bool isLoading;

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

            var canvas = mainCanvas.transform;

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
            SetSignedIn(false);
        }

#if UNITY_EDITOR
        private void Start()
        {
            if (!isLoading &&
                EditorAuthSession.TryRead(out var apiBaseUrl, out var accessToken))
            {
                StartCoroutine(LoadProfile(apiBaseUrl + "/profile", accessToken));
            }
        }
#endif

        public void Load(string apiBaseUrl, string accessToken)
        {
            if (isLoading)
                return;

#if UNITY_EDITOR
            EditorAuthSession.Save(apiBaseUrl, accessToken);
#endif

            StartCoroutine(LoadProfile(apiBaseUrl + "/profile", accessToken));
        }

        public void ResetProfile()
        {
#if UNITY_EDITOR
            EditorAuthSession.Clear();
#endif

            profileRank.SetNickname("Guest");
            profileRank.SetXP(0);
            balanceText.text = "0";

            SetSignedIn(false);
        }

        private IEnumerator LoadProfile(string url, string accessToken)
        {
            using var request = UnityWebRequest.Get(url);
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

            if (request.result != UnityWebRequest.Result.Success ||
                request.responseCode != 200)
            {
#if UNITY_EDITOR
                if (request.responseCode == 401)
                    ResetProfile();
#endif

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
                ErrorPopup.Show("The server returned an invalid profile.", "Profile Error");
                yield break;
            }

            profileRank.SetNickname(profile.nickname);
            profileRank.SetXP(profile.xp);
            balanceText.text = profile.coins.ToString(CultureInfo.InvariantCulture);

            SetSignedIn(true);
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