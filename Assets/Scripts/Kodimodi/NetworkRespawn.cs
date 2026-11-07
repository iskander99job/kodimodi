using Akila.FPSFramework;
using UnityEngine;

namespace Kodimodi
{
    // Смерть приходит от сервера через здоровье, возрождение по команде сервера
    public class NetworkRespawn : MonoBehaviour
    {
        private Damageable damageable;
        private CharacterController controller;

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
            controller = GetComponent<CharacterController>();
        }

        private void OnEnable() => damageable.onDeath.AddListener(OnDeath);
        private void OnDisable() => damageable.onDeath.RemoveListener(OnDeath);

        private void OnDeath()
        {
            FPSFrameworkCore.IsInputActive = false;   // нельзя ходить, смотреть и стрелять
        }

        public void Respawn(Vector3 position)
        {
            if (BattleClient.Instance != null
                && (BattleClient.Instance.IsMatchEnded
                    || BattleClient.Instance.IsDisconnected))
            {
                return;
            }
            
            controller.enabled = false;               // иначе CharacterController перезапишет позицию
            transform.position = position;
            controller.enabled = true;

            DeathCamera.Instance?.Disable();
            damageable.ResetForRespawn();
            FPSFrameworkCore.IsInputActive = true;
        }
    }
}