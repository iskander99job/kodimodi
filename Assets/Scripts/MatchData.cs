using UnityEngine;

public static class MatchData
{
    public static string RoomId;
    public static string PlayerName;

    // Очистка при выходе из матча
    public static void Clear()
    {
        RoomId = null;
        PlayerName = null;
    }
}