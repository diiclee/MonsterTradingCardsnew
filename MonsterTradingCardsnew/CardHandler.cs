using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json.Nodes;

namespace MonsterTradingCardsnew
{
    public class CardHandler : Handler
    {
        public override bool Handle(HttpSvrEventArgs e)
        {
            if ((e.Path.TrimEnd('/', ' ', '\t') == "/cards") && (e.Method == "GET"))
            {
                return _ShowCards(e);
            }
            else if ((e.Path.TrimEnd('/', ' ', '\t') == "/deck") && (e.Method == "PUT"))
            {
                return _MakeDeck(e);
            }
            else if ((e.Path.TrimEnd('/', ' ', '\t') == "/deck") && (e.Method == "GET"))
            {
                return _GetDeck(e);
            }
            else if ((e.Path.TrimEnd('/', ' ', '\t') == "/deck?format=plain") && (e.Method == "GET"))
            {
                return _GetDeck(e);
            }

            return false;
        }

        private bool _ShowCards(HttpSvrEventArgs e)
        {
            if (!AuthenticateUser(e, out var username))
            {
                return true;
            }

            var cardsJson = Card.GetUserCards(username);

            e.Reply(HttpStatusCode.OK, new JsonObject
            {
                ["success"] = true,
                ["cards"] = cardsJson
            }.ToJsonString());

            return true;
        }

        private bool _MakeDeck(HttpSvrEventArgs e)
        {
            if (!AuthenticateUser(e, out var username))
            {
                return true;
            }

            JsonNode? json = JsonNode.Parse(e.Payload);
            if (json == null || json is not JsonArray cardIds || cardIds.Count != 4)
            {
                e.Reply(HttpStatusCode.BAD_REQUEST, "Exactly 4 card IDs are required.");
                return true;
            }

            bool success = Card.SetDeck(username, cardIds.Select(id => id.ToString()).ToList(), out var message);
            e.Reply(success ? HttpStatusCode.OK : HttpStatusCode.BAD_REQUEST, message);
            return true;
        }

        private bool _GetDeck(HttpSvrEventArgs e)
        {
            if (!AuthenticateUser(e, out var username))
            {
                return true;
            }

            var deck = Card.GetDeckByUsername(username);
            var cardsJson = new JsonArray();
            var cardsPlainText = new List<string>();

            foreach (var card in deck)
            {
                var cardJson = new JsonObject
                {
                    ["name"] = card.Name,
                    ["damage"] = card.Damage.ToString("0.0"),
                    ["element_type"] = card.ElementType.ToString(),
                    ["card_type"] = card.CardType,
                    ["monster_type"] = card.MonsterType
                };

                cardsJson.Add(cardJson);
                cardsPlainText.Add($"{card.Name} ({card.Damage} Schaden, {card.ElementType})");
            }

            bool isPlainFormat = e.Path.Contains("format=plain");

            if (isPlainFormat)
            {
                e.Reply(HttpStatusCode.OK, string.Join("\n", cardsPlainText)); // Plain-Text-Antwort
            }
            else
            {
                e.Reply(HttpStatusCode.OK, new JsonObject
                {
                    ["success"] = true,
                    ["cards"] = cardsJson
                }.ToJsonString()); // JSON-Antwort
            }

            return true;
        }


        private bool AuthenticateUser(HttpSvrEventArgs e, out string username)
        {
            username = "";

            var authorizationHeader = e.Headers
                .FirstOrDefault(h => h.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))?.Value;

            if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
            {
                e.Reply(HttpStatusCode.UNAUTHORIZED, "Authorization header is missing or invalid.");
                return false;
            }

            var token = authorizationHeader.Substring("Bearer ".Length).Trim();
            var (isAuthenticated, authenticatedUser) = Token.Authenticate(token);
            if (!isAuthenticated)
            {
                e.Reply(HttpStatusCode.UNAUTHORIZED, "Invalid or expired token.");
                return false;
            }

            username = authenticatedUser.UserName;
            return true;
        }
    }
}
