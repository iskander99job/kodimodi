using UnityEngine;

namespace Kodimodi.UI
{
    public class ClipboardManager : MonoBehaviour
    {
        // Подключаем внешнюю функцию только для WebGL
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void CopyTextToClipboard(string text);
#endif

        // Универсальный метод для вызова из UI
        public void CopyToClipboard(string textToCopy)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
        CopyTextToClipboard(textToCopy);
#else
            // Для редактора и других платформ используем стандартный метод
            GUIUtility.systemCopyBuffer = textToCopy;
#endif
        }
    }
}