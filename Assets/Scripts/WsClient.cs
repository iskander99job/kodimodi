using System.Text;
using Dto;
using MemoryPack;
using NativeWebSocket;
using UnityEngine;

public class WsClient : MonoBehaviour
{
    [SerializeField] private string serverUrl = "ws://localhost:8080/ws";
    private WebSocket _ws;

    async void Start()
    {
        _ws = new WebSocket(serverUrl);

        _ws.OnOpen += () =>
        {
            Debug.Log("Подключено, отправляю MoveMessage");
            SendMove();
        };

        _ws.OnMessage += (bytes) =>
        {
            var msg = Encoding.UTF8.GetString(bytes);
            Debug.Log($"Получено от сервера: {msg}");
        };

        _ws.OnError += (e) => Debug.LogError($"Ошибка: {e}");
        _ws.OnClose += (e) => Debug.Log("Соединение закрыто");

        await _ws.Connect();
    }

    private async void SendMove()
    {
        var msg = new MoveMessage
        {
            X = 1.5f,
            Y = 0f,
            Z = 3.2f,
            RY = 45f
        };

        var bytes = MemoryPackSerializer.Serialize(msg);
        Debug.Log($"Отправляю {bytes.Length} байт: [{string.Join(",", bytes)}]");

        await _ws.Send(bytes); // ← Send, а не SendText! Binary-фрейм
    }
    
    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        _ws?.DispatchMessageQueue();
#endif
    }

    private async void OnApplicationQuit()
    {
        if (_ws != null) await _ws.Close();
    }
}