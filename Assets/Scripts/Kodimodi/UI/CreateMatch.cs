using System;
using System.Collections;
using System.Text;
using Kodimodi.Models;
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
        public TMP_Text matchInfoTextUI;
        private int playersCount = 2;
        public GameObject searchMatchScrollViewItemPrefabUI;
        public GameObject searchMatchScrollViewUI;   // Content
        public TMP_InputField matchLinkUI;
        public GameObject cancelMatchBtnUI, startMatchBtnUI;
        public Image startMatchCapUI;                // Image у объекта Cap внутри кнопки Start
        public GameObject createMatchModal, searchMatchModal;
        public LeanWindow modal;
        public GameObject createMatchModalSpinner, searchMatchModalSpinner;

        [SerializeField] private LobbyClient lobby;
        // Временно: пока нет ввода никнейма
        [SerializeField] private string nickname = "Player";
        // Для проверки в редакторе, например: https://site.com/game/?room=A7K2QX
        [SerializeField] private string editorTestUrl = "";

        // Код комнаты, null если комнаты ещё нет
        public string RoomCode { get; private set; }
        // Секрет создателя, нужен при входе в /lobby
        public string HostToken { get; private set; }
        public bool IsHost { get; private set; }

        private bool isBusy;
        private LeanButton startLeanButton;

        private const int MinToStart = 2;
        private static readonly Color StartDisabledColor = new Color32(0x5B, 0x58, 0x58, 0xFF); // #5B5858
        private static readonly Color StartEnabledColor = Color.black;

        private void OnEnable()
        {
            lobby.StateReceived += OnLobbyState;
            lobby.MatchStarted += OnMatchStarted;
            lobby.Closed += OnLobbyClosed;
        }

        private void OnDisable()
        {
            lobby.StateReceived -= OnLobbyState;
            lobby.MatchStarted -= OnMatchStarted;
            lobby.Closed -= OnLobbyClosed;
        }

        void Start()
        {
            SetSpinner(createMatchModalSpinner, false);
            SetSpinner(searchMatchModalSpinner, false);
            UpdateMatchInfoText();

            RoomCode = RoomLinkChecker.GetRoomCode(editorTestUrl);

            if (RoomCode != null)
            {
                // Пришли по ссылке: заходим в лобби как обычный игрок
                SetMatchLink(RoomCode);
                SwitchModal("search");
                HideButtons();
                modal.TurnOn();
                ConnectLobby(hostToken: null);
            }
            else
            {
                SwitchModal("create");
            }
        }

        // ---------- Создание комнаты ----------

        public void OnCreateMatch()
        {
            if (isBusy || playersCount < 2) return;
            StartCoroutine(CreateRoomRoutine());
        }

        private IEnumerator CreateRoomRoutine()
        {
            isBusy = true;
            SetSpinner(createMatchModalSpinner, true);

            string json = JsonUtility.ToJson(new CreateRoomRequest { maxPlayers = playersCount });

            using (var req = new UnityWebRequest($"{ServerConfig.HttpUrl}/rooms", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 10;

                yield return req.SendWebRequest();

                SetSpinner(createMatchModalSpinner, false);
                isBusy = false;

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Не удалось создать комнату: {req.error} {req.downloadHandler.text}");
                    yield break;
                }

                var resp = JsonUtility.FromJson<CreateRoomResponse>(req.downloadHandler.text);
                if (resp == null || string.IsNullOrEmpty(resp.code))
                {
                    Debug.LogError($"Неожиданный ответ сервера: {req.downloadHandler.text}");
                    yield break;
                }

                RoomCode = resp.code;
                HostToken = resp.hostToken;
                IsHost = true;

                SetMatchLink(RoomCode);
                SwitchModal("search");
                ShowHostButtons();
                ConnectLobby(HostToken);
            }
        }

        // ---------- Лобби ----------

        private void ConnectLobby(string hostToken)
        {
            SetSpinner(searchMatchModalSpinner, true);   // до первого LobbyState
            lobby.Connect(RoomCode, nickname, hostToken);
        }

        private void OnLobbyState(LobbyStateModel state)
        {
            SetSpinner(searchMatchModalSpinner, false);

            IsHost = state.YourId == state.HostId;
            RenderPlayers(state);

            cancelMatchBtnUI.SetActive(true);
            startMatchBtnUI.SetActive(IsHost);   // гостю кнопки «Начать» нет
            if (IsHost) SetStartEnabled(state.Players.Length >= MinToStart);
        }

        private void RenderPlayers(LobbyStateModel state)
        {
            var content = searchMatchScrollViewUI.transform;
            foreach (Transform child in content)
                Destroy(child.gameObject);

            for (int i = 0; i < state.Players.Length; i++)
            {
                var p = state.Players[i];
                var item = Instantiate(searchMatchScrollViewItemPrefabUI, content);
                var label = item.GetComponentInChildren<TMP_Text>();
                if (label == null) continue;

                string text = $"Игрок {i + 1}";
                if (p.Id == state.YourId) text += " - Вы";
                label.text = text;
            }
        }

        public void OnStartMatch()
        {
            if (!IsHost) return;
            SetStartEnabled(false);                      // защита от двойного нажатия
            SetSpinner(searchMatchModalSpinner, true);   // ждём MatchStarted
            lobby.StartMatch();
        }

        public void OnCancelMatchSearch()
        {
            if (IsHost)
            {
                // сервер разошлёт RoomClosed("cancelled") всем, включая нас
                lobby.CancelMatch();
            }
            else
            {
                lobby.Disconnect();
                ResetToCreate();
            }
        }

        private void OnMatchStarted(MatchStartedModel m)
        {
            MatchSession.RoomId = m.RoomId;
            MatchSession.PlayerId = m.PlayerId;
            Debug.Log($"Матч начался. Комната {m.RoomId}, ваш id {m.PlayerId}");
            // TODO: загрузить сцену и подключиться к /battle
        }

        private void OnLobbyClosed(string reason)
        {
            Debug.Log($"Лобби закрыто: {ReasonToText(reason)}");
            // TODO: показать игроку сообщение
            ResetToCreate();
        }

        private void ResetToCreate()
        {
            RoomCode = null;
            HostToken = null;
            IsHost = false;
            isBusy = false;
            SetSpinner(createMatchModalSpinner, false);
            SetSpinner(searchMatchModalSpinner, false);
            SwitchModal("create");
            modal.TurnOff();
        }

        private static string ReasonToText(string reason) => reason switch
        {
            "cancelled"       => "Создатель отменил матч",
            "host_left"       => "Создатель вышел из комнаты",
            "room_not_found"  => "Комната не найдена",
            "full"            => "Комната заполнена",
            "started"         => "Матч уже начался",
            "duplicate"       => "Вы уже в этой комнате",
            "closed"          => "Комната закрыта",
            "connection_lost" => "Соединение потеряно",
            _                 => reason
        };

        // ---------- Кнопка «Начать» ----------

        // Всегда видна хосту, но нажимается только когда игроков достаточно
        private void SetStartEnabled(bool enabled)
        {
            if (startLeanButton == null)
                startLeanButton = startMatchBtnUI.GetComponent<LeanButton>();

            if (startLeanButton != null)
                startLeanButton.interactable = enabled;

            if (startMatchCapUI != null)
                startMatchCapUI.color = enabled ? StartEnabledColor : StartDisabledColor;
        }

        // Хост: обе кнопки видны, «Начать» пока неактивна
        private void ShowHostButtons()
        {
            startMatchBtnUI.SetActive(true);
            cancelMatchBtnUI.SetActive(true);
            SetStartEnabled(false);
        }

        // ---------- Остальное ----------

        public void SetMatchLink(string code)
        {
            matchLinkUI.text = RoomLinkChecker.BuildLink(code, editorTestUrl);
        }

        private void SwitchModal(string name)
        {
            createMatchModal.SetActive(name == "create");
            searchMatchModal.SetActive(name == "search");
        }

        public void OnMinus()
        {
            if (isBusy) return;
            if (playersCount > 2)
            {
                playersCount -= 1;
                UpdateMatchInfoText();
            }
        }

        public void OnPlus()
        {
            if (isBusy) return;
            if (playersCount < 6)
            {
                playersCount += 1;
                UpdateMatchInfoText();
            }
        }

        private void UpdateMatchInfoText()
        {
            matchInfoTextUI.text =
                "<b>Режим:</b> каждый сам за себя\n" +
                $"<b>Кол-во игроков:</b> {playersCount}";
        }

        public void OnCopyMatchLink()
        {
            FindAnyObjectByType<ClipboardManager>().CopyToClipboard(matchLinkUI.text);
        }

        private void HideButtons()
        {
            startMatchBtnUI.SetActive(false);
            cancelMatchBtnUI.SetActive(false);
        }

        private static void SetSpinner(GameObject spinner, bool visible)
        {
            if (spinner != null) spinner.SetActive(visible);
        }
    }
}