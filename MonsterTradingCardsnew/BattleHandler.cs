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
                UpdateElo(authenticatedUser1.UserName, authenticatedUser2.UserName, 3, -5);
            }
            else if (deck2.Count > 0)
            {
                battleResult = $"{authenticatedUser2.UserName} gewinnt!";
                UpdateElo(authenticatedUser2.UserName, authenticatedUser1.UserName, 3, -5);
            }
            else
            {
                battleResult = "Der Kampf endet unentschieden.";
                UpdateElo(authenticatedUser1.UserName, authenticatedUser2.UserName, 1, 1);
            }

            log.Add(battleResult);
            SendBattleLog(player1, player2, log);
        }

        private static void UpdateElo(string winner, string loser, int winnerPoints, int loserPoints)
        {
            using var connection = new NpgsqlConnection(DBHandler.ConnectionString);
            connection.Open();

            string updateQuery = "UPDATE users SET elo = GREATEST(0, elo + @elo) WHERE username = @username";
            using var updateCommand = new NpgsqlCommand(updateQuery, connection);

            // Gewinner-ELO aktualisieren
            updateCommand.Parameters.AddWithValue("@elo", winnerPoints);
            updateCommand.Parameters.AddWithValue("@username", winner);
            updateCommand.ExecuteNonQuery();

            // Verlierer-ELO aktualisieren
            updateCommand.Parameters["@elo"].Value = loserPoints;
            updateCommand.Parameters["@username"].Value = loser;
            updateCommand.ExecuteNonQuery();
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
            // Spezialfälle prüfen
            if (card1.MonsterType == "Goblin" && card2.MonsterType == "Dragon")
            {
                log.Add("Goblin hat zu viel Angst vor Dragon, kann nicht angreifen!");
                return 2;
            }

            if (card2.MonsterType == "Goblin" && card1.MonsterType == "Dragon")
            {
                log.Add("Goblin hat zu viel Angst vor Dragon, kann nicht angreifen!");
                return 1;
            }

            if (card1.MonsterType == "Kraken" && card2.CardType == "Spell")
            {
                log.Add("Kraken ist immun gegen Zauber!");
                return 1;
            }

            if (card2.MonsterType == "Kraken" && card1.CardType == "Spell")
            {
                log.Add("Kraken ist immun gegen Zauber!");
                return 2;
            }

            if (card1.MonsterType == "Knight" && card2.CardType == "Spell" && card2.ElementType == Element.Water)
            {
                log.Add("Knight wird von WaterSpell sofort besiegt!");
                return 2;
            }

            if (card2.MonsterType == "Knight" && card1.CardType == "Spell" && card1.ElementType == Element.Water)
            {
                log.Add("Knight wird von WaterSpell sofort besiegt!");
                return 1;
            }

            // Spezialfälle: FireElves und Wizards
            if (card1.MonsterType == "FireElf" && card2.MonsterType == "Dragon")
            {
                log.Add("FireElf weicht dem Angriff des Dragons aus!");
                return 2;
            }

            if (card2.MonsterType == "FireElf" && card1.MonsterType == "Dragon")
            {
                log.Add("FireElf weicht dem Angriff des Dragons aus!");
                return 1;
            }

            if (card1.MonsterType == "Wizard" && card2.MonsterType == "Ork")
            {
                log.Add("Wizard kontrolliert den Ork! Der Ork gibt auf.");
                return 1;
            }

            if (card2.MonsterType == "Wizard" && card1.MonsterType == "Ork")
            {
                log.Add("Wizard kontrolliert den Ork! Der Ork gibt auf.");
                return 2;
            }

            // Elementarboni berechnen
            float damage1 = card1.Damage * GetElementMultiplier(card1.ElementType, card2.ElementType);
            float damage2 = card2.Damage * GetElementMultiplier(card2.ElementType, card1.ElementType);

            log.Add($"Effektive Schadenswerte: {card1.Name} ({damage1}) vs. {card2.Name} ({damage2})");

            return damage1 > damage2 ? 1 : damage2 > damage1 ? 2 : 0;
        }
    }
}