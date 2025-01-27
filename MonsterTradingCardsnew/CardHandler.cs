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
        string query = "SELECT card_id, name, damage, element_type, card_type, monster_type FROM cards WHERE username = @username";
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
                ["damage"] = reader.GetFloat(2),
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

}