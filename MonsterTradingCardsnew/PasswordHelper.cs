using System;
using System.Security.Cryptography;
using System.Text;

public static class PasswordHelper
{
    // Methode zum Erzeugen eines sicheren Passwort-Hashes ohne Salt
    public static string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hash); // Rückgabe des Hashes als Base64
        }
    }

    // Methode zum Verifizieren des Passworts (ohne Salt)
    public static bool VerifyPassword(string enteredPassword, string storedHash)
    {
        string enteredHash = HashPassword(enteredPassword); // Hash des eingegebenen Passworts
        return enteredHash == storedHash; // Vergleiche Hashes
    }
}
