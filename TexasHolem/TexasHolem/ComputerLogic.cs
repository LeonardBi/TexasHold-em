using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TexasHoldemClient
{
    public static class ComputerLogic
    {
        public static void DecideRiverSwap(Player computer, List<Card> communityCards, Deck deck)
        {
            HandResult currentBest = HandEvaluator.GetBestHand(computer.HoleCards, communityCards);

            if (currentBest.Type == HandType.HighCard ||
               (currentBest.Type == HandType.OnePair && currentBest.Kickers[0] < Rank.Ten))
            {
                Card cardToKeep;
                Card cardToDrop;

                if (computer.HoleCards[0].Rank >= computer.HoleCards[1].Rank)
                {
                    cardToKeep = computer.HoleCards[0];
                    cardToDrop = computer.HoleCards[1];
                }
                else
                {
                    cardToKeep = computer.HoleCards[1];
                    cardToDrop = computer.HoleCards[0];
                }

                deck.InsertCardRandomly(cardToDrop);

                Card? newCard = deck.DealCard();

                if (newCard != null)
                {
                    computer.HoleCards.Clear();
                    computer.HoleCards.Add(cardToKeep);
                    computer.HoleCards.Add(newCard);
                }
            }
        }
        public static bool DecideCallOrFold(Player computer, List<Card> communityCards, int amountToCall)
        {
            if (amountToCall >= computer.Chips) amountToCall = computer.Chips;

            HandResult current = HandEvaluator.GetBestHand(computer.HoleCards, communityCards);
            Random rnd = new Random();
            int randomAction = rnd.Next(1, 101);

            // Always call with strong hands (Two Pair or better)
            if (current.Type >= HandType.TwoPair) return true;

            //  Marginal hands with risk assessment
            if (current.Type == HandType.OnePair || computer.HoleCards.Max(c => (int)c.Rank) >= (int)Rank.Jack)
            {
                // High betting pressure: 50% chance to call
                if (amountToCall > computer.Chips / 3)
                {
                    return randomAction <= 50;
                }
                return true;
            }

            // Weak hands with a 10% pseudo-random bluffing chance
            return randomAction <= 10;
        }
    }
}