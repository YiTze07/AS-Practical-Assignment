using System;
using System.Security.Cryptography;
using System.Text;

namespace AS_Practical_Assignment.Services
{
    public static class PasswordHelper
    {
        public static string GenerateSalt()
        {
            byte[] salt = new byte[16];
            RandomNumberGenerator.Fill(salt);
            return Convert.ToBase64String(salt);
        }

        public static string HashPassword(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            byte[] saltBytes = Convert.FromBase64String(salt); 
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] combined = new byte[saltBytes.Length + passwordBytes.Length];
            Array.Copy(saltBytes, combined, saltBytes.Length);
            Array.Copy(passwordBytes, 0, combined, saltBytes.Length, passwordBytes.Length);
            byte[] hash = sha256.ComputeHash(combined);
            return Convert.ToBase64String(hash);
        }

        public static bool VerifyPassword(string password, string salt, string hash)
        {
            return HashPassword(password, salt) == hash;
        }

        public static bool ValidatePasswordComplexity(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 12) return false;

            bool hasLower = false, hasUpper = false, hasDigit = false, hasSpecial = false;
            foreach (char c in password)
            {
                if (char.IsLower(c)) hasLower = true;
                else if (char.IsUpper(c)) hasUpper = true;
                else if (char.IsDigit(c)) hasDigit = true;
                else hasSpecial = true;
            }
            return hasLower && hasUpper && hasDigit && hasSpecial;
        }
    }
}