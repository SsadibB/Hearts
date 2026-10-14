using System.Collections.Generic;

namespace Hearts.Core
{
    public static class HeartsBot
    {
        public static List<CardId> ChoosePass(IReadOnlyList<CardId> hand)
        {
            var ranked = new List<CardId>();
            if (hand != null)
            {
                for (int i = 0; i < hand.Count; i++)
                    ranked.Add(hand[i]);
            }
            ranked.Sort((a, b) => PassScore(b).CompareTo(PassScore(a)));
            var pass = new List<CardId>(HeartsRules.PassCount);
            int count = ranked.Count < HeartsRules.PassCount ? ranked.Count : HeartsRules.PassCount;
            for (int i = 0; i < count; i++)
                pass.Add(ranked[i]);
            return pass;
        }

        public static CardId ChoosePlay(IReadOnlyList<CardId> legal, bool leading)
        {
            CardId choice = legal[0];
            if (leading)
            {
                bool anySafe = false;
                for (int i = 0; i < legal.Count; i++)
                {
                    if (!legal[i].IsHeart)
                        anySafe = true;
                }
                for (int i = 0; i < legal.Count; i++)
                {
                    if (anySafe && legal[i].IsHeart)
                        continue;
                    if (legal[i].Rank < choice.Rank || (anySafe && choice.IsHeart))
                        choice = legal[i];
                }
                return choice;
            }

            bool mixedSuits = false;
            for (int i = 1; i < legal.Count; i++)
            {
                if (legal[i].Suit != legal[0].Suit)
                    mixedSuits = true;
            }

            if (mixedSuits)
            {
                for (int i = 1; i < legal.Count; i++)
                {
                    if (PassScore(legal[i]) > PassScore(choice))
                        choice = legal[i];
                }
                return choice;
            }

            for (int i = 1; i < legal.Count; i++)
            {
                if (legal[i].Rank < choice.Rank)
                    choice = legal[i];
            }
            return choice;
        }

        static int PassScore(CardId card)
        {
            if (card.IsQueenOfSpades)
                return 1000;
            if (card.IsHeart)
                return 500 + (int)card.Rank;
            if (card.Suit == Suit.Spades)
                return 200 + (int)card.Rank;
            return (int)card.Rank;
        }
    }
}
