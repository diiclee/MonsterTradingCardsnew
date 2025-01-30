using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;

namespace MonsterTradingCardsnew
{
    public class BattleHandler : Handler
    {
        public override bool Handle(HttpSvrEventArgs e)
        {
            return false;
        }

        public static void _StartBattle(HttpSvrEventArgs player1, HttpSvrEventArgs player2)
        {
            var log = new List<string>();

            var authorizationHeader1 = player1.Headers
                .FirstOrDefault(h => h.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))?.Value;

            if (string.IsNullOrEmpty(authorizationHeader1) || !authorizationHeader1.StartsWith("Bearer "))
            {
                player1.Reply(HttpStatusCode.UNAUTHORIZED, "Authorization header is missing or invalid.");
                return;
            }

            var token1 = authorizationHeader1.Substring("Bearer ".Length).Trim();
            var (isAuthenticated1, authenticatedUser1) = Token.Authenticate(token1);
            if (!isAuthenticated1)
            {
                player1.Reply(HttpStatusCode.UNAUTHORIZED, "Invalid or expired token.");
                return;
            }

            var authorizationHeader2 = player2.Headers
                .FirstOrDefault(h => h.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))?.Value;

            if (string.IsNullOrEmpty(authorizationHeader2) || !authorizationHeader2.StartsWith("Bearer "))
            {
                player2.Reply(HttpStatusCode.UNAUTHORIZED, "Authorization header is missing or invalid.");
                return;
            }

            var token2 = authorizationHeader2.Substring("Bearer ".Length).Trim();
            var (isAuthenticated2, authenticatedUser2) = Token.Authenticate(token2);
            if (!isAuthenticated2)
            {
                player2.Reply(HttpStatusCode.UNAUTHORIZED, "Invalid or expired token.");
                return;
            }

            log.Add($"Kampf startet zwischen {authenticatedUser1.UserName} und {authenticatedUser2.UserName}!");

            // Decks abrufen
            var deck1 = CardHandler.GetDeckByUsername(authenticatedUser1.UserName);
            var deck2 = CardHandler.GetDeckByUsername(authenticatedUser2.UserName);

            if (deck1.Count == 0 || deck2.Count == 0)
            {
                log.Add("Einer der Spieler hat kein vollständiges Deck.");
                SendBattleLog(player1, player2, log);
                return;
            }

            int round = 0;
            while (deck1.Count > 0 && deck2.Count > 0 && round < 100)
            {
                round++;
                log.Add($"Runde {round}:");

                var card1 = deck1[new Random().Next(deck1.Count)];
                var card2 = deck2[new Random().Next(deck2.Count)];

                log.Add(
                    $"{authenticatedUser1.UserName} spielt {card1.Name} ({card1.Damage} Schaden, {card1.ElementType})");
                log.Add(
                    $"{authenticatedUser2.UserName} spielt {card2.Name} ({card2.Damage} Schaden, {card2.ElementType})");

                var winner = DetermineWinner(card1, card2, log);

                if (winner == 1)
                {
                    deck1.Add(card2);
                    deck2.Remove(card2);
                    log.Add($"{authenticatedUser1.UserName} gewinnt die Runde!");
                }
                else if (winner == 2)
                {
                    deck2.Add(card1);
                    deck1.Remove(card1);
                    log.Add($"{authenticatedUser2.UserName} gewinnt die Runde!");
                }
                else
                {
                    log.Add("Unentschieden, beide Karten bleiben.");
                }
            }

            // Gewinner bestimmen
            string battleResult;
            if (deck1.Count > 0)
            {
                battleResult = $"{authenticatedUser1.UserName} gewinnt!";
                UpdateStats(authenticatedUser1.UserName, authenticatedUser2.UserName, 3, -5, 5, 1, 1);
            }
            else if (deck2.Count > 0)
            {
                battleResult = $"{authenticatedUser2.UserName} gewinnt!";
                UpdateStats(authenticatedUser2.UserName, authenticatedUser1.UserName, 3, -5, 5, 1, 1);
            }
            else
            {
                battleResult = "Der Kampf endet unentschieden.";
                UpdateStats(authenticatedUser1.UserName, authenticatedUser2.UserName, 1, 1, 0, 0, 0);
            }

            log.Add(battleResult);
            SendBattleLog(player1, player2, log);
        }

        /// <summary>
        /// Aktualisiert Elo, Coins, Wins und Losses in einer Transaktion
        /// </summary>
        private static void UpdateStats(string winner, string loser, int winnerElo, int loserElo, int coins, int winCount, int lossCount)
        {
            using var connection = new NpgsqlConnection(DBHandler.ConnectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                string updateQuery = @"
                    UPDATE users 
                    SET elo = GREATEST(0, elo + @elo), coins = coins + @coins, wins = wins + @wins
                    WHERE username = @username;
                    
                    UPDATE users 
                    SET elo = GREATEST(0, elo + @elo), losses = losses + @losses
                    WHERE username = @username_loser;
                ";

                using var updateCommand = new NpgsqlCommand(updateQuery, connection);
                updateCommand.Parameters.AddWithValue("@elo", winnerElo);
                updateCommand.Parameters.AddWithValue("@coins", coins);
                updateCommand.Parameters.AddWithValue("@wins", winCount);
                updateCommand.Parameters.AddWithValue("@username", winner);

                updateCommand.Parameters.AddWithValue("@elo", loserElo);
                updateCommand.Parameters.AddWithValue("@losses", lossCount);
                updateCommand.Parameters.AddWithValue("@username_loser", loser);

                updateCommand.ExecuteNonQuery();
                transaction.Commit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Updaten der Spielerstatistiken: {ex.Message}");
                transaction.Rollback();
            }
        }

        private static void SendBattleLog(HttpSvrEventArgs player1, HttpSvrEventArgs player2, List<string> log)
        {
            string logText = string.Join("\n", log);
            player1.Reply(HttpStatusCode.OK, logText);
            player2.Reply(HttpStatusCode.OK, logText);
        }

        private static float GetElementMultiplier(Element attacker, Element defender)
        {
            if (attacker == Element.Water && defender == Element.Fire) return 2.0f;
            if (attacker == Element.Fire && defender == Element.Normal) return 2.0f;
            if (attacker == Element.Normal && defender == Element.Water) return 2.0f;
            if (attacker == Element.Fire && defender == Element.Water) return 0.5f;
            if (attacker == Element.Normal && defender == Element.Fire) return 0.5f;
            if (attacker == Element.Water && defender == Element.Normal) return 0.5f;

            return 1.0f; // Kein Effekt
        }

        private static int DetermineWinner(Card card1, Card card2, List<string> log)
        {
            float damage1 = card1.Damage * GetElementMultiplier(card1.ElementType, card2.ElementType);
            float damage2 = card2.Damage * GetElementMultiplier(card2.ElementType, card1.ElementType);

            log.Add($"Effektive Schadenswerte: {card1.Name} ({damage1}) vs. {card2.Name} ({damage2})");

            return damage1 > damage2 ? 1 : damage2 > damage1 ? 2 : 0;
        }
    }
}
