using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;
using Npgsql;

namespace MonsterTradingCardsnew
{
    public class PackagesDataHandler
    {
        public static bool CreatePackage(JsonArray cardsArray, out string message)
        {
            message = "";
            List<string> cardIds = new List<string>();

            try
            {
                using var connection = new NpgsqlConnection(DBHandler.ConnectionString);
                connection.Open();

                foreach (JsonNode cardNode in cardsArray)
                {
                    if (cardNode is not JsonObject cardJson || !cardJson.ContainsKey("Id") ||
                        !cardJson.ContainsKey("Name") || !cardJson.ContainsKey("Damage"))
                    {
                        message = "Invalid card data.";
                        return false;
                    }

                    string cardId = cardJson["Id"]!.ToString();
                    string cardName = cardJson["Name"]!.ToString();
                    float damage = float.Parse(cardJson["Damage"]!.ToString(), CultureInfo.InvariantCulture);

                    string elementType = ExtractElementType(cardName);
                    string cardType = ExtractCardType(cardName);
                    string? monsterType = cardType == "Monster" ? ExtractMonsterType(cardName) : null;

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

                    cardIds.Add(cardId);
                }

                string insertPackageQuery = @"
                INSERT INTO packages (card_id1, card_id2, card_id3, card_id4, card_id5)
                VALUES (@card_id1, @card_id2, @card_id3, @card_id4, @card_id5)";

                using var insertPackageCommand = new NpgsqlCommand(insertPackageQuery, connection);
                for (int i = 0; i < cardIds.Count; i++)
                {
                    insertPackageCommand.Parameters.AddWithValue($"@card_id{i + 1}", cardIds[i]);
                }

                insertPackageCommand.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        public static bool BuyPackage(string username, out string message)
        {
            message = "";

            try
            {
                using var connection = new NpgsqlConnection(DBHandler.ConnectionString);
                connection.Open();

                string userQuery = "SELECT coins FROM users WHERE username = @username";
                using var userCommand = new NpgsqlCommand(userQuery, connection);
                userCommand.Parameters.AddWithValue("@username", username);
                object? result = userCommand.ExecuteScalar();
                int coins = Convert.ToInt32(result);

                if (coins < 5)
                {
                    message = "Not enough coins to buy a package.";
                    return false;
                }

                string packageQuery = "SELECT * FROM packages WHERE username IS NULL ORDER BY package_id ASC LIMIT 1";
                using var packageCommand = new NpgsqlCommand(packageQuery, connection);
                using var reader = packageCommand.ExecuteReader();

                if (!reader.Read())
                {
                    message = "No packages available.";
                    return false;
                }

                int packageId = reader.GetInt32(0);
                string cardId1 = reader.GetString(1);
                string cardId2 = reader.GetString(2);
                string cardId3 = reader.GetString(3);
                string cardId4 = reader.GetString(4);
                string cardId5 = reader.GetString(5);

                reader.Close();

                string updatePackageQuery = "UPDATE packages SET username = @username WHERE package_id = @packageId";
                using var updatePackageCommand = new NpgsqlCommand(updatePackageQuery, connection);
                updatePackageCommand.Parameters.AddWithValue("@username", username);
                updatePackageCommand.Parameters.AddWithValue("@packageId", packageId);
                updatePackageCommand.ExecuteNonQuery();

                string updateCardsQuery =
                    @"UPDATE cards SET username = @username WHERE card_id IN (@cardId1, @cardId2, @cardId3, @cardId4, @cardId5)";
                using var updateCardsCommand = new NpgsqlCommand(updateCardsQuery, connection);
                updateCardsCommand.Parameters.AddWithValue("@username", username);
                updateCardsCommand.Parameters.AddWithValue("@cardId1", cardId1);
                updateCardsCommand.Parameters.AddWithValue("@cardId2", cardId2);
                updateCardsCommand.Parameters.AddWithValue("@cardId3", cardId3);
                updateCardsCommand.Parameters.AddWithValue("@cardId4", cardId4);
                updateCardsCommand.Parameters.AddWithValue("@cardId5", cardId5);
                updateCardsCommand.ExecuteNonQuery();

                string updateCoinsQuery = "UPDATE users SET coins = coins - 5 WHERE username = @username";
                using var coinsCommand = new NpgsqlCommand(updateCoinsQuery, connection);
                coinsCommand.Parameters.AddWithValue("@username", username);
                coinsCommand.ExecuteNonQuery();

                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private static string ExtractElementType(string cardName) =>
            cardName.Contains("Water", StringComparison.OrdinalIgnoreCase) ? "Water" :
            cardName.Contains("Fire", StringComparison.OrdinalIgnoreCase) ? "Fire" : "Normal";

        private static string ExtractCardType(string cardName) =>
            cardName.Contains("Spell", StringComparison.OrdinalIgnoreCase) ? "Spell" : "Monster";

        private static string? ExtractMonsterType(string cardName)
        {
            string[] monsterTypes = { "Goblin", "Dragon", "Ork", "Knight", "Kraken", "FireElf", "Wizard" };
            foreach (string monsterType in monsterTypes)
            {
                if (cardName.Contains(monsterType, StringComparison.OrdinalIgnoreCase))
                {
                    return monsterType;
                }
            }
            return null;
        }
    }
}
