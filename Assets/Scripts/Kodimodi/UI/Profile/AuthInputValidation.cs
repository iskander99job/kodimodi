namespace Kodimodi.UI.Profile
{
    internal static class AuthInputValidation
    {
        public static char Nickname(string text, int index, char character)
        {
            bool isLetter = (character >= 'A' && character <= 'Z') ||
                            (character >= 'a' && character <= 'z');
            bool isDigit = character >= '0' && character <= '9';
            return isLetter || isDigit || character == '_' ? character : '\0';
        }

        public static char Password(string text, int index, char character)
        {
            return character >= ' ' && character <= '~' ? character : '\0';
        }
    }
}
