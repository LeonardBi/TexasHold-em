namespace TexasHoldemServer.Models
{
    public class User
    {
        public int UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string SecurityQ1 { get; set; } = string.Empty;
        public string SecurityA1 { get; set; } = string.Empty;
        public string SecurityQ2 { get; set; } = string.Empty;
        public string SecurityA2 { get; set; } = string.Empty;

        public int Chips { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}