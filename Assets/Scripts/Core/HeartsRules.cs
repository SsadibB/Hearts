using System.Collections.Generic;

namespace Hearts.Core
{
    public static class HeartsRules
    {
        public const int PlayerCount = 4;
        public const int HandSize = 13;
        public const int PassCount = 3;
        public const int TrickCount = 13;
        public const int LosingScore = 1000;
        public const int HeartPoints = 5;
        public const int QueenPoints = 13;

        public static int NextSeat(int seat) => (seat + 1) % PlayerCount;

        public static int PreviousSeat(int seat) => (seat + PlayerCount - 1) % PlayerCount;

        public static List<CardId> LegalCards(
            IReadOnlyList<CardId> hand,
            IReadOnlyList<CardId> trickInOrder,
            bool heartsBroken,
            bool isFirstTrick)
        {
            var legal = new List<CardId>();
            if (hand == null || hand.Count == 0)
                return legal;

            bool leading = trickInOrder == null || trickInOrder.Count == 0;
            if (leading && isFirstTrick)
            {
                for (int i = 0; i < hand.Count; i++)
                {
                    if (hand[i].IsTwoOfClubs)
                        legal.Add(hand[i]);
                }
                return legal;
            }

            List<CardId> pool;
            if (leading)
            {
                pool = new List<CardId>();
                if (!heartsBroken)
                {
                    for (int i = 0; i < hand.Count; i++)
                    {
                        if (!hand[i].IsHeart)
                            pool.Add(hand[i]);
                    }
                }
                if (pool.Count == 0)
                {
                    for (int i = 0; i < hand.Count; i++)
                        pool.Add(hand[i]);
                }
            }
            else
            {
                Suit lead = trickInOrder[0].Suit;
                pool = new List<CardId>();
                for (int i = 0; i < hand.Count; i++)
                {
                    if (hand[i].Suit == lead)
                        pool.Add(hand[i]);
                }
                if (pool.Count == 0)
                {
                    for (int i = 0; i < hand.Count; i++)
                        pool.Add(hand[i]);
                }
            }

            if (isFirstTrick)
            {
                var safe = new List<CardId>();
                for (int i = 0; i < pool.Count; i++)
                {
                    if (!pool[i].IsPenalty)
                        safe.Add(pool[i]);
                }
                if (safe.Count > 0)
                    return safe;
            }

            return pool;
        }

        public static bool IsLegal(
            IReadOnlyList<CardId> hand,
            IReadOnlyList<CardId> trickInOrder,
            bool heartsBroken,
            bool isFirstTrick,
            CardId card)
        {
            var legal = LegalCards(hand, trickInOrder, heartsBroken, isFirstTrick);
            for (int i = 0; i < legal.Count; i++)
            {
                if (legal[i] == card)
                    return true;
            }
            return false;
        }

        public static int TrickWinner(IReadOnlyList<CardId> trickInOrder, int leadPlayer)
        {
            Suit lead = trickInOrder[0].Suit;
            int bestRank = -1;
            int winnerOffset = 0;
            for (int i = 0; i < trickInOrder.Count; i++)
            {
                if (trickInOrder[i].Suit != lead)
                    continue;
                int rank = (int)trickInOrder[i].Rank;
                if (rank > bestRank)
                {
                    bestRank = rank;
                    winnerOffset = i;
                }
            }
            return (leadPlayer + winnerOffset) % PlayerCount;
        }

        public static int TrickPoints(IReadOnlyList<CardId> cards)
        {
            int points = 0;
            for (int i = 0; i < cards.Count; i++)
                points += cards[i].PenaltyPoints;
            return points;
        }
    }
}
