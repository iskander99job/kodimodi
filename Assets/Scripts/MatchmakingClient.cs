using System;
using System.Text;
using NativeWebSocket;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MatchmakingClient : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button findMatchButton;
    [SerializeField] private TMP_Text findMatchText;

    [Header("Network")]
    [SerializeField] private string matchmakingUrl = "ws://localhost:8080/matchmaking";
    [SerializeField] private string playerName = "Player";
    [SerializeField] private string gameSceneName = "SampleScene";

    private WebSocket _mm;
    private bool _matchFound;

    void Awake()
    {
        findMatchText.gameObject.SetActive(false);
        findMatchButton.interactable = true;
    }

    public void FindMatch()
    {
        findMatchButton.interactable = false;
        findMatchText.gameObject.SetActive(true);
        findMatchText.text = "Поиск противника...";
        _ = ConnectAndWait();
    }

    private async System.Threading.Tasks.Task ConnectAndWait()
    {
        try
        {
            _mm = new WebSocket(matchmakingUrl);

            _mm.OnOpen += () =>
            {
                Debug.Log("Подключено к матчмейкингу, ждём противника...");
            };

            _mm.OnMessage += (bytes) =>
            {
                var json = Encoding.UTF8.GetString(bytes);
                Debug.Log($"MM: {json}");

                var response = JsonUtility.FromJson<MatchFoundResponse>(json);
                if (response.type == "matchfound")
                {
                    Debug.Log($"Противник найден! Комната: {response.roomId}");

                    MatchData.RoomId = response.roomId;
                    MatchData.PlayerName = playerName;

                    findMatchText.text = "Противник найден!";

                    // НЕ грузим сцену здесь — только ставим флаг
                    _matchFound = true;
                }
            };

            _mm.OnError += (e) =>
            {
                Debug.LogError($"MM ошибка: {e}");
                findMatchButton.interactable = true;
                findMatchText.text = "Ошибка подключения";
            };

            _mm.OnClose += (e) => Debug.Log("MM соединение закрыто");

            await _mm.Connect();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Не удалось подключиться: {ex.Message}");
            findMatchButton.interactable = true;
            findMatchText.text = "Ошибка подключения";
        }
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        _mm?.DispatchMessageQueue();
#endif

        // Грузим сцену из главного потока, вне сетевого колбэка
        if (_matchFound)
        {
            _matchFound = false;
            Debug.Log($"Загружаю сцену {gameSceneName}");
            SceneManager.LoadScene(1);
        }
    }

    private async void OnDestroy()
    {
        if (_mm != null) await _mm.Close();
    }

    [Serializable]
    private class MatchFoundResponse
    {
        public string type;
        public string roomId;
    }
}