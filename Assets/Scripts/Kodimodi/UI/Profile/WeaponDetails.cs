using System;
using System.Collections;
using System.Globalization;
using Kodimodi.Errors;
using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Kodimodi.UI.Profile
{
    [RequireComponent(typeof(Image))]
    public sealed class WeaponDetails : MonoBehaviour
    {
        private Transform content;
        private Transform weaponInfo;
        private TMP_Text title;
        private LeanButton backButton;
        private LeanButton buyButton;
        private GameObject purchasePanel;
        private GameObject purchasedPanel;
        private GameObject purchaseSpinner;
        private TMP_Text priceText;
        private ProfileLoader profileLoader;

        private string selectedApiBaseUrl;
        private string selectedWeaponCode;
        private bool isBuying;
        private UnityWebRequest activeRequest;
        private Coroutine loadRoutine;

        [Serializable]
        private sealed class DetailsResponse
        {
            public string name;
            public string categoryCode;
            public int price;
            public RankRequirement minRank;
            public WeaponStat[] stats;
        }

        [Serializable]
        private sealed class RankRequirement
        {
            public string code;
            public string englishName;
        }

        [Serializable]
        private sealed class WeaponStat
        {
            public string statCode;
            public int value;
        }

        [Serializable]
        private sealed class PurchaseResponse
        {
            public string code;
            public int coins;
            public string error;
        }

        private void Awake()
        {
            content = FindRequired<Transform>(transform, "Content");
            weaponInfo = FindRequired<Transform>(content, "WeaponInfo");
            title = FindRequired<TMP_Text>(transform, "Header/Title");
            backButton = FindRequired<LeanButton>(transform, "Header/BackButton");

            var purchase = FindRequired<Transform>(weaponInfo, "Purchase");
            purchasePanel = purchase.gameObject;
            purchasedPanel = FindRequired<Transform>(weaponInfo, "Purchased").gameObject;
            purchaseSpinner = FindRequired<Transform>(purchase, "Spinner").gameObject;
            buyButton = FindRequired<LeanButton>(purchase, "BuyButton");
            priceText = FindRequired<TMP_Text>(purchase, "Price");

            profileLoader = FindAnyObjectByType<ProfileLoader>();

            GetComponent<Image>().raycastTarget = true;
            backButton.OnClick.AddListener(Close);
            buyButton.OnClick.AddListener(Buy);
        }

        private void OnDisable()
        {
            CancelRequest();
            isBuying = false;
        }

        private void OnDestroy()
        {
            if (backButton != null)
                backButton.OnClick.RemoveListener(Close);

            if (buyButton != null)
                buyButton.OnClick.RemoveListener(Buy);
        }

        public void Open(string apiBaseUrl, string code, Sprite sprite)
        {
            if (isBuying)
                return;

            CancelRequest();

            selectedApiBaseUrl = apiBaseUrl;
            selectedWeaponCode = code;

            transform.SetAsLastSibling();
            gameObject.SetActive(true);

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            title.text = "Loading…";
            content.gameObject.SetActive(false);
            backButton.interactable = true;
            UpdateBalance();

            string url = apiBaseUrl + "/weapons/" + Uri.EscapeDataString(code);
            var request = UnityWebRequest.Get(url);

            Send(
                request,
                completed => ReadDetails(completed, sprite),
                () => ShowDetailsError("Couldn't connect to the shop."));
        }

        public void Close()
        {
            if (!isBuying)
                gameObject.SetActive(false);
        }

        private void Buy()
        {
            if (isBuying || activeRequest != null)
                return;

            string accessToken = profileLoader?.GetAccessToken(selectedApiBaseUrl);

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                ErrorPopup.Show(WeaponPurchaseErrors.SignInRequired, "Purchase Error");
                return;
            }

            if (profileLoader.OwnsWeapon(selectedWeaponCode))
                return;

            isBuying = true;
            UpdatePurchaseControls();

            string url = selectedApiBaseUrl + "/weapons/" +
                Uri.EscapeDataString(selectedWeaponCode) + "/buy";

            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + accessToken);

            Send(
                request,
                completed => ReadPurchase(completed, accessToken),
                () =>
                {
                    isBuying = false;
                    UpdatePurchaseControls();
                    ErrorPopup.Show(WeaponPurchaseErrors.PurchaseFailed, "Purchase Error");
                });
        }

        private void ReadPurchase(UnityWebRequest request, string accessToken)
        {
            isBuying = false;

            if (profileLoader.GetAccessToken(selectedApiBaseUrl) != accessToken)
            {
                UpdatePurchaseControls();
                return;
            }

            if (request.responseCode == 401)
            {
                profileLoader.ClearRejectedSession();
                UpdateBalance();
                UpdatePurchaseControls();
                ErrorPopup.Show(WeaponPurchaseErrors.SignInRequired, "Purchase Error");
                return;
            }

            var response = ReadJson<PurchaseResponse>(request.downloadHandler.text);

            if (request.responseCode == 409 &&
                response?.error == WeaponPurchaseErrors.AlreadyOwned)
            {
                profileLoader.RememberOwnedWeapon(selectedWeaponCode);
                UpdatePurchaseControls();
                return;
            }

            if (request.result != UnityWebRequest.Result.Success ||
                request.responseCode != 200 ||
                response == null ||
                response.code != selectedWeaponCode)
            {
                UpdatePurchaseControls();
                ErrorPopup.Show(
                    WeaponPurchaseErrors.GetMessage(response?.error), "Purchase Error");
                return;
            }

            profileLoader.RememberOwnedWeapon(response.code);
            profileLoader.SetCoins(response.coins);
            UpdatePurchaseControls();
        }

        private void ReadDetails(UnityWebRequest request, Sprite sprite)
        {
            if (request.result != UnityWebRequest.Result.Success)
            {
                ShowDetailsError(request.responseCode == 404
                    ? "Weapon not found."
                    : "Couldn't load weapon details.");
                return;
            }

            var response = ReadJson<DetailsResponse>(request.downloadHandler.text);

            if (response == null || string.IsNullOrWhiteSpace(response.name))
            {
                ShowDetailsError("The shop returned invalid weapon data.");
                return;
            }

            Render(response, sprite);
        }

        private void Send(
            UnityWebRequest request,
            Action<UnityWebRequest> onCompleted,
            Action onStartFailed)
        {
            request.timeout = 15;

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
            loadRoutine = StartCoroutine(WaitForResponse(request, operation, onCompleted));
        }

        private IEnumerator WaitForResponse(
            UnityWebRequest request,
            UnityWebRequestAsyncOperation operation,
            Action<UnityWebRequest> onCompleted)
        {
            yield return operation;

            loadRoutine = null;
            activeRequest = null;

            using (request)
            {
                onCompleted(request);
            }
        }

        private void Render(DetailsResponse weapon, Sprite sprite)
        {
            title.text = weapon.name;
            FindRequired<TMP_Text>(weaponInfo, "WeaponName").text = weapon.name;
            FindRequired<TMP_Text>(weaponInfo, "WeaponType").text =
                WeaponShop.GetCategoryTitle(weapon.categoryCode);

            var preview = FindRequired<Image>(content, "WeaponPreview/Weapon_Icon");
            preview.sprite = sprite;
            preview.enabled = sprite != null;

            SetStat(weapon.stats, "DAMAGE", "Damage");
            SetStat(weapon.stats, "FIRE_RATE", "FireRate");
            SetStat(weapon.stats, "ACCURACY", "Accuracy");
            SetStat(weapon.stats, "RECOIL_CONTROL", "RecoilControl");

            var magazine = FindStat(weapon.stats, "MAGAZINE_SIZE");
            var magazineLabel = FindRequired<Transform>(weaponInfo, "MagazineLabel");
            var magazineValue = FindRequired<TMP_Text>(weaponInfo, "MagazineValue");

            magazineLabel.gameObject.SetActive(magazine != null);
            magazineValue.gameObject.SetActive(magazine != null);

            if (magazine != null)
                magazineValue.text = $"{magazine.value} rounds";

            ShowRequiredRank(weapon.minRank);

            priceText.text = weapon.price
                .ToString("N0", CultureInfo.InvariantCulture)
                .Replace(',', ' ');

            UpdatePurchaseControls();
            content.gameObject.SetActive(true);
        }

        private void UpdatePurchaseControls()
        {
            bool canBuy = profileLoader != null &&
                !string.IsNullOrWhiteSpace(profileLoader.GetAccessToken(selectedApiBaseUrl));

            bool isOwned = canBuy && profileLoader.OwnsWeapon(selectedWeaponCode);

            purchasePanel.SetActive(!isOwned);
            purchasedPanel.SetActive(isOwned);

            buyButton.gameObject.SetActive(canBuy && !isOwned && !isBuying);
            purchaseSpinner.SetActive(isBuying);
            backButton.interactable = !isBuying;
        }

        private void ShowRequiredRank(RankRequirement minRank)
        {
            var label = FindRequired<TMP_Text>(
                weaponInfo, "RankRequirement/RequiredRank");

            string rankName = minRank?.englishName;

            if (string.IsNullOrWhiteSpace(rankName))
                rankName = minRank?.code;

            label.text = string.IsNullOrWhiteSpace(rankName)
                ? "Required rank: Unavailable"
                : $"Required rank: {rankName}";
        }

        private void SetStat(WeaponStat[] stats, string code, string rowName)
        {
            var row = FindRequired<Transform>(weaponInfo, rowName);
            var stat = FindStat(stats, code);

            row.gameObject.SetActive(stat != null);

            if (stat == null)
                return;

            FindRequired<TMP_Text>(row, "Value").text =
                stat.value.ToString(CultureInfo.InvariantCulture);

            var fill = FindRequired<RectTransform>(row, "Track/Fill");
            var anchorMax = fill.anchorMax;
            anchorMax.x = Mathf.Clamp01(stat.value / 100f);
            fill.anchorMax = anchorMax;
        }

        private void UpdateBalance()
        {
            var source = FindRequired<Transform>(
                transform.parent, "RightTopBlock/BalancePanel");

            var target = FindRequired<Transform>(transform, "Header/BalancePanel");

            target.gameObject.SetActive(source.gameObject.activeSelf);

            if (source.gameObject.activeSelf)
            {
                FindRequired<TMP_Text>(target, "BalanceText").text =
                    FindRequired<TMP_Text>(source, "BalanceText").text;
            }
        }

        private static WeaponStat FindStat(WeaponStat[] stats, string code)
        {
            return stats == null
                ? null
                : Array.Find(stats, stat => stat != null && stat.statCode == code);
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

        private void ShowDetailsError(string message)
        {
            Close();
            ErrorPopup.Show(message, "Shop Error");
        }

        private static T FindRequired<T>(Transform root, string path) where T : Component
        {
            var child = root.Find(path);

            if (child != null && child.TryGetComponent<T>(out var component))
                return component;

            throw new InvalidOperationException(
                $"WeaponDetails: expected {typeof(T).Name} at '{root.name}/{path}'.");
        }
    }
}