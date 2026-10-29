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
        
        public void ConnectToRoom(int localId, string roomToken)
        {
            this.localId = localId;
            this.roomToken = roomToken;
            
            StartCoroutine(ConnectRoutine());
        }
        
        private IEnumerator ConnectRoutine()
        {
            // New Room must be called when WebGL assembly is loaded
            mRoom = new Room();

            // Setup the callbacks before connecting to the Room
            mRoom.ParticipantConnected += (p) => { Debug.Log($"Participant connected: {p.Sid}"); };

             // mRoom.LocalTrackPublished +=
             //     (publication, participant) => HandleAddedTrack(publication.Track, publication);
             // mRoom.LocalTrackUnpublished +=
             //     (publication, participant) => HandleRemovedTrack(publication.Track, publication);
            mRoom.TrackSubscribed += (track, publication, participant) => HandleAddedTrack(track, publication, participant);
            mRoom.TrackUnsubscribed += (track, publication, participant) => HandleRemovedTrack(track, publication);

            var c = mRoom.Connect(LivekitURL, roomToken);
            yield return c;

            if (c.IsError)
            {
                Debug.Log("Failed to connect to the room !");
                yield break;
            }
            
            Debug.Log("Connected to the room");
            
            yield return mRoom.LocalParticipant.EnableCameraAndMicrophone();
        }

        private void HandleAddedTrack(Track track, TrackPublication publication, Participant participant)
        {
            if (track.Kind == TrackKind.Video)
            {
                if (localId.ToString() == participant.Identity)
                {
                    return;
                }
                
                GameObject obj = GameObject.Find("Player_" + participant.Identity);
                
                if (obj != null)
                {
                    RawImage image = obj.GetComponentInChildren<RawImage>();
                    
                    var video = track.Attach() as HTMLVideoElement;
                
                    if (video != null)
                    {
                        mVideos.Add(publication, image);
                
                        video.VideoReceived += tex => { image.texture = tex; };   
                    }
                }
            }
            else if (track.Kind == TrackKind.Audio && publication is RemoteTrackPublication)
            {
                Debug.Log($"Audio track from participant: {participant.Identity}");
                track.Attach();
            }
        }

        private void HandleRemovedTrack(Track track, TrackPublication publication)
        {
            track.Detach();

            if (mVideos.TryGetValue(publication, out var view))
                Destroy(view.gameObject);
        }
    }
}