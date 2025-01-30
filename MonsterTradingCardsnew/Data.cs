using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;

namespace MonsterTradingCardsnew
{
    public class Data : Handler
    {
        public override bool Handle(HttpSvrEventArgs e)
        {
            if ((e.Path.TrimEnd('/', ' ', '\t') == "/scoreboard") && (e.Method == "GET"))
            {
                return GetScoreboard(e);
            }
            if ((e.Path.TrimEnd('/', ' ', '\t') == "/stats") && (e.Method == "GET"))
            {
                return GetStats(e);
            }

            return false;
        }

        private bool GetScoreboard(HttpSvrEventArgs e)
{
    JsonObject? reply = new JsonObject() { ["success"] = false, ["message"] = "Invalid request." };
    int status = HttpStatusCode.BAD_REQUEST; // Initialisiere Antwort

    try
    {
        // Authorization Header prüfen
        var authorizationHeader = e.Headers
            .FirstOrDefault(h => h.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))?.Value;

        if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
        {
            e.Reply(HttpStatusCode.UNAUTHORIZED, "Authorization header is missing or invalid.");
            return true;
        }

        // Token extrahieren und Benutzer authentifizieren (aber nicht filtern)
        var token = authorizationHeader.Substring("Bearer ".Length).Trim();
        var (isAuthenticated, authenticatedUser) = Token.Authenticate(token);
        if (!isAuthenticated)
        {
            e.Reply(HttpStatusCode.UNAUTHORIZED, "Invalid or expired token.");
            return true;
        }

        using var connection = new NpgsqlConnection(DBHandler.ConnectionString);
        connection.Open();

        // Scoreboard für alle Benutzer abrufen, nach Elo sortiert
        string query = @"SELECT u.username, u.elo, u.wins, u.losses 
                         FROM users u 
                         ORDER BY u.elo DESC";

        using var command = new NpgsqlCommand(query, connection);
        using var reader = command.ExecuteReader();
        var scoreboardJson = new JsonArray(); // JSON Array für das Scoreboard

        // Spieler-Daten ins JSON-Format umwandeln
        while (reader.Read())
        {
            var scoreboard = new JsonObject
            {
                ["username"] = reader.GetString(0),
                ["elo"] = reader.GetInt32(1),
                ["wins"] = reader.GetInt32(2),
                ["losses"] = reader.GetInt32(3), 
            };
            scoreboardJson.Add(scoreboard);
        }

        // JSON-Antwort erstellen
        reply = new JsonObject
        {
            ["success"] = true,
            ["scoreboard"] = scoreboardJson
        };
        status = HttpStatusCode.OK;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Exception: {ex.Message}");
        reply = new JsonObject
        {
            ["success"] = false,
            ["message"] = $"Internal Server Error: {ex.Message}"
        };
    }

    e.Reply(status, reply?.ToJsonString()); // Antwort senden
    return true;
}

        
        private bool GetStats(HttpSvrEventArgs e)
        {
            JsonObject? reply = new JsonObject() { ["success"] = false, ["message"] = "Invalid request." };
            int status = HttpStatusCode.BAD_REQUEST; // Initialisiere Antwort

            try
            {
                // Authorization Header prüfen
                var authorizationHeader = e.Headers
                    .FirstOrDefault(h => h.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))?.Value;

                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    e.Reply(HttpStatusCode.UNAUTHORIZED, "Authorization header is missing or invalid.");
                    return true;
                }

                // Token extrahieren und Benutzer authentifizieren
                var token = authorizationHeader.Substring("Bearer ".Length).Trim();
                var (isAuthenticated, authenticatedUser) = Token.Authenticate(token);
                if (!isAuthenticated)
                {
                    e.Reply(HttpStatusCode.UNAUTHORIZED, "Invalid or expired token.");
                    return true;
                }

                using var connection = new NpgsqlConnection(DBHandler.ConnectionString);
                connection.Open();

                // Scoreboard aus der Datenbank abrufen
                string query = @"SELECT u.username, u.elo, u.wins, u.losses 
                 FROM users u 
                 WHERE u.username = @username";

                using var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@username", authenticatedUser.UserName);

                using var reader = command.ExecuteReader();
                var scoreboardJson = new JsonArray(); // JSON Array für das Scoreboard

                // Spieler-Daten ins JSON-Format umwandeln
                while (reader.Read())
                {
                    var scoreboard = new JsonObject
                    {
                        ["username"] = reader.GetString(0),
                        ["elo"] = reader.GetInt32(1),
                        ["wins"] = reader.GetInt32(2),
                        ["losses"] = reader.GetInt32(3), 
                    };
                    scoreboardJson.Add(scoreboard);
                }

                // JSON-Antwort erstellen
                reply = new JsonObject
                {
                    ["success"] = true,
                    ["scoreboard"] = scoreboardJson
                };
                status = HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                reply = new JsonObject
                {
                    ["success"] = false,
                    ["message"] = $"Internal Server Error: {ex.Message}"
                };
            }

            e.Reply(status, reply?.ToJsonString()); // Antwort senden
            return true;
        }
    }
}