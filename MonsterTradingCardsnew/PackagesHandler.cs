using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using MonsterTradingCardsnew.Exceptions;
using Npgsql;
    
namespace MonsterTradingCardsnew;

public class PackagesHandler : Handler
{
    public override bool Handle(HttpSvrEventArgs e)
    {
        if ((e.Path.TrimEnd('/', ' ', '\t') == "/packages") && (e.Method == "POST"))
        {   // POST /users wird zur Benutzererstellung verwendet
            return _CreatePackage(e);
        }
        else if ((e.Path.TrimEnd('/', ' ', '\t') == "/transactions/packages") && (e.Method == "POST"))
        {   // POST /login wird zur Benutzeranmeldung verwendet
            return _BuyPackage(e);
        }
        return false;
    }

    private bool _CreatePackage(HttpSvrEventArgs e)
{
    JsonObject? reply = new JsonObject() { ["success"] = false, ["message"] = "Invalid request." };
    int status = HttpStatusCode.BAD_REQUEST; // initialisiere Antwort

    try
    {
        var authorizationHeader = e.Headers
            .FirstOrDefault(h => h.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))?.Value;

        if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
        {
            e.Reply(HttpStatusCode.UNAUTHORIZED, "Authorization header is missing or invalid.");
            return true;
        }

        var token = authorizationHeader.Substring("Bearer ".Length).Trim();

        var (isAuthenticated, authenticatedUser) = Token.Authenticate(token);
        if (!isAuthenticated)
        {
            e.Reply(HttpStatusCode.UNAUTHORIZED, "Invalid or expired token.");
            return true;
        }
        // JSON-Payload parsen
        JsonNode? json = JsonNode.Parse(e.Payload);
        if (json == null || json is not JsonArray cardsArray || cardsArray.Count != 5)
        {
            reply = new JsonObject() { ["success"] = false, ["message"] = "Invalid package data. Exactly 5 cards required." };
            status = HttpStatusCode.BAD_REQUEST;
            e.Reply(status, reply.ToJsonString());
            return true;
        }

        using var connection = new NpgsqlConnection(DBHandler.ConnectionString);
        connection.Open();

        // Karten in der Datenbank erstellen
        List<string> cardIds = new List<string>();
        foreach (JsonNode cardNode in cardsArray)
        {
            if (cardNode is not JsonObject cardJson || !cardJson.ContainsKey("Id") || !cardJson.ContainsKey("Name") || !cardJson.ContainsKey("Damage"))
            {
                reply = new JsonObject() { ["success"] = false, ["message"] = "Invalid card data." };
                status = HttpStatusCode.BAD_REQUEST;
                e.Reply(status, reply.ToJsonString());
                return true;
            }

            string cardId = cardJson["Id"]!.ToString();
            string cardName = cardJson["Name"]!.ToString();
            float damage = float.Parse(cardJson["Damage"]!.ToString(), CultureInfo.InvariantCulture);


            // Element- und Kartentyp bestimmen
            string elementType = ExtractElementType(cardName);
            string cardType = ExtractCardType(cardName);
            string? monsterType = cardType == "Monster" ? ExtractMonsterType(cardName) : null;

            // Karte in die Datenbank einfügen
            string insertCardQuery = @"
                INSERT INTO cards (card_id, name, damage, element_type, card_type, monster_type)
                VALUES (@card_id, @name, @damage, @element_type, @card_type, @monster_type)";
            using var insertCardCommand = new NpgsqlCommand(insertCardQuery, connection);
            insertCardCommand.Parameters.AddWithValue("@card_id", cardId);
            insertCardCommand.Parameters.AddWithValue("@name", cardName);
            insertCardCommand.Parameters.AddWithValue("@damage", damage);
            insertCardCommand.Parameters.AddWithValue("@element_type", elementType);
            insertCardCommand.Parameters.AddWithValue("@card_type", cardType);
            insertCardCommand.Parameters.AddWithValue("@monster_type", (object?)monsterType ?? DBNull.Value);
            insertCardCommand.ExecuteNonQuery();

            cardIds.Add(cardId); // Karte zur Liste hinzufügen
        }

        // Package in der Datenbank erstellen
        string insertPackageQuery = @"
            INSERT INTO packages (card_id1, card_id2, card_id3, card_id4, card_id5)
            VALUES (@card_id1, @card_id2, @card_id3, @card_id4, @card_id5)";
        using var insertPackageCommand = new NpgsqlCommand(insertPackageQuery, connection);
        for (int i = 0; i < cardIds.Count; i++)
        {
            insertPackageCommand.Parameters.AddWithValue($"@card_id{i + 1}", cardIds[i]);
        }
        insertPackageCommand.ExecuteNonQuery();

        status = HttpStatusCode.OK;
        reply = new JsonObject() { ["success"] = true, ["message"] = "Package created successfully." };
    }
    catch (Exception ex)
    {
        reply = new JsonObject() { ["success"] = false, ["message"] = ex.Message };
    }

    e.Reply(status, reply?.ToJsonString()); // Antwort senden
    return true;
}

// Hilfsfunktion, um den `element_type` aus dem Namen zu extrahieren
private string ExtractElementType(string cardName)
{
    if (cardName.Contains("Water", StringComparison.OrdinalIgnoreCase)) return "Water";
    if (cardName.Contains("Fire", StringComparison.OrdinalIgnoreCase)) return "Fire";
    return "Normal"; // Standardwert
}

