using System;
using System.Text.Json.Nodes;
using MonsterTradingCardsnew.Exceptions;

namespace MonsterTradingCardsnew
{
    /// <summary>This class implements a handler for user-specific requests.</summary>
    public class UserHandler : Handler, IHandler
    {
        public override bool Handle(HttpSvrEventArgs e)
        {
            if ((e.Path.TrimEnd('/', ' ', '\t') == "/users") && (e.Method == "POST"))
            {   // POST /users wird zur Benutzererstellung verwendet
                return _CreateUser(e);
            }
            else if (e.Path.StartsWith("/users/") && (e.Method == "GET"))  // GET /users/UserName gibt Benutzerdaten zurück
            {
                return _QueryUser(e);
            }
            else if ((e.Path.TrimEnd('/', ' ', '\t') == "/sessions") && (e.Method == "POST"))
            {   // POST /login wird zur Benutzeranmeldung verwendet
                return _LoginUser(e);
            }

            return false;
        }

        // Benutzer erstellen
        private static bool _CreateUser(HttpSvrEventArgs e)
        {
            JsonObject? reply = new JsonObject() { ["success"] = false, ["message"] = "Invalid request." };
            int status = HttpStatusCode.BAD_REQUEST;  // initialisiere Antwort

            try
            {
                JsonNode? json = JsonNode.Parse(e.Payload);  // Payload parsen
                if (json != null)
                {
                    string userName = (string)json["Username"]!;
                    string password = (string)json["Password"]!;

                    // Überprüfen, ob der Benutzer bereits existiert
                    if (DBHandler.UserExists(userName))
                    {
                        reply = new JsonObject() { ["success"] = false, ["message"] = "User name already exists." };
                        status = HttpStatusCode.BAD_REQUEST;
                    }
                    else
                    {
                        // Passwort hashen (ohne Salt)
                        string hashedPassword = PasswordHelper.HashPassword(password);

                        // Benutzer erstellen und in der DB speichern
                        //User.Create(userName, hashedPassword);  // Benutzer in UserHandler erstellen
                        DBHandler.CreateUser(userName, hashedPassword);  // Benutzer in der DB speichern

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

            e.Reply(status, reply?.ToJsonString());  // Antwort senden
            return true;
        }


        // Benutzerabfrage
        private static bool _QueryUser(HttpSvrEventArgs e)
        {
            JsonObject? reply = new JsonObject() { ["success"] = false, ["message"] = "Invalid request." };
            int status = HttpStatusCode.BAD_REQUEST;

            try
            {
                (bool Success, User? User) ses = Token.Authenticate(e); // Authentifizierung des Nutzers

                if (ses.Success)
                {   // Erfolgreiche Authentifizierung
                    User? user = User.Get(e.Path[7..]);  // Benutzer anhand des Pfades abfragen
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

            e.Reply(status, reply?.ToJsonString());  // Antwort senden
            return true;
        }

        // Benutzeranmeldung
        private static bool _LoginUser(HttpSvrEventArgs e)
        {
            JsonObject? reply = new JsonObject() { ["success"] = false, ["message"] = "Invalid request." };
            int status = HttpStatusCode.BAD_REQUEST;  // initialisiere Antwort

            try
            {
                JsonNode? json = JsonNode.Parse(e.Payload);  // Payload parsen
                if (json != null)
                {
                    string userName = (string)json["Username"]!;
                    string enteredPassword = (string)json["Password"]!;

                    // Benutzer aus der DB abrufen
                    var user = DBHandler.GetUser(userName);  
                    if (user != null)
                    {
                        string storedHash = user.Password;  // Den gespeicherten Hash des Benutzers abrufen

                        // Passwort validieren
                        bool isPasswordValid = PasswordHelper.VerifyPassword(enteredPassword, storedHash);
                        if (isPasswordValid)
                        {
                            // Erfolgreiche Anmeldung
                            status = HttpStatusCode.OK;
                            reply = new JsonObject() { ["success"] = true, ["message"] = "Login successful." };
                        }
                        else
                        {
                            // Ungültiges Passwort
                            status = HttpStatusCode.UNAUTHORIZED;
                            reply = new JsonObject() { ["success"] = false, ["message"] = "Invalid credentials." };
                        }
                    }
                    else
                    {
                        // Benutzer nicht gefunden
                        status = HttpStatusCode.NOT_FOUND;
                        reply = new JsonObject() { ["success"] = false, ["message"] = "User not found." };
                    }
                }
            }
            catch (Exception ex)
            {
                reply = new JsonObject() { ["success"] = false, ["message"] = "Unexpected error." };
                Console.WriteLine($"Fehler bei der Anmeldung: {ex.Message}");
            }

            e.Reply(status, reply?.ToJsonString());  // Antwort senden
            return true;
        }
    }
}
