using System;
using Kodimodi.Models;
using Kodimodi.UI;
using Kodimodi.UI.Profile;
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
        [SerializeField] private string editorTestUrl = "";
        [SerializeField] private string matchSceneName = "LevelTst";

        public string RoomCode { get; private set; }
        public bool IsHost { get; private set; }
        public bool IsLobbyVisible { get; private set; }

        private const int MinPlayers = 2;
        private const float ConnectTimeoutSeconds = 10f;
        private const float ActionTimeoutSeconds = 15f;

        private MatchLobbyView view;
        private RoomPreviewController preview;
        private ClipboardManager clipboard;

        private WebSocket ws;

        private bool finished;
        private bool connected;
        private bool starting;
        private bool leaving;
        private bool previewErrorShown;

        private int capacity;
        private int playerCount;
        private string matchLink;

        private double operationDeadline;
        private string operationTimeoutReason;

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
            Connect(code, hostToken);
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
            previewErrorShown = false;

            UpdateLink(code);

            if (view != null)
            {
                view.ClearPlayers();
                view.SetSummary(0, 0);
                view.SetLoading(true);
                view.SetStatus("Loading room…");
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

            // После успешного ответа следующая ошибка
            // снова сможет показать окно.
            previewErrorShown = false;

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
            if (string.IsNullOrWhiteSpace(message))
                message = "Check the link or try joining again later.";

            ReturnHome();

            ErrorPopup.Show(
                message,
                "Could not load the room");
        }

        public void JoinPreviewRoom()
        {
            if (string.IsNullOrEmpty(RoomCode))
            {
                ErrorPopup.Show(
                    "The room code is missing. Open the match link again.",
                    "Could not join");
                return;
            }

            Connect(RoomCode, null);
        }

        // ---------- Подключение ----------

        public async void Connect(
            string roomId,
            string hostToken)
        {
            if (preview != null)
                preview.Close();

            Disconnect();

            if (string.IsNullOrWhiteSpace(roomId))
            {
                ErrorPopup.Show(
                    "No room code was provided.",
                    "Could not connect");
                return;
            }

            RoomCode = roomId;
            IsHost = !string.IsNullOrEmpty(hostToken);
            IsLobbyVisible = true;

            connected = false;
            starting = false;
            leaving = false;
            playerCount = 0;
            finished = false;
            previewErrorShown = false;

            UpdateLink(roomId);

            if (view != null)
            {
                view.RenderPlayers(0, capacity);
                view.SetLoading(true);
                view.SetStatus("Connecting to room…");
                view.SetActions(false, false, IsHost, false);
            }

            BeginOperation(
                ConnectTimeoutSeconds,
                "connection_timeout");

            WebSocket socket = null;

            try
            {
                socket = new WebSocket(
                    $"{ServerConfig.WsUrl}/lobby");

                ws = socket;

                socket.OnOpen += () =>
                {
                    if (ws != socket || finished)
                    {
                        SafeClose(socket);
                        return;
                    }

                    SendJoin(
                        socket,
                        roomId,
                        hostToken);
                };

                socket.OnMessage += data =>
                {
                    if (ws == socket && !finished)
                        HandleMessage(data);
                };

                socket.OnError += error =>
                {
                    if (ws != socket || finished)
                        return;

                    Debug.LogWarning(
                        $"Lobby socket error: {error}");

                    HandleClosed("connection_lost");
                };

                socket.OnClose += code =>
                {
                    if (ws != socket || finished)
                        return;

                    HandleClosed("connection_lost");
                };

                await socket.Connect();
            }
            catch (Exception exception)
            {
                if (finished ||
                    (socket != null && ws != socket))
                {
                    return;
                }

                Debug.LogWarning(
                    $"Lobby connect failed: {exception.Message}");

                HandleClosed("connection_lost");
            }
        }

        private async void SendJoin(
            WebSocket socket,
            string roomId,
            string hostToken)
        {
            try
            {
                var join = new LobbyJoinModel
                {
                    RoomId = roomId,
                    Nickname = FindAnyObjectByType<ProfileLoader>()?.Nickname ?? "Guest",
                    HostToken = hostToken ?? ""
                };

                await socket.Send(
                    WsProtocol.Pack(
                        WsMessageType.LobbyJoin,
                        join));
            }
            catch (Exception exception)
            {
                if (ws != socket || finished)
                    return;

                Debug.LogWarning(
                    $"Lobby join failed: {exception.Message}");

                HandleClosed("connection_lost");
            }
        }

        private void HandleMessage(byte[] data)
        {
            WsEnvelopeModel envelope;

            try
            {
                envelope = WsProtocol.Unpack(data);

                if (envelope == null)
                    throw new FormatException("Empty envelope.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Lobby response error: {exception.Message}");

                HandleClosed("protocol_error");
                return;
            }

            // Десериализация выполняется отдельно от событий UI.
            switch (envelope.Type)
            {
                case WsMessageType.LobbyState:
                {
                    LobbyStateModel state;

                    try
                    {
                        state =
                            WsProtocol.UnpackPayload<LobbyStateModel>(
                                envelope);

                        if (state == null || state.Players == null)
                            throw new FormatException("Invalid lobby state.");

                        foreach (var player in state.Players)
                        {
                            if (player == null)
                                throw new FormatException("Invalid player.");
                        }
                    }
                    catch (Exception exception)
                    {
                        HandleProtocolError(exception);
                        return;
                    }

                    ApplyLobbyState(state);
                    StateReceived?.Invoke(state);
                    break;
                }

                case WsMessageType.MatchStarted:
                {
                    MatchStartedModel match;

                    try
                    {
                        match =
                            WsProtocol.UnpackPayload<MatchStartedModel>(
                                envelope);

                        if (match == null ||
                            string.IsNullOrWhiteSpace(match.RoomId) ||
                            string.IsNullOrWhiteSpace(match.BattleToken))
                        {
                            throw new FormatException(
                                "Invalid match session.");
                        }
                    }
                    catch (Exception exception)
                    {
                        HandleProtocolError(exception);
                        return;
                    }

                    finished = true;
                    starting = true;
                    ClearOperation();

                    BrowserUrl.ClearRoomParam();

                    MatchSession.RoomId = match.RoomId;
                    MatchSession.PlayerId = match.PlayerId;
                    MatchSession.BattleToken = match.BattleToken;

                    if (view != null)
                    {
                        view.SetLoading(true);
                        view.SetStatus("Loading match…");
                        view.SetActions(
                            false, false, false, false, false);
                    }

                    MatchStarted?.Invoke(match);

                    SceneManager.LoadSceneAsync(matchSceneName);
                    break;
                }

                case WsMessageType.RoomClosed:
                {
                    RoomClosedModel closed;

                    try
                    {
                        closed =
                            WsProtocol.UnpackPayload<RoomClosedModel>(
                                envelope);

                        if (closed == null)
                            throw new FormatException("Invalid room close.");
                    }
                    catch (Exception exception)
                    {
                        HandleProtocolError(exception);
                        return;
                    }

                    HandleClosed(closed.Reason);
                    break;
                }
            }
        }

        private void HandleProtocolError(Exception exception)
        {
            Debug.LogWarning(
                $"Lobby payload error: {exception.Message}");

            HandleClosed("protocol_error");
        }

        private void ApplyLobbyState(LobbyStateModel state)
        {
            connected = true;
            IsHost = state.YourId == state.HostId;
            playerCount = state.Players.Length;

            if (!starting && !leaving)
                ClearOperation();

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
                hostIndex,
                state.Players);

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
                view.SetStatus("Closing room…");
            }
            else if (starting)
            {
                view.SetStatus("Starting match…");
            }
            else if (IsHost)
            {
                view.SetStatus(
                    playerCount >= MinPlayers
                        ? "You can start the match without waiting for the room to fill up."
                        : "Waiting for players…");
            }
            else
            {
                view.SetStatus("Waiting for the host to start the match…");
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
            {
                HandleClosed("connection_lost");
                return;
            }

            starting = true;

            BeginOperation(
                ActionTimeoutSeconds,
                "start_timeout");

            if (view != null)
            {
                view.SetLoading(true);
                view.SetStatus("Starting match…");
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
                view.SetStatus("Leaving lobby…");
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

                BeginOperation(
                    ActionTimeoutSeconds,
                    "cancel_timeout");

                if (view != null)
                {
                    view.SetStatus("Closing room…");
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
                code,
                editorTestUrl);

            if (view != null)
                view.SetLink(matchLink);
        }

        // ---------- Ошибки и закрытие ----------

        private void HandleClosed(string reason)
        {
            if (finished)
                return;

            bool expectedClose =
                leaving &&
                (reason == "cancelled" ||
                 reason == "host_left" ||
                 reason == "closed");

            finished = true;

            ReturnHome();

            if (!expectedClose)
            {
                ErrorPopup.Show(
                    GetCloseMessage(reason),
                    "Match unavailable");
            }

            Closed?.Invoke(reason);
        }

        private static string GetCloseMessage(string reason)
        {
            return reason switch
            {
                "cancelled" =>
                    "The host cancelled the match.",

                "host_left" =>
                    "The host left and closed the lobby.",

                "room_not_found" =>
                    "The room was not found or no longer exists.",

                "full" =>
                    "The room is full.",

                "started" =>
                    "The match has already started.",

                "duplicate" =>
                    "You are already connected to this room.",

                "closed" =>
                    "The room is closed.",

                "connection_timeout" =>
                    "The server did not respond in time.\n\n" +
                    "Check your connection and try joining again.",

                "start_timeout" =>
                    "Timed out waiting for the match to start.\n\n" +
                    "Try joining the room again.",

                "cancel_timeout" =>
                    "The server did not confirm that the room was closed.\n\n" +
                    "The connection to the lobby has been closed.",

                "protocol_error" =>
                    "The server returned invalid data.\n\n" +
                    "Refresh the page and try again.",

                "connection_lost" =>
                    "The connection to the lobby was lost.\n\n" +
                    "Check your connection and try again.",

                _ =>
                    "The lobby is no longer available.\n\n" +
                    "Try connecting again."
            };
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
            previewErrorShown = false;

            capacity = 0;
            playerCount = 0;

            if (view != null)
            {
                view.SetLoading(false);
                view.ClearPlayers();
            }
        }

        // ---------- Ожидание ответа ----------

        private void BeginOperation(float seconds, string reason)
        {
            operationDeadline =
                Time.realtimeSinceStartupAsDouble + seconds;

            operationTimeoutReason = reason;
        }

        private void ClearOperation()
        {
            operationDeadline = 0;
            operationTimeoutReason = null;
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

        private async void Send(WsMessageType type)
        {
            var socket = ws;

            if (socket == null ||
                socket.State != WebSocketState.Open)
            {
                if (!finished)
                    HandleClosed("connection_lost");

                return;
            }

            try
            {
                await socket.Send(WsProtocol.Pack(type));
            }
            catch (Exception exception)
            {
                if (ws != socket || finished)
                    return;

                Debug.LogWarning(
                    $"Lobby send failed: {exception.Message}");

                HandleClosed("connection_lost");
            }
        }

        public void Disconnect()
        {
            finished = true;
            connected = false;
            ClearOperation();

            var old = ws;
            ws = null;

            SafeClose(old);
        }

        private void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            ws?.DispatchMessageQueue();
#endif

            if (!finished &&
                operationDeadline > 0 &&
                Time.realtimeSinceStartupAsDouble >= operationDeadline)
            {
                string reason = operationTimeoutReason;
                ClearOperation();

                HandleClosed(reason);
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private static async void SafeClose(WebSocket socket)
        {
            if (socket == null)
                return;

            try
            {
                if (socket.State == WebSocketState.Open)
                    await socket.Close();
            }
            catch
            {
                // Ошибку обычного закрытия пользователю не показываем.
            }
        }
    }
}