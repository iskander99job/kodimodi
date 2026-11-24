using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kodimodi.UI.Profile
{
    public sealed class ArsenalScreen : MonoBehaviour
    {
        [SerializeField] private string guestApiBaseUrl = "http://localhost:5028";
        [SerializeField] private WeaponAssetsCatalog weaponAssets;

        private readonly List<(Button Button, GameObject Highlight, string Category)> tabs = new();
        private readonly List<(Button Button, ArsenalWeapon Weapon)> cards = new();
        private ArsenalWeapon[] weapons = Array.Empty<ArsenalWeapon>();

        [SerializeField] private WeaponCategoryIcons categoryIcons;

        private Transform page;
        private GameObject categoryBar;
        private GameObject categoryPages;
        private Text loadError;
        private LeanButton retryButton;
        private Action retryLoad;
        private WeaponStatsView statsView;
        private Transform states;
        private GameObject spinner;
        private GameObject canEquip;
        private LeanButton equip;
        private LeanButton back;
        private Text replacementHint;
        private Text message;
        private TMP_Text balance;
        private GameObject balancePanel;
        private ScrollRect scroll;
        private Button template;
        private string category = "pistol";
        private string apiBaseUrl;
        private string token;
        private ArsenalWeapon selected;
        private bool busy;
        private bool inventoryLoaded;
        private Coroutine detailRequest;
        private UnityWebRequest activeDetails;

        [Serializable] private sealed class Profile { public int coins; }
        [Serializable] private sealed class Details { public WeaponStatValue[] stats; }

        private static string weaponToOpen;

        internal static void OpenWeapon(string code)
        {
            weaponToOpen = code;
            SceneManager.LoadScene("ArsenalScene");
        }

        private void Awake()
        {
            var pages = transform.Find("Category Pages");
            categoryPages = pages.gameObject;
            categoryBar = transform.Find("Categories").gameObject;
            page = pages.Find("PISTOL Page");
            statsView = new WeaponStatsView(page, ProfileLoader.UseRussianInventoryStats);
            foreach (Transform child in pages)
                child.gameObject.SetActive(child == page);

            var compactBar = categoryBar.transform.Find("Compact");
            compactBar.gameObject.SetActive(true);
            categoryBar.transform.Find("Full Width").gameObject.SetActive(false);
            BindTabs(compactBar);

            states = page.Find("Equip States");
            canEquip = states.Find("Can Equip").gameObject;
            equip = canEquip.transform.Find("Equip Button").GetComponent<LeanButton>();
            replacementHint = canEquip.transform.Find("Replacement Hint").GetComponent<Text>();
            back = transform.Find("Header/Back").GetComponent<LeanButton>();
            back.OnClick.AddListener(() => SceneManager.LoadScene("ProfileScene"));
            balancePanel = transform.Find("Header/BalancePanel").gameObject;
            balance = balancePanel.transform.Find("BalanceText").GetComponent<TMP_Text>();
            balancePanel.transform.Find("AddCurrencyButton").GetComponent<LeanButton>()
                .OnClick.AddListener(CoinShopScreen.Open);
            balancePanel.SetActive(false);

            spinner = transform.Find("Spinner").gameObject;
            scroll = page.Find("Weapon Scroll View").GetComponent<ScrollRect>();
            scroll.transform.Find("Spinner").gameObject.SetActive(false);
            template = scroll.content.Find("P-12 Card").GetComponent<Button>();
            foreach (Transform card in scroll.content)
                card.gameObject.SetActive(false);

            message = Instantiate(pages.Find("AR Page/Empty Message"), page).GetComponent<Text>();
            message.gameObject.SetActive(false);
            CreateLoadError();
            equip.OnClick.AddListener(Equip);
            RenderSelection();
            statsView.Render(null);
        }

        private void Start()
        {
            var preferredCode = weaponToOpen;
            weaponToOpen = null;

            if (!ProfileLoader.TryGetShopSession(out apiBaseUrl, out token))
                apiBaseUrl = guestApiBaseUrl.Trim().TrimEnd('/');

            StartCoroutine(Load(preferredCode, selectFirstOwnedCategory: true));
        }

        private void OnDisable()
        {
            CancelDetails();
        }

        private void BindTabs(Transform bar)
        {
            foreach (var (name, code) in WeaponCategoryIcons.Tabs)
            {
                var tab = bar.Find(name + " Tab");
                if (tab == null)
                    continue;

                categoryIcons?.Apply(tab, code);
                var button = tab.GetComponent<Button>();
                tabs.Add((button, tab.Find("Selected").gameObject, code));
                button.onClick.AddListener(() =>
                {
                    if (busy)
                        return;

                    category = code;
                    if (!inventoryLoaded)
                        StartCoroutine(Load());
                    else
                        RenderCategory(null);
                });
            }
        }

        private IEnumerator Load(string preferredCode = null, bool selectFirstOwnedCategory = false)
        {
            CancelDetails();
            BeginLoad();
            SetMessage(null);
            Action retry = () => StartCoroutine(Load(preferredCode, selectFirstOwnedCategory));
            if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                ShowLoadError("The API address is invalid.", retry);
                yield break;
            }

            ArsenalResponse response = null;
            if (!string.IsNullOrEmpty(token))
            {
                bool balanceLoaded = false;
                yield return LoadBalance(loaded => balanceLoaded = loaded);
                if (!balanceLoaded && !string.IsNullOrEmpty(token))
                {
                    ShowLoadError(ArsenalApi.LoadFailed, retry);
                    yield break;
                }
            }

            if (!string.IsNullOrEmpty(token))
            {
                using var request = ArsenalApi.CreateRequest(apiBaseUrl, token);
                yield return Send(request);
                if (request.responseCode == 401)
                    ClearRejectedSession();
                else
                    response = ArsenalApi.ReadResponse(request);
            }

            if (string.IsNullOrEmpty(token))
            {
                using var request = UnityWebRequest.Get(apiBaseUrl + "/weapons/starter");
                request.timeout = 15;
                yield return Send(request);
                var starter = ReadSuccess<ArsenalWeapon>(request);
                if (starter != null && !string.IsNullOrWhiteSpace(starter.code) &&
                    !string.IsNullOrWhiteSpace(starter.categoryCode))
                {
                    starter.equipped = true;
                    response = new ArsenalResponse { weapons = new[] { starter } };
                }
            }

            inventoryLoaded = response?.weapons != null;
            if (!inventoryLoaded)
            {
                ShowLoadError(ArsenalApi.LoadFailed, retry);
                yield break;
            }
            weapons = response.weapons;
            if (selected != null)
                selected = Array.Find(weapons, item => item != null && Matches(item.code, selected.code));
            if (selected == null)
                statsView.Render(null);
            FinishLoad();

            if (selectFirstOwnedCategory && preferredCode != null)
            {
                var preferred = Array.Find(weapons, weapon => weapon != null && Matches(weapon.code, preferredCode));
                if (preferred != null)
                {
                    var entry = Array.Find(WeaponCategoryIcons.Tabs, item => Matches(item.Code, preferred.categoryCode));
                    category = entry.Code ?? category;
                }
            }

            if (selectFirstOwnedCategory && !Array.Exists(weapons, weapon =>
                    weapon != null && Matches(weapon.categoryCode, category)))
            {
                var firstCategory = Array.Find(WeaponCategoryIcons.Tabs, entry => Array.Exists(weapons, weapon =>
                    weapon != null && Matches(weapon.categoryCode, entry.Code)));
                category = firstCategory.Code ?? category;
            }

            RenderCategory(preferredCode);
        }

        private void ClearRejectedSession()
        {
            token = null;
            balancePanel.SetActive(false);
            ProfileLoader.ClearInventorySession();
        }

        private IEnumerator LoadBalance(Action<bool> completed)
        {
            using var request = UnityWebRequest.Get(apiBaseUrl + "/profile");
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = 15;
            yield return Send(request);
            if (request.responseCode == 401)
                ClearRejectedSession();

            var profile = ReadSuccess<Profile>(request);
            balancePanel.SetActive(profile != null);
            if (profile != null)
                balance.text = profile.coins.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ');
            completed(profile != null);
        }

        private void RenderCategory(string preferredCode)
        {
            foreach (var tab in tabs)
                tab.Highlight.SetActive(tab.Category == category);

            foreach (var card in cards)
            {
                card.Button.gameObject.SetActive(false);
                Destroy(card.Button.gameObject);
            }

            cards.Clear();
            foreach (var weapon in weapons)
            {
                if (weapon == null || string.IsNullOrWhiteSpace(weapon.code) ||
                    !Matches(weapon.categoryCode, category))
                    continue;

                var card = Instantiate(template, scroll.content);
                card.name = weapon.code;
                var label = card.transform.Find("Weapon Label").GetComponent<Text>();
                label.supportRichText = false;
                label.text = weapon.name;
                SetIcon(card.transform.Find("Thumbnail").GetComponent<Image>(), weapon.code);
                card.onClick.AddListener(() =>
                {
                    if (!ReferenceEquals(weapon, selected))
                        Select(weapon);
                });
                cards.Add((card, weapon));
                card.gameObject.SetActive(true);
            }

            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;

            var entry = cards.Find(card => Matches(card.Weapon.code, preferredCode) ||
                (preferredCode == null && card.Weapon.equipped));
            if (cards.Count > 0)
                Select(entry.Weapon ?? cards[0].Weapon);
            else
                RenderSelection();
            SetMessage(cards.Count == 0 ? "No weapons owned" : null);
        }

        private void Select(ArsenalWeapon weapon)
        {
            if (busy)
                return;

            CancelDetails();
            selected = weapon;
            RenderSelection();
            statsView.Render(null);
            if (weapon != null)
            {
                BeginLoad();
                detailRequest = StartCoroutine(LoadDetails(weapon));
            }
        }

        private IEnumerator LoadDetails(ArsenalWeapon weapon)
        {
            var request = UnityWebRequest.Get(apiBaseUrl + "/weapons/" + Uri.EscapeDataString(weapon.code));
            request.timeout = 15;
            activeDetails = request;
            yield return Send(request);
            activeDetails = null;
            detailRequest = null;
            Details details;
            using (request)
                details = ReadSuccess<Details>(request);

            if (details?.stats == null)
            {
                ShowLoadError("Couldn't load weapon details. Please try again.", () => Select(weapon));
                yield break;
            }

            statsView.Render(details.stats);
            FinishLoad();
            RenderSelection();
        }

        private void CancelDetails()
        {
            if (detailRequest != null)
            {
                StopCoroutine(detailRequest);
                detailRequest = null;
            }

            if (activeDetails != null)
            {
                activeDetails.Abort();
                activeDetails.Dispose();
                activeDetails = null;
            }
        }

        private void Equip()
        {
            if (!busy && !string.IsNullOrEmpty(token) && selected != null && !selected.equipped)
                StartCoroutine(EquipSelected(selected));
        }

        private IEnumerator EquipSelected(ArsenalWeapon weapon)
        {
            SetBusy(true);
            RenderSelection();
            EquipResponse result;
            string error;

            using (var request = ArsenalApi.CreateEquipRequest(apiBaseUrl, token, weapon.code))
            {
                yield return Send(request);
                result = ArsenalApi.ReadEquipResponse(request);
                if (request.result != UnityWebRequest.Result.Success || result == null ||
                    !result.equipped || !Matches(result.code, weapon.code) ||
                    !Matches(result.categoryCode, weapon.categoryCode))
                {
                    error = request.responseCode == 401
                        ? ArsenalApi.SignInRequired
                        : ArsenalApi.GetEquipError(result?.error);
                    if (request.responseCode == 401)
                        ClearRejectedSession();
                    result = null;
                }
                else
                {
                    error = null;
                }
            }

            if (result == null)
            {
                // Leave reloading the server state to Retry, since a failed response may follow a successful save.
                ShowLoadError(error, () => StartCoroutine(Load(weapon.code)));
                yield break;
            }

            foreach (var item in weapons)
            {
                if (item != null && Matches(item.categoryCode, result.categoryCode))
                    item.equipped = Matches(item.code, result.code);
            }

            SetBusy(false);
            RenderSelection();
        }

        private void RenderSelection()
        {
            var title = page.Find("Weapon Name").GetComponent<Text>();
            title.supportRichText = false;
            title.text = selected?.name ?? "";
            SetIcon(page.Find("Weapon Preview").GetComponent<Image>(), selected?.code);

            foreach (var (button, weapon) in cards)
            {
                button.transform.Find("Selected").gameObject.SetActive(ReferenceEquals(weapon, selected));
                button.transform.Find("Equipped Badge").gameObject.SetActive(weapon.equipped);
            }

            canEquip.SetActive(!busy && !string.IsNullOrEmpty(token) && selected != null && !selected.equipped);
            states.Find("Equipped").gameObject.SetActive(!busy && selected?.equipped == true);
            states.Find("Spinner").gameObject.SetActive(busy && selected != null);

            var current = Array.Find(weapons, item => item != null && item.equipped &&
                selected != null && Matches(item.categoryCode, selected.categoryCode) && item != selected);
            replacementHint.supportRichText = false;
            replacementHint.gameObject.SetActive(current != null);
            if (current != null)
            {
                var tab = Array.Find(WeaponCategoryIcons.Tabs, entry => Matches(entry.Code, selected.categoryCode));
                replacementHint.text = $"Replaces {current.name} in the {tab.Tab} slot";
            }
        }

        private void SetBusy(bool value)
        {
            busy = value;
            back.interactable = !value;
            foreach (var tab in tabs)
                tab.Button.interactable = !value;
        }

        private void CreateLoadError()
        {
            loadError = Instantiate(message, transform, false);
            loadError.name = "Load Error";
            loadError.alignment = TextAnchor.MiddleCenter;
            loadError.raycastTarget = false;
            Center(loadError.rectTransform, new Vector2(640f, 48f), 24f);
            loadError.gameObject.SetActive(false);

            retryButton = Instantiate(equip, transform, false);
            retryButton.name = "Retry";
            retryButton.transform.Find("Label").GetComponent<Text>().text = "Retry";
            retryButton.OnClick.AddListener(() => retryLoad?.Invoke());
            Center((RectTransform)retryButton.transform, new Vector2(160f, 48f), -34f);
            retryButton.gameObject.SetActive(false);
        }

        private static void Center(RectTransform rect, Vector2 size, float y)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(0f, y);
        }

        private void BeginLoad()
        {
            retryLoad = null;
            loadError.gameObject.SetActive(false);
            retryButton.gameObject.SetActive(false);
            categoryBar.SetActive(false);
            categoryPages.SetActive(false);
            spinner.SetActive(true);
            SetBusy(true);
        }

        private void FinishLoad()
        {
            spinner.SetActive(false);
            categoryBar.SetActive(true);
            categoryPages.SetActive(true);
            SetBusy(false);
        }

        private void ShowLoadError(string error, Action retry)
        {
            spinner.SetActive(false);
            categoryBar.SetActive(false);
            categoryPages.SetActive(false);
            SetBusy(false);
            retryLoad = retry;
            loadError.text = error;
            loadError.gameObject.SetActive(true);
            retryButton.gameObject.SetActive(true);
        }

        private void SetMessage(string text)
        {
            message.text = text ?? "";
            message.gameObject.SetActive(text != null);
        }

        private void SetIcon(Image image, string code)
        {
            var icon = weaponAssets?.GetIcon(code);
            image.sprite = icon;
            image.enabled = icon != null;
        }

        private static bool Matches(string first, string second) =>
            string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

        private static UnityWebRequestAsyncOperation Send(UnityWebRequest request)
        {
            try
            {
                return request.SendWebRequest();
            }
            catch (InvalidOperationException)
            {
                // Unity can reject an insecure connection before sending.
                return null;
            }
        }

        private static T ReadSuccess<T>(UnityWebRequest request) where T : class
        {
            if (request.result != UnityWebRequest.Result.Success)
                return null;

            try
            {
                return JsonUtility.FromJson<T>(request.downloadHandler.text);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
