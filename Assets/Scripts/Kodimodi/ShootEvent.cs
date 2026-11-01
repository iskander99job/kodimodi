using Kodimodi.Models;
using UnityEngine;

namespace Kodimodi
{
    public class ShootEvent : MonoBehaviour
    {
        public async void OnHitHandler(Transform transform, float damage)
        {
            var model = new ShootModel
            {
                Id = BattleClient.Instance.id,
                HitPlayerId = 0,
                Damage = damage
            };
            
            if (transform.TryGetComponent(out NetworkPlayer player))
            {
                model.HitPlayerId = player.playerId;
            }

            var bytes = WsProtocol.Pack(WsMessageType.Shoot, model);
            await BattleClient.Instance.websocket.Send(bytes);
        }
    }
}