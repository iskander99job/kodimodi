using System.Collections.Generic;
using Akila.FPSFramework;
using Kodimodi.Models;
using NativeWebSocket;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kodimodi
{
    public class BattleClient : MonoBehaviour
    {
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private GameObject playerPrefab, enemyPrefab;
        // Куда вернуться, если сервер отклонил вход. Пусто: только лог
        [SerializeField] private string menuSceneName = "";

        private readonly Dictionary<int, RemotePlayerVisuals> spawned = new();
        public IReadOnlyDictionary<int, RemotePlayerVisuals> Spawned => spawned;

        public WebSocket websocket;
        public int id;
        public string livekitToken;
        private Damageable localDamageable;
        public static BattleClient Instance { get; private set; }

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
            
            // id и комнату выдал сервер при старте матча (см. MatchStarted)
            if (string.IsNullOrEmpty(MatchSession.RoomId))
            {
                Debug.LogError("Нет данных матча. Запускайте игру из меню: создайте комнату и нажмите «Начать».");
                return;
            }
            
            id = MatchSession.PlayerId;
            
            websocket = new WebSocket($"{ServerConfig.WsUrl}/battle");
            
            websocket.OnOpen += async () =>
            {
                var model = new BattlePingRequestModel
                {
                    id = this.id,
                    RoomId = MatchSession.RoomId
                };
            
                await websocket.Send(WsProtocol.Pack(WsMessageType.BattlePingRequest, model));
            };
            
            websocket.OnError += e => Debug.Log("Error! " + e);
            websocket.OnClose += code => Debug.Log("Connection closed!");
            
            websocket.OnMessage += bytes =>
            {
                WsEnvelopeModel model = WsProtocol.Unpack(bytes);
            
                if (model.Type == WsMessageType.BattlePingResponse)
                    HandleSpawn(model);
            
                if (model.Type == WsMessageType.PlayerBodyPosition)
                    HandleBodyPosition(model);
            
                if (model.Type == WsMessageType.Shoot)
                    HandleShoot(model);
            };
            
            await websocket.Connect();
        }

        private void HandleBodyPosition(WsEnvelopeModel model)
        {
            var resp = WsProtocol.UnpackPayload<PlayerBodyPositionModel>(model);

            if (resp.Id == id) return;

            if (!spawned.TryGetValue(resp.Id, out var t) || t == null)
            {
                Debug.LogWarning($"No spawned object for id {resp.Id}");
                return;
            }

            t.Apply(resp);
        }

        private void HandleSpawn(WsEnvelopeModel model)
        {
            var resp = WsProtocol.UnpackPayload<BattlePingResponseModel>(model);

            // сервер отклонил вход: комната закрыта, игрок не участник и т.д.
            if (!string.IsNullOrEmpty(resp.Error))
            {
                Debug.LogError($"Сервер отклонил вход в матч: {resp.Error}");
                if (!string.IsNullOrEmpty(menuSceneName))
                    SceneManager.LoadScene(menuSceneName);
                return;
            }

            foreach (var s in resp.Spawns)
            {
                var pos = new Vector3(s.Position.X, s.Position.Y, s.Position.Z);

                if (s.Id == this.id)
                {
                    livekitToken = resp.LivekitToken;
                    FindAnyObjectByType<Camera>().gameObject.SetActive(false);

                    GameObject player = Instantiate(playerPrefab, pos, Quaternion.identity);
                    player.GetComponent<NetworkPlayer>().playerId = s.Id;
                    player.name = "Player_" + s.Id;
                    localDamageable = player.GetComponentInChildren<Damageable>();
                }
                else
                {
                    GameObject player = Instantiate(enemyPrefab, pos, Quaternion.identity);
                    player.name = "Player_" + s.Id;
                    player.GetComponent<NetworkPlayer>().playerId = s.Id;
                    spawned[s.Id] = player.GetComponent<RemotePlayerVisuals>();
                }
            }

            //GetComponent<Livekit>().ConnectToRoom(this.id, livekitToken);
        }

        private void HandleShoot(WsEnvelopeModel model)
        {
            var resp = WsProtocol.UnpackPayload<ShootModel>(model);
            if (resp.Id == id) return;

            spawned.TryGetValue(resp.Id, out var shooter);

            // попали в меня: урон применяю у себя
            if (resp.Damage > 0f && resp.HitPlayerId == id && localDamageable != null)
                localDamageable.Damage(resp.Damage, shooter != null ? shooter.gameObject : null);

            // просто выстрел: анимация у стрелявшего
            if (resp.Damage <= 0f && shooter != null)
                shooter.Shoot();
        }

        void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            websocket?.DispatchMessageQueue();
#endif
        }

        private async void OnDestroy()
        {
            if (websocket != null) await websocket.Close();
            if (Instance == this) Instance = null;
        }

        private async void OnApplicationQuit()
        {
            if (websocket != null) await websocket.Close();
        }
    }
}