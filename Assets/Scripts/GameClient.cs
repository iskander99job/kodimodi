using System;
using System.Collections.Generic;
using System.Text;
using Akila.FPSFramework;
using NativeWebSocket;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameClient : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private GameObject playerPrefab;

    private WebSocket _ws;
    private GameObject _localPlayer;
    private int _playerIndex = -1;
    private readonly Dictionary<int, GameObject> _remotePlayers = new();

    async void Start()
    {
        if (string.IsNullOrEmpty(MatchData.RoomId))
        {
            Debug.LogError("RoomId пустой! Запусти сцену StartScene.");
            SceneManager.LoadScene("StartScene");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length < 2)
        {
            Debug.LogError("Нужно минимум 2 spawnPoints!");
            return;
        }

        var url = $"ws://localhost:8080/ws?room={MatchData.RoomId}";
        Debug.Log($"Подключаюсь к {url}");

        _ws = new WebSocket(url);

        _ws.OnOpen += () => Debug.Log("Соединение открыто");

        _ws.OnMessage += (bytes) =>
        {
            var msg = Encoding.UTF8.GetString(bytes);
            Debug.Log($"Game: {msg}");

            var envelope = JsonUtility.FromJson<MessageEnvelope>(msg);

            switch (envelope.type)
            {
                case "welcome":
                    var welcome = JsonUtility.FromJson<WelcomeMessage>(msg);
                    HandleWelcome(welcome.playerIndex);
                    break;

                case "playerJoined":
                    var joined = JsonUtility.FromJson<PlayerJoinedMessage>(msg);
                    HandlePlayerJoined(joined.playerIndex);
                    break;

                case "playerLeft":
                    var left = JsonUtility.FromJson<PlayerJoinedMessage>(msg);
                    HandlePlayerLeft(left.playerIndex);
                    break;

                case "move":
                    var move = JsonUtility.FromJson<MoveMessage>(msg);
                    HandleRemoteMove(move);
                    break;
            }
        };

        _ws.OnError += (e) => Debug.LogError($"Game ошибка: {e}");
        _ws.OnClose += (e) => Debug.Log("Игра закрыта");

        await _ws.Connect();
    }

    private void HandleWelcome(int playerIndex)
    {
        _playerIndex = playerIndex;
        Debug.Log($"Мой индекс игрока: {_playerIndex}");

        var spawn = spawnPoints[_playerIndex];
        _localPlayer = Instantiate(playerPrefab, spawn.position, spawn.rotation);
        _localPlayer.name = $"LocalPlayer_{_playerIndex}";
        _localPlayer.gameObject.GetComponent<Actor>().isLocalPlayer = true;
        _localPlayer.gameObject.GetComponent<CharacterManager>().enabled = true;
        _localPlayer.gameObject.GetComponent<CharacterInput>().enabled = true;
        _localPlayer.gameObject.GetComponent<FirstPersonController>().enabled = true;
        _localPlayer.gameObject.GetComponent<Damageable>().enabled = true;
        _localPlayer.gameObject.GetComponent<AimAssister>().enabled = true;
        _localPlayer.gameObject.GetComponent<AimAssistTarget>().enabled = true;
        _localPlayer.gameObject.GetComponent<Ignore>().enabled = true;

        // Включаем камеру только у своего префаба
        var cam = _localPlayer.transform.Find("CameraRoot/CameraPivot/ViewAnimator/Cameras");
        if (cam != null)
            cam.gameObject.SetActive(true);
        else
            Debug.LogWarning("Не найден путь CameraRoot/CameraPivot/ViewAnimator/Cameras");
        
        var pistol = _localPlayer.transform.Find("CameraRoot/CameraPivot/ViewAnimator/Inventory/Pistol_1(Clone)");
        if (pistol != null)
        {
            pistol.gameObject.GetComponent<Firearm>().enabled = true;
            pistol.gameObject.GetComponent<FirearmAttachmentsManager>().enabled = true;
            pistol.gameObject.GetComponent<ItemInput>().enabled = true;
        }
    }

    private void HandlePlayerJoined(int playerIndex)
    {
        if (_remotePlayers.ContainsKey(playerIndex)) return;

        var spawn = spawnPoints[playerIndex];
        var remote = Instantiate(playerPrefab, spawn.position, spawn.rotation);
        remote.name = $"RemotePlayer_{playerIndex}";

        // Камера удалённого игрока — выключена
        var cam = remote.transform.Find("CameraRoot/CameraPivot/ViewAnimator/Cameras");
        if (cam != null)
            cam.gameObject.SetActive(false);

        // AudioListener тоже выключаем
        var listener = remote.GetComponentInChildren<AudioListener>(true);
        if (listener != null)
            listener.enabled = false;

        _remotePlayers[playerIndex] = remote;
        Debug.Log($"Создан удалённый игрок #{playerIndex}");
    }

    private void HandlePlayerLeft(int playerIndex)
    {
        if (_remotePlayers.TryGetValue(playerIndex, out var remote))
        {
            Destroy(remote);
            _remotePlayers.Remove(playerIndex);
            Debug.Log($"Удалён игрок #{playerIndex}");
        }
    }

    private void HandleRemoteMove(MoveMessage move)
    {
        if (_remotePlayers.TryGetValue(move.playerIndex, out var remote))
        {
            remote.transform.position = new Vector3(move.x, move.y, move.z);
            remote.transform.rotation = Quaternion.Euler(0, move.ry, 0);
        }
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

    [Serializable]
    private class PlayerJoinedMessage
    {
        public string type;
        public int playerIndex;
    }

    [Serializable]
    private class MoveMessage
    {
        public string type;
        public int playerIndex;
        public float x;
        public float y;
        public float z;
        public float ry;
    }
}