// Hilfsfunktion, um den `card_type` aus dem Namen zu extrahieren
private string ExtractCardType(string cardName)
{
    if (cardName.Contains("Spell", StringComparison.OrdinalIgnoreCase)) return "Spell";
    return "Monster"; // Standardwert
}

// Hilfsfunktion, um den `monster_type` aus dem Namen zu extrahieren
private string? ExtractMonsterType(string cardName)
{
    string[] monsterTypes = { "Goblin", "Dragon", "Ork", "Knight", "Kraken", "FireElf", "Wizard" };
    foreach (string monsterType in monsterTypes)
    {
        if (cardName.Contains(monsterType, StringComparison.OrdinalIgnoreCase))
        {
            return monsterType;
        }
    }
    return null; // Keine Monsterkarte
}

  public bool _BuyPackage(HttpSvrEventArgs e)
{
    JsonObject? reply = new JsonObject() { ["success"] = false, ["message"] = "Invalid request." };
    int status = HttpStatusCode.BAD_REQUEST; // Initialisiere die Antwort

    try
    {
        var authorizationHeader = e.Headers
            .FirstOrDefault(h => h.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))?.Value;

        if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
        {
            e.Reply(HttpStatusCode.UNAUTHORIZED, "Authorization header is missing or invalid.");
            return true;
        }

        var token = authorizationHeader.Substring("Bearer ".Length).Trim();

        var (isAuthenticated, authenticatedUser) = Token.Authenticate(token);
        if (!isAuthenticated)
        {
            e.Reply(HttpStatusCode.UNAUTHORIZED, "Invalid or expired token.");
            return true;
        }

        using var connection = new NpgsqlConnection(DBHandler.ConnectionString);
        connection.Open();

        // Überprüfen, ob der Benutzer genug Coins hat
        string userQuery = "SELECT coins FROM users WHERE username = @username";
        using var userCommand = new NpgsqlCommand(userQuery, connection);
        userCommand.Parameters.AddWithValue("@username", authenticatedUser.UserName);

        object? result = userCommand.ExecuteScalar();
        int coins = Convert.ToInt32(result);
        if (coins < 5) // Preis des Pakets = 5 Coins
        {
            reply = new JsonObject() { ["success"] = false, ["message"] = "Not enough coins to buy a package." };
            status = HttpStatusCode.BAD_REQUEST;
            e.Reply(status, reply.ToJsonString());
            return true;
        }

        // Ein verfügbares Paket abrufen
        string packageQuery = "SELECT * FROM packages WHERE username IS NULL ORDER BY package_id DESC LIMIT 1";
        using var packageCommand = new NpgsqlCommand(packageQuery, connection);

        using var reader = packageCommand.ExecuteReader();
        if (!reader.Read())
        {
            // Kein verfügbares Paket gefunden
            reply = new JsonObject() { ["success"] = false, ["message"] = "No packages available." };
            status = HttpStatusCode.NOT_FOUND;
            e.Reply(status, reply.ToJsonString());
            return true;
        }

        int packageId = reader.GetInt32(0); // Paket-ID
        string cardId1 = reader.GetString(1);
        string cardId2 = reader.GetString(2);
        string cardId3 = reader.GetString(3);
        string cardId4 = reader.GetString(4);
        string cardId5 = reader.GetString(5);

        reader.Close();

        // Paket dem Benutzer zuweisen
        string updatePackageQuery = "UPDATE packages SET username = @username WHERE package_id = @packageId";
        using var updatePackageCommand = new NpgsqlCommand(updatePackageQuery, connection);
        updatePackageCommand.Parameters.AddWithValue("@username", authenticatedUser.UserName);
        updatePackageCommand.Parameters.AddWithValue("@packageId", packageId);
        updatePackageCommand.ExecuteNonQuery();

        // Karten dem Benutzer zuweisen
        string updateCardsQuery = @"UPDATE cards SET username = @username WHERE card_id IN (@cardId1, @cardId2, @cardId3, @cardId4, @cardId5)";
        using var updateCardsCommand = new NpgsqlCommand(updateCardsQuery, connection);
        updateCardsCommand.Parameters.AddWithValue("@username", authenticatedUser.UserName);
        updateCardsCommand.Parameters.AddWithValue("@cardId1", cardId1);
        updateCardsCommand.Parameters.AddWithValue("@cardId2", cardId2);
        updateCardsCommand.Parameters.AddWithValue("@cardId3", cardId3);
        updateCardsCommand.Parameters.AddWithValue("@cardId4", cardId4);
        updateCardsCommand.Parameters.AddWithValue("@cardId5", cardId5);
        updateCardsCommand.ExecuteNonQuery();

        // Coins des Benutzers aktualisieren
        string updateCoinsQuery = "UPDATE users SET coins = coins - 5 WHERE username = @username";
        using var coinsCommand = new NpgsqlCommand(updateCoinsQuery, connection);
        coinsCommand.Parameters.AddWithValue("@username", authenticatedUser.UserName);
        coinsCommand.ExecuteNonQuery();

        // Erfolgreiche Antwort erstellen
        status = HttpStatusCode.OK;
        reply = new JsonObject() { ["success"] = true, ["message"] = "Package purchased successfully." };
    }
    catch (UserException ex)
    {
        reply = new JsonObject() { ["success"] = false, ["message"] = ex.Message };
    }
    catch (Exception ex)
    {
        reply = new JsonObject() { ["success"] = false, ["message"] = ex.Message };
    }

    e.Reply(status, reply?.ToJsonString()); // Antwort senden
    return true;
}

}