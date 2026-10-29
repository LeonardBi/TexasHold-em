using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TexasHoldemClient
{
    public static class GameSession
    {
        public static string CurrentUsername { get; set; } = string.Empty;

        public static bool IsLoggedIn => !string.IsNullOrEmpty(CurrentUsername);
    }
}
