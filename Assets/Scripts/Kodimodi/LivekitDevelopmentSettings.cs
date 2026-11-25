using UnityEngine;

namespace Kodimodi
{
    internal sealed class LivekitDevelopmentSettings : ScriptableObject
    {
        [SerializeField] private bool disableLivekit;

        public bool DisableLivekit => disableLivekit;
    }
}
