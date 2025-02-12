using System;
using System.Security;
using System.Security.Authentication;
using MonsterTradingCardsnew.Exceptions;


namespace MonsterTradingCardsnew
{
    /// <summary>This class represents a user.</summary>
    public sealed class User
    {
        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // private static members                                                                                           //
        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>Currently holds the system users.</summary>
        /// <remarks>Is to be removed by database implementation later.</remarks>
        private static Dictionary<string, User> _Users = new();


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // constructors                                                                                                     //
        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>Creates a new instance of this class.</summary>
        public User()
        {
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // public properties                                                                                                //
        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>Gets the user name.</summary>
        public string UserName { get; set; }

        public string Password { get; set; } 

        public List<ICard> Deck { get; private set; } 

        public List<ICard> Stack { get; private set; }

        public int Coins { get; set; }

        public int Elo { get; set; }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // public methods                                                                                                   //
        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>Saves changes to the user object.</summary>
        /// <param name="token">Token of the session trying to modify the object.</param>
        /// <exception cref="SecurityException">Thrown in case of an unauthorized attempt to modify data.</exception>
        /// <exception cref="AuthenticationException">Thrown when the token is invalid.</exception>
        public void Save(string token)
        {
            (bool Success, User? User) auth = Token.Authenticate(token);
            if (auth.Success)
            {
                if (auth.User!.UserName != UserName)
                {
                    throw new SecurityException("Trying to change other user's data.");
                }
                // Save data.
            }
            else
            {
                new AuthenticationException("Not authenticated.");
            }
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // public static methods                                                                                            //
        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>Creates a user.</summary>
        /// <param name="userName">User name.</param>
        /// <param name="password">Password.</param>
        /// <param name="fullName">Full name.</param>
        /// <param name="eMail">E-mail addresss.</param>
        /// <exception cref="UserException">Thrown when the user name already exists.</exception>
        /* public static void Create(string userName, string password)
         {
             if(_Users.ContainsKey(userName))
             {
                 throw new UserException("User name already exists.");
             }

             User user = new()
             {
                 UserName = userName,
                 Password = password,
                 Deck = new List<ICard>(),
                 Stack = new List<ICard>(),
                 Coins = 20,
                 Elo = 100,
             };

             _Users.Add(user.UserName, user);
         }*/

        /// <summary>Gets a user by user name.</summary>
        /// <param name="userName">User name.</param>
        /// <returns>Return a user object if the user was found, otherwise returns NULL.</returns>
        public static User? Get(string userName)
        {
            _Users.TryGetValue(userName, out User? user);
            return user;
        }


        /// <summary>Performs a user logon.</summary>
        /// <param name="userName">User name.</param>
        /// <param name="password">Password.</param>
        /// <returns>Returns a tuple of success flag and token.
        ///          If successful, the success flag is TRUE and the token contains a token string,
        ///          otherwise success flag is FALSE and token is empty.</returns>
        public static (bool Success, string Token) Logon(string userName, string password)
        {
            if (_Users.ContainsKey(userName) &&
                ValidatePassword(userName, password)) //check if right user with right password  
            {
                return (true, Token._CreateTokenFor(_Users[userName]));
            }

            return (false, string.Empty);
        }

        public static bool ValidatePassword(string userName, string password)
        {
            if (_Users.ContainsKey(userName) && _Users[userName].Password == password)
            {
                return true;
            }

            return false;
        }
    }
}