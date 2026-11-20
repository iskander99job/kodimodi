using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Kodimodi.UI.Profile
{
    public class WeaponShop : MonoBehaviour
    {
        [SerializeField] private string apiBaseUrl = "http://localhost:5028";
        [SerializeField] private RectTransform weaponCardPrefab;
        [SerializeField] private WeaponIcon[] weaponIcons = Array.Empty<WeaponIcon>();

        private static readonly (string Tab, string Code, string Title)[] Categories =
        {
            ("AR_Tab", "AR", "ASSAULT RIFLES"),
            ("PISTOL_Tab", "PST", "PISTOLS"),
            ("SMG_Tab", "SMG", "SUBMACHINE GUNS"),
            ("SHOTGUN_Tab", "SG", "SHOTGUNS"),
            ("MG_Tab", "MG", "MACHINE GUNS"),
            ("SR_Tab", "SR", "SNIPER RIFLES"),
            ("GL_Tab", "GL", "GRENADE LAUNCHERS")
        };

        private readonly List<GameObject> cards = new List<GameObject>();
        private readonly Dictionary<string, Sprite> icons =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private ScrollRect scrollView;
        private TMP_Text sectionTitle;
        private TMP_Text modelCount;
        private WeaponDetails weaponDetails;
        private int selectedCategory;
        private Coroutine loadRoutine;
        private UnityWebRequest activeRequest;

        [Serializable]
        private struct WeaponIcon
        {
            public string code;
            public Sprite sprite;
        }

        [Serializable]
        private sealed class CatalogResponse
        {
            public CatalogWeapon[] weapons;
        }

        [Serializable]
        private sealed class CatalogWeapon
        {
            public string code;
            public string name;
            public int price;
            public RankRequirement minRank;
        }

        [Serializable]
        private sealed class RankRequirement
        {
            public string englishName;
            public int minXp;
        }

        internal static string GetCategoryTitle(string code)
        {
            foreach (var category in Categories)
            {
                if (category.Code == code)
                    return category.Title;
            }

            return code ?? string.Empty;
        }

        private void Awake()
        {
            scrollView = FindRequired<ScrollRect>(transform, "WeaponsScrollView");
            sectionTitle = FindRequired<TMP_Text>(transform, "SectionHeading/SectionTitle");
            modelCount = FindRequired<TMP_Text>(transform, "SectionHeading/ModelCount");
            weaponDetails = FindRequired<WeaponDetails>(transform.parent, "WeaponDetailsUI");

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
                        LoadSelectedCategory();
                });
            }
        }

        private void OnEnable()
        {
            LoadSelectedCategory();
        }

        private void OnDisable()
        {
            CancelRequest();
        }

        public void Open()
        {
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void LoadSelectedCategory()
        {
            CancelRequest();
            ClearCards();

            var category = Categories[selectedCategory];
            sectionTitle.text = category.Title;
            modelCount.text = "Loading…";

            if (weaponCardPrefab == null || scrollView.content == null)
            {
                ShowLoadError("The shop card prefab or scroll content is missing.");
                return;
            }

            string baseUrl = apiBaseUrl.Trim().TrimEnd('/');

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                ShowLoadError("The API address is invalid.");
                return;
            }

            string url = baseUrl + "/weapons?category=" +
                UnityWebRequest.EscapeURL(category.Code);

            var request = UnityWebRequest.Get(url);
            request.timeout = 15;

            UnityWebRequestAsyncOperation operation;

            try
            {
                operation = request.SendWebRequest();
            }
            catch (InvalidOperationException)
            {
                request.Dispose();
                ShowLoadError("Couldn't connect to the shop.");
                return;
            }

            activeRequest = request;
            loadRoutine = StartCoroutine(ReadCatalog(request, operation, baseUrl));
        }

        private IEnumerator ReadCatalog(
            UnityWebRequest request,
            UnityWebRequestAsyncOperation operation,
            string baseUrl)
        {
            yield return operation;

            loadRoutine = null;
            activeRequest = null;

            using (request)
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    ShowLoadError("Couldn't load weapons. Please try again.");
                    yield break;
                }

                var catalog = ParseCatalog(request.downloadHandler.text);

                if (catalog?.weapons == null)
                {
                    ShowLoadError("The shop returned invalid data.");
                    yield break;
                }

                ShowWeapons(catalog.weapons, baseUrl);
            }
        }

        private void ShowWeapons(CatalogWeapon[] weapons, string baseUrl)
        {
            foreach (var weapon in weapons)
            {
                if (weapon == null || string.IsNullOrWhiteSpace(weapon.code))
                    continue;

                var card = Instantiate(weaponCardPrefab, scrollView.content, false);

                cards.Add(card.gameObject);
                SetCard(card, weapon, baseUrl);
                card.gameObject.SetActive(true);
            }

            modelCount.text = cards.Count == 1 ? "1 model" : $"{cards.Count} models";

            Canvas.ForceUpdateCanvases();
            scrollView.StopMovement();
            scrollView.verticalNormalizedPosition = 1f;
        }

        private void SetCard(RectTransform card, CatalogWeapon weapon, string baseUrl)
        {
            card.name = weapon.code;

            FindRequired<TMP_Text>(card, "WeaponName").text = weapon.name;
            FindRequired<TMP_Text>(card, "Price/Amount").text = weapon.price
                .ToString("N0", CultureInfo.InvariantCulture)
                .Replace(',', ' ');

            var icon = FindRequired<Image>(card, "Weapon_Icon");

            icons.TryGetValue(weapon.code, out var sprite);
            icon.sprite = sprite;
            icon.enabled = sprite != null;

            var requirement = FindRequired<Transform>(card, "RankRequirement");
            bool hasRankRequirement = weapon.minRank != null && weapon.minRank.minXp > 0;

            if (hasRankRequirement)
            {
                FindRequired<TMP_Text>(requirement, "RequiredRank").text =
                    $"{weapon.minRank.englishName} required";
            }

            requirement.gameObject.SetActive(hasRankRequirement);

            card.GetComponent<LeanButton>().OnClick.AddListener(() =>
                weaponDetails.Open(baseUrl, weapon.code, sprite));
        }

        private void ClearCards()
        {
            foreach (var card in cards)
            {
                card.SetActive(false);
                Destroy(card);
            }

            cards.Clear();
        }

        private void CancelRequest()
        {
            if (loadRoutine != null)
            {
                StopCoroutine(loadRoutine);
                loadRoutine = null;
            }

            if (activeRequest != null)
            {
                activeRequest.Abort();
                activeRequest.Dispose();
                activeRequest = null;
            }
        }

        private void ShowLoadError(string message)
        {
            modelCount.text = "0 models";
            ErrorPopup.Show(message, "Shop Error");
        }

        private static CatalogResponse ParseCatalog(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonUtility.FromJson<CatalogResponse>(json);
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
                $"WeaponShop: expected {typeof(T).Name} at '{root.name}/{path}'.");
        }
    }
}