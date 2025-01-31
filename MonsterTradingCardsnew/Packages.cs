using System.Linq;
using System.Net;
using System.Text.Json.Nodes;

namespace MonsterTradingCardsnew
{
    public class PackagesHandler : Handler
    {
        public override bool Handle(HttpSvrEventArgs e)
        {
            if ((e.Path.TrimEnd('/', ' ', '\t') == "/packages") && (e.Method == "POST"))
            {
                return _CreatePackage(e);
            }
            else if ((e.Path.TrimEnd('/', ' ', '\t') == "/transactions/packages") && (e.Method == "POST"))
            {
                return _BuyPackage(e);
            }

            return false;
        }

        private bool _CreatePackage(HttpSvrEventArgs e)
        {
            if (!AuthenticateUser(e, out _, out var status))
            {
                return true;
            }

            JsonNode? json = JsonNode.Parse(e.Payload);
            if (json == null || json is not JsonArray cardsArray || cardsArray.Count != 5)
            {
                e.Reply(HttpStatusCode.BAD_REQUEST, "Invalid package data. Exactly 5 cards required.");
                return true;
            }

            bool success = PackagesDataHandler.CreatePackage(cardsArray, out var message);
            e.Reply(success ? HttpStatusCode.OK : HttpStatusCode.BAD_REQUEST, message);
            return true;
        }

        private bool _BuyPackage(HttpSvrEventArgs e)
        {
            if (!AuthenticateUser(e, out var username, out var status))
            {
                return true;
            }

            bool success = PackagesDataHandler.BuyPackage(username, out var message);
            e.Reply(success ? HttpStatusCode.OK : HttpStatusCode.BAD_REQUEST, message);
            return true;
        }

        private bool AuthenticateUser(HttpSvrEventArgs e, out string username, out int status)
        {
            username = "";
            status = HttpStatusCode.UNAUTHORIZED;
            var authorizationHeader = e.Headers
                .FirstOrDefault(h => h.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))?.Value;

            if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
            {
                e.Reply(status, "Authorization header is missing or invalid.");
                return false;
            }

            var token = authorizationHeader.Substring("Bearer ".Length).Trim();
            var (isAuthenticated, authenticatedUser) = Token.Authenticate(token);
            if (!isAuthenticated)
            {
                e.Reply(status, "Invalid or expired token.");
                return false;
            }

            username = authenticatedUser.UserName;
            status = HttpStatusCode.OK;
            return true;
        }
    }
}