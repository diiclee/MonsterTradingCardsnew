using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MonsterTradingCardsnew
{
    public class Data : Handler
    {
        public override bool Handle(HttpSvrEventArgs e)
        {
            if ((e.Path.TrimEnd('/', ' ', '\t') == "/scoreboard") && (e.Method == "GET"))
            {
                return GetScoreboard(e);
            }
            if ((e.Path.TrimEnd('/', ' ', '\t') == "/stats") && (e.Method == "GET"))
            {
                return GetStats(e);
            }

            return false;
        }

        private bool GetScoreboard(HttpSvrEventArgs e)
        {
            int status = HttpStatusCode.BAD_REQUEST;
            JsonObject reply = new JsonObject { ["success"] = false, ["message"] = "Invalid request." };

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
                var (isAuthenticated, _) = Token.Authenticate(token);
                if (!isAuthenticated)
                {
                    e.Reply(HttpStatusCode.UNAUTHORIZED, "Invalid or expired token.");
                    return true;
                }

                var scoreboardJson = DataHandler.GetScoreboard();

                reply = new JsonObject
                {
                    ["success"] = true,
                    ["scoreboard"] = scoreboardJson
                };
                status = HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                reply = new JsonObject
                {
                    ["success"] = false,
                    ["message"] = $"Internal Server Error: {ex.Message}"
                };
            }

            e.Reply(status, reply.ToJsonString());
            return true;
        }

        private bool GetStats(HttpSvrEventArgs e)
        {
            int status = HttpStatusCode.BAD_REQUEST;
            JsonObject reply = new JsonObject { ["success"] = false, ["message"] = "Invalid request." };

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

                var userStatsJson = DataHandler.GetUserStats(authenticatedUser.UserName);

                reply = new JsonObject
                {
                    ["success"] = true,
                    ["stats"] = userStatsJson
                };
                status = HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                reply = new JsonObject
                {
                    ["success"] = false,
                    ["message"] = $"Internal Server Error: {ex.Message}"
                };
            }

            e.Reply(status, reply.ToJsonString());
            return true;
        }
    }
}
