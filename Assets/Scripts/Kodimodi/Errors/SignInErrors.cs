namespace Kodimodi.Errors
{
    public static class SignInErrors
    {
        private const string InvalidCredentialsCode = "INVALID_CREDENTIALS";

        public const string InvalidCredentials = "Incorrect nickname or password.";
        public const string InvalidApiUrl = "The API address is invalid.";
        public const string ConnectionFailed = "Couldn't connect to the server.";
        public const string SignInFailed = "Sign-in failed. Please try again.";
        public const string MissingAccessToken = "The server didn't return an access token.";
        public const string CookieNotSaved = "The browser couldn't save your session.";

        public static string GetMessage(string code)
        {
            return code == InvalidCredentialsCode
                ? InvalidCredentials
                : SignInFailed;
        }
    }
}