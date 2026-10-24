using System.Collections.Generic;
using Kodimodi.Models;
using NativeWebSocket;
using UnityEngine;
using Random = UnityEngine.Random;


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
            id = Random.Range(352, 647392);
            
            websocket = new WebSocket("ws://localhost:8080/battle");
            
            websocket.OnOpen += async () =>
            {
                BattlePingRequestModel model = new BattlePingRequestModel
                {
                    id = this.id
                };

                var bytes = WsProtocol.Pack(WsMessageType.BattlePingRequest, model);
                
                await websocket.Send(bytes);
            };
            
            websocket.OnError += (e) =>
            {
                Debug.Log("Error! " + e);
            };
            websocket.OnClose += (code) =>
            {
                Debug.Log("Connection closed!");
            };

            websocket.OnMessage += (bytes) =>
            {
                WsEnvelopeModel model = WsProtocol.Unpack(bytes);

                if (model.Type == WsMessageType.BattlePingResponse)
                {
                    HandleSpawn(model);
                }

                if (model.Type == WsMessageType.PlayerBodyPosition)
                {
                    HandleBodyPosition(model);
                }
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

            foreach (var s in resp.Spawns)
            {
                var pos = new Vector3(s.Position.X, s.Position.Y, s.Position.Z);

                if (s.Id == this.id)
                {
                    FindAnyObjectByType<Camera>().gameObject.SetActive(false);
                            
                    GameObject player = Instantiate(playerPrefab, pos, Quaternion.identity);
                    player.name = "Player_" + s.Id;
                }
                else
                {
                    GameObject player = Instantiate(enemyPrefab, pos, Quaternion.identity);
                    player.name = "Player_" + s.Id;
                    spawned[s.Id] = player.GetComponent<RemotePlayerVisuals>();
                }
            }
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
            await websocket.Close();
        }
    }
}