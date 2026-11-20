using System;
using System.Collections;
using System.Collections.Generic;
using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Kodimodi.UI.Profile
{
    public class Arsenal : MonoBehaviour
    {
        [SerializeField] private RectTransform weaponCardPrefab;
        [SerializeField] private WeaponIcon[] weaponIcons = Array.Empty<WeaponIcon>();

        private static readonly (string Tab, string Code)[] Categories =
        {
            ("AR_Tab", "AR"),
            ("PISTOL_Tab", "PST"),
            ("SMG_Tab", "SMG"),
            ("SHOTGUN_Tab", "SG"),
            ("MG_Tab", "MG"),
            ("SR_Tab", "SR"),
            ("GL_Tab", "GL")
        };

        private readonly List<GameObject> cards = new List<GameObject>();
        private readonly List<LeanButton> equipButtons = new List<LeanButton>();
        private readonly Dictionary<string, Sprite> icons =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private ScrollRect scrollView;
        private TMP_Text sectionTitle;
        private TMP_Text modelCount;
        private LeanButton backButton;
        private ProfileLoader profileLoader;
        private ArsenalWeapon[] weapons = Array.Empty<ArsenalWeapon>();

        private int selectedCategory;
        private bool isLoading;
        private bool isEquipping;
        private Coroutine requestRoutine;
        private UnityWebRequest activeRequest;

        [Serializable]
        private struct WeaponIcon
        {
            public string code;
            public Sprite sprite;
        }

        private void Awake()
        {
            scrollView = FindRequired<ScrollRect>(transform, "WeaponsScrollView");
            sectionTitle = FindRequired<TMP_Text>(transform, "SectionHeading/SectionTitle");
            modelCount = FindRequired<TMP_Text>(transform, "SectionHeading/ModelCount");
            backButton = FindRequired<LeanButton>(transform, "Header/BackButton");

            foreach (var entry in weaponIcons)
            {
                if (!string.IsNullOrWhiteSpace(entry.code) && entry.sprite != null)
                    icons[entry.code.Trim()] = entry.sprite;
            }

            var tabs = FindRequired<Transform>(transform, "Categories_Fixed");

            for (int i = 0; i < Categories.Length; i++)
            {
                int index = i;
                var toggle = FindRequired<Toggle>(tabs, Categories[index].Tab);

                if (toggle.isOn)
                    selectedCategory = index;

                toggle.onValueChanged.AddListener(isOn =>
                {
                    if (!isOn || selectedCategory == index)
                        return;

                    selectedCategory = index;

                    if (isActiveAndEnabled)
                        RenderSelectedCategory();
                });
            }
        }

        public void Open()
        {
            if (isEquipping)
                return;

            profileLoader = FindAnyObjectByType<ProfileLoader>();

            transform.SetAsLastSibling();
            gameObject.SetActive(true);

            if (profileLoader != null &&
                profileLoader.TryGetSession(out var apiBaseUrl, out var token))
            {
                LoadArsenal(apiBaseUrl, token);
                return;
            }

            ShowGuestArsenal();
        }

        public void Close()
        {
            if (!isEquipping)
                gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            CancelRequest();
            isLoading = false;

            if (backButton != null)
                SetEquipping(false);
        }

        private void ShowGuestArsenal()
        {
            CancelRequest();
            isLoading = false;
            weapons = Array.Empty<ArsenalWeapon>();
            SetEquipping(false);
            RenderSelectedCategory();
        }

        private void LoadArsenal(string apiBaseUrl, string token)
        {
            CancelRequest();
            weapons = Array.Empty<ArsenalWeapon>();

            if (weaponCardPrefab == null || scrollView.content == null)
            {
                isLoading = false;
                RenderSelectedCategory();

                ErrorPopup.Show(
                    "The arsenal card prefab or scroll content is missing.",
                    "Arsenal Error");

                return;
            }

            isLoading = true;
            RenderSelectedCategory();

            var request = ArsenalApi.CreateRequest(apiBaseUrl, token);

            Send(
                request,
                completed => ReadArsenal(completed, apiBaseUrl, token),
                ShowLoadError);
        }

        private void ReadArsenal(
            UnityWebRequest request,
            string apiBaseUrl,
            string token)
        {
            isLoading = false;

            if (!CheckSession(request, apiBaseUrl, token))
                return;

            var result = ArsenalApi.ReadResponse(request);

            if (result?.weapons == null)
            {
                ShowLoadError();
                return;
            }

            weapons = result.weapons;
            profileLoader.SyncOwnedWeapons(weapons);
            RenderSelectedCategory();
        }

        private void Equip(ArsenalWeapon weapon)
        {
            if (isLoading || isEquipping || weapon.equipped)
                return;

            if (profileLoader == null ||
                !profileLoader.TryGetSession(out var apiBaseUrl, out var token))
            {
                ShowGuestArsenal();
                ErrorPopup.Show("Sign in to equip this weapon.", "Arsenal");
                return;
            }

            SetEquipping(true);

            var request = ArsenalApi.CreateEquipRequest(apiBaseUrl, token, weapon.code);

            Send(
                request,
                completed => ReadEquipment(completed, apiBaseUrl, token, weapon),
                () =>
                {
                    SetEquipping(false);
                    ErrorPopup.Show(ArsenalApi.EquipFailed, "Arsenal Error");
                });
        }

        private void ReadEquipment(
            UnityWebRequest request,
            string apiBaseUrl,
            string token,
            ArsenalWeapon selectedWeapon)
        {
            SetEquipping(false);

            if (!CheckSession(request, apiBaseUrl, token))
                return;

            var result = ArsenalApi.ReadEquipResponse(request);

            if (request.result != UnityWebRequest.Result.Success ||
                request.responseCode != 200 ||
                result == null ||
                !result.equipped ||
                result.code != selectedWeapon.code ||
                result.categoryCode != selectedWeapon.categoryCode)
            {
                ErrorPopup.Show(ArsenalApi.GetEquipError(result?.error), "Arsenal Error");
                return;
            }

            foreach (var weapon in weapons)
            {
                if (weapon != null && weapon.categoryCode == result.categoryCode)
                    weapon.equipped = weapon.code == result.code;
            }

            RenderSelectedCategory(resetScroll: false);
        }

        private bool CheckSession(
            UnityWebRequest request,
            string apiBaseUrl,
            string token)
        {
            if (profileLoader == null ||
                profileLoader.GetAccessToken(apiBaseUrl) != token)
            {
                ShowGuestArsenal();
                return false;
            }

            if (request.responseCode != 401)
                return true;

            profileLoader.ClearRejectedSession();
            ShowGuestArsenal();
            ErrorPopup.Show("Please sign in again.", "Arsenal");
            return false;
        }

        private void SetEquipping(bool value)
        {
            isEquipping = value;
            backButton.interactable = !value;

            foreach (var button in equipButtons)
                button.interactable = !value;
        }

        private void RenderSelectedCategory(bool resetScroll = true)
        {
            float scrollPosition =
                resetScroll ? 1f : scrollView.verticalNormalizedPosition;

            ClearCards();

            string categoryCode = Categories[selectedCategory].Code;
            sectionTitle.text = WeaponShop.GetCategoryTitle(categoryCode);

            if (isLoading)
            {
                modelCount.text = "Loading…";
                return;
            }

            foreach (var weapon in weapons)
            {
                if (weapon == null ||
                    string.IsNullOrWhiteSpace(weapon.code) ||
                    weapon.categoryCode != categoryCode)
                {
                    continue;
                }

                var card = Instantiate(weaponCardPrefab, scrollView.content, false);

                cards.Add(card.gameObject);
                SetCard(card, weapon);
                card.gameObject.SetActive(true);
            }

            modelCount.text = cards.Count == 1 ? "1 model" : $"{cards.Count} models";

            Canvas.ForceUpdateCanvases();
            scrollView.StopMovement();
            scrollView.verticalNormalizedPosition = scrollPosition;
        }

        private void SetCard(RectTransform card, ArsenalWeapon weapon)
        {
            card.name = weapon.code;
            FindRequired<TMP_Text>(card, "WeaponName").text = weapon.name;

            var icon = FindRequired<Image>(card, "Weapon_Icon");
            icons.TryGetValue(weapon.code, out var sprite);
            icon.sprite = sprite;
            icon.enabled = sprite != null;

            var equipped = FindRequired<Transform>(card, "RankRequirement");
            FindRequired<TMP_Text>(equipped, "RequiredRank").text = "Equipped";
            equipped.gameObject.SetActive(weapon.equipped);

            var equipButton = FindRequired<LeanButton>(card, "DetailsButton");
            FindRequired<TMP_Text>(equipButton.transform, "Label").text = "Equip";
            equipButton.interactable = !isEquipping;
            equipButton.gameObject.SetActive(!weapon.equipped);
            equipButton.OnClick.AddListener(() => Equip(weapon));

            equipButtons.Add(equipButton);
        }

        private void Send(
            UnityWebRequest request,
            Action<UnityWebRequest> onCompleted,
            Action onStartFailed)
        {
            UnityWebRequestAsyncOperation operation;

            try
            {
                operation = request.SendWebRequest();
            }
            catch (InvalidOperationException)
            {
                request.Dispose();
                onStartFailed();
                return;
            }

            activeRequest = request;
            requestRoutine = StartCoroutine(
                WaitForResponse(request, operation, onCompleted));
        }

        private IEnumerator WaitForResponse(
            UnityWebRequest request,
            UnityWebRequestAsyncOperation operation,
            Action<UnityWebRequest> onCompleted)
        {
            yield return operation;

            requestRoutine = null;
            activeRequest = null;

            using (request)
            {
                onCompleted(request);
            }
        }

        private void ClearCards()
        {
            foreach (var card in cards)
            {
                card.SetActive(false);
                Destroy(card);
            }

            cards.Clear();
            equipButtons.Clear();
        }

        private void CancelRequest()
        {
            if (requestRoutine != null)
            {
                StopCoroutine(requestRoutine);
                requestRoutine = null;
            }

            if (activeRequest != null)
            {
                activeRequest.Abort();
                activeRequest.Dispose();
                activeRequest = null;
            }
        }

        private void ShowLoadError()
        {
            isLoading = false;
            modelCount.text = "0 models";
            ErrorPopup.Show(ArsenalApi.LoadFailed, "Arsenal Error");
        }

        private static T FindRequired<T>(Transform root, string path) where T : Component
        {
            var child = root.Find(path);

            if (child != null && child.TryGetComponent<T>(out var component))
                return component;

            throw new InvalidOperationException(
                $"Arsenal: expected {typeof(T).Name} at '{root.name}/{path}'.");
        }
    }
}