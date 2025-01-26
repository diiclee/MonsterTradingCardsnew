using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using MonsterTradingCardsnew.Exceptions;
using Npgsql;
    
namespace MonsterTradingCardsnew;

public class PackagesHandler : Handler
{
    public override bool Handle(HttpSvrEventArgs e)
    {
        if ((e.Path.TrimEnd('/', ' ', '\t') == "/packages") && (e.Method == "POST"))
        {   // POST /users wird zur Benutzererstellung verwendet
            return _CreatePackage(e);
        }
        /*else if ((e.Path.TrimEnd('/', ' ', '\t') == "/buy-package") && (e.Method == "POST"))
        {   // POST /login wird zur Benutzeranmeldung verwendet
            return _BuyPackage(e);
        }*/

        return false;
    }

    private bool _CreatePackage(HttpSvrEventArgs e)
    {
            JsonObject? reply = new JsonObject() { ["success"] = false, ["message"] = "Invalid request." };
            int status = HttpStatusCode.BAD_REQUEST;  // initialisiere Antwort

            try
            {
                using var connection = new NpgsqlConnection(DBHandler.ConnectionString);
                connection.Open();
                
                List<(int, Card)> cards = new List<(int, Card)>();
                
                string query = "SELECT * FROM cards";
                using var cmd = new NpgsqlCommand(query, connection);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Card card = new Card(reader.GetString(1),
                            reader.GetInt32(2),
                            Enum.Parse<Element>(reader.GetString(3)),
                            reader.GetString(4));
                        cards.Add((reader.GetInt32(0), card));
                    }
                }
                

                List<int> packages = new List<int>();
                Random rnd = new Random();
                while (cards.Count > 0)
                {
                    if (packages.Count < 5)
                    {
                        int cardNumbers = rnd.Next(cards.Count); // Zufälligen Index auswählen
                        packages.Add(cards[cardNumbers].Item1); // Karten-ID hinzufügen
                        cards.RemoveAt(cardNumbers); // Karte aus der Liste entfernen
                    }

                    if (packages.Count == 5 || (cards.Count == 0 && packages.Count > 0))
                    {
                        // Paket in der Datenbank erstellen
                        string query2 =
                            "INSERT INTO packages (card_id1, card_id2, card_id3, card_id4, card_id5) VALUES (@card_id1, @card_id2, @card_id3, @card_id4, @card_id5)";
                        using var command = new NpgsqlCommand(query2, connection);
                        for (int i = 0; i < packages.Count; i++)
                        {
                            command.Parameters.AddWithValue("@card_id" + (i + 1), packages[i]);
                        }

                        // Fehlende Karten im Paket mit NULL auffüllen, falls nötig
                        for (int i = packages.Count + 1; i <= 5; i++)
                        {
                            command.Parameters.AddWithValue("@card_id" + i, DBNull.Value);
                        }

                        command.ExecuteNonQuery();
                        packages.Clear(); // Liste leeren
                    }
                    
                }
                status = HttpStatusCode.OK;
                reply = new JsonObject() { ["success"] = true, ["message"] = "Package created." };
            
            }
            catch (UserException ex)
            {
                reply = new JsonObject() { ["success"] = false, ["message"] = ex.Message };
            }
            catch (Exception ex)
            {
                reply = new JsonObject() { ["success"] = false, ["message"] = ex.Message };
            }

            e.Reply(status, reply?.ToJsonString());  // Antwort senden
            return true;
        }
}