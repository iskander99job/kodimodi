using Akila.FPSFramework;
using UnityEngine;

namespace Kodimodi
{
    [RequireComponent(typeof(Firearm))]
    public class NetworkWeaponReload : MonoBehaviour
    {
        private Firearm firearm;
        private bool lastReloading;

        private void Awake()
        {
            firearm = GetComponent<Firearm>();
        }

        private void LateUpdate()
        {
            SetState(firearm.isReloading);
        }

        private void OnDisable()
        {
            SetState(false);
        }

        private void SetState(bool value)
        {
            if (lastReloading == value)
                return;

            lastReloading = value;

            BattleClient.Instance?.SendReloadState(value);
        }
    }
}