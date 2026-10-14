using System;

namespace Hearts.Core
{
    public enum Suit : byte
    {
        Clubs = 0,
        Diamonds = 1,
        Hearts = 2,
        Spades = 3
    }

    public enum Rank : byte
    {
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13,
        Ace = 14
    }

    public enum MatchPhase : byte
    {
        Waiting = 0,
        Passing = 1,
        Playing = 2,
        RoundEnd = 3,
        MatchOver = 4
    }

    public readonly struct CardId : IEquatable<CardId>
    {
        public readonly Suit Suit;
        public readonly Rank Rank;

        public CardId(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public int Index => ((int)Suit * 13) + ((int)Rank - 2);

        public bool IsHeart => Suit == Suit.Hearts;

        public bool IsQueenOfSpades => Suit == Suit.Spades && Rank == Rank.Queen;

        public bool IsPenalty => IsHeart || IsQueenOfSpades;

        public bool IsTwoOfClubs => Suit == Suit.Clubs && Rank == Rank.Two;

        public int PenaltyPoints => IsQueenOfSpades ? 13 : IsHeart ? 5 : 0;

        public static CardId FromIndex(int index)
        {
            if (index < 0 || index > 51)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new CardId((Suit)(index / 13), (Rank)(index % 13 + 2));
        }

        public static string SpriteFileName(CardId card)
        {
            string rank = card.Rank switch
            {
                Rank.Ace => "ace",
                Rank.Jack => "jack",
                Rank.Queen => "queen",
                Rank.King => "king",
                _ => ((int)card.Rank).ToString()
            };
            return rank + "_of_" + card.Suit.ToString().ToLowerInvariant() + ".png";
        }

        public string ShortName
        {
            get
            {
                string rank = Rank switch
                {
                    Rank.Ace => "A",
                    Rank.King => "K",
                    Rank.Queen => "Q",
                    Rank.Jack => "J",
                    Rank.Ten => "10",
                    _ => ((int)Rank).ToString()
                };
                string suit = Suit switch
                {
                    Suit.Clubs => "C",
                    Suit.Diamonds => "D",
                    Suit.Hearts => "H",
                    _ => "S"
                };
                return rank + suit;
            }
        }

        public bool Equals(CardId other) => Suit == other.Suit && Rank == other.Rank;

        public override bool Equals(object obj) => obj is CardId other && Equals(other);

        public override int GetHashCode() => Index;

        public static bool operator ==(CardId left, CardId right) => left.Equals(right);

        public static bool operator !=(CardId left, CardId right) => !left.Equals(right);

        public override string ToString() => ShortName;
    }
}
