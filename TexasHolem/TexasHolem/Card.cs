using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TexasHoldemClient
{
    public enum Suit { Clubs, Diamonds, Hearts, Spades }

    public enum Rank
    {
        Two = 2, Three = 3, Four = 4, Five = 5, Six = 6, Seven = 7, Eight = 8, Nine = 9,
        Ten = 10, Jack = 11, Queen = 12, King = 13, Ace = 14
    }

    public class Card
    {
        public Rank Rank { get; }
        public Suit Suit { get; }

        public Card(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        public string CardName => $"{Rank} of {Suit}";

        public string ImageFileName
        {
            get
            {
                string rankStr = (int)Rank <= 10 ? ((int)Rank).ToString() : Rank.ToString().ToLower();
                return $"{rankStr}_of_{Suit.ToString().ToLower()}.png";
            }
        }

        public int CardId => ((int)Suit + 1) * 100 + (int)Rank;
    }
}