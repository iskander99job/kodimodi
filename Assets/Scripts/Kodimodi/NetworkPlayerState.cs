using System;
using Akila.FPSFramework;
using Kodimodi.Models;
using UnityEngine;

namespace Kodimodi
{
    public class NetworkPlayerState : MonoBehaviour
    {
        public Transform cameraTransform;

        private const float SendInterval = 0.05f;

        private float nextSend;
        private bool isSending;

        private BattleClient client;
        private CharacterInput input;

        private void Awake()
        {
            input = GetComponent<CharacterInput>();
        }

        private void Start()
        {
            client = BattleClient.Instance;
        }

        private async void LateUpdate()
        {
            if (client == null ||
                !client.CanPlay ||
                client.websocket == null ||
                isSending ||
                Time.time < nextSend)
            {
                return;
            }

            nextSend = Time.time + SendInterval;
            isSending = true;

            try
            {
                Vector3 pos = transform.position;

                Vector3 rot = cameraTransform != null
                    ? cameraTransform.eulerAngles
                    : transform.eulerAngles;

                var model = new PlayerBodyPositionModel
                {
                    Id = client.id,
                    Position = new Vec3(pos.x, pos.y, pos.z),
                    Rotation = new Vec3(rot.x, rot.y, rot.z),
                    IsJumping = input != null && input.JumpInput,
                    H = Input.GetAxis("Horizontal"),
                    V = Input.GetAxis("Vertical"),
                    SentAt = Time.timeAsDouble
                };

                await client.websocket.Send(
                    WsProtocol.Pack(
                        WsMessageType.PlayerBodyPosition,
                        model));
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "NetworkPlayerState send failed: " + e.Message);
            }
            finally
            {
                isSending = false;
            }
        }
    }
}