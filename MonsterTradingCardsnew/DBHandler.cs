using System;
using Npgsql;

namespace MonsterTradingCardsnew
{
    public static class DBHandler
    {
        // Angepasster ConnectionString für Docker-Compose
        public const string ConnectionString = "Host=127.0.0.1;Port=5432;Database=mtcg;Username=postgres;Password=dicle";

        
        // Funktion zum Testen der Datenbankverbindung
        public static bool TestDatabaseConnection()
        {
            try
            {
                // Versuche, eine Verbindung zur Datenbank aufzubauen
                using var connection = new NpgsqlConnection(ConnectionString);
                connection.Open();

                // Führe eine einfache SELECT-Abfrage aus, um zu überprüfen, ob die Verbindung funktioniert
                using var command = new NpgsqlCommand("SELECT 1", connection);
                var result = command.ExecuteScalar();

                // Wenn die Abfrage erfolgreich ist, geben wir true zurück
                if (result != null && Convert.ToInt32(result) == 1)
                {
                    Console.WriteLine("Datenbankverbindung erfolgreich!");
                    return true;
                }

                Console.WriteLine("Datenbankverbindung fehlgeschlagen!");
                return false;
            }
            catch (Exception ex)
            {
                // Wenn ein Fehler auftritt, gib eine Fehlermeldung aus
                Console.WriteLine($"Fehler bei der Verbindung zur Datenbank: {ex.Message}");
                return false;
            }
        }

        // Überprüft, ob ein Benutzer existiert
        public static bool UserExists(string userName)
        {
            try
            {
                using var connection = new NpgsqlConnection(ConnectionString);
                connection.Open();

                string query = "SELECT COUNT(*) FROM Users WHERE UserName = @UserName";
                using var command = new NpgsqlCommand(query, connection);

                command.Parameters.AddWithValue("@UserName", userName);
                var result = command.ExecuteScalar();
                return Convert.ToInt32(result) > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Überprüfen des Benutzers: {ex.Message}");
                return false;
            }
        }

        // Liest einen Benutzer aus der Datenbank
        public static User? GetUser(string userName)
        {
            try
            {
                using var connection = new NpgsqlConnection(ConnectionString);
                connection.Open();

                string query = "SELECT UserName, Password FROM Users WHERE UserName = @UserName";
                using var command = new NpgsqlCommand(query, connection);

                command.Parameters.AddWithValue("@UserName", userName);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new User
                    {
                        UserName = reader["UserName"].ToString(),
                        Password = reader["Password"].ToString(),
                    };
                }

                return null; // Benutzer nicht gefunden
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Abrufen des Benutzers: {ex.Message}");
                return null;
            }
        }

        // Methode zum Erstellen eines Benutzers in der Datenbank
        public static void CreateUser(string userName, string hashedPassword)
        {
            try
            {
                using var connection = new NpgsqlConnection(ConnectionString);
                connection.Open();

                string query =
                    "INSERT INTO Users (UserName, Password) VALUES (@UserName, @Password)";
                using var command = new NpgsqlCommand(query, connection);

                command.Parameters.AddWithValue("@UserName", userName);
                command.Parameters.AddWithValue("@Password", hashedPassword);

                command.ExecuteNonQuery();
                Console.WriteLine("Benutzer erfolgreich erstellt.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Erstellen des Benutzers: {ex.Message}");
            }
        }
    }
}
        
        