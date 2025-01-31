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

        //Methode für Testdaten
        private void InsertTestData(bool includeCards = false, bool includeDeck = false)
        {
            using var connection = new NpgsqlConnection(TestConnectionString);
            connection.Open();

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

            using var disableConstraints = new NpgsqlCommand("SET CONSTRAINTS ALL DEFERRED;", connection);
            disableConstraints.ExecuteNonQuery();

            using var command = new NpgsqlCommand(@"
        DELETE FROM packages;
        DELETE FROM cards;
        DELETE FROM users WHERE username IN ('TestUser1', 'TestUser2', 'NewUser', 'TestUser3');
    ", connection);
            command.ExecuteNonQuery();
        }


        [Test]
        public void GetScoreboard_UsersOrderedByElo()
        {
            var result = DataHandler.GetScoreboard();
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result[0]["username"].ToString(), Is.EqualTo("TestUser1"));
            Assert.That(result[1]["username"].ToString(), Is.EqualTo("TestUser2"));
            Assert.That(result[2]["username"].ToString(), Is.EqualTo("TestUser3"));
        }

        //should return correct user
        [Test]
        public void GetUserStats()
        {
            var result = DataHandler.GetUserStats("TestUser1");
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0]["username"].ToString(), Is.EqualTo("TestUser1"));
            Assert.That(result[0]["elo"].ToString(), Is.EqualTo("1500"));
            Assert.That(result[0]["wins"].ToString(), Is.EqualTo("20"));
            Assert.That(result[0]["losses"].ToString(), Is.EqualTo("5"));
        }

        //should insert package when valid data is provided
        [Test]
        public void CreatePackage()
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

        //should allow transaction when user has enough coins
        [Test]
        public void BuyPackage()
        {
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

            bool result = PackagesDataHandler.BuyPackage("TestUser1", out string message);

            Assert.That(result, Is.True, $"BuyPackage failed: {message}");
        }

        //should fail when user has not enough coins
        [Test]
        public void BuyPackage_Fail()
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

        //there should be a winner when decks are unequal
        [Test]
        public void StartBattle_UnequalDecks()
        {
            var deck1 = new List<Card>
            {
                new("Fire Dragon", 50, Element.Fire, "Monster",
                    "Dragon"),
                new("Water Goblin", 30, Element.Water, "Monster",
                    "Goblin")
            };

            var deck2 = new List<Card>
            {
                new("Normal Knight", 20, Element.Normal, "Monster",
                    "Knight"),
                new("Fire Spell", 40, Element.Fire, "Spell")
            };


            var result = BattleLogic.StartBattle("TestUser1", "TestUser2", deck1, deck2);

            Assert.That(result, Is.Not.Empty);
            Assert.That(result[^1], Does.Contain("gewinnt!"));
        }

        //should fail when decks are empty
        [Test]
        public void StartBattle_EmptyDeck()
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

        //hashing should be successful
        [Test]
        public void HashPassword_NotEmptyHash()
        {
            string hash = PasswordHelper.HashPassword("securepassword");

            Assert.That(hash, Is.Not.Null.And.Not.Empty);
        }

        //different passwords -> different hashes
        [Test]
        public void HashPassword_DifferentHashes()
        {
            string hash1 = PasswordHelper.HashPassword("password123");
            string hash2 = PasswordHelper.HashPassword("differentpassword");

            Assert.That(hash1, Is.Not.EqualTo(hash2));
        }

        [Test]
        public void VerifyPassword_CorrectPassword()
        {
            string password = "correctpassword";
            string hash = PasswordHelper.HashPassword(password);

            bool result = PasswordHelper.VerifyPassword(password, hash);

            Assert.That(result, Is.True);
        }

        [Test]
        public void VerifyPassword_IncorrectPassword()
        {
            string hash = PasswordHelper.HashPassword("originalpassword");

            bool result = PasswordHelper.VerifyPassword("wrongpassword", hash);

            Assert.That(result, Is.False);
        }

        //four valid cards
        [Test]
        public void SetDeck_Success()
        {
            InsertTestData(includeCards: true);
            List<string> cardIds = new List<string> { "card1", "card2", "card3", "card4" };

            bool result = Card.SetDeck("TestUser1", cardIds, out string message);

            Assert.That(result, Is.True, $"SetDeck failed: {message}");
        }

        //only two cards for deck
        [Test]
        public void SetDeck_Fail()
        {
            List<string> cardIds = new List<string> { "card1", "card2" };

            bool result = Card.SetDeck("TestUser1", cardIds, out string message);

            Assert.That(result, Is.False);
            Assert.That(message, Is.EqualTo("Some cards do not belong to the user or are invalid."));
        }

        //when no deck is set
        [Test]
        public void GetDeckByUsername_EmptyList()
        {
            var result = Card.GetDeckByUsername("TestUser3");

            Assert.That(result, Is.Empty, "Erwartet wurde ein leeres Deck.");
        }

        [Test]
        public void UserExists_True()
        {
            bool result = DBHandler.UserExists("TestUser1");
            Assert.That(result, Is.True);
        }

        [Test]
        public void UserExists_False()
        {
            bool result = DBHandler.UserExists("UnknownUser");
            Assert.That(result, Is.False);
        }

        [Test]
        public void GetUser_WhenUserExists()
        {
            var user = DBHandler.GetUser("TestUser1");
            Assert.That(user, Is.Not.Null);
            Assert.That(user.UserName, Is.EqualTo("TestUser1"));
            Assert.That(user.Password, Is.EqualTo("hashed_password"));
        }

        [Test]
        public void GetUser_WhenUserDoesNotExist()
        {
            var user = DBHandler.GetUser("UnknownUser");
            Assert.That(user, Is.Null);
        }

        [Test]
        public void CreateUser_NewUser()
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
        public void GetUserCards_NoCards()
        {
            var result = Card.GetUserCards("TestUser3");

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(0));
        }
    }
}