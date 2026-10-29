using System;
using Akila.FPSFramework;
using Kodimodi.Models;
using UnityEngine;

namespace Kodimodi
{
    public class NetworkPlayerState : MonoBehaviour
    {
        public Transform cameraTransform;

        private float nextSend;
        private bool isSending;

        private Vector3 lastPos;
        private Quaternion lastRot;
        private float lastSentTime;
        
        private CharacterInput input;

        private const float SendInterval = 0.05f;   // 20 Hz максимум
        private const float PosEpsilon = 0.001f;    // 1 мм
        private const float RotEpsilonDeg = 0.1f;   // 0.1 градуса
        private const float HeartbeatInterval = 1.5f; // раз в 1.5 с шлём даже без изменений

        private BattleClient client;

        private void Start()
        {
            client = BattleClient.Instance;
        }

        private void Awake()
        {
            lastPos = transform.position;
            lastRot = cameraTransform != null ? cameraTransform.rotation : transform.rotation;
            lastSentTime = Time.time;
            
            input = GetComponent<CharacterInput>();
        }
        
        async void Update()
        {
            if (client == null || client.websocket == null) return;
            
            if (Input.GetMouseButtonDown(0))
            {
                var model = new ShootModel
                {
                    Id = client.id,
                };

                var bytes = WsProtocol.Pack(WsMessageType.Shoot, model);
                Debug.Log("Shoot");
                await client.websocket.Send(bytes);
            }
            
            if (isSending || Time.time < nextSend) return;
            
            var pos = transform.position;
            var rot = cameraTransform != null ? cameraTransform.rotation : transform.rotation;

            bool posChanged = (pos - lastPos).sqrMagnitude > PosEpsilon * PosEpsilon;
            bool rotChanged = Quaternion.Angle(lastRot, rot) > RotEpsilonDeg;
            bool heartbeat = Time.time - lastSentTime >= HeartbeatInterval;
            
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            if (!posChanged && !rotChanged && !heartbeat) return;

            nextSend = Time.time + SendInterval;
            isSending = true;

            lastPos = pos;
            lastRot = rot;
            lastSentTime = Time.time;

            try
            {
                var euler = rot.eulerAngles;

                var model = new PlayerBodyPositionModel
                {
                    Id = client.id,
                    Position = new Vec3(pos.x, pos.y, pos.z),
                    Rotation = new Vec3(euler.x, euler.y, euler.z),
                    IsJumping = input.JumpInput,
                    H = h,
                    V = v
                };

                var bytes = WsProtocol.Pack(WsMessageType.PlayerBodyPosition, model);
                await client.websocket.Send(bytes);
            }
            catch (Exception e)
            {
                Debug.LogWarning("NetworkPlayerState send failed: " + e.Message);
            }
            finally
            {
                isSending = false;
            }
        }
    }
}