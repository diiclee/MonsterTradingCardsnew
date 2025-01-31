using System;
using System.Text.Json.Nodes;
using MonsterTradingCardsnew.Exceptions;
using Npgsql;

namespace MonsterTradingCardsnew
{
    public class UserHandler : Handler, IHandler
    {
        public override bool Handle(HttpSvrEventArgs e)
        {
            if ((e.Path.TrimEnd('/', ' ', '\t') == "/users") && (e.Method == "POST"))
            {
                return _CreateUser(e);
            }
            else if (e.Path.StartsWith("/users/") && (e.Method == "GET"))
            {
                return _GetUserFromDB(e);
            }
            else if ((e.Path.TrimEnd('/', ' ', '\t') == "/sessions") && (e.Method == "POST"))
            {
                return _LoginUser(e);
            }
            else if (e.Path.StartsWith("/users/") && (e.Method == "PUT"))
            {
                return _UpdateUser(e);
            }

            return false;
        }

        private static bool _CreateUser(HttpSvrEventArgs e)
        {
            JsonObject? reply = new JsonObject() { ["success"] = false, ["message"] = "Invalid request." };
            int status = HttpStatusCode.BAD_REQUEST;

            try
            {
                JsonNode? json = JsonNode.Parse(e.Payload);
                if (json != null)
                {
                    string userName = (string)json["Username"]!;
                    string password = (string)json["Password"]!;

                    if (DBHandler.UserExists(userName))
                    {
                        reply = new JsonObject() { ["success"] = false, ["message"] = "User name already exists." };
                        status = HttpStatusCode.BAD_REQUEST;
                    }
                    else
                    {
                        string hashedPassword = PasswordHelper.HashPassword(password);

                        DBHandler.CreateUser(userName, hashedPassword);

                        status = HttpStatusCode.OK;
                        reply = new JsonObject() { ["success"] = true, ["message"] = "User created." };
                    }
                }
            }
            catch (UserException ex)
            {
                reply = new JsonObject() { ["success"] = false, ["message"] = ex.Message };
            }
            catch (Exception)
            {
                reply = new JsonObject() { ["success"] = false, ["message"] = "Unexpected error." };
            }

            e.Reply(status, reply?.ToJsonString());
            return true;
        }

        private static bool _QueryUser(HttpSvrEventArgs e)
        {
            JsonObject? reply = new JsonObject() { ["success"] = false, ["message"] = "Invalid request." };
            int status = HttpStatusCode.BAD_REQUEST;

            try
            {
                (bool Success, User? User) ses = Token.Authenticate(e);

                if (ses.Success)
                {
                    User? user = User.Get(e.Path[7..]);
                    if (user == null)
                    {
                        status = HttpStatusCode.NOT_FOUND;
                        reply = new JsonObject() { ["success"] = false, ["message"] = "User not found." };
                    }
                    else
                    {
                        status = HttpStatusCode.OK;
                        reply = new JsonObject() { ["success"] = true, ["username"] = user!.UserName };
                    }
                }
                else
                {
                    status = HttpStatusCode.UNAUTHORIZED;
                    reply = new JsonObject() { ["success"] = false, ["message"] = "Unauthorized." };
                }
            }
            catch (Exception)
            {
                reply = new JsonObject() { ["success"] = false, ["message"] = "Unexpected error." };
            }

            e.Reply(status, reply?.ToJsonString());
            return true;
        }

        private static bool _LoginUser(HttpSvrEventArgs e)
        {
            JsonObject? reply = new JsonObject() { ["success"] = false, ["message"] = "Invalid request." };
            int status = HttpStatusCode.BAD_REQUEST;

            try
            {
                JsonNode? json = JsonNode.Parse(e.Payload);
                if (json != null)
                {
                    string userName = (string)json["Username"]!;
                    string enteredPassword = (string)json["Password"]!;

                    var user = DBHandler.GetUser(userName);
                    if (user != null)
                    {
                        string storedHash = user.Password;

                        bool isPasswordValid = PasswordHelper.VerifyPassword(enteredPassword, storedHash);
                        if (isPasswordValid)
                        {
                            string token = Token._CreateTokenFor(user);

                            status = HttpStatusCode.OK;
                            reply = new JsonObject()
                            {
                                ["success"] = true,
                                ["message"] = "Login successful.",
                                ["token"] = token
                            };
                        }
                        else
                        {
                            status = HttpStatusCode.UNAUTHORIZED;
                            reply = new JsonObject() { ["success"] = false, ["message"] = "Invalid credentials." };
                        }
                    }
                    else
                    {
                        status = HttpStatusCode.NOT_FOUND;
                        reply = new JsonObject() { ["success"] = false, ["message"] = "User not found." };
                    }
                }
            }
            catch (Exception ex)
            {
                reply = new JsonObject() { ["success"] = false, ["message"] = "Unexpected error: " + ex.Message };
            }

            e.Reply(status, reply?.ToJsonString());
            return true;
        }

