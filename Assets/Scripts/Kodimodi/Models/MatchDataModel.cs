namespace Kodimodi
{
    public static class MatchDataModel
    {
        public static string RoomId;

        // Очистка при выходе из матча
        public static void Clear()
        {
            RoomId = null;
        }
    }
}