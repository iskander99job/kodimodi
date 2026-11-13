using System;
using System.Collections.Generic;
using Akila.FPSFramework;
using Kodimodi.Models;
using Lean.Gui;
using NativeWebSocket;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kodimodi
{
    public class BattleClient : MonoBehaviour
    {
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private GameObject playerPrefab, enemyPrefab;

        private readonly Dictionary<int, RemotePlayerVisuals> spawned = new();
        public IReadOnlyDictionary<int, RemotePlayerVisuals> Spawned => spawned;

        public WebSocket websocket;
        public int id;
        public string livekitToken;
        private Damageable localDamageable;
        public static BattleClient Instance { get; private set; }
        
        public event Action<ScoreboardModel> ScoreboardUpdated;
        public event Action<MatchEndedModel> MatchEnded;
        public event Action<int> PlayerLeft;
        public event Action<int, int> PlayerKilled;   // victimId, killerId
        
        private NetworkRespawn localRespawn;
        public bool IsMatchEnded { get; private set; }
        
        public bool IsDisconnected { get; private set; }
        public string DisconnectReason { get; private set; }

        public event Action<string> BattleDisconnected;

        private readonly HashSet<int> departedPlayerIds = new();
        
        public IReadOnlyCollection<int> DepartedPlayerIds =>
            departedPlayerIds;

        private bool leaving;
        private bool opened;
        private bool battleReady;
        private bool heartbeatSending;
        private bool connectingStarted;

        private float connectStartedAt;
        private float openedAt;
        private float lastReceivedAt;
        private float nextHeartbeatAt;
        public bool HasBattleConnection => opened;

        public bool HasLocalPlayer =>
            battleReady && localPlayer != null;

        public bool IsLocalCameraReady => desiredReady;
        public bool VideoMovementBlocked { get; private set; }

        public bool CanPublishPlayerState =>
            IsBattleStarted &&
            !IsMatchEnded &&
            !IsDisconnected &&
            !leaving &&
            localDamageable != null &&
            localDamageable.health > 0f;

        public bool CanPlay =>
            CanPublishPlayerState && !VideoMovementBlocked;

        public void SetVideoMovementBlocked(bool blocked)
        {
            VideoMovementBlocked = blocked;

            if (localPlayer == null)
                return;

            localPlayer.GetComponent<FirstPersonController>()
                ?.SetVideoMovementBlocked(blocked);

            var input = localPlayer.GetComponent<CharacterInput>();

            if (input != null)
                input.VideoInputBlocked = blocked;
        }

        public MatchEndedModel FinalResult { get; private set; }

        private GameObject localPlayer;
        public GameObject LocalPlayer => localPlayer;
        
        public bool IsBattleStarted { get; private set; }

        public BattleCountdownModel LastCountdown { get; private set; }
        public double CountdownDeadline { get; private set; }

        public event Action<BattleCountdownModel> CountdownUpdated;
        public event Action BattlePlayStarted;

        private int countdownVersion = -1;

        private bool desiredReady;
        private bool? sentReady;
        private bool readySending;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        async void Start()
        {
            if (Instance != this) return;
            
            ScoreboardUpdated += b => Debug.Log($"Убил {b.KillerId}, погиб {b.VictimId}");
            
            // id и комнату выдал сервер при старте матча (см. MatchStarted)
            if (string.IsNullOrEmpty(MatchSession.RoomId)
                || string.IsNullOrEmpty(MatchSession.BattleToken))
            {
                FailConnection("no_session");
                return;
            }
            
            id = MatchSession.PlayerId;
            connectStartedAt = Time.realtimeSinceStartup;
            connectingStarted = true;
            
            websocket = new WebSocket($"{ServerConfig.WsUrl}/battle");
            
            websocket.OnOpen += async () =>
            {
                if (leaving || IsDisconnected) return;

                opened = true;
                openedAt = lastReceivedAt = Time.realtimeSinceStartup;
                nextHeartbeatAt = openedAt + 5f;

                var model = new BattlePingRequestModel
                {
                    id = this.id,
                    RoomId = MatchSession.RoomId,
                    BattleToken = MatchSession.BattleToken
                };

                try
                {
                    await websocket.Send(
                        WsProtocol.Pack(
                            WsMessageType.BattlePingRequest,
                            model));
                }
                catch
                {
                    FailConnection("connection_lost");
                }
            };
            
            websocket.OnError += e => FailConnection("connection_lost");

            websocket.OnClose += code => FailConnection("connection_lost");
            
            websocket.OnMessage += bytes =>
            {
                if (leaving || IsDisconnected) return;

                lastReceivedAt = Time.realtimeSinceStartup;

                WsEnvelopeModel model;

                try
                {
                    model = WsProtocol.Unpack(bytes);
                }
                catch
                {
                    FailConnection("protocol_error");
                    return;
                }

                switch (model.Type)
                {
                    case WsMessageType.BattleCountdown:
                        HandleCountdown(
                            WsProtocol.UnpackPayload<BattleCountdownModel>(model));
                        break;

                    case WsMessageType.BattleStarted:
                        HandleBattleStarted(
                            WsProtocol.UnpackPayload<BattleStartedModel>(model));
                        break;
                    case WsMessageType.BattlePingResponse: HandleSpawn(model); break;
                    case WsMessageType.PlayerBodyPosition: HandleBodyPosition(model); break;
                    case WsMessageType.Shoot: HandleShoot(model); break;
                    case WsMessageType.PlayerHealth:
                        HandleHealth(WsProtocol.UnpackPayload<PlayerHealthModel>(model)); break;
                    case WsMessageType.PlayerDied:
                        HandleDied(WsProtocol.UnpackPayload<PlayerDiedModel>(model)); break;
                    case WsMessageType.PlayerRespawned:
                        HandleRespawned(WsProtocol.UnpackPayload<PlayerRespawnedModel>(model)); break;
                    case WsMessageType.Scoreboard:
                        ScoreboardUpdated?.Invoke(WsProtocol.UnpackPayload<ScoreboardModel>(model)); break;
                    case WsMessageType.MatchEnded:
                        HandleMatchEnded(WsProtocol.UnpackPayload<MatchEndedModel>(model)); break;
                    case WsMessageType.BattleHeartbeatAck:
                        break;
                    case WsMessageType.PlayerReload:
                        HandleReload(
                            WsProtocol.UnpackPayload<PlayerReloadModel>(model));
                        break;
                    case WsMessageType.PlayerLeft:
                        HandlePlayerLeft(
                            WsProtocol.UnpackPayload<PlayerLeftModel>(model));
                        break;

                    case WsMessageType.BattleClosed:
                        FailConnection(
                            WsProtocol.UnpackPayload<BattleClosedModel>(model).Reason);
                        break;
                }
            };
            
            try
            {
                await websocket.Connect();
            }
            catch
            {
                FailConnection("connection_lost");
            }
        }

        private void HandleCountdown(BattleCountdownModel model)
{
    if (IsBattleStarted ||
        IsMatchEnded ||
        IsDisconnected ||
        leaving ||
        model.Version < countdownVersion)
    {
        return;
    }

    // Повторное сообщение не должно перезапускать таймер.
    if (model.Version == countdownVersion)
        return;

    countdownVersion = model.Version;
    LastCountdown = model;

    if (model.StartsAtUnixMs > 0)
    {
        double seconds = Math.Max(
            0,
            (model.StartsAtUnixMs - model.ServerNowUnixMs) / 1000.0);

        CountdownDeadline =
            Time.realtimeSinceStartupAsDouble + seconds;
    }
    else
    {
        CountdownDeadline = 0;
    }

    CountdownUpdated?.Invoke(model);
}

private void HandleBattleStarted(BattleStartedModel model)
{
    if (IsBattleStarted ||
        IsMatchEnded ||
        IsDisconnected ||
        leaving ||
        model.Version < countdownVersion)
    {
        return;
    }

    countdownVersion = model.Version;
    IsBattleStarted = true;

    LastCountdown = null;
    CountdownDeadline = 0;

    BattlePlayStarted?.Invoke();
}

public async void SetBattleReady(bool ready)
{
    desiredReady = ready;

    if (readySending ||
        !battleReady ||
        websocket == null ||
        IsBattleStarted ||
        IsMatchEnded ||
        IsDisconnected ||
        leaving)
    {
        return;
    }

    readySending = true;

    try
    {
        while (!IsBattleStarted &&
               !IsMatchEnded &&
               !IsDisconnected &&
               !leaving &&
               websocket != null &&
               (!sentReady.HasValue || sentReady.Value != desiredReady))
        {
            bool value = desiredReady;

            await websocket.Send(
                WsProtocol.Pack(
                    WsMessageType.BattleReady,
                    new BattleReadyModel
                    {
                        Ready = value
                    }));

            sentReady = value;
        }
    }
    catch (Exception e)
    {
        Debug.LogWarning(
            "Battle ready send failed: " + e.Message);

        FailConnection("connection_lost");
    }
    finally
    {
        readySending = false;
    }
}
        
        public async void SendReloadState(bool isReloading)
        {
            if (websocket == null ||
                IsMatchEnded ||
                IsDisconnected ||
                leaving ||
                !battleReady)
            {
                return;
            }

            if (isReloading && !CanPlay)
                return;

            try
            {
                await websocket.Send(
                    WsProtocol.Pack(
                        WsMessageType.PlayerReload,
                        new PlayerReloadModel
                        {
                            Id = id,
                            IsReloading = isReloading
                        }));
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "Reload state send failed: " + e.Message);
            }
        }
        
        private void HandleBodyPosition(WsEnvelopeModel model)
        {
            if (IsMatchEnded) return;
            
            var resp = WsProtocol.UnpackPayload<PlayerBodyPositionModel>(model);

            if (resp.Id == id) return;

            if (!spawned.TryGetValue(resp.Id, out var t) || t == null)
            {
                Debug.LogWarning($"No spawned object for id {resp.Id}");
                return;
            }

            t.Apply(resp);
        }

        private void HandleReload(PlayerReloadModel model)
        {
            if (IsMatchEnded || model.Id == id)
                return;

            if (spawned.TryGetValue(model.Id, out var visual) &&
                visual != null)
            {
                visual.SetReloading(model.IsReloading);
            }
        }
        
        private void HandlePlayerLeft(PlayerLeftModel model)
        {
            bool firstLeave = departedPlayerIds.Add(model.Id);

            if (spawned.TryGetValue(model.Id, out var visual))
            {
                spawned.Remove(model.Id);

                if (visual != null)
                {
                    // Сразу скрываем модель и коллайдеры.
                    visual.gameObject.SetActive(false);
                    Destroy(visual.gameObject);
                }
            }
            
            if (firstLeave)
                PlayerLeft?.Invoke(model.Id);
        }

        private void FailConnection(string reason)
        {
            Debug.Log(
                $"[BattleClient] FailConnection: {reason}; " +
                $"leaving={leaving}; disconnected={IsDisconnected}; " +
                $"ended={IsMatchEnded}");
            if (leaving || IsDisconnected) return;

            // После окончания матча сохраняем финальные окна.
            if (IsMatchEnded)
            {
                Leave();
                return;
            }

            IsDisconnected = true;
            DisconnectReason = reason;

            FreezeLocalPlayer();

            foreach (var visual in spawned.Values)
            {
                if (visual != null)
                    visual.Freeze();
            }

            Leave();
            BattleDisconnected?.Invoke(reason);
        }

        private async void SendHeartbeat()
        {
            if (heartbeatSending || websocket == null)
                return;

            heartbeatSending = true;
            var socket = websocket;

            try
            {
                await socket.Send(
                    WsProtocol.Pack(WsMessageType.BattleHeartbeat));
            }
            catch
            {
                FailConnection("connection_lost");
            }
            finally
            {
                heartbeatSending = false;
            }
        }
        
        private void HandleSpawn(WsEnvelopeModel model)
        {
            var resp = WsProtocol.UnpackPayload<BattlePingResponseModel>(model);

            // сервер отклонил вход: комната закрыта, игрок не участник и т.д.
            if (!string.IsNullOrEmpty(resp.Error))
            {
                FailConnection(resp.Error);
                return;
            }
            battleReady = true;
            foreach (var s in resp.Spawns)
            {
                var pos = new Vector3(s.Position.X, s.Position.Y, s.Position.Z);

                if (s.Id == this.id)
                {
                    livekitToken = resp.LivekitToken;
                    FindAnyObjectByType<Camera>().gameObject.SetActive(false);

                    GameObject player = Instantiate(playerPrefab, pos, Quaternion.identity);
                    localPlayer = player;
                    player.GetComponent<NetworkPlayer>().playerId = s.Id;
                    player.name = "Player_" + s.Id;
                    localDamageable = player.GetComponent<Damageable>();
                    localRespawn = player.GetComponent<NetworkRespawn>();
                }
                else
                {
                    GameObject player = Instantiate(enemyPrefab, pos, Quaternion.identity);
                    player.name = "Player_" + s.Id;
                    player.GetComponent<NetworkPlayer>().playerId = s.Id;
                    spawned[s.Id] = player.GetComponent<RemotePlayerVisuals>();
                }
            }

            GetComponent<Livekit>().ConnectToRoom(id, livekitToken);
        }

        private void HandleShoot(WsEnvelopeModel model)
        {
            if (IsMatchEnded) return;
            
            var resp = WsProtocol.UnpackPayload<ShootModel>(model);
            if (resp.Id == id) return;

            if (spawned.TryGetValue(resp.Id, out var shooter) && shooter != null)
                shooter.Shoot();
        }

        private void HandleHealth(PlayerHealthModel m)
        {
            if (IsMatchEnded) return;
            
            if (m.Id == id)
            {
                if (localDamageable != null) localDamageable.health = m.Health;
                return;
            }

            if (spawned.TryGetValue(m.Id, out var v) && v != null) v.SetHealth(m.Health);
        }

        private void HandleDied(PlayerDiedModel m)
        {
            if (IsMatchEnded) return;
            
            if (m.VictimId != id && spawned.TryGetValue(m.VictimId, out var v) && v != null)
                v.SetAlive(false);

            PlayerKilled?.Invoke(m.VictimId, m.KillerId);
        }

        private void HandleRespawned(PlayerRespawnedModel m)
        {
            if (IsMatchEnded) return;
            
            var pos = new Vector3(m.Position.X, m.Position.Y, m.Position.Z);

            if (m.Id == id)
            {
                if (localRespawn != null) localRespawn.Respawn(pos);
                return;
            }

            if (spawned.TryGetValue(m.Id, out var v) && v != null) v.OnRemoteRespawn(pos);
        }

        private void HandleMatchEnded(MatchEndedModel m)
        {
            if (IsMatchEnded) return;

            IsMatchEnded = true;
            FinalResult = m;

            FreezeLocalPlayer();

            foreach (var visual in spawned.Values)
            {
                if (visual != null)
                    visual.Freeze();
            }

            MatchEnded?.Invoke(m);
        }

        private void FreezeLocalPlayer()
        {
            FPSFrameworkCore.IsInputActive = false;

            if (localPlayer == null) return;

            // Останавливаем скрипты FPS Framework у локального персонажа.
            foreach (var script in
                     localPlayer.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (script != null
                    && script.GetType().Namespace != null
                    && script.GetType().Namespace.StartsWith("Akila.FPSFramework"))
                {
                    script.enabled = false;
                }
            }

            foreach (var body in
                     localPlayer.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.constraints = RigidbodyConstraints.FreezeAll;
            }

            foreach (var controller in
                     localPlayer.GetComponentsInChildren<CharacterController>(true))
            {
                controller.enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (!IsBattleStarted ||
                IsMatchEnded ||
                IsDisconnected ||
                VideoMovementBlocked)
            {
                FPSFrameworkCore.IsInputActive = false;
            }
        }

        private void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            websocket?.DispatchMessageQueue();
#endif

            if (leaving || IsDisconnected || !connectingStarted)
                return;

            float now = Time.realtimeSinceStartup;

            if (!opened)
            {
                if (now - connectStartedAt > 10f)
                    FailConnection("connection_timeout");

                return;
            }

            if (now - lastReceivedAt > 30f)
            {
                FailConnection("connection_timeout");
                return;
            }

            // Запасной клиентский таймаут ожидания старта.
            if (!battleReady && now - openedAt > 45f)
            {
                FailConnection("loading_timeout");
                return;
            }

            if (websocket != null
                && websocket.State == WebSocketState.Open
                && now >= nextHeartbeatAt)
            {
                nextHeartbeatAt = now + 5f;
                SendHeartbeat();
            }
        }

        private void OnDestroy()
        {
            Leave();

            if (Instance == this)
                Instance = null;
        }

        private void OnApplicationQuit() => Leave();

        public void Leave()
        {
            // Устанавливаем до закрытия сокета, чтобы обычный выход
            // не считался потерей соединения.
            leaving = true;
            GetComponent<BattleCameraGuard>()?.EndWatch();
            GetComponent<Livekit>()?.DisconnectFromRoom();

            var socket = websocket;
            websocket = null;

            SafeClose(socket);
        }

        private static async void SafeClose(WebSocket socket)
        {
            try
            {
                if (socket != null
                    && socket.State != WebSocketState.Closed)
                {
                    await socket.Close();
                }
            }
            catch
            {
            }
        }
    }
}