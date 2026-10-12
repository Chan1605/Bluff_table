using System.Security.Cryptography;

namespace LiarsBar.Server
{
    // PBKDF2-SHA256 비밀번호 해시 (서버 전용)
    public static class PasswordHasher
    {
        public const int SaltSize = 16;
        public const int HashSize = 32;
        private const int Iterations = 100_000;

        public static byte[] CreateSalt()
        {
            byte[] salt = new byte[SaltSize];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            return salt;
        }

        public static byte[] Hash(string password, byte[] salt)
        {
            using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(HashSize);
            }
        }

        // 고정 시간 비교
        public static bool AreEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
