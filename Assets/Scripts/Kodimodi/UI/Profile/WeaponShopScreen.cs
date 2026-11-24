using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Kodimodi.Errors;
using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kodimodi.UI.Profile
{
    public sealed class WeaponShopScreen : MonoBehaviour
    {
        [SerializeField] private string apiBaseUrl = "http://localhost:5028";
        [SerializeField] private WeaponAssetsCatalog weaponAssets;

        [SerializeField] private ShopRankIcons rankIcons;

        private readonly List<WeaponCard> cards = new();
        private readonly HashSet<string> owned = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<(Button Button, GameObject Selected, string Code)> tabs = new();

        [SerializeField] private WeaponCategoryIcons categoryIcons;

        private Transform page;
        private WeaponStatsView statsView;
        private GameObject categoryBar;
        private GameObject categoryPages;
        private GameObject loadingSpinner;
        private Text centerMessage;
        private LeanButton retryButton;
        private Action retryLoad;
        private Transform states;
        private ScrollRect scroll;
        private Button template;
        private Sprite selectedCardSprite;
        private RectTransform priceBadgeTemplate;
        private RectTransform ownedBadgeTemplate;
        private LeanButton back;
        private LeanButton buy;
        private LeanButton viewInArsenal;
        private TMP_Text balance;
        private GameObject balancePanel;
        private Text message;
        private string token;
        private string guestStarterCode;
        private string category;
        private Weapon selected;
        private string selectedCode;
        private RankProfile profile;
        private bool buying;
        private UnityWebRequest activeRequest;
        private Coroutine pending;

        [Serializable] private sealed class Catalog { public Weapon[] weapons; }
        [Serializable] private sealed class RankProfile { public int coins; public int xp; }
        [Serializable] private sealed class Rank
        {
            public string code;
            public string englishName;
            public int minXp;
        }
        [Serializable] private sealed class Purchase
        {
            public string code;
            public int coins;
            public string error;
        }

        [Serializable]
        private sealed class Weapon
        {
            public string code;
            public string name;
            public string categoryCode;
            public int price;
            public Rank minRank;
            public WeaponStatValue[] stats;
        }

        private sealed class WeaponCard
        {
            public Button Button;
            public Weapon Weapon;
            public Image Background;
            public GameObject PriceBadge;
            public GameObject OwnedBadge;
            public Text RequiredRank;
        }

        private void Awake()
        {
            var pages = transform.Find("Category Pages");
            categoryPages = pages.gameObject;
            page = pages.Find("PISTOL Page");
            statsView = new WeaponStatsView(page, ProfileLoader.UseRussianInventoryStats);
            foreach (Transform child in pages)
                child.gameObject.SetActive(child == page);

            categoryBar = transform.Find("Categories").gameObject;
            categoryBar.transform.Find("Full Width").gameObject.SetActive(false);
            var compactBar = categoryBar.transform.Find("Compact");
            compactBar.gameObject.SetActive(true);
            foreach (var (name, code) in WeaponCategoryIcons.Tabs)
            {
                var tab = compactBar.Find(name + " Tab");
                categoryIcons?.Apply(tab, code);
                var button = tab.GetComponent<Button>();
                tabs.Add((button, tab.Find("Selected").gameObject, code));
                button.onClick.AddListener(() => SelectCategory(code));
            }

            scroll = page.Find("Weapon Scroll View").GetComponent<ScrollRect>();
            template = scroll.content.Find("P-22 Card").GetComponent<Button>();
            var priceCard = scroll.content.Find("P-07 Card");
            selectedCardSprite = priceCard.GetComponent<Image>().sprite;
            priceBadgeTemplate = (RectTransform)priceCard.Find("Status Badge");
            ownedBadgeTemplate = (RectTransform)scroll.content.Find("P-12 Card/Status Badge");
            foreach (Transform mockCard in scroll.content)
                mockCard.gameObject.SetActive(false);

            // Reuse the empty state from the assault rifle mockup on the shared page.
            message = Instantiate(pages.Find("AR Page/Empty Message"), page).GetComponent<Text>();
            states = page.Find("Purchase States");
            buy = states.Find("Available").GetComponent<LeanButton>();
            viewInArsenal = states.Find("Owned").GetComponent<LeanButton>();
            viewInArsenal.OnClick.AddListener(ViewInArsenal);
            buy.transform.Find("Label").GetComponent<Text>().text = "BUY";
            buy.transform.Find("Coin Icon").gameObject.SetActive(false);
            back = transform.Find("Header/Back").GetComponent<LeanButton>();
            back.OnClick.AddListener(ReturnToProfile);
            balancePanel = transform.Find("Header/BalancePanel").gameObject;
            balance = balancePanel.transform.Find("BalanceText").GetComponent<TMP_Text>();
            balancePanel.transform.Find("AddCurrencyButton").GetComponent<LeanButton>()
                .OnClick.AddListener(CoinShopScreen.Open);
            balancePanel.SetActive(false);
            loadingSpinner = transform.Find("Spinner").gameObject;
            loadingSpinner.SetActive(false);
            CreateLoadError();
            buy.OnClick.AddListener(Buy);
            scroll.transform.Find("Spinner").gameObject.SetActive(false);
        }

        private void Start()
        {
            if (ProfileLoader.TryGetShopSession(out var sessionUrl, out token))
                apiBaseUrl = sessionUrl;

            UpdateBalance();
            apiBaseUrl = apiBaseUrl.Trim().TrimEnd('/');
            SelectCategory("pistol");
        }

        private void OnDisable()
        {
            CancelPending();
        }

        private void SelectCategory(string code)
        {
            if (buying)
                return;

            CancelPending();
            category = code;
            foreach (var tab in tabs)
                tab.Selected.SetActive(tab.Code == code);

            foreach (var card in cards)
            {
                card.Button.gameObject.SetActive(false);
                Destroy(card.Button.gameObject);
            }

            cards.Clear();
            RenderSelection();
            SetMessage(null);
            BeginLoad();
            Run(LoadCatalog());
        }

        private IEnumerator LoadCatalog()
        {
            Action retry = () => SelectCategory(category);
            if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var url) ||
                (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
            {
                ShowLoadError("The API address is invalid.", retry);
                yield break;
            }

            if (!string.IsNullOrEmpty(token))
            {
                bool loaded = false;
                yield return LoadAccount(() => loaded = true);
                if (!loaded && !string.IsNullOrEmpty(token))
                {
                    ShowLoadError("Couldn't load the shop. Please try again.", retry);
                    yield break;
                }
            }

            if (string.IsNullOrEmpty(token) && guestStarterCode == null)
            {
                yield return Send(UnityWebRequest.Get(apiBaseUrl + "/weapons/starter"),
                    request => guestStarterCode = ReadSuccess<ArsenalWeapon>(request)?.code);

                if (string.IsNullOrWhiteSpace(guestStarterCode))
                {
                    guestStarterCode = null;
                    ShowLoadError("Couldn't load the shop. Please try again.", retry);
                    yield break;
                }

                if (IsGuestStarter(selected?.code))
                {
                    selected = null;
                    selectedCode = null;
                }
            }

            Catalog catalog = null;
            yield return Send(UnityWebRequest.Get(apiBaseUrl + "/weapons"),
                request => catalog = ReadSuccess<Catalog>(request));

            if (catalog?.weapons == null)
            {
                ShowLoadError("Couldn't load weapons. Please try again.", retry);
                yield break;
            }

            Weapon first = null;
            bool hasCatalogWeapons = false;
            bool hasUnownedWeapons = false;
            foreach (var weapon in catalog.weapons)
            {
                if (weapon == null || string.IsNullOrWhiteSpace(weapon.code) || IsGuestStarter(weapon.code))
                    continue;

                hasCatalogWeapons = true;
                if (owned.Contains(weapon.code))
                    continue;

                hasUnownedWeapons = true;
                if (!string.Equals(weapon.categoryCode, category, StringComparison.OrdinalIgnoreCase))
                    continue;

                first ??= weapon;
                CreateCard(weapon);
            }

            if (!hasUnownedWeapons)
            {
                ShowEmptyCatalog(hasCatalogWeapons);
                yield break;
            }

            SetMessage(first == null ? "No weapons in this category." : null);
            if (first == null)
            {
                RenderSelection();
                FinishLoad();
            }
            else
                yield return LoadDetails(first);

            if (categoryPages.activeSelf)
                ResetScroll();
        }

        private bool IsGuestStarter(string code)
        {
            return guestStarterCode != null && string.IsNullOrEmpty(token) &&
                string.Equals(guestStarterCode, code, StringComparison.OrdinalIgnoreCase);
        }

        private void CreateCard(Weapon weapon)
        {
            var button = Instantiate(template, scroll.content);
            button.name = weapon.code;
            button.transition = Selectable.Transition.None;

            var label = button.transform.Find("Weapon Label").GetComponent<Text>();
            label.text = weapon.name;
            label.supportRichText = false;
            SetWeaponImage(button.transform.Find("Thumbnail").GetComponent<Image>(), weaponAssets?.GetIcon(weapon.code));

            var rank = button.transform.Find("Required Rank").GetComponent<Text>();
            rank.text = weapon.minRank?.englishName ?? "";
            rank.supportRichText = false;
            rank.resizeTextForBestFit = true;
            rank.resizeTextMinSize = 10;
            rank.resizeTextMaxSize = rank.fontSize;

            var originalBadge = button.transform.Find("Status Badge");
            originalBadge.gameObject.SetActive(false);
            int badgeIndex = originalBadge.GetSiblingIndex();
            Destroy(originalBadge.gameObject);

            var priceBadge = Instantiate(priceBadgeTemplate, button.transform, false);
            priceBadge.SetSiblingIndex(badgeIndex);
            priceBadge.Find("Status Label").GetComponent<Text>().text = FormatAmount(weapon.price);

            var ownedBadge = Instantiate(ownedBadgeTemplate, button.transform, false);
            ownedBadge.SetSiblingIndex(badgeIndex);

            var card = new WeaponCard
            {
                Button = button,
                Weapon = weapon,
                Background = button.GetComponent<Image>(),
                PriceBadge = priceBadge.gameObject,
                OwnedBadge = ownedBadge.gameObject,
                RequiredRank = rank
            };
            cards.Add(card);
            RenderCard(card);
            button.onClick.AddListener(() =>
            {
                if (!string.Equals(weapon.code, selectedCode, StringComparison.OrdinalIgnoreCase))
                    SelectWeapon(weapon);
            });
            button.gameObject.SetActive(true);
        }

        private void RenderCard(WeaponCard card)
        {
            bool isOwned = owned.Contains(card.Weapon.code);
            bool isSelected = string.Equals(card.Weapon.code, selectedCode, StringComparison.OrdinalIgnoreCase);
            bool locked = !isOwned && IsRankLocked(card.Weapon);
            card.Background.sprite = isSelected ? selectedCardSprite : template.image.sprite;
            card.RequiredRank.gameObject.SetActive(locked && !string.IsNullOrEmpty(card.RequiredRank.text));
            card.PriceBadge.SetActive(!isOwned);
            card.OwnedBadge.SetActive(isOwned);
            card.Button.transform.Find("Lock Icon").gameObject.SetActive(locked);
        }

        private bool IsRankLocked(Weapon weapon)
        {
            return weapon?.minRank != null && profile != null && profile.xp < weapon.minRank.minXp;
        }

        private IEnumerator LoadAccount(Action onLoaded = null)
        {
            var request = UnityWebRequest.Get(apiBaseUrl + "/profile");
            request.SetRequestHeader("Authorization", "Bearer " + token);
            yield return Send(request, result =>
            {
                profile = ReadSuccess<RankProfile>(result);
                if (result.responseCode == 401)
                    ClearRejectedSession();
            });
            UpdateBalance();

            if (profile == null)
                yield break;

            yield return Send(ArsenalApi.CreateRequest(apiBaseUrl, token), result =>
            {
                if (result.responseCode == 401)
                    ClearRejectedSession();

                var arsenal = ArsenalApi.ReadResponse(result);
                if (arsenal?.weapons == null)
                    return;

                owned.Clear();
                foreach (var weapon in arsenal.weapons)
                {
                    if (weapon != null && !string.IsNullOrWhiteSpace(weapon.code))
                        owned.Add(weapon.code);
                }

                onLoaded?.Invoke();
            });
        }

        private void SelectWeapon(Weapon weapon)
        {
            if (buying)
                return;

            CancelPending();
            BeginLoad();
            Run(LoadDetails(weapon));
        }

        private IEnumerator LoadDetails(Weapon weapon)
        {
            Weapon details = null;
            selectedCode = weapon.code;

            yield return Send(
                UnityWebRequest.Get(apiBaseUrl + "/weapons/" + Uri.EscapeDataString(weapon.code)),
                request => details = ReadSuccess<Weapon>(request));

            if (details == null)
            {
                ShowLoadError("Couldn't load weapon details. Please try again.", () => SelectWeapon(weapon));
                yield break;
            }

            selected = details;
            selected.code = weapon.code;
            RenderSelection();
            FinishLoad();
        }

        private void Buy()
        {
            if (buying || selected == null || profile == null || IsRankLocked(selected) ||
                owned.Contains(selected.code))
                return;

            buying = true;
            RenderSelection();
            CancelPending();
            Run(PurchaseSelected());
        }

        private IEnumerator PurchaseSelected()
        {
            var path = "/weapons/" + Uri.EscapeDataString(selected.code) + "/buy";
            var request = new UnityWebRequest(apiBaseUrl + path, UnityWebRequest.kHttpVerbPOST)
            {
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Authorization", "Bearer " + token);

            string error = null;
            bool refresh = false;
            yield return Send(request, result =>
            {
                var purchase = ReadJson<Purchase>(result.downloadHandler.text);
                if (result.result == UnityWebRequest.Result.Success && purchase?.code == selected.code)
                {
                    owned.Add(purchase.code);
                    profile.coins = purchase.coins;
                    UpdateBalance();
                }
                else if (result.responseCode == 409 && purchase?.error == WeaponPurchaseErrors.AlreadyOwned)
                {
                    owned.Add(selected.code);
                    refresh = true;
                }
                else
                {
                    error = result.responseCode == 401
                        ? WeaponPurchaseErrors.SignInRequired
                        : WeaponPurchaseErrors.GetMessage(purchase?.error);
                    if (result.responseCode == 401)
                        ClearRejectedSession();

                    refresh = result.result is UnityWebRequest.Result.ConnectionError
                        or UnityWebRequest.Result.DataProcessingError;
                }
            });

            if (refresh)
                yield return LoadAccount();

            buying = false;
            RenderSelection();
            if (error != null)
                ErrorPopup.Show(error, "Purchase Error");
        }

        private void ViewInArsenal()
        {
            if (!buying && selected != null && owned.Contains(selected.code))
                ArsenalScreen.OpenWeapon(selected.code);
        }

        private void RenderSelection()
        {
            page.Find("Weapon Name").GetComponent<Text>().text = selected?.name ?? "";
            SetWeaponImage(page.Find("Weapon Preview").GetComponent<Image>(), weaponAssets?.GetPreview(selected?.code));
            statsView.Render(selected?.stats);

            bool isOwned = selected != null && owned.Contains(selected.code);
            bool locked = IsRankLocked(selected);
            var unavailable = states.Find("Rank Locked");
            unavailable.gameObject.SetActive(selected != null && !isOwned && !buying &&
                (locked || profile == null));
            var rankIcon = unavailable.Find("Rank Icon").GetComponent<Image>();
            rankIcon.sprite = locked ? rankIcons?.Find(selected.minRank.code) : null;
            rankIcon.gameObject.SetActive(rankIcon.sprite != null);
            unavailable.Find("Label").GetComponent<Text>().text = locked
                ? selected.minRank.englishName + " required"
                : string.IsNullOrEmpty(token) ? "Sign in to purchase" : "Profile unavailable";

            viewInArsenal.gameObject.SetActive(isOwned && !buying);
            viewInArsenal.interactable = isOwned && !buying;
            states.Find("Spinner").gameObject.SetActive(buying);
            buy.gameObject.SetActive(selected != null && !isOwned && !locked && profile != null && !buying);
            foreach (var card in cards)
                RenderCard(card);

            back.interactable = !buying;
            foreach (var tab in tabs)
                tab.Button.interactable = !buying;
        }

        private static void SetWeaponImage(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private void ClearRejectedSession()
        {
            token = null;
            profile = null;
            owned.Clear();
            ProfileLoader.ClearInventorySession();
            UpdateBalance();
        }

        private void UpdateBalance()
        {
            balancePanel.SetActive(profile != null && !string.IsNullOrEmpty(token));
            if (profile != null)
                balance.text = FormatAmount(profile.coins);
        }

        private static string FormatAmount(int amount)
        {
            return amount.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ');
        }

        private void CreateLoadError()
        {
            centerMessage = Instantiate(message, transform, false);
            centerMessage.name = "Load Error";
            centerMessage.alignment = TextAnchor.MiddleCenter;
            centerMessage.raycastTarget = false;
            Center(centerMessage.rectTransform, new Vector2(640f, 48f), 24f);
            centerMessage.gameObject.SetActive(false);

            retryButton = Instantiate(buy, transform, false);
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
            centerMessage.gameObject.SetActive(false);
            retryButton.gameObject.SetActive(false);
            categoryBar.SetActive(false);
            categoryPages.SetActive(false);
            loadingSpinner.SetActive(true);
        }

        private void FinishLoad()
        {
            loadingSpinner.SetActive(false);
            categoryBar.SetActive(true);
            categoryPages.SetActive(true);
        }

        private void ShowLoadError(string error, Action retry)
        {
            loadingSpinner.SetActive(false);
            retryLoad = retry;
            centerMessage.rectTransform.anchoredPosition = new Vector2(0f, 24f);
            centerMessage.text = error;
            centerMessage.gameObject.SetActive(true);
            retryButton.gameObject.SetActive(true);
        }

        private void ShowEmptyCatalog(bool hasCatalogWeapons)
        {
            loadingSpinner.SetActive(false);
            retryButton.gameObject.SetActive(false);
            centerMessage.rectTransform.anchoredPosition = Vector2.zero;

            if (ProfileLoader.UseRussianInventoryStats)
            {
                centerMessage.text = hasCatalogWeapons
                    ? "Вы купили всё доступное оружие."
                    : "В магазине пока нет оружия.";
            }
            else
            {
                centerMessage.text = hasCatalogWeapons
                    ? "You've purchased all available weapons."
                    : "No weapons are currently available.";
            }

            centerMessage.gameObject.SetActive(true);
        }

        private void ResetScroll()
        {
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }

        private void SetMessage(string value)
        {
            message.gameObject.SetActive(value != null);
            message.text = value;
        }

        private void ReturnToProfile()
        {
            if (!buying)
                SceneManager.LoadScene("ProfileScene");
        }

        private void Run(IEnumerator routine)
        {
            pending = StartCoroutine(routine);
        }

        private IEnumerator Send(UnityWebRequest request, Action<UnityWebRequest> completed)
        {
            request.timeout = 15;
            activeRequest = request;
            UnityWebRequestAsyncOperation operation = null;
            try
            {
                operation = request.SendWebRequest();
            }
            catch (InvalidOperationException)
            {
                // Unity can reject the URL or an insecure connection before sending.
            }

            if (operation != null)
                yield return operation;

            activeRequest = null;
            using (request)
                completed(request);
        }

        private void CancelPending()
        {
            if (pending != null)
            {
                StopCoroutine(pending);
                pending = null;
            }

            if (activeRequest != null)
            {
                activeRequest.Abort();
                activeRequest.Dispose();
                activeRequest = null;
            }
        }

        private static T ReadSuccess<T>(UnityWebRequest request) where T : class
        {
            return request.result == UnityWebRequest.Result.Success
                ? ReadJson<T>(request.downloadHandler.text) : null;
        }

        private static T ReadJson<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
