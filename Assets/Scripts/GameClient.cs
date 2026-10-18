using System;
using System.Collections.Generic;
using System.Text;
using NativeWebSocket;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LiveKit;

public class GameClient : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private GameObject playerPrefab, enemyPrefab;

    [Header("Network")]
    [SerializeField] private float sendInterval = 0.1f;
    [SerializeField] private float interpolationSpeed = 15f;

    [Header("Rig")]
    [SerializeField] private string cameraRootName = "CameraRoot";

    [Header("LiveKit Settings")]
    [SerializeField] private bool useLiveKit = true; // <--- ФЛАГ ДЛЯ ВКЛЮЧЕНИЯ/ВЫКЛЮЧЕНИЯ

    [Header("LiveKit Tokens")]
    private string token_1 = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJkZXZrZXkiLCJzdWIiOiJhbGljZSIsImV4cCI6MTc5MjM0NjYxMSwibmJmIjoxNzkyMjYwMjExLCJpYXQiOjE3OTIyNjAyMTEsImlkZW50aXR5IjoiYWxpY2UiLCJuYW1lIjoiYWxpY2UiLCJ2aWRlbyI6eyJyb29tSm9pbiI6dHJ1ZSwicm9vbSI6Im15LXJvb20ifX0.9dyFiiB-rOXjsj6sGB38yJ5MKB0OV40TmXoGCvtRyXQ";
    private string token_2 = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJkZXZrZXkiLCJzdWIiOiJib2IiLCJleHAiOjE3OTIzNDY2MzMsIm5iZiI6MTc5MjI2MDIzMywiaWF0IjoxNzkyMjYwMjMzLCJpZGVudGl0eSI6ImJvYiIsIm5hbWUiOiJib2IiLCJ2aWRlbyI6eyJyb29tSm9pbiI6dHJ1ZSwicm9vbSI6Im15LXJvb20ifX0.oRfbw3gTCcaaR5wrdyI2VsN3oXkDGgOtS1IaCBg2u9U";
    private string livekitURL = "ws://localhost:7880";

    private WebSocket _ws;
    private GameObject _localPlayer;
    private Transform _localCameraRoot;
    private int _playerIndex = -1;

    private readonly Dictionary<int, GameObject> _remotePlayers = new();
    private readonly Dictionary<int, Transform> _remoteCameraRoots = new();
    
    // LiveKit
    private Room m_Room;
    private readonly Dictionary<int, RawImage> _remoteVideoViews = new();
    private TrackPublication _remoteVideoPublication;

    private float _sendTimer;

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

        _localCameraRoot = _localPlayer.transform.Find(cameraRootName);
        if (_localCameraRoot != null)
            Debug.Log($"CameraRoot найден: {_localCameraRoot.name}");
        else
            Debug.LogWarning($"Не найден {cameraRootName} у локального игрока!");
    }

    private void HandlePlayerJoined(int playerIndex)
    {
        if (playerIndex == _playerIndex) return;
        if (_remotePlayers.ContainsKey(playerIndex)) return;

        var spawn = spawnPoints[playerIndex];
        var remote = Instantiate(enemyPrefab, spawn.position, spawn.rotation);
        remote.name = $"RemotePlayer_{playerIndex}";

        var listener = remote.GetComponentInChildren<AudioListener>(true);
        if (listener != null)
            listener.enabled = false;

        _remotePlayers[playerIndex] = remote;

        var remoteCamRoot = remote.transform.Find(cameraRootName);
        if (remoteCamRoot != null)
            _remoteCameraRoots[playerIndex] = remoteCamRoot;
        else
            Debug.LogWarning($"Не найден {cameraRootName} у удалённого игрока #{playerIndex}");

        // Работа с RawImage и LiveKit только если флаг включен
        if (useLiveKit)
        {
            var rawImage = remote.GetComponentInChildren<RawImage>(true);
            if (rawImage != null)
            {
                _remoteVideoViews[playerIndex] = rawImage;
                rawImage.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning($"Не найден RawImage в префабе удалённого игрока #{playerIndex}");
            }

            if (m_Room == null)
            {
                StartCoroutine(ConnectToLiveKit());
            }
        }
        else
        {
            // Если LiveKit выключен, просто скрываем экран, если он есть
            var rawImage = remote.GetComponentInChildren<RawImage>(true);
            if (rawImage != null)
                rawImage.gameObject.SetActive(false);
        }

        Debug.Log($"Создан удалённый игрок #{playerIndex}");
    }

    private System.Collections.IEnumerator ConnectToLiveKit()
    {
        m_Room = new Room();

        m_Room.ParticipantConnected += (p) => Debug.Log($"LiveKit: Participant connected: {p.Sid}");

        m_Room.TrackSubscribed += (track, publication, participant) =>
        {
            if (track.Kind == TrackKind.Video)
            {
                _remoteVideoPublication = publication;
                var video = track.Attach() as HTMLVideoElement;
                
                video.VideoReceived += tex =>
                {
                    if (_remoteVideoViews.TryGetValue(1 - _playerIndex, out var rawImage))
                    {
                        rawImage.texture = tex;
                        Debug.Log("Видео удаленного игрока привязано!");
                    }
                };
            }
            else if (track.Kind == TrackKind.Audio && publication is RemoteTrackPublication)
            {
                track.Attach();
            }
        };

        m_Room.TrackUnsubscribed += (track, publication, participant) =>
        {
            track.Detach();
        };

        string myToken = (_playerIndex == 0) ? token_1 : token_2;

        var c = m_Room.Connect(livekitURL, myToken);
        yield return c;

        if (c.IsError)
        {
            Debug.LogError("Не удалось подключиться к LiveKit!");
            yield break;
        }

        Debug.Log("LiveKit подключен");
        yield return m_Room.LocalParticipant.EnableCameraAndMicrophone();
    }

    private void HandlePlayerLeft(int playerIndex)
    {
        if (_remotePlayers.TryGetValue(playerIndex, out var remote))
        {
            Destroy(remote);
            _remotePlayers.Remove(playerIndex);
            _remoteCameraRoots.Remove(playerIndex);
            _remoteVideoViews.Remove(playerIndex);
            Debug.Log($"Удалён игрок #{playerIndex}");
        }
    }

    private void HandleRemoteMove(MoveMessage move)
    {
        if (!_remotePlayers.TryGetValue(move.playerIndex, out var remote)) return;

        remote.transform.position = Vector3.Lerp(
            remote.transform.position,
            new Vector3(move.x, move.y, move.z),
            Time.deltaTime * interpolationSpeed
        );

        if (_remoteCameraRoots.TryGetValue(move.playerIndex, out var camRoot))
        {
            camRoot.rotation = Quaternion.Slerp(
                camRoot.rotation,
                Quaternion.Euler(0, move.ry, 0),
                Time.deltaTime * interpolationSpeed
            );
        }
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        _ws?.DispatchMessageQueue();
#endif

        if (_ws != null && _ws.State == WebSocketState.Open && _localPlayer != null)
        {
            _sendTimer += Time.deltaTime;
            if (_sendTimer >= sendInterval)
            {
                _sendTimer = 0f;
                SendMove();
            }
        }
    }

    private async void SendMove()
    {
        if (_ws == null || _ws.State != WebSocketState.Open) return;
        if (_localPlayer == null) return;

        var pos = _localPlayer.transform.position;
        float ry = 0f;
        if (_localCameraRoot != null)
            ry = _localCameraRoot.eulerAngles.y;

        var msg = new MoveMessage
        {
            type = "move",
            playerIndex = _playerIndex,
            x = pos.x,
            y = pos.y,
            z = pos.z,
            ry = ry
        };

        var json = JsonUtility.ToJson(msg);

        try
        {
            await _ws.SendText(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Ошибка отправки move: {ex.Message}");
        }
    }

    private async void OnDestroy()
    {
        if (_ws != null) await _ws.Close();
        if (m_Room != null && useLiveKit) m_Room.Disconnect();
    }

    public void LeaveMatch()
    {
        MatchData.Clear();
        SceneManager.LoadScene("StartScene");
    }

    [Serializable] private class MessageEnvelope { public string type; }
    [Serializable] private class WelcomeMessage { public string type; public int playerIndex; }
    [Serializable] private class PlayerJoinedMessage { public string type; public int playerIndex; }
    [Serializable] private class MoveMessage { public string type; public int playerIndex; public float x, y, z, ry; }
}