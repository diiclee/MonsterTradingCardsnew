using System;
using System.Text.Json.Nodes;
using Npgsql;
using NUnit.Framework;

namespace MonsterTradingCardsnew.Tests
{
    [TestFixture]
    public class Tests
    {
        private const string TestConnectionString =
            "Host=127.0.0.1;Port=5432;Database=UnitTestDB;Username=postgres;Password=dicle";

        [SetUp]
        public void Setup()
        {
            DBHandler.SetConnectionString(TestConnectionString);
            InsertTestData();
        }

        private void InsertTestData(bool includeCards = false, bool includeDeck = false)
        {
            using var connection = new NpgsqlConnection(TestConnectionString);
            connection.Open();

            // Benutzer-Daten einfügen
            string insertUsersQuery = @"
        INSERT INTO users (username, password, coins, elo, wins, losses, bio, image) 
        VALUES 
            ('TestUser1', 'hashed_password', 10, 1500, 20, 5, 'A bio', 'image1.jpg'),
            ('TestUser2', 'hashed_password', 2, 1200, 10, 3, NULL, NULL),
            ('TestUser3', 'hashed_password', 2, 1000, 9, 3, NULL, NULL)
        ON CONFLICT (username) DO NOTHING;
    ";

            using var command = new NpgsqlCommand(insertUsersQuery, connection);
            command.ExecuteNonQuery();

            // Falls Karten für Tests benötigt werden
            if (includeCards)
            {
                string insertCardsQuery = @"
            INSERT INTO cards (card_id, username, name, damage, element_type, card_type, monster_type) VALUES
            ('card1', 'TestUser1', 'Fire Dragon', 50, 'Fire', 'Monster', 'Dragon'),
            ('card2', 'TestUser1', 'Water Goblin', 30, 'Water', 'Monster', 'Goblin'),
            ('card3', 'TestUser1', 'Normal Knight', 40, 'Normal', 'Monster', 'Knight'),
            ('card4', 'TestUser1', 'Fire Spell', 25, 'Fire', 'Spell', NULL)
        ON CONFLICT (card_id) DO NOTHING;
        ";

                using var cardsCommand = new NpgsqlCommand(insertCardsQuery, connection);
                cardsCommand.ExecuteNonQuery();
            }

            // Falls Deck-Daten benötigt werden
            if (includeDeck)
            {
                string insertDeckQuery = @"
            INSERT INTO deck (username, card1_id, card2_id, card3_id, card4_id)
            VALUES ('TestUser1', 'card1', 'card2', 'card3', 'card4')
            ON CONFLICT (username) DO UPDATE
            SET card1_id = 'card1', card2_id = 'card2', card3_id = 'card3', card4_id = 'card4';
        ";

                using var deckCommand = new NpgsqlCommand(insertDeckQuery, connection);
                deckCommand.ExecuteNonQuery();
            }
        }


        [TearDown]
        public void Cleanup()
        {
            using var connection = new NpgsqlConnection(TestConnectionString);
            connection.Open();

            // Temporär alle Constraints deaktivieren
            using var disableConstraints = new NpgsqlCommand("SET CONSTRAINTS ALL DEFERRED;", connection);
            disableConstraints.ExecuteNonQuery();

            using var command = new NpgsqlCommand(@"
        DELETE FROM packages;
        DELETE FROM cards;
        DELETE FROM users WHERE username IN ('TestUser1', 'TestUser2', 'NewUser', 'TestUser3');
    ", connection);
            command.ExecuteNonQuery();
        }


        // ✅ Test für DataHandler
        [Test]
        public void GetScoreboard_ShouldReturnUsersOrderedByElo()
        {
            var result = DataHandler.GetScoreboard();
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result[0]["username"].ToString(), Is.EqualTo("TestUser1"));
            Assert.That(result[1]["username"].ToString(), Is.EqualTo("TestUser2"));
            Assert.That(result[2]["username"].ToString(), Is.EqualTo("TestUser3"));
        }

