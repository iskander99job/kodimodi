using System;
using System.Text;
using NativeWebSocket;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameClient : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private Transform[] spawnPoints;   // 2 элемента
    [SerializeField] private GameObject playerPrefab;

    private WebSocket _ws;
    private GameObject _localPlayer;
    private int _playerIndex = -1;

    // async void Start()
    // {
    //     if (string.IsNullOrEmpty(MatchData.RoomId))
    //     {
    //         Debug.LogError("RoomId пустой! Запусти сцену StartScene.");
    //         SceneManager.LoadScene("StartScene");
    //         return;
    //     }
    //
    //     if (spawnPoints == null || spawnPoints.Length < 2)
    //     {
    //         Debug.LogError("Нужно минимум 2 spawnPoints!");
    //         return;
    //     }
    //
    //     var url = $"ws://localhost:8080/ws?room={MatchData.RoomId}";
    //     Debug.Log($"Подключаюсь к {url}");
    //
    //     _ws = new WebSocket(url);
    //
    //     _ws.OnOpen += () => Debug.Log("Соединение открыто");
    //
    //     _ws.OnMessage += (bytes) =>
    //     {
    //         var msg = Encoding.UTF8.GetString(bytes);
    //         Debug.Log($"Game: {msg}");
    //
    //         var envelope = JsonUtility.FromJson<MessageEnvelope>(msg);
    //
    //         switch (envelope.type)
    //         {
    //             case "welcome":
    //                 var welcome = JsonUtility.FromJson<WelcomeMessage>(msg);
    //                 HandleWelcome(welcome.playerIndex);
    //                 break;
    //         }
    //     };
    //
    //     _ws.OnError += (e) => Debug.LogError($"Game ошибка: {e}");
    //     _ws.OnClose += (e) => Debug.Log("Игра закрыта");
    //
    //     await _ws.Connect();
    // }

    private void HandleWelcome(int playerIndex)
    {
        _playerIndex = playerIndex;
        Debug.Log($"Мой индекс игрока: {_playerIndex}");

        var spawn = spawnPoints[_playerIndex];

        _localPlayer = Instantiate(playerPrefab, spawn.position, spawn.rotation);
        _localPlayer.name = $"LocalPlayer_{_playerIndex}";

        // Включаем камеру только у своего префаба
        // var camera = _localPlayer.GetComponentInChildren<Camera>(true);
        // if (camera != null)
        //     camera.gameObject.SetActive(true);
        // else
        //     Debug.LogWarning("В префабе нет Camera!");
    }

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

    public void LeaveMatch()
    {
        MatchData.Clear();
        SceneManager.LoadScene("StartScene");
    }

    [Serializable]
    private class MessageEnvelope
    {
        public string type;
    }

    [Serializable]
    private class WelcomeMessage
    {
        public string type;
        public int playerIndex;
    }
}