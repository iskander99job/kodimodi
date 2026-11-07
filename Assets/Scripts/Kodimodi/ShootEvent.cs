using Kodimodi.Models;
using NativeWebSocket;
using UnityEngine;

namespace Kodimodi
{
    public class ShootEvent : MonoBehaviour
    {
        // Firearm Events → On Fire Demand
        public void OnFireHandler(
            Vector3 position,
            Quaternion rotation,
            Vector3 direction)
        {
            var client = BattleClient.Instance;

            if (client == null || !client.CanPlay)
                return;

            Send(new ShootModel
            {
                Id = client.id,
                HitPlayerId = 0,
                Damage = 0f
            });
        }

        // Firearm Events → On Hit
        public void OnHitHandler(Transform hit, float damage)
        {
            var client = BattleClient.Instance;
            if (client == null || hit == null || damage <= 0f) return;

            var target = hit.GetComponentInParent<NetworkPlayer>();
            if (target == null || target.playerId == client.id) return;

            Send(new ShootModel { Id = client.id, HitPlayerId = target.playerId, Damage = damage });
        }

        private static async void Send(ShootModel model)
        {
            var client = BattleClient.Instance;
            if (client == null
                 || !client.CanPlay
                 || client.websocket == null
                 || client.websocket.State != WebSocketState.Open)
            {
                return;
            }

            try { await client.websocket.Send(WsProtocol.Pack(WsMessageType.Shoot, model)); }
            catch (System.Exception e) { Debug.LogWarning("Shoot send failed: " + e.Message); }
        }
    }
}