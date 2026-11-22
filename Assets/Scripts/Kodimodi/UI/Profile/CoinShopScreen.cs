using System;
using System.Collections;
using System.Globalization;
using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kodimodi.UI.Profile
{
    public sealed class CoinShopScreen : MonoBehaviour
    {
        private static readonly (int Coins, decimal Price)[] Packs =
        {
            (500, 1.99m), (1200, 3.99m), (2800, 7.99m), (6000, 14.99m)
        };

        private static readonly Color ActiveRadio = new Color32(255, 166, 0, 255);
        private static readonly Color InactiveRadio = new Color32(171, 173, 176, 255);

        [SerializeField] private Sprite normalPanel;
        [SerializeField] private Sprite selectedPanel;
        [SerializeField] private Button[] packButtons;
        [SerializeField] private Button[] methodButtons;
        [SerializeField] private Image[] methodRings;
        [SerializeField] private Image[] methodDots;
        [SerializeField] private TMP_Text summaryAmount;
        [SerializeField] private TMP_Text summaryTotal;
        [SerializeField] private Button payButton;
        [SerializeField] private TMP_Text payLabel;
        [SerializeField] private TMP_Text status;

        private static string sourceScene;
        private string returnScene;
        private GameObject balancePanel;
        private TMP_Text balance;
        private int selectedPack = 1;
        private int selectedMethod;

        [Serializable]
        private sealed class ProfileResponse
        {
            public int coins;
        }

        internal static void Open()
        {
            sourceScene = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene("CoinShopScene");
        }

        private void Awake()
        {
            returnScene = string.IsNullOrEmpty(sourceScene) ? "ProfileScene" : sourceScene;
            sourceScene = null;

            var header = transform.Find("Header");
            header.Find("Back").GetComponent<LeanButton>().OnClick.AddListener(() =>
                SceneManager.LoadScene(returnScene));
            balancePanel = header.Find("BalancePanel").gameObject;
            balance = balancePanel.transform.Find("BalanceText").GetComponent<TMP_Text>();
            balancePanel.SetActive(false);

            for (int i = 0; i < packButtons.Length; i++)
            {
                int index = i;
                packButtons[i].onClick.AddListener(() =>
                {
                    selectedPack = index;
                    RenderSelection();
                });
            }

            for (int i = 0; i < methodButtons.Length; i++)
            {
                int index = i;
                methodButtons[i].onClick.AddListener(() =>
                {
                    selectedMethod = index;
                    RenderSelection();
                });
            }

            payButton.onClick.AddListener(() => status.text = "Payments are not available yet.");
            RenderSelection();
        }

        private IEnumerator Start()
        {
            if (!ProfileLoader.TryGetShopSession(out var apiBaseUrl, out var token))
                yield break;

            using var request = UnityWebRequest.Get(apiBaseUrl.TrimEnd('/') + "/profile");
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = 15;
            UnityWebRequestAsyncOperation operation;
            try
            {
                operation = request.SendWebRequest();
            }
            catch (InvalidOperationException)
            {
                yield break;
            }

            yield return operation;
            if (request.responseCode == 401)
                ProfileLoader.ClearInventorySession();

            if (request.result != UnityWebRequest.Result.Success)
                yield break;

            ProfileResponse profile;
            try
            {
                profile = JsonUtility.FromJson<ProfileResponse>(request.downloadHandler.text);
            }
            catch (ArgumentException)
            {
                yield break;
            }

            if (profile != null)
            {
                balance.text = FormatCoins(profile.coins);
                balancePanel.SetActive(true);
            }
        }

        private void RenderSelection()
        {
            for (int i = 0; i < packButtons.Length; i++)
                packButtons[i].image.sprite = i == selectedPack ? selectedPanel : normalPanel;

            for (int i = 0; i < methodButtons.Length; i++)
            {
                bool selected = i == selectedMethod;
                methodButtons[i].image.sprite = selected ? selectedPanel : normalPanel;
                methodRings[i].color = selected ? ActiveRadio : InactiveRadio;
                methodDots[i].gameObject.SetActive(selected);
            }

            var pack = Packs[selectedPack];
            string price = "$" + pack.Price.ToString("0.00", CultureInfo.InvariantCulture);
            summaryAmount.text = FormatCoins(pack.Coins) + " coins";
            summaryTotal.text = "Total " + price;
            payLabel.text = "PAY " + price;
            status.text = "";
        }

        private static string FormatCoins(int amount) =>
            amount.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ');
    }
}
