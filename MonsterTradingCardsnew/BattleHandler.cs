using System;
using System.Collections.Generic;
using System.Linq;

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

            if (!AuthenticateUser(player1, out var username1) || !AuthenticateUser(player2, out var username2))
            {
                return;
            }

            var deck1 = CardHandler.GetDeckByUsername(username1);
            var deck2 = CardHandler.GetDeckByUsername(username2);

            log = BattleLogic.StartBattle(username1, username2, deck1, deck2);
            SendBattleLog(player1, player2, log);
        }

        private static bool AuthenticateUser(HttpSvrEventArgs player, out string username)
        {
            username = "";

            var authorizationHeader = player.Headers
                .FirstOrDefault(h => h.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))?.Value;

            if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
            {
                player.Reply(HttpStatusCode.UNAUTHORIZED, "Authorization header is missing or invalid.");
                return false;
            }

            var token = authorizationHeader.Substring("Bearer ".Length).Trim();
            var (isAuthenticated, authenticatedUser) = Token.Authenticate(token);
            if (!isAuthenticated)
            {
                player.Reply(HttpStatusCode.UNAUTHORIZED, "Invalid or expired token.");
                return false;
            }

            username = authenticatedUser.UserName;
            return true;
        }

        private static void SendBattleLog(HttpSvrEventArgs player1, HttpSvrEventArgs player2, List<string> log)
        {
            string logText = string.Join("\n", log);
            player1.Reply(HttpStatusCode.OK, logText);
            player2.Reply(HttpStatusCode.OK, logText);
        }
    }
}
