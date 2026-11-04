using System;
using Kodimodi.Models;
using NativeWebSocket;
using UnityEngine;

namespace Kodimodi
{
    public class LobbyClient : MonoBehaviour
    {
        public event Action<LobbyStateModel> StateReceived;
        public event Action<MatchStartedModel> MatchStarted;
        // reason: cancelled, host_left, room_not_found, full, started, duplicate, closed, connection_lost
        public event Action<string> Closed;

        private WebSocket ws;
        private bool finished;   // true, когда сервер сам завершил лобби или мы вышли

        public async void Connect(string roomId, string nickname, string hostToken)
        {
            Disconnect();
            finished = false;

            ws = new WebSocket($"{ServerConfig.WsUrl}/lobby");

            ws.OnOpen += () =>
            {
                // первым сообщением обязательно вход в комнату
                var join = new LobbyJoinModel
                {
                    RoomId = roomId,
                    Nickname = nickname,
                    HostToken = hostToken ?? ""
                };
                ws.Send(WsProtocol.Pack(WsMessageType.LobbyJoin, join));
            };

            ws.OnMessage += HandleMessage;

            ws.OnError += err => Debug.LogWarning($"Lobby socket error: {err}");

            ws.OnClose += code =>
            {
                if (finished) return;
                finished = true;
                Closed?.Invoke("connection_lost");
            };

            try
            {
                // Task завершается, когда соединение закрыто, поэтому после него ничего не делаем
                await ws.Connect();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Lobby connect failed: {e.Message}");
                if (!finished)
                {
                    finished = true;
                    Closed?.Invoke("connection_lost");
                }
            }
        }

        private void HandleMessage(byte[] data)
        {
            WsEnvelopeModel env;
            try { env = WsProtocol.Unpack(data); }
            catch { return; }

            switch (env.Type)
            {
                case WsMessageType.LobbyState:
                    StateReceived?.Invoke(WsProtocol.UnpackPayload<LobbyStateModel>(env));
                    break;

                case WsMessageType.MatchStarted:
                    finished = true; // сервер закроет лобби-сокет сам
                    MatchStarted?.Invoke(WsProtocol.UnpackPayload<MatchStartedModel>(env));
                    break;

                case WsMessageType.RoomClosed:
                    finished = true;
                    Closed?.Invoke(WsProtocol.UnpackPayload<RoomClosedModel>(env).Reason);
                    break;
            }
        }

        public void StartMatch() => Send(WsMessageType.StartMatch);
        public void CancelMatch() => Send(WsMessageType.CancelMatch);

        private void Send(WsMessageType type)
        {
            if (ws != null && ws.State == WebSocketState.Open)
                ws.Send(WsProtocol.Pack(type));
        }

        // Выход из лобби без событий (игрок сам ушёл)
        public void Disconnect()
        {
            if (ws == null) return;
            finished = true;
            var old = ws;
            ws = null;
            SafeClose(old);
        }

        private void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            ws?.DispatchMessageQueue();
#endif
        }

        private void OnDestroy() => Disconnect();
        
        private static async void SafeClose(WebSocket socket)
        {
            try
            {
                if (socket != null && socket.State == WebSocketState.Open)
                    await socket.Close();
            }
            catch
            {
                // сокет уже закрыт, ничего делать не нужно
            }
        }
    }
}