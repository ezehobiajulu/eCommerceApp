using System.Security.Cryptography;

namespace SimpleeCommerceApp.Auth
{
    public static class PasswordHasher
    {
        public static (byte[] hash, byte[] salt) HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(32);
            return (hash, salt);
        }

        public static bool Verify(string password, byte[] salt, byte[] expectedHash)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(32);
            return CryptographicOperations.FixedTimeEquals(hash, expectedHash);
        }
    }
}
