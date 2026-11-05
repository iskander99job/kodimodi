using System.Runtime.InteropServices;

namespace Kodimodi.UI
{
    public static class BrowserUrl
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void ClearRoomFromUrl();
#endif

        // Убирает ?room=... из адресной строки, страница не перезагружается
        public static void ClearRoomParam()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ClearRoomFromUrl();
#endif
        }
    }
}