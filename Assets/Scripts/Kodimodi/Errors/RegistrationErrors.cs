namespace Kodimodi.Errors
{
    public static class RegistrationErrors
    {
        private const string InvalidNicknameCode = "INVALID_NICKNAME";
        private const string InvalidPasswordCode = "INVALID_PASSWORD";
        private const string NicknameTakenCode = "NICKNAME_TAKEN";

        public const string InvalidNickname =
            "Nickname needs 4–15 chars (A–Z, a–z, 0–9, _).";

        public const string InvalidPassword =
            "Your password must be 6–30 characters long.";

        public const string NicknameTaken =
            "This nickname is already taken.";

        public const string ConsentRequired =
            "Please check both consent boxes.";

        public const string InvalidApiUrl =
            "The API address is invalid.";

        public const string ConnectionFailed =
            "Couldn't connect to the server. Please try again.";

        public const string RegistrationFailed =
            "Registration couldn't be completed. Please try again.";

        public const string MissingAccessToken =
            "Your account was created, but the server didn't return an access token.";

        public const string CookieNotSaved =
            "Your account was created, but the browser couldn't save your session.";

        public static string GetMessage(string code)
        {
            return code switch
            {
                InvalidNicknameCode => InvalidNickname,
                InvalidPasswordCode => InvalidPassword,
                NicknameTakenCode => NicknameTaken,
                _ => RegistrationFailed
            };
        }
    }
}