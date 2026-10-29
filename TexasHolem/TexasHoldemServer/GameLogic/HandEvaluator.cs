using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TexasHoldemServer.GameLogic;

namespace TexasHoldemServer.GameLogic
{
    // identifies the type of poker hand
    public enum HandType
    {
        HighCard = 1, OnePair = 2, TwoPair = 3, ThreeOfAKind = 4, Straight = 5,
        Flush = 6, FullHouse = 7, FourOfAKind = 8, StraightFlush = 9, RoyalFlush = 10
    }

    // final result of evaluating a 5-card hand, including the hand type, the best 5 cards, and the kickers for tie-breaking
    public class HandResult : IComparable<HandResult>
    {
        public HandType Type { get; set; }
        public List<Card> Best5Cards { get; set; } = new List<Card>();
        public List<Rank> Kickers { get; set; } = new List<Rank>();

        public int CompareTo(HandResult? other)
        {
            if (other == null) return 1;

            if (this.Type != other.Type)
                return this.Type.CompareTo(other.Type);

            for (int i = 0; i < this.Kickers.Count; i++)
            {
                if (this.Kickers[i] != other.Kickers[i])
                    return this.Kickers[i].CompareTo(other.Kickers[i]);
            }

            return 0;
        }
    }

    public static class HandEvaluator
    {
        public static HandResult GetBestHand(List<Card> holeCards, List<Card> communityCards)
        {
            // conclude all 7 cards (2 hole + 5 community)
            var all7Cards = new List<Card>(holeCards);
            all7Cards.AddRange(communityCards);

            // get all combinations of 5 cards from the 7 cards
            var combinations = GetCombinations(all7Cards, 5);
            HandResult? bestResult = null;

            foreach (var combo in combinations)
            {
                var result = Evaluate5Cards(combo);
                // if this combo's hand result is better than the best result so far, update bestResult
                if (bestResult == null || result.CompareTo(bestResult) > 0)
                {
                    bestResult = result;
                }
            }

            // if for some reason we couldn't evaluate any hand, return a default High Card result
            return bestResult ?? new HandResult { Type = HandType.HighCard };
        }

        // clearify the hand type of a specific 5-card combination, and also determine the kickers for tie-breaking
        private static HandResult Evaluate5Cards(List<Card> cards)
        {
            var orderedCards = cards.OrderByDescending(c => c.Rank).ToList();
            var groups = cards.GroupBy(c => c.Rank)
                              .OrderByDescending(g => g.Count())
                              .ThenByDescending(g => g.Key)
                              .ToList();

            bool isFlush = cards.GroupBy(c => c.Suit).Any(g => g.Count() == 5);
            bool isStraight = IsStraight(orderedCards);

            HandResult result = new HandResult { Best5Cards = orderedCards, Kickers = new List<Rank>() };

            foreach (var g in groups)
            {
                for (int i = 0; i < g.Count(); i++) result.Kickers.Add(g.Key);
            }

            // special case for A-2-3-4-5 straight: treat Ace as the lowest card for kickers
            if (isStraight && orderedCards[0].Rank == Rank.Ace && orderedCards[1].Rank == Rank.Five)
            {
                result.Kickers.Clear();
                result.Kickers.AddRange(new[] { Rank.Five, Rank.Four, Rank.Three, Rank.Two, Rank.Ace });
            }

            // card groups and straight/flush status determine the hand type
            if (isFlush && isStraight)
            {
                result.Type = orderedCards[0].Rank == Rank.Ace && orderedCards[1].Rank == Rank.King ? HandType.RoyalFlush : HandType.StraightFlush;
            }
            else if (groups[0].Count() == 4) result.Type = HandType.FourOfAKind;
            else if (groups[0].Count() == 3 && groups[1].Count() == 2) result.Type = HandType.FullHouse;
            else if (isFlush) result.Type = HandType.Flush;
            else if (isStraight) result.Type = HandType.Straight;
            else if (groups[0].Count() == 3) result.Type = HandType.ThreeOfAKind;
            else if (groups[0].Count() == 2 && groups[1].Count() == 2) result.Type = HandType.TwoPair;
            else if (groups[0].Count() == 2) result.Type = HandType.OnePair;
            else result.Type = HandType.HighCard;

            return result;
        }

        private static bool IsStraight(List<Card> orderedCards)
        {
            var ranks = orderedCards.Select(c => (int)c.Rank).Distinct().ToList();
            if (ranks.Count != 5) return false;
            if (ranks.First() - ranks.Last() == 4) return true;
            if (ranks[0] == 14 && ranks[1] == 5 && ranks[4] == 2) return true;
            return false;
        }

        // get all combinations of a specific length from a list of cards
        private static List<List<Card>> GetCombinations(List<Card> list, int length)
        {
            if (length == 1) return list.Select(t => new List<Card> { t }).ToList();
            if (list.Count == length) return new List<List<Card>> { new List<Card>(list) };
            if (list.Count < length) return new List<List<Card>>();

            var result = new List<List<Card>>();
            var head = list[0];
            var tail = list.Skip(1).ToList();

            foreach (var combo in GetCombinations(tail, length - 1))
            {
                combo.Insert(0, head);
                result.Add(combo);
            }
            result.AddRange(GetCombinations(tail, length));

            return result;
        }
    }
}