using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MemoryPack;
using NativeWebSocket;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kodimodi
{
    public class MatchmakingClient : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button findMatchButton, createMatchButton;
        [SerializeField] private TMP_Text findMatchText;
        [SerializeField] private TMP_InputField playersCountUI;

        private readonly string matchmakingUrl = "ws://localhost:8080/matchmaking";

        private WebSocket _webSocket;

        private void Awake()
        {
            findMatchText.gameObject.SetActive(false);
            findMatchButton.interactable = true;
        }

        async void Start()
        {
            _webSocket = new WebSocket(matchmakingUrl);

            _webSocket.OnOpen += () =>
            {
                Debug.Log("Connection open!");
            };
            _webSocket.OnError += (e) => Debug.Log("Error! " + e);
            _webSocket.OnClose += (code) => Debug.Log("Connection closed!");

            _webSocket.OnMessage += (bytes) =>
            {
                Debug.Log("Received: ");
            };

            await _webSocket.Connect();
        }
        
        private void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            _webSocket?.DispatchMessageQueue();
#endif
        }

        private async void OnDestroy()
        {
            if (_webSocket != null)
            {
                try { await _webSocket.Close(); }
                catch
                {
                    // ignored
                }
            }
        }
        
        private async void OnApplicationQuit()
        {
            if (_webSocket != null)
            {
                try { await _webSocket.Close(); }
                catch
                {
                    // ignored
                }
            }
        }
    }
}