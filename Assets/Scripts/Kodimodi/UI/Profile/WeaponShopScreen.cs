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
        private static readonly (string Tab, string Code)[] Categories =
        {
            ("AR", "AR"), ("PISTOL", "PST"), ("SMG", "SMG"),
            ("SHOTGUN", "SG"), ("MG", "MG"), ("SR", "SR")
        };

        [SerializeField] private string apiBaseUrl = "http://localhost:5028";
        [SerializeField] private WeaponIcon[] weaponIcons = Array.Empty<WeaponIcon>();

        private readonly List<GameObject> cards = new();
        private readonly HashSet<string> owned = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<(Button Button, GameObject Selected, string Code)> tabs = new();

        private Transform page;
        private Transform states;
        private ScrollRect scroll;
        private Button template;
        private LeanButton back;
        private LeanButton buy;
        private TMP_Text balance;
        private Text message;
        private string token;
        private string category;
        private Weapon selected;
        private RankProfile profile;
        private bool buying;
        private UnityWebRequest activeRequest;
        private Coroutine pending;

        [Serializable]
        private struct WeaponIcon
        {
            public string code;
            public Sprite sprite;
        }

        [Serializable] private sealed class Catalog { public Weapon[] weapons; }
        [Serializable] private sealed class RankProfile { public int coins; public int xp; }
        [Serializable] private sealed class Rank { public string englishName; public int minXp; }
        [Serializable] private sealed class Stat { public string statCode; public int value; }
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
            public int price;
            public Rank minRank;
            public Stat[] stats;
        }

        private void Awake()
        {
            var pages = transform.Find("Category Pages");
            page = pages.Find("PISTOL Page");
            foreach (Transform child in pages)
                child.gameObject.SetActive(child == page);

            var categoryBar = transform.Find("Categories");
            categoryBar.Find("Compact").gameObject.SetActive(false);
            var fullBar = categoryBar.Find("Full Width");
            fullBar.gameObject.SetActive(true);
            foreach (var (name, code) in Categories)
            {
                var tab = fullBar.Find(name + " Tab");
                var button = tab.GetComponent<Button>();
                tabs.Add((button, tab.Find("Selected").gameObject, code));
                button.onClick.AddListener(() => SelectCategory(code));
            }

            scroll = page.Find("Weapon Scroll View").GetComponent<ScrollRect>();
            template = scroll.content.Find("P-12 Card").GetComponent<Button>();
            foreach (Transform mockCard in scroll.content)
                mockCard.gameObject.SetActive(false);

            // Reuse the empty state from the assault rifle mockup on the shared page.
            message = Instantiate(pages.Find("AR Page/Empty Message"), page).GetComponent<Text>();
            states = page.Find("Purchase States");
            buy = states.Find("Available").GetComponent<LeanButton>();
            buy.OnClick.AddListener(Buy);
            back = transform.Find("Header/Back").GetComponent<LeanButton>();
            back.OnClick.AddListener(ReturnToProfile);
            balance = transform.Find("Header/BalancePanel/BalanceText").GetComponent<TMP_Text>();
            transform.Find("Header/BalancePanel/AddCurrencyButton").gameObject.SetActive(false);
            transform.Find("Spinner").gameObject.SetActive(false);
            scroll.transform.Find("Spinner").gameObject.SetActive(false);
        }

        private void Start()
        {
            if (ProfileLoader.TryGetShopSession(out var sessionUrl, out token))
                apiBaseUrl = sessionUrl;

            UpdateBalance();
            apiBaseUrl = apiBaseUrl.Trim().TrimEnd('/');
            if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var url) ||
                (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
            {
                SetMessage("The API address is invalid.");
                RenderSelection();
                foreach (var tab in tabs)
                    tab.Button.interactable = false;
                return;
            }

            SelectCategory("AR");
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
                card.SetActive(false);
                Destroy(card);
            }

            cards.Clear();
            selected = null;
            RenderSelection();
            SetMessage("Loading…");
            Run(LoadCatalog());
        }

        private IEnumerator LoadCatalog()
        {
            if (profile == null && !string.IsNullOrEmpty(token))
                yield return LoadAccount();

            Catalog catalog = null;
            string path = "/weapons?category=" + UnityWebRequest.EscapeURL(category);
            yield return Send(UnityWebRequest.Get(apiBaseUrl + path),
                request => catalog = ReadSuccess<Catalog>(request));

            if (catalog?.weapons == null)
            {
                SetMessage("Couldn't load weapons. Select a category to retry.");
                yield break;
            }

            Weapon first = null;
            foreach (var weapon in catalog.weapons)
            {
                if (weapon == null || string.IsNullOrWhiteSpace(weapon.code))
                    continue;

                first ??= weapon;
                var card = Instantiate(template, scroll.content);
                card.name = weapon.code;
                card.transform.Find("Weapon Label").GetComponent<Text>().text = weapon.name;
                card.transform.Find("Status Badge").gameObject.SetActive(false);
                RenderIcon(card.transform.Find("Thumbnail").GetComponent<Image>(), weapon.code);
                card.onClick.AddListener(() => SelectWeapon(weapon));
                card.gameObject.SetActive(true);
                cards.Add(card.gameObject);
            }

            SetMessage(first == null ? "No weapons in this category." : null);
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
            if (first != null)
                yield return LoadDetails(first);
        }

        private IEnumerator LoadAccount()
        {
            var request = UnityWebRequest.Get(apiBaseUrl + "/profile");
            request.SetRequestHeader("Authorization", "Bearer " + token);
            yield return Send(request, result =>
            {
                profile = ReadSuccess<RankProfile>(result);
                if (result.responseCode == 401)
                    token = null;
            });
            UpdateBalance();

            if (profile == null)
                yield break;

            yield return Send(ArsenalApi.CreateRequest(apiBaseUrl, token), result =>
            {
                var arsenal = ArsenalApi.ReadResponse(result);
                if (arsenal?.weapons == null)
                    return;

                owned.Clear();
                foreach (var weapon in arsenal.weapons)
                {
                    if (weapon != null && !string.IsNullOrWhiteSpace(weapon.code))
                        owned.Add(weapon.code);
                }
            });
        }

        private void SelectWeapon(Weapon weapon)
        {
            if (buying)
                return;

            CancelPending();
            Run(LoadDetails(weapon));
        }

        private IEnumerator LoadDetails(Weapon weapon)
        {
            selected = null;
            RenderSelection();
            page.Find("Weapon Name").GetComponent<Text>().text = "Loading…";

            yield return Send(
                UnityWebRequest.Get(apiBaseUrl + "/weapons/" + Uri.EscapeDataString(weapon.code)),
                request => selected = ReadSuccess<Weapon>(request));

            if (selected == null)
            {
                page.Find("Weapon Name").GetComponent<Text>().text =
                    "Couldn't load details. Select the weapon to retry.";
                yield break;
            }

            selected.code = weapon.code;
            RenderSelection();
        }

        private void Buy()
        {
            if (buying || selected == null || profile == null || owned.Contains(selected.code))
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
                    {
                        token = null;
                        profile = null;
                        owned.Clear();
                        UpdateBalance();
                    }

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

        private void RenderSelection()
        {
            page.Find("Weapon Name").GetComponent<Text>().text = selected?.name ?? "";
            RenderIcon(page.Find("Weapon Preview").GetComponent<Image>(), selected?.code);
            RenderStat("DAMAGE", "DAMAGE");
            RenderStat("FIRE RATE", "FIRE_RATE");
            RenderStat("MAGAZINE", "MAGAZINE_SIZE");
            RenderStat("RELOAD", "RELOAD");

            bool isOwned = selected != null && owned.Contains(selected.code);
            bool locked = selected?.minRank != null && profile != null &&
                profile.xp < selected.minRank.minXp;
            var unavailable = states.Find("Rank Locked");
            unavailable.gameObject.SetActive(selected != null && !isOwned && !buying &&
                (locked || profile == null));
            unavailable.Find("Rank Icon").gameObject.SetActive(locked);
            unavailable.Find("Label").GetComponent<Text>().text = locked
                ? selected.minRank.englishName + " required"
                : string.IsNullOrEmpty(token) ? "Sign in to purchase" : "Profile unavailable";

            states.Find("Owned").gameObject.SetActive(isOwned && !buying);
            states.Find("Spinner").gameObject.SetActive(buying);
            buy.gameObject.SetActive(selected != null && !isOwned && !locked && profile != null && !buying);
            buy.transform.Find("Label").GetComponent<Text>().text = selected == null
                ? "" : "BUY  " + FormatAmount(selected.price);

            back.interactable = !buying;
            foreach (var tab in tabs)
                tab.Button.interactable = !buying;
        }

        private void RenderStat(string rowName, string code)
        {
            var row = page.Find(rowName);
            var stat = selected?.stats == null
                ? null : Array.Find(selected.stats, item => item?.statCode == code);
            row.gameObject.SetActive(stat != null);
            if (stat != null)
                row.Find("Value").GetComponent<Text>().text = stat.value.ToString(CultureInfo.InvariantCulture);
        }

        private void RenderIcon(Image image, string code)
        {
            var icon = code == null ? default : Array.Find(weaponIcons,
                item => string.Equals(item.code, code, StringComparison.OrdinalIgnoreCase));
            image.sprite = icon.sprite;
            image.enabled = icon.sprite != null;
        }

        private void UpdateBalance()
        {
            balance.text = profile == null ? "—" : FormatAmount(profile.coins);
        }

        private static string FormatAmount(int amount)
        {
            return amount.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ');
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
