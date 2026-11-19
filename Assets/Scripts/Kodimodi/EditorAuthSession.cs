#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Kodimodi
{
    internal static class EditorAuthSession
    {
        private static string Key =>
            "Kodimodi.AuthSession:" + Application.dataPath;

        [Serializable]
        private sealed class Session
        {
            public string apiBaseUrl;
            public string accessToken;
        }

        public static void Save(string apiBaseUrl, string accessToken)
        {
            var session = new Session
            {
                apiBaseUrl = apiBaseUrl,
                accessToken = accessToken
            };

            EditorPrefs.SetString(Key, JsonUtility.ToJson(session));
        }

        public static bool TryRead(out string apiBaseUrl, out string accessToken)
        {
            apiBaseUrl = null;
            accessToken = null;

            string json = EditorPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            Session session;

            try
            {
                session = JsonUtility.FromJson<Session>(json);
            }
            catch (ArgumentException)
            {
                Clear();
                return false;
            }

            if (session == null ||
                string.IsNullOrWhiteSpace(session.accessToken) ||
                !Uri.TryCreate(session.apiBaseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                Clear();
                return false;
            }

            apiBaseUrl = session.apiBaseUrl;
            accessToken = session.accessToken;
            return true;
        }

        public static void Clear()
        {
            EditorPrefs.DeleteKey(Key);
        }
    }
}
#endif