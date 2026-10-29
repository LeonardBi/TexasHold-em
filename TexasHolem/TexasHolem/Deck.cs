using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TexasHoldemClient
{
    public class Deck
    {
        private List<Card> _cards = new List<Card>();
        private static readonly Random _random = new Random();

        public Deck()
        {
            Reset();
        }

        public void CreateDeck()
        {
            _cards = new List<Card>();
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                {
                    _cards.Add(new Card(rank, suit));
                }
            }
        }

        public void Shuffle()
        {
            int n = _cards.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                Card value = _cards[k];
                _cards[k] = _cards[n];
                _cards[n] = value;
            }
        }

        public Card? DealCard()
        {
            if (_cards.Count == 0) return null;

            Card dealtCard = _cards[^1];
            _cards.RemoveAt(_cards.Count - 1);
            return dealtCard;
        }

        public int CardsRemaining => _cards.Count;

        public void Reset()
        {
            CreateDeck();
            Shuffle();
        }

        public void InsertCardRandomly(Card card)
        {
            int pos = _random.Next(0, _cards.Count + 1);
            _cards.Insert(pos, card);
        }
    }
}