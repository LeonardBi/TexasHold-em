using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TexasHoldemClient
{
    public class GameEngine
    {
        public Deck Deck { get; private set; }
        public List<Card> CommunityCards { get; private set; }
        public int Pot { get; set; }

        public GameEngine()
        {
            Deck = new Deck();
            CommunityCards = new List<Card>();
        }

        public void StartNewRound(Player player, Player computer)
        {
            Deck.Reset();
            CommunityCards.Clear();
            Pot = 0;

            player.ResetForNewRound();
            computer.ResetForNewRound();

            player.HoleCards.Add(Deck.DealCard()!);
            player.HoleCards.Add(Deck.DealCard()!);

            computer.HoleCards.Add(Deck.DealCard()!);
            computer.HoleCards.Add(Deck.DealCard()!);
        }

        public void DealCommunityCards(int count)
        {
            for (int i = 0; i < count; i++)
            {
                CommunityCards.Add(Deck.DealCard()!);
            }
        }

        public Player? DetermineWinner(Player player, Player computer)
        {
            player.BestHandResult = HandEvaluator.GetBestHand(player.HoleCards, CommunityCards);
            computer.BestHandResult = HandEvaluator.GetBestHand(computer.HoleCards, CommunityCards);

            int compare = player.BestHandResult.CompareTo(computer.BestHandResult);

            if (compare > 0)
            {
                player.Chips += Pot;
                return player;
            }
            else if (compare < 0)
            {
                computer.Chips += Pot;
                return computer;
            }
            else
            {
                player.Chips += Pot / 2;
                computer.Chips += Pot / 2;
                return null;
            }
        }
    }
}