using System;
using System.Collections;
using System.Collections.Generic;
using LiveKit;
using UnityEngine;
using UnityEngine.UI;

namespace Kodimodi
{
    public class Livekit : MonoBehaviour
    {
        private static string LivekitURL = "ws://localhost:7880";

        private const float ConnectionRetryInterval = 2f;
        private const float VideoBindInterval = 0.25f;

        [SerializeField, Min(0f)] private float videoSubscribeDistance = 15f;
        [SerializeField, Min(0f)] private float videoUnsubscribeDistance = 20f;

        [SerializeField]
        private BattleCameraGuard cameraGuard;

        private sealed class VideoBinding
        {
            public Track Track;
            public string Identity;

            public RawImage Image;
            public Texture Placeholder;
            public Rect PlaceholderUv;
            public HTMLVideoElement Video;

            public HTMLVideoElement.VideoReceivedDelegate VideoReceived;
        }

        private readonly Dictionary<TrackPublication, VideoBinding>
            videoBindings =
                new Dictionary<TrackPublication, VideoBinding>();

        private readonly HashSet<Track> attachedTracks =
            new HashSet<Track>();

        private readonly HashSet<string> requestedSubscriptions = new();
        private readonly HashSet<string> seenPublications = new();

        [SerializeField] private bool showVideoSubscriptionLines = true;

#if UNITY_WEBGL && !UNITY_EDITOR
        private readonly HashSet<int> playersWithVideo = new();
        private readonly Dictionary<int, LineRenderer> subscriptionLines = new();
        private readonly List<int> removedLines = new();
        private Material subscriptionLineMaterial;
#endif

        private Room mRoom;
        private BattleClient battleClient;
        private bool refreshSubscriptions;
        private LivekitCameraMonitor monitor;

        private int localId;
        private string roomToken;

        private bool disconnecting;
        private bool retryConnectionRequested;

        private float nextVideoBind;

        public void ConnectToRoom(int localId, string roomToken)
        {
            DisconnectFromRoom();

            disconnecting = false;

            this.localId = localId;
            this.roomToken = roomToken;

            nextVideoBind = 0f;
            battleClient = GetComponent<BattleClient>();

            if (cameraGuard == null)
                cameraGuard = GetComponent<BattleCameraGuard>();

            monitor = GetComponent<LivekitCameraMonitor>();

            if (cameraGuard == null || monitor == null)
            {
                Debug.LogError(
                    "Добавь BattleCameraGuard и LivekitCameraMonitor " +
                    "на объект BattleClient.",
                    this);

                GetComponent<BattleClient>()?.Leave();
                return;
            }

            if (cameraGuard.BeginWatch())
                StartCoroutine(ConnectRoutine());
        }

        private IEnumerator ConnectRoutine()
        {
            while (!disconnecting)
            {
                // Переподключение не сбрасывает
                // отсчёт BattleCameraGuard.
                CloseCurrentRoom();

                retryConnectionRequested = false;

                var connection = TryConnect();
                var expected = mRoom;

                if (connection != null)
                    yield return connection;

                if (disconnecting ||
                    !ReferenceEquals(expected, mRoom))
                {
                    yield break;
                }

                if (connection == null ||
                    connection.IsError ||
                    expected.State != ConnectionState.Connected)
                {
                    cameraGuard.OnBattleVideoError("network");

                    CloseCurrentRoom();

                    yield return WaitForConnectionRetry();
                    continue;
                }

                var camera = TryOperation(
                    () => expected.LocalParticipant
                        .SetCameraEnabled(true),
                    "Камера не запустилась.");

                if (camera != null)
                    yield return camera;

                if (disconnecting ||
                    !ReferenceEquals(expected, mRoom))
                {
                    yield break;
                }

                monitor.InitialCameraAttemptFinished(
                    camera == null || camera.IsError);

                // Ошибка микрофона не блокирует игру.
                StartCoroutine(EnableMicrophone(expected));

                // Во время Reconnecting восстановление
                // выполняет сам SDK.
                while (!disconnecting &&
                       ReferenceEquals(expected, mRoom) &&
                       expected.State != ConnectionState.Disconnected)
                {
                    yield return null;
                }

                if (disconnecting)
                    yield break;

                cameraGuard.OnBattleVideoError("network");

                CloseCurrentRoom();

                yield return WaitForConnectionRetry();
            }
        }

