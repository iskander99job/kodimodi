using System;
using UnityEngine;
using UnityEngine.Networking;

namespace Kodimodi.UI.Profile
{
    [Serializable]
    internal sealed class ArsenalResponse
    {
        public ArsenalWeapon[] weapons;
    }

    [Serializable]
    internal sealed class ArsenalWeapon
    {
        public string code;
        public string name;
        public string categoryCode;
        public bool equipped;
    }

    [Serializable]
    internal sealed class EquipResponse
    {
        public string code;
        public string categoryCode;
        public bool equipped;
        public string error;
    }

    internal static class ArsenalApi
    {
        public const string LoadFailed = "Couldn't load your arsenal.";
        public const string SignInRequired = "Please sign in to view your arsenal.";
        public const string EquipFailed = "Couldn't equip this weapon.";

        public static UnityWebRequest CreateRequest(string apiBaseUrl, string token)
        {
            var request = UnityWebRequest.Get(apiBaseUrl + "/arsenal");
            SetAuthorization(request, token);
            return request;
        }

        public static UnityWebRequest CreateEquipRequest(
            string apiBaseUrl,
            string token,
            string weaponCode)
        {
            string url = apiBaseUrl + "/arsenal/" +
                Uri.EscapeDataString(weaponCode) + "/equip";

            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.downloadHandler = new DownloadHandlerBuffer();
            SetAuthorization(request, token);
            return request;
        }

        public static ArsenalResponse ReadResponse(UnityWebRequest request)
        {
            return request.result == UnityWebRequest.Result.Success
                ? ReadJson<ArsenalResponse>(request.downloadHandler.text)
                : null;
        }

        public static EquipResponse ReadEquipResponse(UnityWebRequest request)
        {
            return ReadJson<EquipResponse>(request.downloadHandler.text);
        }

        public static string GetEquipError(string code)
        {
            return code switch
            {
                "WEAPON_NOT_OWNED" => "You don't own this weapon.",
                "WEAPON_NOT_FOUND" => "Weapon not found.",
                _ => EquipFailed
            };
        }

        private static void SetAuthorization(UnityWebRequest request, string token)
        {
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = 15;
        }

        private static T ReadJson<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}