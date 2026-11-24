using System;
using Kodimodi.Models;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Kodimodi.UI
{
    public class RoomPreviewController : MonoBehaviour
    {
        public bool IsPreviewing { get; private set; }

        private const int RequestTimeoutSeconds = 10;
        private const float PollIntervalSeconds = 2f;

        private LobbyClient lobby;
        private ProfileController profile;
        private ConsentPopupController consentController;

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
            consentController = FindAnyObjectByType<ConsentPopupController>(
                FindObjectsInactive.Include);

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

            if (string.IsNullOrWhiteSpace(code))
            {
                ErrorPopup.Show(
                    "The link is missing a room code.",
                    "Could not open the match");

                return;
            }

            roomCode = code.Trim().ToUpperInvariant();

            IsPreviewing = true;
            canJoin = false;

            lobby.ShowPreview(roomCode);

            pollingRoutine = StartCoroutine(PollRoom());
        }

        private IEnumerator PollRoom()
        {
            // Позволяет сохранить ссылку pollingRoutine
            // до выполнения первого запроса.
            yield return null;

            try
            {
                while (IsPreviewing)
                {
                    bool continuePolling = true;

                    string url =
                        $"{ServerConfig.HttpUrl}/rooms/" +
                        $"{Uri.EscapeDataString(roomCode)}/preview";

                    using (var request = UnityWebRequest.Get(url))
                    {
                        activeRequest = request;
                        request.timeout = RequestTimeoutSeconds;

                        yield return request.SendWebRequest();

                        activeRequest = null;

                        if (!IsPreviewing)
                            yield break;

                        if (request.result !=
                            UnityWebRequest.Result.Success)
                        {
                            canJoin = false;

                            Debug.LogWarning(
                                $"Room preview failed: " +
                                $"HTTP {request.responseCode}; " +
                                $"{request.error}");

                            if (request.responseCode == 404)
                            {
                                lobby.SetPreviewError(
                                    "The room was not found or no longer " +
                                    "exists. Check the link.");

                                continuePolling = false;
                            }
                            else if (
                                request.responseCode == 401 ||
                                request.responseCode == 403)
                            {
                                lobby.SetPreviewError(
                                    "You do not have access to this room.");

                                continuePolling = false;
                            }
                            else if (request.responseCode == 400)
                            {
                                lobby.SetPreviewError(
                                    "The server rejected the room request. " +
                                    "Check the match link.");

                                continuePolling = false;
                            }
                            else
                            {
                                lobby.SetPreviewError(
                                    "Could not load room data.\n\n" +
                                    "Check your internet connection. " +
                                    "Retrying automatically.");
                            }
                        }
                        else
                        {
                            continuePolling = ApplyResponse(
                                request.downloadHandler.text);
                        }
                    }

                    if (!continuePolling)
                        yield break;

                    yield return new WaitForSecondsRealtime(
                        PollIntervalSeconds);
                }
            }
            finally
            {
                activeRequest = null;
                pollingRoutine = null;
            }
        }

        // false — дальнейший опрос комнаты не нужен.
        private bool ApplyResponse(string json)
        {
            PreviewResponse data;

            try
            {
                data = JsonUtility.FromJson<PreviewResponse>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Ошибка чтения комнаты: " +
                    exception.Message);

                ShowInvalidResponse();
                return true;
            }

            if (data == null ||
                string.IsNullOrWhiteSpace(data.code) ||
                !string.Equals(
                    data.code,
                    roomCode,
                    StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(data.state) ||
                data.players == null ||
                data.maxPlayers < 2 ||
                data.players.Length > data.maxPlayers)
            {
                ShowInvalidResponse();
                return true;
            }

            foreach (var player in data.players)
            {
                if (player == null)
                {
                    ShowInvalidResponse();
                    return true;
                }
            }

            if (data.state == "started")
            {
                canJoin = false;

                lobby.SetPreviewError(
                    "The match has already started. " +
                    "You can no longer join it.");

                return false;
            }

            if (data.state == "closed")
            {
                canJoin = false;

                lobby.SetPreviewError(
                    "The room is closed. " +
                    "Ask the host to send a new link.");

                return false;
            }

            if (data.state != "lobby")
            {
                ShowInvalidResponse();
                return true;
            }

            canJoin = data.canJoin;

            string status = canJoin
                ? "Click ‘Join’ to enter."
                : "Joining this room is currently unavailable.";

            lobby.ApplyPreview(
                Array.ConvertAll(data.players, player => new LobbyPlayerModel
                {
                    Id = player.id,
                    Nickname = player.nickname
                }),
                data.maxPlayers,
                canJoin,
                status);

            return true;
        }

        private void ShowInvalidResponse()
        {
            canJoin = false;

            lobby.SetPreviewError(
                "The server returned invalid room data.\n\n" +
                "Retrying automatically.");
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
                    "RoomPreviewController: " +
                    "не найден ProfileController.",
                    this);

                ErrorPopup.Show(
                    "Could not prepare to join the match. " +
                    "Try refreshing the page.",
                    "Could not connect");

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