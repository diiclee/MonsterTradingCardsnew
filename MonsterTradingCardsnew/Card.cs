using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Npgsql;

namespace MonsterTradingCardsnew
{
    public class Card
    {
        public string Name { get; set; }
        public float Damage { get; set; }
        public Element ElementType { get; set; }
        public string CardType { get; set; }
        public string? MonsterType { get; set; }

        public Card(string name, float damage, Element elementType, string cardType, string? monsterType = null)
        {
            Name = name;
            Damage = damage;
            ElementType = elementType;
            CardType = cardType;
            MonsterType = (cardType == "Monster") ? monsterType : null;
        }

        public static JsonArray GetUserCards(string username)
        {
            var cardsJson = new JsonArray();

            try
            {
                using var connection = new NpgsqlConnection(DBHandler.ConnectionString);
                connection.Open();

                string query =
                    "SELECT card_id, name, damage, element_type, card_type, monster_type FROM cards WHERE username = @username";
                using var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@username", username);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var card = new JsonObject
                    {
                        ["card_id"] = reader.GetString(0),
                        ["name"] = reader.GetString(1),
                        ["damage"] = reader.GetFloat(2).ToString("0.0"),
                        ["element_type"] = reader.GetString(3),
                        ["card_type"] = reader.GetString(4),
                        ["monster_type"] = reader.IsDBNull(5) ? null : reader.GetString(5)
                    };

                    cardsJson.Add(card);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }

            return cardsJson;
        }

        public static bool SetDeck(string username, List<string> cardIds, out string message)
        {
            message = "";
            try
            {
                using var connection = new NpgsqlConnection(DBHandler.ConnectionString);
                connection.Open();

                string query = "SELECT card_id FROM cards WHERE username = @username AND card_id = ANY(@card_ids)";
                using var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@username", username);
                command.Parameters.AddWithValue("@card_ids", cardIds.ToArray());

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
                    message = "Some cards do not belong to the user or are invalid.";
                    return false;
                }

                string updateDeckQuery = @"
                INSERT INTO deck (username, card1_id, card2_id, card3_id, card4_id)
                VALUES (@username, @card1_id, @card2_id, @card3_id, @card4_id)
                ON CONFLICT (username)
                DO UPDATE SET card1_id = @card1_id, card2_id = @card2_id, card3_id = @card3_id, card4_id = @card4_id";

                using var updateCommand = new NpgsqlCommand(updateDeckQuery, connection);
                updateCommand.Parameters.AddWithValue("@username", username);
                updateCommand.Parameters.AddWithValue("@card1_id", validCardIds[0]);
                updateCommand.Parameters.AddWithValue("@card2_id", validCardIds[1]);
                updateCommand.Parameters.AddWithValue("@card3_id", validCardIds[2]);
                updateCommand.Parameters.AddWithValue("@card4_id", validCardIds[3]);
                updateCommand.ExecuteNonQuery();

                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        public static List<Card> GetDeckByUsername(string username)
        {
            var deck = new List<Card>();

            try
            {
                using var connection = new NpgsqlConnection(DBHandler.ConnectionString);
                connection.Open();

                string query = @"
                SELECT c.name, c.damage, c.element_type, c.card_type, c.monster_type
                FROM deck d
                JOIN cards c ON c.card_id = ANY(ARRAY[d.card1_id, d.card2_id, d.card3_id, d.card4_id])
                WHERE d.username = @username";

                using var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@username", username);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    deck.Add(new Card(
                        reader.GetString(0),
                        reader.GetFloat(1),
                        Enum.Parse<Element>(reader.GetString(2)),
                        reader.GetString(3),
                        reader.IsDBNull(4) ? null : reader.GetString(4)
                    ));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Laden des Decks für {username}: {ex.Message}");
            }

            return deck;
        }
    }
}