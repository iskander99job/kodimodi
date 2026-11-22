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
        [SerializeField] private WeaponIcon[] weaponIcons = Array.Empty<WeaponIcon>();

        private static readonly (string Tab, string Code)[] Categories =
        {
            ("AR", "AR"), ("PISTOL", "PST"), ("SMG", "SMG"),
            ("SHOTGUN", "SG"), ("MG", "MG"), ("SR", "SR")
        };

        private readonly List<(Button Button, GameObject Highlight, string Category)> tabs = new();
        private readonly List<(Button Button, ArsenalWeapon Weapon)> cards = new();
        private ArsenalWeapon[] weapons = Array.Empty<ArsenalWeapon>();

        private Transform page;
        private Transform compactBar;
        private Transform fullBar;
        private Transform states;
        private GameObject spinner;
        private GameObject canEquip;
        private LeanButton equip;
        private LeanButton back;
        private Text replacementHint;
        private Text message;
        private TMP_Text balance;
        private ScrollRect scroll;
        private Button template;
        private string category = "AR";
        private string apiBaseUrl;
        private string token;
        private ArsenalWeapon selected;
        private bool busy;
        private bool inventoryLoaded;
        private Coroutine detailRequest;
        private UnityWebRequest activeDetails;

        [Serializable]
        private struct WeaponIcon
        {
            public string code;
            public Sprite sprite;
        }

        [Serializable] private sealed class Profile { public int coins; }
        [Serializable] private sealed class Details { public Stat[] stats; }
        [Serializable] private sealed class Stat { public string statCode; public int value; }

        private void Awake()
        {
            var pages = transform.Find("Category Pages");
            page = pages.Find("PISTOL Page");
            foreach (Transform child in pages)
                child.gameObject.SetActive(child == page);

            compactBar = transform.Find("Categories/Compact");
            fullBar = transform.Find("Categories/Full Width");
            BindTabs(compactBar);
            BindTabs(fullBar);

            states = page.Find("Equip States");
            canEquip = states.Find("Can Equip").gameObject;
            equip = canEquip.transform.Find("Equip Button").GetComponent<LeanButton>();
            equip.OnClick.AddListener(Equip);
            replacementHint = canEquip.transform.Find("Replacement Hint").GetComponent<Text>();
            back = transform.Find("Header/Back").GetComponent<LeanButton>();
            back.OnClick.AddListener(() => SceneManager.LoadScene("ProfileScene"));
            balance = transform.Find("Header/BalancePanel/BalanceText").GetComponent<TMP_Text>();
            balance.text = "—";

            spinner = transform.Find("Spinner").gameObject;
            scroll = page.Find("Weapon Scroll View").GetComponent<ScrollRect>();
            scroll.transform.Find("Spinner").gameObject.SetActive(false);
            template = scroll.content.Find("P-12 Card").GetComponent<Button>();
            foreach (Transform card in scroll.content)
                card.gameObject.SetActive(false);

            message = Instantiate(pages.Find("AR Page/Empty Message"), page).GetComponent<Text>();
            message.gameObject.SetActive(false);
            RenderSelection();
            RenderStats(null);
        }

        private void Start()
        {
            if (!ProfileLoader.TryGetShopSession(out apiBaseUrl, out token))
            {
                spinner.SetActive(false);
                SetMessage(ArsenalApi.SignInRequired);
                return;
            }

            StartCoroutine(Load(selectFirstOwnedCategory: true));
            StartCoroutine(LoadBalance());
        }

        private void OnDisable()
        {
            CancelDetails();
        }

        private void BindTabs(Transform bar)
        {
            foreach (var (name, code) in Categories)
            {
                var tab = bar.Find(name + " Tab");
                if (tab == null)
                    continue;

                var button = tab.GetComponent<Button>();
                tabs.Add((button, tab.Find("Selected").gameObject, code));
                button.onClick.AddListener(() =>
                {
                    if (busy || string.IsNullOrEmpty(token))
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
            SetBusy(true);
            RenderSelection();
            spinner.SetActive(true);
            SetMessage(null);

            using var request = ArsenalApi.CreateRequest(apiBaseUrl, token);
            yield return Send(request);
            var response = ArsenalApi.ReadResponse(request);
            inventoryLoaded = response?.weapons != null;
            weapons = response?.weapons ?? Array.Empty<ArsenalWeapon>();
            spinner.SetActive(false);
            SetBusy(false);

            bool hasAdditionalCategories = Array.Exists(weapons, weapon =>
                weapon != null && (Matches(weapon.categoryCode, "MG") || Matches(weapon.categoryCode, "SR")));
            compactBar.gameObject.SetActive(!hasAdditionalCategories);
            fullBar.gameObject.SetActive(hasAdditionalCategories);
            if (!hasAdditionalCategories && (category == "MG" || category == "SR"))
                category = "AR";

            if (selectFirstOwnedCategory && !Array.Exists(weapons, weapon =>
                    weapon != null && Matches(weapon.categoryCode, category)))
            {
                var firstCategory = Array.Find(Categories, entry => Array.Exists(weapons, weapon =>
                    weapon != null && Matches(weapon.categoryCode, entry.Code)));
                category = firstCategory.Code ?? category;
            }

            RenderCategory(preferredCode);
            if (response?.weapons == null)
                SetMessage(request.responseCode == 401 ? ArsenalApi.SignInRequired : ArsenalApi.LoadFailed);
        }

        private IEnumerator LoadBalance()
        {
            using var request = UnityWebRequest.Get(apiBaseUrl + "/profile");
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = 15;
            yield return Send(request);
            var profile = ReadSuccess<Profile>(request);
            if (profile != null)
                balance.text = profile.coins.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ');
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
                card.onClick.AddListener(() => Select(weapon));
                cards.Add((card, weapon));
                card.gameObject.SetActive(true);
            }

            var entry = cards.Find(card => Matches(card.Weapon.code, preferredCode) ||
                (preferredCode == null && card.Weapon.equipped));
            Select(entry.Weapon ?? (cards.Count > 0 ? cards[0].Weapon : null));
            SetMessage(cards.Count == 0 ? "No weapons owned" : null);
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }

        private void Select(ArsenalWeapon weapon)
        {
            if (busy)
                return;

            CancelDetails();
            selected = weapon;
            RenderSelection();
            RenderStats(null);
            if (weapon != null)
                detailRequest = StartCoroutine(LoadDetails(weapon));
        }

        private IEnumerator LoadDetails(ArsenalWeapon weapon)
        {
            var request = UnityWebRequest.Get(apiBaseUrl + "/weapons/" + Uri.EscapeDataString(weapon.code));
            request.timeout = 15;
            activeDetails = request;
            yield return Send(request);
            activeDetails = null;
            detailRequest = null;
            using (request)
                RenderStats(ReadSuccess<Details>(request)?.stats);
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
            if (!busy && selected != null && !selected.equipped)
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
                    result = null;
                }
                else
                {
                    error = null;
                }
            }

            if (result == null)
            {
                // A lost response can follow a successful save on the server.
                yield return Load(weapon.code);
                if (selected != null && !selected.equipped && Matches(selected.code, weapon.code))
                {
                    replacementHint.text = error;
                    replacementHint.gameObject.SetActive(true);
                }
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

            canEquip.SetActive(!busy && selected != null && !selected.equipped);
            states.Find("Equipped").gameObject.SetActive(!busy && selected?.equipped == true);
            states.Find("Spinner").gameObject.SetActive(busy && selected != null);

            var current = Array.Find(weapons, item => item != null && item.equipped &&
                selected != null && Matches(item.categoryCode, selected.categoryCode) && item != selected);
            replacementHint.supportRichText = false;
            replacementHint.gameObject.SetActive(current != null);
            if (current != null)
            {
                var tab = Array.Find(Categories, entry => entry.Code == category);
                replacementHint.text = $"Replaces {current.name} in the {tab.Tab} slot";
            }
        }

        private void RenderStats(Stat[] stats)
        {
            RenderStat(stats, "DAMAGE", "DAMAGE");
            RenderStat(stats, "FIRE RATE", "FIRE_RATE");
            RenderStat(stats, "MAGAZINE", "MAGAZINE_SIZE");
            RenderStat(stats, "RELOAD", "RELOAD");
        }

        private void RenderStat(Stat[] stats, string rowName, string code)
        {
            var row = page.Find(rowName);
            var stat = stats == null ? null : Array.Find(stats, item => item?.statCode == code);
            row.gameObject.SetActive(stat != null);
            if (stat != null)
                row.Find("Value").GetComponent<Text>().text = stat.value.ToString(CultureInfo.InvariantCulture);
        }

        private void SetBusy(bool value)
        {
            busy = value;
            back.interactable = !value;
            foreach (var tab in tabs)
                tab.Button.interactable = !value;
        }

        private void SetMessage(string text)
        {
            message.text = text ?? "";
            message.gameObject.SetActive(text != null);
        }

        private void SetIcon(Image image, string code)
        {
            var icon = code == null ? default : Array.Find(weaponIcons, entry => Matches(entry.code, code));
            image.sprite = icon.sprite;
            image.enabled = icon.sprite != null;
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
