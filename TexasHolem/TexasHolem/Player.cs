using System.Collections.Generic;

namespace TexasHoldemClient
{
    public class Player
    {
        public string Name { get; set; }
        public int Chips { get; set; }

        public List<Card> HoleCards { get; set; }
        public HandResult? BestHandResult { get; set; }

        public bool IsSittingOut { get; set; } = false;

        public bool IsSeatEmpty { get; set; } = false;

        public int CurrentHandInvestment { get; set; } = 0;

        public Player(string name, int startingChips)
        {
            Name = name;
            Chips = startingChips;
            HoleCards = new List<Card>();
        }

        public void ResetForNewRound()
        {
            HoleCards.Clear();
            BestHandResult = null;
            CurrentHandInvestment = 0;
        }
    }
}