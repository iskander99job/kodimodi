using System;
using System.Collections;
using LiveKit;
using UnityEngine;

namespace Kodimodi
{
    [RequireComponent(typeof(BattleCameraGuard))]
    public class LivekitCameraMonitor : MonoBehaviour
    {
        private const float RetryInterval = 2f;

        private Room room;
        private Track cameraTrack;
        private BattleCameraGuard guard;

        private bool ended;
        private bool retrying;
        private bool autoRecoveryEnabled;

        private string lastState;
        private float nextCheck;
        private float nextAutoRetry;
        private int generation;

        public void Bind(Room value)
        {
            Unbind();

            guard = GetComponent<BattleCameraGuard>();
            room = value;

            nextCheck = 0;

            room.LocalTrackPublished += OnPublished;
            room.LocalTrackUnpublished += OnPublished;

            room.TrackMuted += OnPublicationChanged;
            room.TrackUnmuted += OnPublicationChanged;

            room.Reconnecting += OnReconnecting;
            room.Reconnected += Refresh;
            room.Disconnected += OnDisconnected;

            Refresh();
        }

        public void InitialCameraAttemptFinished(bool failed = false)
        {
            if (room == null)
                return;

            autoRecoveryEnabled = true;

            nextAutoRetry =
                Time.unscaledTime + RetryInterval;

            if (failed)
                ReportFailure("missing");
            else
                Refresh();
        }

        private void Update()
        {
            if (room == null || Time.unscaledTime < nextCheck)
                return;

            nextCheck = Time.unscaledTime + 0.25f;

            Refresh();

            if (autoRecoveryEnabled &&
                lastState == "missing" &&
                !retrying &&
                Time.unscaledTime >= nextAutoRetry)
            {
                RetryCamera();
            }
        }

        private void Refresh()
        {
            if (room == null)
                return;

            if (room.State != ConnectionState.Connected)
            {
                Report("network");
                return;
            }

            // Микрофон не влияет
            // на готовность обязательного видео.
            var publication = room.LocalParticipant
                .GetTrackPublication(TrackSource.Camera);

            Observe(publication?.Track);

            bool ready =
                !retrying &&
                !ended &&
                publication != null &&
                cameraTrack != null &&
                !publication.IsMuted &&
                !cameraTrack.IsMuted;

            Report(ready ? "ready" : "missing");
        }

        private void Observe(Track track)
        {
            if (ReferenceEquals(cameraTrack, track))
                return;

            if (cameraTrack != null)
            {
                cameraTrack.Ended -= OnEnded;
                cameraTrack.Muted -= OnTrackChanged;
                cameraTrack.Unmuted -= OnTrackChanged;
            }

            cameraTrack = track;
            ended = false;

            if (cameraTrack != null)
            {
                cameraTrack.Ended += OnEnded;
                cameraTrack.Muted += OnTrackChanged;
                cameraTrack.Unmuted += OnTrackChanged;
            }
        }

        private void OnEnded(Track track)
        {
            ended = true;
            ReportFailure("missing");
        }

        private void OnTrackChanged(Track track)
        {
            Refresh();
        }

        private void OnReconnecting()
        {
            ReportFailure("network");
        }

        private void OnDisconnected(DisconnectReason? reason)
        {
            ReportFailure("network");
        }

        private void OnPublished(
            LocalTrackPublication publication,
            LocalParticipant participant)
        {
            if (publication.Source == TrackSource.Camera)
                Refresh();
        }

        private void OnPublicationChanged(
            TrackPublication publication,
            Participant participant)
        {
            if (room != null &&
                publication.Source == TrackSource.Camera &&
                participant.Identity == room.LocalParticipant.Identity)
            {
                Refresh();
            }
        }

        private void Report(string state)
        {
            if (state == lastState)
                return;

            lastState = state;
            guard.OnBattleCameraState(state);
        }

        private void ReportFailure(string state)
        {
            lastState = state;

            // Явную ошибку передаём даже тогда,
            // когда обычное состояние не изменилось.
            guard.OnBattleVideoError(state);
        }

        public void RetryCamera()
        {
            // Не запускаем параллельно
            // с первоначальным SetCameraEnabled.
            if (!autoRecoveryEnabled ||
                retrying ||
                room == null ||
                room.State != ConnectionState.Connected)
            {
                return;
            }

            retrying = true;

            Refresh();

            StartCoroutine(RetryRoutine(room, generation));
        }

        private bool Current(Room expected, int version)
        {
            return generation == version &&
                   ReferenceEquals(room, expected) &&
                   room != null &&
                   room.State == ConnectionState.Connected;
        }

        private JSPromise TryOperation(Func<JSPromise> operation)
        {
            try
            {
                return operation();
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "Camera operation failed: " + e.Message);

                ReportFailure("missing");
                return null;
            }
        }

        private IEnumerator RetryRoutine(
            Room expected,
            int version)
        {
            yield return null;

            try
            {
                if (!Current(expected, version))
                    yield break;

                var publication = expected.LocalParticipant
                    .GetTrackPublication(TrackSource.Camera);

                var video = publication?.Track as LocalVideoTrack;

                if (video != null)
                {
                    var restart = TryOperation(
                        () => video.RestartTrack());

                    if (restart == null)
                        yield break;

                    yield return restart;

                    if (!Current(expected, version) ||
                        restart.IsError)
                    {
                        yield break;
                    }
                }

                var enable = TryOperation(
                    () => expected.LocalParticipant
                        .SetCameraEnabled(true));

                if (enable == null)
                    yield break;

                yield return enable;

                if (!Current(expected, version) ||
                    enable.IsError)
                {
                    yield break;
                }

                ended = false;
            }
            finally
            {
                if (generation == version)
                {
                    retrying = false;

                    // Интервал считаем от завершения операции.
                    nextAutoRetry =
                        Time.unscaledTime + RetryInterval;

                    Refresh();
                }
            }
        }

        public void Unbind()
        {
            generation++;

            StopAllCoroutines();

            if (room != null)
            {
                room.LocalTrackPublished -= OnPublished;
                room.LocalTrackUnpublished -= OnPublished;

                room.TrackMuted -= OnPublicationChanged;
                room.TrackUnmuted -= OnPublicationChanged;

                room.Reconnecting -= OnReconnecting;
                room.Reconnected -= Refresh;
                room.Disconnected -= OnDisconnected;
            }

            Observe(null);

            room = null;
            retrying = false;
            autoRecoveryEnabled = false;
            lastState = null;
        }

        private void OnDisable()
        {
            Unbind();
        }
    }
}