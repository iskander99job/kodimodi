using System;
using System.Collections;
using System.Text;
using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Kodimodi.UI
{
    [Serializable]
    public class CreateRoomRequest
    {
        public int maxPlayers;
    }

    [Serializable]
    public class CreateRoomResponse
    {
        public string code;
        public int maxPlayers;
        public string hostToken;
    }

    public class CreateMatch : MonoBehaviour
    {
        [Header("Количество игроков")]
        [SerializeField] private TMP_Text playersCountText;
        [SerializeField] private Selectable minusButton;
        [SerializeField] private Selectable plusButton;

        [Header("Основные кнопки")]
        [SerializeField] private Selectable closeButton;
        [SerializeField] private Selectable cancelButton;
        [SerializeField] private Selectable createButton;

        [Header("Загрузка")]
        [SerializeField] private GameObject spinner;

        private const int MinPlayers = 2;
        private const int MaxPlayers = 6;

        private LeanWindow window;
        private LobbyClient lobby;

        private int playersCount = MinPlayers;
        private bool busy;

        public bool IsBusy => busy;

        private void Awake()
        {
            window = GetComponent<LeanWindow>();
            lobby = FindAnyObjectByType<LobbyClient>();

            if (window == null || lobby == null)
            {
                Debug.LogError(
                    "CreateMatch: нужны LeanWindow на этом объекте " +
                    "и активный LobbyClient в сцене.",
                    this);

                enabled = false;
                return;
            }

            RefreshUI();
        }

        public void OnMinus()
        {
            if (!isActiveAndEnabled ||
                busy ||
                playersCount <= MinPlayers)
            {
                return;
            }

            playersCount--;
            RefreshUI();
        }

        public void OnPlus()
        {
            if (!isActiveAndEnabled ||
                busy ||
                playersCount >= MaxPlayers)
            {
                return;
            }

            playersCount++;
            RefreshUI();
        }

        public void OnClose()
        {
            if (!isActiveAndEnabled || busy)
                return;

            if (window != null)
                window.TurnOff();
        }

        public void OnCreateMatch()
        {
            if (!isActiveAndEnabled || busy)
                return;

            if (lobby == null)
            {
                ShowCreationError(
                    "Не удалось открыть лобби. " +
                    "Попробуй обновить страницу.");

                return;
            }

            SetBusy(true);
            StartCoroutine(CreateRoomRoutine());
        }

        private IEnumerator CreateRoomRoutine()
        {
            try
            {
                string json = JsonUtility.ToJson(
                    new CreateRoomRequest
                    {
                        maxPlayers = playersCount
                    });

                using (var request = new UnityWebRequest(
                    $"{ServerConfig.HttpUrl}/rooms",
                    "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(
                        Encoding.UTF8.GetBytes(json));

                    request.downloadHandler =
                        new DownloadHandlerBuffer();

                    request.SetRequestHeader(
                        "Content-Type",
                        "application/json");

                    request.timeout = 10;

                    yield return request.SendWebRequest();

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError(
                            $"Не удалось создать комнату: " +
                            $"{request.error}");

                        ShowCreationError(
                            "Не удалось создать матч.\n\n" +
                            "Проверь подключение к интернету " +
                            "и попробуй ещё раз.");

                        yield break;
                    }

                    CreateRoomResponse response;

                    try
                    {
                        response =
                            JsonUtility.FromJson<CreateRoomResponse>(
                                request.downloadHandler.text);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError(
                            "Ошибка чтения ответа создания комнаты: " +
                            exception.Message);

                        ShowCreationError(
                            "Сервер вернул некорректный ответ.\n\n" +
                            "Попробуй создать матч ещё раз.");

                        yield break;
                    }

                    if (response == null ||
                        string.IsNullOrEmpty(response.code) ||
                        string.IsNullOrEmpty(response.hostToken) ||
                        response.maxPlayers < MinPlayers)
                    {
                        ShowCreationError(
                            "Не удалось получить данные созданного матча.\n\n" +
                            "Попробуй ещё раз.");

                        yield break;
                    }

                    if (lobby == null)
                    {
                        ShowCreationError(
                            "Не удалось открыть лобби.\n\n" +
                            "Попробуй обновить страницу.");

                        yield break;
                    }

                    lobby.EnterCreatedRoom(
                        response.code,
                        response.hostToken,
                        response.maxPlayers);

                    if (window != null)
                        window.TurnOff();
                }
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ShowCreationError(string message)
        {
            // Закрываем напрямую: OnClose блокируется во время запроса.
            if (window != null)
                window.TurnOff();

            ErrorPopup.Show(message, "Не удалось создать матч");
        }

        private void SetBusy(bool value)
        {
            busy = value;
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (playersCountText != null)
                playersCountText.text = playersCount.ToString();

            SetMainButtonState(closeButton, !busy);
            SetMainButtonState(cancelButton, !busy);
            SetMainButtonState(createButton, !busy);

            if (minusButton != null)
            {
                minusButton.interactable =
                    !busy && playersCount > MinPlayers;
            }

            if (plusButton != null)
            {
                plusButton.interactable =
                    !busy && playersCount < MaxPlayers;
            }

            if (spinner != null)
                spinner.SetActive(busy);
        }

        private static void SetMainButtonState(
            Selectable button,
            bool visible)
        {
            if (button == null)
                return;

            button.interactable = visible;

            if (button.gameObject.activeSelf != visible)
                button.gameObject.SetActive(visible);
        }
    }
}