        private JSPromise TryConnect()
        {
            try
            {
                mRoom = new Room();

                mRoom.TrackSubscribed += OnTrackSubscribed;
                mRoom.TrackUnsubscribed += OnTrackUnsubscribed;
                mRoom.Reconnected += OnRoomReconnected;

                monitor.Bind(mRoom);

                return mRoom.Connect(LivekitURL, roomToken,
                    new RoomConnectOptions { AutoSubscribe = false });
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "LiveKit connect failed: " + e.Message);

                cameraGuard.OnBattleVideoError("network");
                return null;
            }
        }

        private JSPromise TryOperation(
            Func<JSPromise> operation,
            string message)
        {
            try
            {
                return operation();
            }
            catch (Exception e)
            {
                Debug.LogWarning(message + " " + e.Message);
                return null;
            }
        }

        private IEnumerator EnableMicrophone(Room expected)
        {
            var microphone = TryOperation(
                () => expected.LocalParticipant
                    .SetMicrophoneEnabled(true),
                "Микрофон недоступен. Игра продолжается.");

            if (microphone == null)
                yield break;

            yield return microphone;

            if (!disconnecting &&
                ReferenceEquals(expected, mRoom) &&
                microphone.IsError)
            {
                Debug.LogWarning(
                    "Микрофон недоступен. Игра продолжается.");
            }
        }

        private IEnumerator WaitForConnectionRetry()
        {
            float nextAttempt =
                Time.unscaledTime + ConnectionRetryInterval;

            while (!disconnecting &&
                   !retryConnectionRequested &&
                   Time.unscaledTime < nextAttempt)
            {
                yield return null;
            }
        }

        public void RetryCamera()
        {
            if (disconnecting)
                return;

            if (mRoom != null &&
                mRoom.State == ConnectionState.Connected)
            {
                monitor.RetryCamera();
            }
            else
            {
                retryConnectionRequested = true;
            }
        }

        private void OnTrackSubscribed(
            RemoteTrack track,
            RemoteTrackPublication publication,
            RemoteParticipant participant)
        {
            HandleAddedTrack(track, publication, participant);
        }

        private void OnTrackUnsubscribed(
            RemoteTrack track,
            RemoteTrackPublication publication,
            RemoteParticipant participant)
        {
            HandleRemovedTrack(track, publication);
        }

        private void HandleAddedTrack(
            Track track,
            TrackPublication publication,
            Participant participant)
        {
            if (disconnecting)
                return;

            if (track.Kind == TrackKind.Video)
            {
                if (localId.ToString() == participant.Identity)
                    return;

                if (videoBindings.TryGetValue(
                        publication, out var previous))
                {
                    if (ReferenceEquals(previous.Track, track))
                    {
                        TryBindVideo(publication, previous);
                        return;
                    }

                    videoBindings.Remove(publication);
                    ReleaseVideo(previous);
                }

                // Сохраняем трек, даже если персонаж
                // или его RawImage пока не появились.
                var binding = new VideoBinding
                {
                    Track = track,
                    Identity = participant.Identity
                };

                videoBindings[publication] = binding;

                TryBindVideo(publication, binding);
            }
            else if (
                track.Kind == TrackKind.Audio &&
                publication is RemoteTrackPublication &&
                !attachedTracks.Contains(track))
            {
                track.Attach();
                attachedTracks.Add(track);
            }
        }

        private void OnRoomReconnected()
        {
            refreshSubscriptions = true;
            nextVideoBind = 0f;
        }

        private void UpdateSubscriptions()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            playersWithVideo.Clear();
#endif
            if (mRoom == null || mRoom.State != ConnectionState.Connected)
                return;

            var localPlayer = battleClient != null ? battleClient.LocalPlayer : null;
            float nearDistance = videoSubscribeDistance;
            float farDistance = Mathf.Max(nearDistance, videoUnsubscribeDistance);
            seenPublications.Clear();

            foreach (var participant in mRoom.RemoteParticipants.Values)
            {
                bool hasPosition = TryGetDistanceSquared(participant, localPlayer, out var playerId, out float distanceSquared);
                bool videoAvailable = false;

                foreach (var track in participant.VideoTrackPublications.Values)
                {
                    if (track is not RemoteTrackPublication publication)
                        continue;

                    float threshold = requestedSubscriptions.Contains(publication.TrackSid)
                        ? farDistance
                        : nearDistance;
                    bool inRange = hasPosition && distanceSquared <= threshold * threshold;
                    UpdateSubscription(publication, inRange);

                    if (inRange && publication.IsSubscribed && !publication.IsMuted)
                        videoAvailable = true;
                }

#if UNITY_WEBGL && !UNITY_EDITOR
                if (showVideoSubscriptionLines && videoAvailable)
                    playersWithVideo.Add(playerId);
#endif

                foreach (var track in participant.AudioTrackPublications.Values)
                {
                    if (track is RemoteTrackPublication publication)
                        UpdateSubscription(publication, videoAvailable);
                }
            }

