namespace Kodimodi.Errors
{
    public static class WeaponPurchaseErrors
    {
        public const string AlreadyOwned = "WEAPON_ALREADY_OWNED";
        public const string SignInRequired = "Please sign in again.";
        public const string PurchaseFailed = "Couldn't complete the purchase.";

        public static string GetMessage(string code)
        {
            return code switch
            {
                "RANK_REQUIRED" => "Your rank is too low.",
                "INSUFFICIENT_COINS" => "Not enough coins.",
                "WEAPON_NOT_FOUND" => "Weapon not found.",
                _ => PurchaseFailed
            };
        }
    }
}