using System.Collections.Generic;

namespace TexasHoldemServer.Hubs
{
    public static class SessionManager
    {
        private static readonly HashSet<string> _loggedInUsers = new HashSet<string>();

        public static bool IsUserLoggedIn(string username)
        {
            lock (_loggedInUsers) { return _loggedInUsers.Contains(username); }
        }

        public static void LoginUser(string username)
        {
            lock (_loggedInUsers) { _loggedInUsers.Add(username); }
        }

        public static void LogoutUser(string username)
        {
            lock (_loggedInUsers) { _loggedInUsers.Remove(username); }
        }
    }
}