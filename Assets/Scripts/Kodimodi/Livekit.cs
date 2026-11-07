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

        private readonly Dictionary<TrackPublication, RawImage> mVideos = new();
        private Room mRoom;
        
        private int localId;
        private string roomToken;
        
        private readonly HashSet<Track> attachedTracks = new();
        private bool disconnecting;
        
        public void ConnectToRoom(int localId, string roomToken)
        {
            DisconnectFromRoom();
            disconnecting = false;
            
            this.localId = localId;
            this.roomToken = roomToken;
            
            StartCoroutine(ConnectRoutine());
        }
        
        private IEnumerator ConnectRoutine()
        {
            // New Room must be called when WebGL assembly is loaded
            mRoom = new Room();

             // mRoom.LocalTrackPublished +=
             //     (publication, participant) => HandleAddedTrack(publication.Track, publication);
             // mRoom.LocalTrackUnpublished +=
             //     (publication, participant) => HandleRemovedTrack(publication.Track, publication);
             mRoom.TrackSubscribed += OnTrackSubscribed;
             mRoom.TrackUnsubscribed += OnTrackUnsubscribed;

            var c = mRoom.Connect(LivekitURL, roomToken);
            yield return c;

            if (disconnecting || c.IsError)
            {
                Debug.Log("Failed to connect to the room!");
                yield break;
            }
            
            Debug.Log("Connected to the room");
            
            yield return mRoom.LocalParticipant.EnableCameraAndMicrophone();
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
        
        private void HandleAddedTrack(Track track, TrackPublication publication, Participant participant)
        {
            if (disconnecting || !attachedTracks.Add(track))
                return;
            
            if (track.Kind == TrackKind.Video)
            {
                if (localId.ToString() == participant.Identity)
                {
                    return;
                }
                
                GameObject obj = GameObject.Find("Player_" + participant.Identity);
                
                if (obj != null)
                {
                    RawImage image = obj.GetComponentInChildren<RawImage>(true);
                    if (image == null) return;
                    
                    var video = track.Attach() as HTMLVideoElement;
                
                    if (video != null)
                    {
                        mVideos[publication] = image;
                        image.texture = video.Texture;

                        video.VideoReceived += tex =>
                        {
                            if (!disconnecting && image != null)
                                image.texture = tex;
                        };
                    }
                }
            }
            else if (track.Kind == TrackKind.Audio && publication is RemoteTrackPublication)
            {
                Debug.Log($"Audio track from participant: {participant.Identity}");
                track.Attach();
            }
        }

        private void HandleRemovedTrack(
            Track track,
            TrackPublication publication)
        {
            attachedTracks.Remove(track);
            track.Detach();

            if (mVideos.TryGetValue(publication, out var view))
            {
                if (view != null)
                    view.texture = null;

                mVideos.Remove(publication);
            }
        }
        
        public void DisconnectFromRoom()
        {
            if (disconnecting) return;
            disconnecting = true;

            StopAllCoroutines();

            foreach (var view in mVideos.Values)
            {
                if (view != null)
                    view.texture = null;
            }

            mVideos.Clear();

            foreach (var track in new List<Track>(attachedTracks))
            {
                try
                {
                    track.Detach();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning(e.Message);
                }
            }

            attachedTracks.Clear();

            var room = mRoom;
            mRoom = null;

            if (room != null)
            {
                room.TrackSubscribed -= OnTrackSubscribed;
                room.TrackUnsubscribed -= OnTrackUnsubscribed;

                try
                {
                    // true — остановить также локальную камеру и микрофон.
                    room.Disconnect(true);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning(e.Message);
                }
            }
        }

        private void OnDestroy() => DisconnectFromRoom();

        private void OnApplicationQuit() => DisconnectFromRoom();
    }
}