using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Npgsql;

namespace MonsterTradingCardsnew
{
    public class DataHandler
    {
        public static JsonArray GetScoreboard()
        {
            var scoreboardJson = new JsonArray();

            try
            {
                using var connection = new NpgsqlConnection(DBHandler.ConnectionString);
                connection.Open();

                string query = @"SELECT u.username, u.elo, u.wins, u.losses 
                                 FROM users u 
                                 ORDER BY u.elo DESC";

                using var command = new NpgsqlCommand(query, connection);
                using var reader = command.ExecuteReader();

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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }

            return scoreboardJson;
        }

        public static JsonArray GetUserStats(string username)
        {
            var userStatsJson = new JsonArray();

            try
            {
                using var connection = new NpgsqlConnection(DBHandler.ConnectionString);
                connection.Open();

                string query = @"SELECT u.username, u.elo, u.wins, u.losses 
                                 FROM users u 
                                 WHERE u.username = @username";

                using var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@username", username);

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    var userStats = new JsonObject
                    {
                        ["username"] = reader.GetString(0),
                        ["elo"] = reader.GetInt32(1),
                        ["wins"] = reader.GetInt32(2),
                        ["losses"] = reader.GetInt32(3),
                    };
                    userStatsJson.Add(userStats);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }

            return userStatsJson;
        }
    }
}
