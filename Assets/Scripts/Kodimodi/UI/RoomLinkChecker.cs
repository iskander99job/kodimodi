using System;
using UnityEngine;

namespace Kodimodi.UI
{
    public static class RoomLinkChecker
    {
        // Код комнаты из адресной строки или null, если его нет.
        // editorTestUrl нужен только в редакторе, где адресной строки нет.
        public static string GetRoomCode(string editorTestUrl = "")
        {
            string url = Application.absoluteURL;

#if UNITY_EDITOR
            if (string.IsNullOrEmpty(url)) url = editorTestUrl;
#endif

            return ParseRoomCode(url);
        }

        // Собирает ссылку на комнату: адрес страницы без параметров + ?room=CODE
        public static string BuildLink(string code, string editorTestUrl = "")
        {
            string url = Application.absoluteURL;

#if UNITY_EDITOR
            if (string.IsNullOrEmpty(url)) url = editorTestUrl;
#endif

            if (string.IsNullOrEmpty(url)) url = "http://localhost/";

            // отрезаем старые параметры и якорь
            int cut = url.IndexOfAny(new[] { '?', '#' });
            if (cut >= 0) url = url.Substring(0, cut);

            return $"{url}?room={code}";
        }
        
        // Достаёт параметр room из ?room=CODE
        private static string ParseRoomCode(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            int q = url.IndexOf('?');
            if (q < 0) return null;

            string query = url.Substring(q + 1);

            int hash = query.IndexOf('#');
            if (hash >= 0) query = query.Substring(0, hash);

            foreach (string pair in query.Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;

                string key = pair.Substring(0, eq);
                if (!key.Equals("room", StringComparison.OrdinalIgnoreCase)) continue;

                string code = Uri.UnescapeDataString(pair.Substring(eq + 1)).Trim().ToUpperInvariant();
                return IsValidCode(code) ? code : null;
            }

            return null;
        }

        // Код на сервере состоит из 6 букв и цифр
        private static bool IsValidCode(string code)
        {
            if (code.Length != 6) return false;
            foreach (char c in code)
                if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
                    return false;
            return true;
        }
    }
}