            requestedSubscriptions.IntersectWith(seenPublications);
            refreshSubscriptions = false;
        }

        private void UpdateSubscription(RemoteTrackPublication publication, bool shouldSubscribe)
        {
            string sid = publication.TrackSid;
            if (string.IsNullOrEmpty(sid))
                return;

            seenPublications.Add(sid);
            bool wasRequested = requestedSubscriptions.Contains(sid);
            if (shouldSubscribe == wasRequested && !refreshSubscriptions &&
                (shouldSubscribe || !publication.IsSubscribed))
                return;

            publication.SetSubscribed(shouldSubscribe);
            if (shouldSubscribe)
                requestedSubscriptions.Add(sid);
            else
                requestedSubscriptions.Remove(sid);
        }

        private bool TryGetDistanceSquared(RemoteParticipant participant, GameObject localPlayer,
            out int playerId, out float distanceSquared)
        {
            distanceSquared = 0f;
            if (!int.TryParse(participant.Identity, out playerId) ||
                localPlayer == null ||
                !battleClient.Spawned.TryGetValue(playerId, out var remotePlayer) ||
                remotePlayer == null)
            {
                return false;
            }

            distanceSquared = (localPlayer.transform.position - remotePlayer.transform.position).sqrMagnitude;
            return true;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void LateUpdate()
        {
            var localPlayer = battleClient != null ? battleClient.LocalPlayer : null;
            if (!showVideoSubscriptionLines || disconnecting || mRoom == null || localPlayer == null)
            {
                ClearSubscriptionLines();
                return;
            }

            if (subscriptionLineMaterial == null)
            {
                var shader = Resources.Load<Shader>("VideoSubscriptionLine");
                if (shader == null)
                    return;

                subscriptionLineMaterial = new Material(shader);
            }

            removedLines.Clear();
            foreach (var pair in subscriptionLines)
            {
                if (!battleClient.Spawned.TryGetValue(pair.Key, out var player) || player == null)
                {
                    Destroy(pair.Value.gameObject);
                    removedLines.Add(pair.Key);
                }
            }

            foreach (var playerId in removedLines)
                subscriptionLines.Remove(playerId);

            Vector3 offset = Vector3.up;
            Vector3 origin = localPlayer.transform.position + offset;
            foreach (var pair in battleClient.Spawned)
            {
                if (pair.Value == null)
                    continue;

                if (!subscriptionLines.TryGetValue(pair.Key, out var line))
                {
                    var lineObject = new GameObject("Video subscription line");
                    lineObject.transform.SetParent(transform, false);
                    line = lineObject.AddComponent<LineRenderer>();
                    line.sharedMaterial = subscriptionLineMaterial;
                    line.useWorldSpace = true;
                    line.positionCount = 2;
                    line.widthMultiplier = 0.035f;
                    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    line.receiveShadows = false;
                    subscriptionLines.Add(pair.Key, line);
                }

                Color color = playersWithVideo.Contains(pair.Key) ? Color.green : Color.red;
                line.startColor = line.endColor = color;
                line.SetPosition(0, origin);
                line.SetPosition(1, pair.Value.transform.position + offset);
            }
        }

        private void ClearSubscriptionLines()
        {
            foreach (var line in subscriptionLines.Values)
                Destroy(line.gameObject);

            subscriptionLines.Clear();
        }
#endif

        private void Update()
        {
            if (disconnecting ||
                Time.unscaledTime < nextVideoBind)
            {
                return;
            }

            nextVideoBind =
                Time.unscaledTime + VideoBindInterval;

            UpdateSubscriptions();

            foreach (var pair in videoBindings)
            {
                var binding = pair.Value;

                // Уничтоженный Unity-объект также равен null.
                // Работающую привязку повторно не ищем.
                if (binding.Image == null ||
                    binding.Video == null)
                {
                    TryBindVideo(pair.Key, binding);
                }
            }
        }

        private void TryBindVideo(
            TrackPublication publication,
            VideoBinding binding)
        {
            if (binding.Image == null)
            {
                var player = GameObject.Find(
                    "Player_" + binding.Identity);

                if (player == null)
                    return;

                binding.Image =
                    player.GetComponentInChildren<RawImage>(true);

                if (binding.Image == null)
                    return;

                binding.Placeholder = binding.Image.texture;
                binding.PlaceholderUv = binding.Image.uvRect;
            }

            if (binding.Video == null)
            {
                binding.Video =
                    binding.Track.Attach() as HTMLVideoElement;

                if (binding.Video == null)
                    return;

                attachedTracks.Add(binding.Track);

                binding.VideoReceived = texture =>
                {
                    if (texture != null && !disconnecting &&
                        videoBindings.TryGetValue(
                            publication, out var current) &&
                        ReferenceEquals(current, binding))
                    {
                        ApplyVideoTexture(
                            binding.Image, texture);
                    }
                };

                binding.Video.VideoReceived +=
                    binding.VideoReceived;
            }

            // При замене головы используем тот же видеотрек
            // и его текущую текстуру.
            if (binding.Video.Texture != null)
                ApplyVideoTexture(binding.Image, binding.Video.Texture);
        }

        private static void ApplyVideoTexture(
            RawImage image,
            Texture texture)
        {
            if (image == null)
                return;

            image.texture = texture;
            image.uvRect = new Rect(0f, 0f, 1f, 1f);

            if (texture == null ||
                texture.width <= 0 ||
                texture.height <= 0)
            {
                return;
            }

            Rect rect = image.rectTransform.rect;

            float targetAspect =
                rect.width > 0f && rect.height > 0f
                    ? rect.width / rect.height
                    : 1f;

            float sourceAspect =
                (float)texture.width / texture.height;

            float cropWidth =
                Mathf.Min(1f, targetAspect / sourceAspect);

            float cropHeight =
                Mathf.Min(1f, sourceAspect / targetAspect);

            image.uvRect = new Rect(
                (1f - cropWidth) * 0.5f,
                (1f - cropHeight) * 0.5f,
                cropWidth,
                cropHeight);
        }

        private void HandleRemovedTrack(
            Track track,
            TrackPublication publication)
        {
            if (videoBindings.TryGetValue(
                    publication, out var binding) &&
                ReferenceEquals(binding.Track, track))
            {
                videoBindings.Remove(publication);
                ReleaseVideo(binding);
            }
            else
            {
                // Запоздалое событие старого трека
                // не должно удалить новую привязку.
                DetachTrack(track);
            }
        }

        private void ReleaseVideo(VideoBinding binding)
        {
            if (binding.Video != null)
            {
                binding.Video.VideoReceived -=
                    binding.VideoReceived;

                if (binding.Image != null &&
                    binding.Image.texture ==
                    binding.Video.Texture)
                {
                    binding.Image.texture = binding.Placeholder;
                    binding.Image.uvRect = binding.PlaceholderUv;
                }
            }

            binding.Image = null;
            binding.Video = null;
            binding.VideoReceived = null;

            DetachTrack(binding.Track);
        }

        private void DetachTrack(Track track)
        {
            if (!attachedTracks.Remove(track))
                return;

            try
            {
                track.Detach();
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
            }
        }

        private void CloseCurrentRoom()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            playersWithVideo.Clear();
            ClearSubscriptionLines();
#endif
            monitor?.Unbind();

            foreach (var binding in videoBindings.Values)
                ReleaseVideo(binding);

            videoBindings.Clear();
            requestedSubscriptions.Clear();
            seenPublications.Clear();
            refreshSubscriptions = false;

            foreach (var track in new List<Track>(attachedTracks))
                DetachTrack(track);

            var room = mRoom;
            mRoom = null;

            if (room == null)
                return;

            room.TrackSubscribed -= OnTrackSubscribed;
            room.TrackUnsubscribed -= OnTrackUnsubscribed;
            room.Reconnected -= OnRoomReconnected;

            try
            {
                room.Disconnect(true);
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
            }
        }

        public void DisconnectFromRoom()
        {
            if (disconnecting)
                return;

            disconnecting = true;

            StopAllCoroutines();
            CloseCurrentRoom();

            cameraGuard?.EndWatch();
        }

        private void OnDestroy()
        {
            DisconnectFromRoom();
#if UNITY_WEBGL && !UNITY_EDITOR
            if (subscriptionLineMaterial != null)
                Destroy(subscriptionLineMaterial);
#endif
        }

        private void OnApplicationQuit()
        {
            DisconnectFromRoom();
        }
    }
}