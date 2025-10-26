namespace Keytietkiem.Models
{
    public enum UserStatus { Active, Locked, Disabled }

    public static class UserStatusHelper
    {
        public static bool IsValid(string? s)
            => !string.IsNullOrWhiteSpace(s) && Enum.TryParse<UserStatus>(s, true, out _);

        public static string Normalize(string s)
            => Enum.Parse<UserStatus>(s, true).ToString();
    }
}