        private static bool _GetUserFromDB(HttpSvrEventArgs e)
        {
            JsonObject? reply = new JsonObject() { ["success"] = false, ["message"] = "Invalid request." };
            int status = HttpStatusCode.BAD_REQUEST;

            try
            {
                (bool Success, User? AuthUser) = Token.Authenticate(e);

                if (Success)
                {
                    string requestedUser = e.Path[7..]; //username aus dem path
                    using var connection = new NpgsqlConnection(DBHandler.ConnectionString);
                    connection.Open();

                    string query =
                        "SELECT username, coins, elo, name_choice, bio, image FROM users WHERE username = @Username";
                    using var command = new NpgsqlCommand(query, connection);
                    command.Parameters.AddWithValue("@Username", requestedUser);

                    using var reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        status = HttpStatusCode.OK;
                        reply = new JsonObject()
                        {
                            ["success"] = true,
                            ["Username"] = reader["username"].ToString(),
                            ["Coins"] = Convert.ToInt32(reader["coins"]),
                            ["Elo"] = Convert.ToInt32(reader["elo"]),
                            ["Name"] = reader["name_choice"].ToString(),
                            ["Bio"] = reader["bio"].ToString(),
                            ["Image"] = reader["image"].ToString(),
                        };
                    }
                    else
                    {
                        status = HttpStatusCode.NOT_FOUND;
                        reply = new JsonObject() { ["success"] = false, ["message"] = "User not found." };
                    }
                }
                else
                {
                    status = HttpStatusCode.UNAUTHORIZED;
                    reply = new JsonObject() { ["success"] = false, ["message"] = "Unauthorized." };
                }
            }
            catch (Exception ex)
            {
                reply = new JsonObject() { ["success"] = false, ["message"] = "Unexpected error: " + ex.Message };
            }

            e.Reply(status, reply?.ToJsonString());
            return true;
        }

        private static bool _UpdateUser(HttpSvrEventArgs e)
        {
            JsonObject? reply = new JsonObject() { ["success"] = false, ["message"] = "Invalid request." };
            int status = HttpStatusCode.BAD_REQUEST;

            try
            {
                (bool Success, User? AuthUser) = Token.Authenticate(e);

                if (Success)
                {
                    string requestedUser = e.Path[7..]; // username aus dem path

                    // Prüfen, ob der authentifizierte Nutzer seinen eigenen Account bearbeitet
                    if (AuthUser == null || AuthUser.UserName != requestedUser)
                    {
                        status = HttpStatusCode.UNAUTHORIZED;
                        reply = new JsonObject()
                            { ["success"] = false, ["message"] = "You can only update your own profile." };
                    }
                    else
                    {
                        JsonNode? json = JsonNode.Parse(e.Payload);
                        if (json != null)
                        {
                            string? name = json["Name"]?.ToString();
                            string? bio = json["Bio"]?.ToString();
                            string? image = json["Image"]?.ToString();

                            bool updated = DBHandler.UpdateUser(requestedUser, name, bio, image);

                            if (updated)
                            {
                                status = HttpStatusCode.OK;
                                reply = new JsonObject()
                                    { ["success"] = true, ["message"] = "User updated successfully." };
                            }
                            else
                            {
                                status = HttpStatusCode.BAD_REQUEST;
                                reply = new JsonObject() { ["success"] = false, ["message"] = "User update failed." };
                            }
                        }
                    }
                }
                else
                {
                    status = HttpStatusCode.UNAUTHORIZED;
                    reply = new JsonObject() { ["success"] = false, ["message"] = "Unauthorized." };
                }
            }
            catch (Exception ex)
            {
                reply = new JsonObject() { ["success"] = false, ["message"] = "Unexpected error: " + ex.Message };
            }

            e.Reply(status, reply?.ToJsonString());
            return true;
        }
    }
}