        [Test]
        public void GetUserStats_ShouldReturnCorrectUser()
        {
            var result = DataHandler.GetUserStats("TestUser1");
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0]["username"].ToString(), Is.EqualTo("TestUser1"));
            Assert.That(result[0]["elo"].ToString(), Is.EqualTo("1500"));
            Assert.That(result[0]["wins"].ToString(), Is.EqualTo("20"));
            Assert.That(result[0]["losses"].ToString(), Is.EqualTo("5"));
        }

        // ✅ Test für PackagesDataHandler
        [Test]
        public void CreatePackage_ShouldInsertPackage_WhenValidDataProvided()
        {
            JsonArray cardsArray = new JsonArray
            {
                new JsonObject { ["Id"] = "card1", ["Name"] = "Fire Goblin", ["Damage"] = "50" },
                new JsonObject { ["Id"] = "card2", ["Name"] = "Water Dragon", ["Damage"] = "80" },
                new JsonObject { ["Id"] = "card3", ["Name"] = "Ork", ["Damage"] = "60" },
                new JsonObject { ["Id"] = "card4", ["Name"] = "Fire Spell", ["Damage"] = "40" },
                new JsonObject { ["Id"] = "card5", ["Name"] = "Normal Knight", ["Damage"] = "70" }
            };

            bool result = PackagesDataHandler.CreatePackage(cardsArray, out string message);
            Assert.That(result, Is.True);
            Assert.That(message, Is.Empty);
        }

        [Test]
        public void BuyPackage_ShouldAllowPurchase_WhenUserHasEnoughCoins()
        {
            // Erst ein Paket erstellen
            JsonArray cardsArray = new JsonArray
            {
                new JsonObject { ["Id"] = "card1", ["Name"] = "Fire Goblin", ["Damage"] = "50" },
                new JsonObject { ["Id"] = "card2", ["Name"] = "Water Dragon", ["Damage"] = "80" },
                new JsonObject { ["Id"] = "card3", ["Name"] = "Ork", ["Damage"] = "60" },
                new JsonObject { ["Id"] = "card4", ["Name"] = "Fire Spell", ["Damage"] = "40" },
                new JsonObject { ["Id"] = "card5", ["Name"] = "Normal Knight", ["Damage"] = "70" }
            };
            bool packageCreated = PackagesDataHandler.CreatePackage(cardsArray, out string createMessage);

            Assert.That(packageCreated, Is.True, $"CreatePackage failed: {createMessage}");

            // Teste Kauf
            bool result = PackagesDataHandler.BuyPackage("TestUser1", out string message);

            Assert.That(result, Is.True, $"BuyPackage failed: {message}");
        }


        [Test]
        public void BuyPackage_ShouldFail_WhenUserHasNotEnoughCoins()
        {
            JsonArray cardsArray = new JsonArray
            {
                new JsonObject { ["Id"] = "card1", ["Name"] = "Fire Goblin", ["Damage"] = "50" },
                new JsonObject { ["Id"] = "card2", ["Name"] = "Water Dragon", ["Damage"] = "80" },
                new JsonObject { ["Id"] = "card3", ["Name"] = "Ork", ["Damage"] = "60" },
                new JsonObject { ["Id"] = "card4", ["Name"] = "Fire Spell", ["Damage"] = "40" },
                new JsonObject { ["Id"] = "card5", ["Name"] = "Normal Knight", ["Damage"] = "70" }
            };
            PackagesDataHandler.CreatePackage(cardsArray, out _);

            bool result = PackagesDataHandler.BuyPackage("TestUser2", out string message);
            Assert.That(result, Is.False);
            Assert.That(message, Is.EqualTo("Not enough coins to buy a package."));
        }

        [Test]
        public void StartBattle_ShouldDeclareWinner_WhenDecksAreUnequal()
        {
            var deck1 = new List<Card>
            {
                new ("Fire Dragon", 50, Element.Fire, "Monster",
                    "Dragon"),
                new ("Water Goblin", 30, Element.Water, "Monster",
                    "Goblin")
            };

            var deck2 = new List<Card>
            {
                new ("Normal Knight", 20, Element.Normal, "Monster",
                    "Knight"),
                new ("Fire Spell", 40, Element.Fire, "Spell")
            };


            var result = BattleLogic.StartBattle("TestUser1", "TestUser2", deck1, deck2);

            Assert.That(result, Is.Not.Empty);
            Assert.That(result[^1], Does.Contain("gewinnt!"));
        }
        
        [Test]
        public void StartBattle_ShouldReturnError_WhenDeckIsEmpty()
        {
            var deck1 = new List<MonsterTradingCardsnew.Card>();
            var deck2 = new List<MonsterTradingCardsnew.Card>
            {
                new MonsterTradingCardsnew.Card("Water Dragon", 50, MonsterTradingCardsnew.Element.Water, "Monster",
                    "Dragon")
            };

            var result = BattleLogic.StartBattle("TestUser1", "TestUser2", deck1, deck2);

            Assert.That(result, Is.Not.Empty);
            Assert.That(result[^1], Is.EqualTo("Einer der Spieler hat kein vollständiges Deck."));
        }

        [Test]
        public void HashPassword_ShouldReturnNonEmptyHash()
        {
            // Act
            string hash = PasswordHelper.HashPassword("securepassword");

            // Assert
            Assert.That(hash, Is.Not.Null.And.Not.Empty);
        }
        
        [Test]
        public void HashPassword_DifferentInputs_ShouldProduceDifferentHashes()
        {
            // Act
            string hash1 = PasswordHelper.HashPassword("password123");
            string hash2 = PasswordHelper.HashPassword("differentpassword");

            // Assert
            Assert.That(hash1, Is.Not.EqualTo(hash2));
        }

        [Test]
        public void VerifyPassword_CorrectPassword_ShouldReturnTrue()
        {
            // Arrange
            string password = "correctpassword";
            string hash = PasswordHelper.HashPassword(password);

            // Act
            bool result = PasswordHelper.VerifyPassword(password, hash);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void VerifyPassword_IncorrectPassword_ShouldReturnFalse()
        {
            // Arrange
            string hash = PasswordHelper.HashPassword("originalpassword");

            // Act
            bool result = PasswordHelper.VerifyPassword("wrongpassword", hash);

            // Assert
            Assert.That(result, Is.False);
        }


        // ✅ Test für SetDeck() (Erfolgreiches Setzen)
        [Test]
        public void SetDeck_ShouldSucceed_WhenFourValidCardsAreGiven()
        {
            InsertTestData(includeCards: true);
            // Arrange
            List<string> cardIds = new List<string> { "card1", "card2", "card3", "card4" };

            // Act
            bool result = Card.SetDeck("TestUser1", cardIds, out string message);

            // Assert
            Assert.That(result, Is.True, $"SetDeck failed: {message}");
        }

        // ✅ Test für SetDeck() (Fehlschlag mit falscher Anzahl an Karten)
        [Test]
        public void SetDeck_ShouldFail_WhenNotFourCardsAreGiven()
        {
            // Arrange
            List<string> cardIds = new List<string> { "card1", "card2" };

            // Act
            bool result = Card.SetDeck("TestUser1", cardIds, out string message);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(message, Is.EqualTo("Some cards do not belong to the user or are invalid."));
        }


        // ✅ Test für GetDeckByUsername() (Leeres Deck)
        [Test]
        public void GetDeckByUsername_ShouldReturnEmptyList_WhenNoDeckIsSet()
        {
            // Act
            var result = Card.GetDeckByUsername("TestUser3");

            // Assert
            Assert.That(result, Is.Empty, "Erwartet wurde ein leeres Deck.");
        }

        [Test]
        public void UserExists_ShouldReturnTrue_WhenUserExists()
        {
            bool result = DBHandler.UserExists("TestUser1");
            Assert.That(result, Is.True);
        }

        [Test]
        public void UserExists_ShouldReturnFalse_WhenUserDoesNotExist()
        {
            bool result = DBHandler.UserExists("UnknownUser");
            Assert.That(result, Is.False);
        }

        [Test]
        public void GetUser_ShouldReturnUser_WhenUserExists()
        {
            var user = DBHandler.GetUser("TestUser1");
            Assert.That(user, Is.Not.Null);
            Assert.That(user.UserName, Is.EqualTo("TestUser1"));
            Assert.That(user.Password, Is.EqualTo("hashed_password"));
        }

        [Test]
        public void GetUser_ShouldReturnNull_WhenUserDoesNotExist()
        {
            var user = DBHandler.GetUser("UnknownUser");
            Assert.That(user, Is.Null);
        }

        [Test]
        public void CreateUser_ShouldInsertNewUser()
        {
            DBHandler.CreateUser("NewUser", "secure_hash");

            bool userExists = DBHandler.UserExists("NewUser");
            Assert.That(userExists, Is.True);

            var user = DBHandler.GetUser("NewUser");
            Assert.That(user, Is.Not.Null);
            Assert.That(user.UserName, Is.EqualTo("NewUser"));
            Assert.That(user.Password, Is.EqualTo("secure_hash"));
        }
        
        [Test]
        public void GetUserCards_ShouldReturnEmptyList_WhenUserHasNoCards()
        {
            // Act: Abrufen der Karten eines Benutzers ohne Karten
            var result = Card.GetUserCards("TestUser3"); // TestUser3 hat keine Karten

            // Assert: Die Liste sollte leer sein
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(0));
        }


    }
}