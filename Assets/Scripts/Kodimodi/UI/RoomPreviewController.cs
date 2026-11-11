using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace Kodimodi.UI
{
    [RequireComponent(typeof(CreateMatch))]
    public class RoomPreviewController : MonoBehaviour
    {
        [SerializeField] private GameObject joinButton;
        [SerializeField] private TMP_Text statusText;

        public bool IsPreviewing { get; private set; }
        public bool IsAwaitingMedia { get; private set; }

        private CreateMatch match;
        private ProfileController profile;

        private string roomCode;
        private bool canJoin;

        private Coroutine pollingRoutine;
        private Coroutine mediaRoutine;
        private UnityWebRequest request;

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
            match = GetComponent<CreateMatch>();
            profile = GetComponentInParent<ProfileController>();

            UpdateJoinButton();
        }

        public void Show(string code)
        {
            Close();

            roomCode = code;
            IsPreviewing = true;

            match.SetMatchLink(code);
            match.createMatchModal.SetActive(false);
            match.searchMatchModal.SetActive(true);
            match.startMatchBtnUI.SetActive(false);
            match.cancelMatchBtnUI.SetActive(true);

            if (match.searchMatchModalSpinner != null)
                match.searchMatchModalSpinner.SetActive(false);

            ClearPlayers();
            SetStatus("Загружаем комнату…");

            match.modal.TurnOn();
            pollingRoutine = StartCoroutine(PollRoom());
        }

        private IEnumerator PollRoom()
        {
            while (IsPreviewing)
            {
                using (var req = UnityWebRequest.Get(
                    $"{ServerConfig.HttpUrl}/rooms/{roomCode}/preview"))
                {
                    request = req;
                    req.timeout = 10;

                    yield return req.SendWebRequest();

                    request = null;

                    if (!IsPreviewing)
                        yield break;

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        canJoin = false;

                        SetStatus(
                            req.responseCode == 404
                                ? "Комната больше не существует."
                                : "Не удалось обновить комнату. Повторяем…");
                    }
                    else
                    {
                        ApplyResponse(req.downloadHandler.text);
                    }

                    UpdateJoinButton();
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
            catch (Exception e)
            {
                Debug.LogWarning(
                    "Ошибка чтения комнаты: " + e.Message);

                canJoin = false;
                SetStatus("Не удалось прочитать данные комнаты.");
                return;
            }

            if (data == null ||
                string.IsNullOrEmpty(data.state) ||
                data.players == null)
            {
                canJoin = false;
                SetStatus("Не удалось прочитать данные комнаты.");
                return;
            }

            RenderPlayers(data.players.Length);

            canJoin = data.state == "lobby" && data.canJoin;

            string message;

            if (data.state == "started")
                message = "Матч уже начался.";
            else if (data.state == "closed")
                message = "Комната закрыта.";
            else if (!canJoin)
                message = "Нет свободных мест.";
            else
                message = "Нажми «Подключиться», чтобы войти.";

            SetStatus(
                $"Игроки: {data.players.Length}/{data.maxPlayers}\n"
                + message);
        }

        private void ClearPlayers()
        {
            foreach (Transform child in
                     match.searchMatchScrollViewUI.transform)
            {
                Destroy(child.gameObject);
            }
        }

        private void RenderPlayers(int count)
        {
            ClearPlayers();

            for (int i = 0; i < count; i++)
            {
                var item = Instantiate(
                    match.searchMatchScrollViewItemPrefabUI,
                    match.searchMatchScrollViewUI.transform);

                var label = item.GetComponentInChildren<TMP_Text>();

                if (label != null)
                    label.text = $"Игрок {i + 1}";
            }
        }

        // Вызывается кнопкой «Подключиться».
        public void OnJoinClick()
        {
            if (!IsPreviewing || !canJoin || IsAwaitingMedia)
                return;

            if (profile == null || !profile.isActiveAndEnabled)
            {
                SetStatus("Не найден активный ProfileController.");
                return;
            }

            IsAwaitingMedia = true;
            UpdateJoinButton();

            // ProfileController откроет окно снова,
            // когда его проверка будет пройдена.
            match.modal.TurnOff();
            profile.OnPlayButtonClick();

            if (IsAwaitingMedia)
                mediaRoutine = StartCoroutine(WaitForCamera());
        }

        private IEnumerator WaitForCamera()
        {
            yield return null;

            while (IsPreviewing && IsAwaitingMedia)
            {
                if (profile == null || !profile.isActiveAndEnabled)
                    break;

                if (profile.IsCameraReady)
                {
                    mediaRoutine = null;

                    // Итоговую проверку камеры и микрофона
                    // выполняет существующий ProfileController.
                    profile.OnPlayButtonClick();
                    yield break;
                }

                yield return null;
            }

            mediaRoutine = null;
        }

        // Событие On On у LeanWindow окна создания матча.
        public void OnMediaApproved()
        {
            if (!IsPreviewing ||
                !IsAwaitingMedia ||
                profile == null ||
                !profile.IsCameraReady)
            {
                return;
            }

            match.JoinLinkedRoomAfterMediaCheck();
        }

        private void UpdateJoinButton()
        {
            if (joinButton != null)
            {
                joinButton.SetActive(
                    IsPreviewing && canJoin && !IsAwaitingMedia);
            }
        }

        private void SetStatus(string text)
        {
            if (statusText != null)
                statusText.text = text;
        }

        public void Close()
        {
            IsPreviewing = false;
            IsAwaitingMedia = false;
            canJoin = false;

            if (request != null)
            {
                request.Abort();
                request = null;
            }

            if (pollingRoutine != null)
            {
                StopCoroutine(pollingRoutine);
                pollingRoutine = null;
            }

            if (mediaRoutine != null)
            {
                StopCoroutine(mediaRoutine);
                mediaRoutine = null;
            }

            UpdateJoinButton();
            SetStatus("");
        }

        private void OnDisable()
        {
            Close();
        }
    }
}