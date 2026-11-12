using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Kodimodi.UI
{
    public class RoomPreviewController : MonoBehaviour
    {
        public bool IsPreviewing { get; private set; }

        private LobbyClient lobby;
        private ProfileController profile;

        private string roomCode;
        private bool canJoin;

        private Coroutine pollingRoutine;
        private UnityWebRequest activeRequest;

        [Serializable]
        private class PreviewResponse
        {
            public string code;
            public int maxPlayers;
            public string state;
            public bool canJoin;
            public PreviewPlayer[] players;
        }

        [Serializable]
        private class PreviewPlayer
        {
            public int id;
            public string nickname;
        }

        private void Awake()
        {
            lobby = GetComponent<LobbyClient>();
            profile = FindAnyObjectByType<ProfileController>();

            if (lobby == null)
            {
                Debug.LogError(
                    "RoomPreviewController должен находиться " +
                    "на одном объекте с LobbyClient.",
                    this);

                enabled = false;
            }
        }

        public void Show(string code)
        {
            if (!isActiveAndEnabled || lobby == null)
                return;

            Close();

            roomCode = code;
            IsPreviewing = true;
            canJoin = false;

            lobby.ShowPreview(code);

            pollingRoutine = StartCoroutine(PollRoom());
        }

        private IEnumerator PollRoom()
        {
            while (IsPreviewing)
            {
                using (var request = UnityWebRequest.Get(
                    $"{ServerConfig.HttpUrl}/rooms/{roomCode}/preview"))
                {
                    activeRequest = request;
                    request.timeout = 10;

                    yield return request.SendWebRequest();

                    activeRequest = null;

                    if (!IsPreviewing)
                        yield break;

                    if (request.result !=
                        UnityWebRequest.Result.Success)
                    {
                        canJoin = false;

                        lobby.SetPreviewError(
                            request.responseCode == 404
                                ? "Комната больше не существует."
                                : "Не удалось обновить комнату. Повторяем…");
                    }
                    else
                    {
                        ApplyResponse(request.downloadHandler.text);
                    }
                }

                yield return new WaitForSecondsRealtime(2f);
            }

            pollingRoutine = null;
        }

        private void ApplyResponse(string json)
        {
            PreviewResponse data;

            try
            {
                data = JsonUtility.FromJson<PreviewResponse>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Ошибка чтения комнаты: " + exception.Message);

                canJoin = false;
                lobby.SetPreviewError(
                    "Не удалось прочитать данные комнаты.");

                return;
            }

            if (data == null ||
                string.IsNullOrEmpty(data.state) ||
                data.players == null ||
                data.maxPlayers < 1)
            {
                canJoin = false;
                lobby.SetPreviewError(
                    "Не удалось прочитать данные комнаты.");

                return;
            }

            canJoin = data.state == "lobby" && data.canJoin;

            string status;

            if (data.state == "started")
                status = "Матч уже начался.";
            else if (data.state == "closed")
                status = "Комната закрыта.";
            else if (!canJoin)
                status = "Нет свободных мест.";
            else
                status = "Нажми «Подключиться», чтобы войти.";

            lobby.ApplyPreview(
                data.players.Length,
                data.maxPlayers,
                canJoin,
                status);
        }

        public void OnJoinClick()
        {
            if (!IsPreviewing || !canJoin)
                return;

            if (profile == null)
                profile = FindAnyObjectByType<ProfileController>();

            if (profile == null)
            {
                Debug.LogError(
                    "RoomPreviewController: не найден ProfileController.",
                    this);

                return;
            }

            if (!profile.MediaReady)
            {
                profile.ShowDeviceSetupForLobby();
                return;
            }

            canJoin = false;
            lobby.JoinPreviewRoom();
        }

        public void Close()
        {
            IsPreviewing = false;
            canJoin = false;

            if (activeRequest != null)
            {
                activeRequest.Abort();
                activeRequest = null;
            }

            if (pollingRoutine != null)
            {
                StopCoroutine(pollingRoutine);
                pollingRoutine = null;
            }
        }

        private void OnDisable()
        {
            Close();
        }
    }
}