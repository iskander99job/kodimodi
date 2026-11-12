using System;
using Kodimodi.Models;
using Kodimodi.UI;
using NativeWebSocket;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kodimodi
{
    public class LobbyClient : MonoBehaviour
    {
        public event Action<LobbyStateModel> StateReceived;
        public event Action<MatchStartedModel> MatchStarted;
        public event Action<string> Closed;

        [Header("Настройки")]
        [SerializeField] private string nickname = "Player";
        [SerializeField] private string editorTestUrl = "";
        [SerializeField] private string matchSceneName = "LevelTst";

        public string RoomCode { get; private set; }
        public bool IsHost { get; private set; }
        public bool IsLobbyVisible { get; private set; }

        private const int MinPlayers = 2;

        private MatchLobbyView view;
        private RoomPreviewController preview;
        private ClipboardManager clipboard;

        private WebSocket ws;

        private bool finished;
        private bool connected;
        private bool starting;
        private bool leaving;

        private int capacity;
        private int playerCount;
        private string matchLink;

        private void Awake()
        {
            view = FindAnyObjectByType<MatchLobbyView>(
                FindObjectsInactive.Include);

            preview = GetComponent<RoomPreviewController>();
            clipboard = FindAnyObjectByType<ClipboardManager>();

            if (view == null)
                Debug.LogWarning("Не найден MatchLobbyView.", this);

            if (preview == null)
            {
                Debug.LogWarning(
                    "Добавь RoomPreviewController на объект Lobby.",
                    this);
            }
        }

        private void Start()
        {
            string code = MatchSession.LinkHandled
                ? null
                : RoomLinkChecker.GetRoomCode(editorTestUrl);

            MatchSession.LinkHandled = true;

            if (!string.IsNullOrEmpty(code) && preview != null)
                preview.Show(code);
        }

        // ---------- Создание и предпросмотр ----------

        public void EnterCreatedRoom(
            string code,
            string hostToken,
            int maxPlayers)
        {
            capacity = maxPlayers;
            Connect(code, nickname, hostToken);
        }

        public void ShowPreview(string code)
        {
            Disconnect();

            RoomCode = code;
            IsHost = false;
            IsLobbyVisible = true;

            capacity = 0;
            playerCount = 0;
            starting = false;
            leaving = false;

            UpdateLink(code);

            if (view != null)
            {
                view.ClearPlayers();
                view.SetSummary(0, 0);
                view.SetLoading(true);
                view.SetStatus("Загружаем комнату…");
                view.SetActions(true, false, false, false);
            }
        }

        public void ApplyPreview(
            int count,
            int maxPlayers,
            bool canJoin,
            string status)
        {
            capacity = maxPlayers;

            if (view != null)
            {
                view.SetLoading(false);
                view.RenderPlayers(count, capacity);
                view.SetStatus(status);
                view.SetActions(true, canJoin, false, false);
            }
        }

        public void SetPreviewError(string message)
        {
            // Временная ошибка предпросмотра:
            // RoomPreviewController продолжает повторять запрос.
            if (view != null)
            {
                view.SetLoading(false);
                view.SetStatus(message);
                view.SetActions(true, false, false, false);
            }
        }

        public void JoinPreviewRoom()
        {
            if (string.IsNullOrEmpty(RoomCode))
                return;

            Connect(RoomCode, nickname, null);
        }

        // ---------- Подключение ----------

        public async void Connect(
            string roomId,
            string playerNickname,
            string hostToken)
        {
            if (preview != null)
                preview.Close();

            Disconnect();

            RoomCode = roomId;
            IsHost = !string.IsNullOrEmpty(hostToken);
            IsLobbyVisible = true;

            connected = false;
            starting = false;
            leaving = false;
            playerCount = 0;
            finished = false;

            UpdateLink(roomId);

            if (view != null)
            {
                view.RenderPlayers(0, capacity);
                view.SetLoading(true);
                view.SetStatus("Подключаемся к комнате…");
                view.SetActions(false, false, IsHost, false);
            }

            var socket = new WebSocket(
                $"{ServerConfig.WsUrl}/lobby");

            ws = socket;

            socket.OnOpen += () =>
            {
                if (ws != socket)
                {
                    SafeClose(socket);
                    return;
                }

                var join = new LobbyJoinModel
                {
                    RoomId = roomId,
                    Nickname = playerNickname,
                    HostToken = hostToken ?? ""
                };

                socket.Send(
                    WsProtocol.Pack(WsMessageType.LobbyJoin, join));
            };

            socket.OnMessage += data =>
            {
                if (ws == socket)
                    HandleMessage(data);
            };

            socket.OnError += error =>
            {
                if (ws == socket)
                    Debug.LogWarning($"Lobby socket error: {error}");
            };

            socket.OnClose += code =>
            {
                if (ws != socket || finished)
                    return;

                HandleClosed("connection_lost");
            };

            try
            {
                await socket.Connect();
            }
            catch (Exception exception)
            {
                if (ws != socket || finished)
                    return;

                Debug.LogWarning(
                    $"Lobby connect failed: {exception.Message}");

                HandleClosed("connection_lost");
            }
        }

        private void HandleMessage(byte[] data)
        {
            WsEnvelopeModel envelope;

            try
            {
                envelope = WsProtocol.Unpack(data);
            }
            catch
            {
                return;
            }

            switch (envelope.Type)
            {
                case WsMessageType.LobbyState:
                {
                    var state =
                        WsProtocol.UnpackPayload<LobbyStateModel>(
                            envelope);

                    ApplyLobbyState(state);
                    StateReceived?.Invoke(state);
                    break;
                }

                case WsMessageType.MatchStarted:
                {
                    finished = true;
                    starting = true;

                    var match =
                        WsProtocol.UnpackPayload<MatchStartedModel>(
                            envelope);

                    BrowserUrl.ClearRoomParam();

                    MatchSession.RoomId = match.RoomId;
                    MatchSession.PlayerId = match.PlayerId;
                    MatchSession.BattleToken = match.BattleToken;

                    if (view != null)
                    {
                        view.SetLoading(true);
                        view.SetStatus("Загружаем матч…");
                        view.SetActions(
                            false, false, false, false, false);
                    }

                    MatchStarted?.Invoke(match);

                    SceneManager.LoadSceneAsync(matchSceneName);
                    break;
                }

                case WsMessageType.RoomClosed:
                {
                    var closed =
                        WsProtocol.UnpackPayload<RoomClosedModel>(
                            envelope);

                    HandleClosed(closed.Reason);
                    break;
                }
            }
        }

        private void ApplyLobbyState(LobbyStateModel state)
        {
            connected = true;
            IsHost = state.YourId == state.HostId;
            playerCount = state.Players.Length;

            int yourIndex = -1;
            int hostIndex = -1;

            for (int i = 0; i < state.Players.Length; i++)
            {
                if (state.Players[i].Id == state.YourId)
                    yourIndex = i;

                if (state.Players[i].Id == state.HostId)
                    hostIndex = i;
            }

            if (view == null)
                return;

            view.SetLoading(starting || leaving);

            view.RenderPlayers(
                playerCount,
                capacity,
                yourIndex,
                hostIndex);

            view.SetActions(
                false,
                false,
                IsHost,
                IsHost &&
                playerCount >= MinPlayers &&
                !starting &&
                !leaving,
                !leaving);

            if (leaving)
            {
                view.SetStatus("Закрываем комнату…");
            }
            else if (starting)
            {
                view.SetStatus("Запускаем матч…");
            }
            else if (IsHost)
            {
                view.SetStatus(
                    playerCount >= MinPlayers
                        ? "Можно начать матч, не дожидаясь заполнения комнаты."
                        : "Ждём игроков…");
            }
            else
            {
                view.SetStatus("Ожидаем запуска от создателя…");
            }
        }

        // ---------- Кнопки ----------

        public void OnStartMatch()
        {
            if (!IsHost ||
                !connected ||
                starting ||
                leaving ||
                playerCount < MinPlayers)
            {
                return;
            }

            if (ws == null || ws.State != WebSocketState.Open)
                return;

            starting = true;

            if (view != null)
            {
                view.SetLoading(true);
                view.SetStatus("Запускаем матч…");
                view.SetActions(false, false, true, false);
            }

            StartMatch();
        }

        public void OnCancelMatchSearch()
        {
            if (leaving || starting)
                return;

            if (view != null)
            {
                view.SetLoading(true);
                view.SetStatus("Выходим из лобби…");
            }

            if (preview != null && preview.IsPreviewing)
            {
                ReturnHome();
                return;
            }

            if (IsHost &&
                connected &&
                ws != null &&
                ws.State == WebSocketState.Open)
            {
                leaving = true;

                if (view != null)
                {
                    view.SetStatus("Закрываем комнату…");
                    view.SetActions(
                        false, false, true, false, false);
                }

                CancelMatch();
            }
            else
            {
                ReturnHome();
            }
        }

        public void OnCopyMatchLink()
        {
            if (string.IsNullOrEmpty(matchLink))
                return;

            if (clipboard == null)
                clipboard = FindAnyObjectByType<ClipboardManager>();

            if (clipboard != null)
                clipboard.CopyToClipboard(matchLink);
        }

        private void UpdateLink(string code)
        {
            matchLink = RoomLinkChecker.BuildLink(
                code, editorTestUrl);

            if (view != null)
                view.SetLink(matchLink);
        }

        // ---------- Закрытие и ошибки ----------

        private void HandleClosed(string reason)
        {
            bool expectedClose =
                leaving &&
                (reason == "cancelled" ||
                 reason == "host_left" ||
                 reason == "closed");

            finished = true;

            // Сначала закрываем лобби и останавливаем соединение.
            ReturnHome();

            if (!expectedClose)
            {
                ErrorPopup.Show(
                    GetCloseMessage(reason),
                    "Матч недоступен");
            }

            Closed?.Invoke(reason);
        }

        private static string GetCloseMessage(string reason)
        {
            switch (reason)
            {
                case "cancelled":
                    return "Создатель отменил матч.";

                case "host_left":
                    return "Создатель вышел и закрыл лобби.";

                case "room_not_found":
                    return "Комната не найдена или больше не существует.";

                case "full":
                    return "В комнате нет свободных мест.";

                case "started":
                    return "Матч уже начался.";

                case "duplicate":
                    return "Ты уже подключён к этой комнате.";

                case "closed":
                    return "Комната закрыта.";

                case "connection_lost":
                    return
                        "Соединение с лобби потеряно.\n\n" +
                        "Проверь подключение и попробуй ещё раз.";

                default:
                    return
                        "Лобби стало недоступно.\n\n" +
                        "Попробуй подключиться снова.";
            }
        }

        private void ReturnHome()
        {
            if (preview != null)
                preview.Close();

            Disconnect();
            BrowserUrl.ClearRoomParam();

            RoomCode = null;
            matchLink = null;

            IsHost = false;
            IsLobbyVisible = false;

            connected = false;
            starting = false;
            leaving = false;

            capacity = 0;
            playerCount = 0;

            if (view != null)
            {
                view.SetLoading(false);
                view.ClearPlayers();
            }
        }

        // ---------- WebSocket ----------

        public void StartMatch()
        {
            Send(WsMessageType.StartMatch);
        }

        public void CancelMatch()
        {
            Send(WsMessageType.CancelMatch);
        }

        private void Send(WsMessageType type)
        {
            if (ws != null && ws.State == WebSocketState.Open)
                ws.Send(WsProtocol.Pack(type));
        }

        public void Disconnect()
        {
            finished = true;
            connected = false;

            if (ws == null)
                return;

            WebSocket old = ws;
            ws = null;

            SafeClose(old);
        }

        private void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            ws?.DispatchMessageQueue();
#endif
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private static async void SafeClose(WebSocket socket)
        {
            try
            {
                if (socket != null &&
                    socket.State == WebSocketState.Open)
                {
                    await socket.Close();
                }
            }
            catch
            {
                // Соединение уже закрыто.
            }
        }
    }
}