using System.Security.Cryptography;
using System.Text;

namespace KeytietkiemApi.Utils
{
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 100_000; // PBKDF2

        public static byte[] HashPassword(string password)
        {
            using var rng = RandomNumberGenerator.Create();
            var salt = new byte[SaltSize];
            rng.GetBytes(salt);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
            var subkey = pbkdf2.GetBytes(KeySize);

            var result = new byte[SaltSize + KeySize];
            Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
            Buffer.BlockCopy(subkey, 0, result, SaltSize, KeySize);
            return result;
        }

        public static bool VerifyPassword(byte[]? stored, string password)
        {
            if (stored == null || stored.Length != SaltSize + KeySize) return false;

            var salt = new byte[SaltSize];
            Buffer.BlockCopy(stored, 0, salt, 0, SaltSize);

            var expected = new byte[KeySize];
            Buffer.BlockCopy(stored, SaltSize, expected, 0, KeySize);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
            var actual = pbkdf2.GetBytes(KeySize);

            // constant-time compare
            var diff = 0;
            for (int i = 0; i < KeySize; i++)
                diff |= actual[i] ^ expected[i];

            return diff == 0;
        }
    }
}
