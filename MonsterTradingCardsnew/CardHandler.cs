using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using MonsterTradingCardsnew.Exceptions;
using Npgsql;

namespace MonsterTradingCardsnew;

public class CardHandler : Handler
{
    public override bool Handle(HttpSvrEventArgs e)
    {
        if ((e.Path.TrimEnd('/', ' ', '\t') == "/cards") && (e.Method == "GET"))
        {
            // POST /users wird zur Benutzererstellung verwendet
            return _ShowCards(e);
        }
        else if ((e.Path.TrimEnd('/', ' ', '\t') == "/deck") && (e.Method == "PUT"))
        {
            // PUT /deck wird verwendet, um Karten einem Deck zuzuweisen
            return _MakeDeck(e);
        }
        else if ((e.Path.TrimEnd('/', ' ', '\t') == "/deck") && (e.Method == "GET"))
        {
            // GET /deck: Karten des Decks abrufen
            return _GetDeck(e);
        }
        else if ((e.Path.TrimEnd('/', ' ', '\t') == "/deck?format=plain") && (e.Method == "GET"))
        {
            // GET /deck: Karten des Decks abrufen
            return _GetDeck(e);
        }

        return false;
    }

    public bool _ShowCards(HttpSvrEventArgs e)
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

            // Karten des Benutzers aus der Datenbank abrufen
            string query =
                "SELECT card_id, name, damage, element_type, card_type, monster_type FROM cards WHERE username = @username";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@username", authenticatedUser.UserName);

            using var reader = command.ExecuteReader();
            var cards = new JsonArray();

            // Karten in JSON-Format konvertieren
            while (reader.Read())
            {
                var card = new JsonObject
                {
                    ["card_id"] = reader.GetString(0),
                    ["name"] = reader.GetString(1),
                    ["damage"] = reader.GetFloat(2).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                    ["element_type"] = reader.GetString(3),
                    ["card_type"] = reader.GetString(4),
                    ["monster_type"] = reader.IsDBNull(5) ? null : reader.GetString(5)
                };

                cards.Add(card);
            }

            // Erfolgreiche Antwort erstellen
            reply = new JsonObject()
            {
                ["success"] = true,
                ["cards"] = cards
            };
            status = HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
            reply = new JsonObject() { ["success"] = false, ["message"] = ex.Message };
        }

        e.Reply(status, reply?.ToJsonString()); // Antwort senden
        return true;
    }

    public bool _MakeDeck(HttpSvrEventArgs e)
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

            // JSON-Payload parsen
            JsonNode? json = JsonNode.Parse(e.Payload);
            if (json == null || json is not JsonArray cardIds || cardIds.Count != 4)
            {
                reply = new JsonObject() { ["success"] = false, ["message"] = "Exactly 4 card IDs are required." };
                status = HttpStatusCode.BAD_REQUEST;
                e.Reply(status, reply.ToJsonString());
                return true;
            }

            using var connection = new NpgsqlConnection(DBHandler.ConnectionString);
            connection.Open();

            // Überprüfen, ob die Karten dem Benutzer gehören
            string query = "SELECT card_id FROM cards WHERE username = @username AND card_id = ANY(@card_ids)";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@username", authenticatedUser.UserName);
            command.Parameters.AddWithValue("@card_ids", cardIds.Select(id => id.ToString()).ToArray());

            var validCardIds = new List<string>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    validCardIds.Add(reader.GetString(0));
                }
            }

            if (validCardIds.Count != 4)
            {
                reply = new JsonObject()
                {
                    ["success"] = false,
                    ["message"] = "Some cards do not belong to the user or are invalid."
                };
                status = HttpStatusCode.BAD_REQUEST;
                e.Reply(status, reply.ToJsonString());
                return true;
            }

            // Deck aktualisieren
            string updateDeckQuery = @"
            INSERT INTO deck (username, card1_id, card2_id, card3_id, card4_id)
            VALUES (@username, @card1_id, @card2_id, @card3_id, @card4_id)
            ON CONFLICT (username)
            DO UPDATE SET card1_id = @card1_id, card2_id = @card2_id, card3_id = @card3_id, card4_id = @card4_id";
            using var updateCommand = new NpgsqlCommand(updateDeckQuery, connection);
            updateCommand.Parameters.AddWithValue("@username", authenticatedUser.UserName);
            updateCommand.Parameters.AddWithValue("@card1_id", validCardIds[0]);
            updateCommand.Parameters.AddWithValue("@card2_id", validCardIds[1]);
            updateCommand.Parameters.AddWithValue("@card3_id", validCardIds[2]);
            updateCommand.Parameters.AddWithValue("@card4_id", validCardIds[3]);
            updateCommand.ExecuteNonQuery();

            // Erfolgreiche Antwort erstellen
            reply = new JsonObject() { ["success"] = true, ["message"] = "Deck updated successfully." };
            status = HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
            reply = new JsonObject() { ["success"] = false, ["message"] = ex.Message };
        }

        e.Reply(status, reply?.ToJsonString()); // Antwort senden
        return true;
    }

    public bool _GetDeck(HttpSvrEventArgs e)
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

            // Deck aus der Datenbank abrufen
            string query = @"
            SELECT c.card_id, c.name, c.damage, c.element_type, c.card_type, c.monster_type
            FROM deck d
            JOIN cards c ON c.card_id = ANY(ARRAY[d.card1_id, d.card2_id, d.card3_id, d.card4_id])
            WHERE d.username = @username";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@username", authenticatedUser.UserName);

            using var reader = command.ExecuteReader();
            var cards = new List<string>(); // Liste für Plain-Text Formatierung
            var cardsJson = new JsonArray(); // JSON für Standardausgabe

            // Karten in JSON-Format und Plain-Text konvertieren
            while (reader.Read())
            {
                var card = new JsonObject
                {
                    ["card_id"] = reader.GetString(0),
                    ["name"] = reader.GetString(1),
                    ["damage"] = reader.GetFloat(2).ToString("0.0"), // Stelle sicher, dass .0 angezeigt wird
                    ["element_type"] = reader.GetString(3),
                    ["card_type"] = reader.GetString(4),
                    ["monster_type"] = reader.IsDBNull(5) ? null : reader.GetString(5)
                };

                cardsJson.Add(card);
                cards.Add($"{card["name"]} ({card["damage"]})"); // Beispiel für Plain-Text-Format
            }

            // **Alternative Methode zur Überprüfung von Query-Parametern**
            bool isPlainFormat = e.Path.Contains("format=plain") || e.Path.EndsWith("format=plain");

            if (isPlainFormat)
            {
                reply = new JsonObject() { ["success"] = true, ["deck"] = string.Join("\n", cards) };
            }
            else
            {
                reply = new JsonObject() { ["success"] = true, ["cards"] = cardsJson };
            }

            status = HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
            reply = new JsonObject() { ["success"] = false, ["message"] = ex.Message };
        }

        e.Reply(status, reply?.ToJsonString()); // Antwort senden
        return true;
    }
}