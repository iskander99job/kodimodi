using NativeWebSocket;
using UnityEngine;


namespace Kodimodi
{
    public class GameClient : MonoBehaviour
    {
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private GameObject playerPrefab, enemyPrefab;

        private WebSocket _ws;
        private GameObject _localPlayer;
        private Transform _localCameraRoot;
        
        void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            _ws?.DispatchMessageQueue();
#endif
        }

        private async void OnDestroy()
        {
            if (_ws != null) await _ws.Close();
        }
        
    }
}