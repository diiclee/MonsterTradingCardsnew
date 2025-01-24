using System;
using Npgsql;

namespace MonsterTradingCardsnew
{
    public static class DBHandler
    {
        private const string ConnectionString = "Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=postgres";

        // Testet Datenbankverbindung 
        public static void TestConnection()
        {
            try
            {
                using var connection = new NpgsqlConnection(ConnectionString);
                connection.Open();
                Console.WriteLine("Verbindung zur Datenbank erfolgreich!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler bei der Datenbankverbindung: {ex.Message}");
            }
        }

        // Erstellt neuen Benutzer
        public static void CreateUser(string userName, string password)
        {
            try
            {
                using var connection = new NpgsqlConnection(ConnectionString);
                connection.Open();

                string query = "INSERT INTO Users (UserName, Password, Coins, Elo) VALUES (@UserName, @Password, 20, 0)";
                using var command = new NpgsqlCommand(query, connection);

                command.Parameters.AddWithValue("@UserName", userName);
                command.Parameters.AddWithValue("@Password", password); // Passwörter noch hashen!

                int rowsAffected = command.ExecuteNonQuery();
                Console.WriteLine($"{rowsAffected} Benutzer erfolgreich erstellt.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Erstellen des Benutzers: {ex.Message}");
            }
        }

        // Liest einen Benutzer aus der Datenbank
        public static void GetUser(string userName)
        {
            try
            {
                using var connection = new NpgsqlConnection(ConnectionString);
                connection.Open();

                string query = "SELECT * FROM Users WHERE UserName = @UserName";
                using var command = new NpgsqlCommand(query, connection);

                command.Parameters.AddWithValue("@UserName", userName);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    Console.WriteLine($"UserName: {reader["UserName"]}, Coins: {reader["Coins"]}, Elo: {reader["Elo"]}");
                }
                else
                {
                    Console.WriteLine("Benutzer nicht gefunden.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Lesen des Benutzers: {ex.Message}");
            }
        }

        // Aktualisiert Münzen eines Benutzers
        public static void UpdateCoins(string userName, int coins)
        {
            try
            {
                using var connection = new NpgsqlConnection(ConnectionString);
                connection.Open();

                string query = "UPDATE Users SET Coins = @Coins WHERE UserName = @UserName";
                using var command = new NpgsqlCommand(query, connection);

                command.Parameters.AddWithValue("@UserName", userName);
                command.Parameters.AddWithValue("@Coins", coins);

                int rowsAffected = command.ExecuteNonQuery();
                Console.WriteLine($"{rowsAffected} Benutzer erfolgreich aktualisiert.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Aktualisieren der Münzen: {ex.Message}");
            }
        }

        // Löscht Benutzer
        public static void DeleteUser(string userName)
        {
            try
            {
                using var connection = new NpgsqlConnection(ConnectionString);
                connection.Open();

                string query = "DELETE FROM Users WHERE UserName = @UserName";
                using var command = new NpgsqlCommand(query, connection);

                command.Parameters.AddWithValue("@UserName", userName);

                int rowsAffected = command.ExecuteNonQuery();
                Console.WriteLine($"{rowsAffected} Benutzer erfolgreich gelöscht.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Löschen des Benutzers: {ex.Message}");
            }
        }
    }
}
