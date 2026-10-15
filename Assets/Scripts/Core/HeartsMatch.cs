using System;
using System.Collections.Generic;

namespace Hearts.Core
{
    public sealed class HeartsMatch
    {
        readonly List<CardId>[] _hands = new List<CardId>[HeartsRules.PlayerCount];
        readonly List<CardId>[] _pendingPasses = new List<CardId>[HeartsRules.PlayerCount];
        readonly bool[] _passSubmitted = new bool[HeartsRules.PlayerCount];
        readonly List<CardId> _trickOrder = new List<CardId>(HeartsRules.PlayerCount);
        readonly CardId?[] _trickBySeat = new CardId?[HeartsRules.PlayerCount];

        public MatchPhase Phase { get; private set; } = MatchPhase.Waiting;
        public int CurrentPlayer { get; private set; } = -1;
        public int LeadPlayer { get; private set; } = -1;
        public int TrickNumber { get; private set; }
        public bool HeartsBroken { get; private set; }
        public int RoundIndex { get; private set; }
        public int LastTrickWinner { get; private set; } = -1;
        public int Loser { get; private set; } = -1;
        public bool AwaitingTrickConfirm { get; private set; }
        public int[] RoundScores { get; } = new int[HeartsRules.PlayerCount];
        public int[] TotalScores { get; } = new int[HeartsRules.PlayerCount];

        public HeartsMatch()
        {
            for (int i = 0; i < HeartsRules.PlayerCount; i++)
            {
                _hands[i] = new List<CardId>(HeartsRules.HandSize);
                _pendingPasses[i] = new List<CardId>(HeartsRules.PassCount);
            }
        }

        public IReadOnlyList<CardId> Hand(int player) => _hands[player];

        public CardId? TrickCard(int player) => _trickBySeat[player];

        public IReadOnlyList<CardId> TrickInOrder => _trickOrder;

        public bool PassSubmitted(int player) => _passSubmitted[player];

        public void ResetMatch()
        {
            Phase = MatchPhase.Waiting;
            CurrentPlayer = -1;
            LeadPlayer = -1;
            TrickNumber = 0;
            HeartsBroken = false;
            RoundIndex = 0;
            LastTrickWinner = -1;
            Loser = -1;
            AwaitingTrickConfirm = false;
            ClearTrick();
            for (int i = 0; i < HeartsRules.PlayerCount; i++)
            {
                _hands[i].Clear();
                _pendingPasses[i].Clear();
                _passSubmitted[i] = false;
                RoundScores[i] = 0;
                TotalScores[i] = 0;
            }
        }

        public bool TryStartRound(int seed, out string error)
        {
            if (Phase != MatchPhase.Waiting && Phase != MatchPhase.RoundEnd)
            {
                error = "A round cannot start right now.";
                return false;
            }
            if (Phase == MatchPhase.MatchOver || Loser >= 0)
            {
                error = "The match is already over.";
                return false;
            }

            var deck = new List<CardId>(52);
            for (int i = 0; i < 52; i++)
                deck.Add(CardId.FromIndex(i));
            Shuffle(deck, seed);

            for (int i = 0; i < HeartsRules.PlayerCount; i++)
            {
                _hands[i].Clear();
                _pendingPasses[i].Clear();
                _passSubmitted[i] = false;
                RoundScores[i] = 0;
            }

            for (int i = 0; i < 52; i++)
                _hands[i % HeartsRules.PlayerCount].Add(deck[i]);

            for (int i = 0; i < HeartsRules.PlayerCount; i++)
                SortHand(_hands[i]);

            BeginPlayingAfterDeal();
            Phase = MatchPhase.Passing;
            error = null;
            return true;
        }

        public bool TryPass(int player, IReadOnlyList<CardId> cards, out string error)
        {
            if (Phase != MatchPhase.Passing)
            {
                error = "It is not the passing step.";
                return false;
            }
            if (player < 0 || player >= HeartsRules.PlayerCount)
            {
                error = "Unknown player.";
                return false;
            }
            if (_passSubmitted[player])
            {
                error = "Those cards were already passed.";
                return false;
            }
            if (cards == null || cards.Count != HeartsRules.PassCount)
            {
                error = "Pass exactly three cards.";
                return false;
            }

            var seen = new HashSet<int>();
            for (int i = 0; i < cards.Count; i++)
            {
                if (!seen.Add(cards[i].Index) || !Contains(_hands[player], cards[i]))
                {
                    error = "Each passed card must be in your hand.";
                    return false;
                }
            }

            _pendingPasses[player].Clear();
            for (int i = 0; i < cards.Count; i++)
                _pendingPasses[player].Add(cards[i]);
            _passSubmitted[player] = true;

            for (int i = 0; i < HeartsRules.PlayerCount; i++)
            {
                if (!_passSubmitted[i])
                {
                    error = null;
                    return true;
                }
            }

            ApplyPasses();
            error = null;
            return true;
        }

        public bool TryPlay(int player, CardId card, out string error)
        {
            if (Phase != MatchPhase.Playing || AwaitingTrickConfirm)
            {
                error = "It is not time to play a card.";
                return false;
            }
            if (player != CurrentPlayer)
            {
                error = "It is not your turn.";
                return false;
            }
            if (!Contains(_hands[player], card))
            {
                error = "That card is not in your hand.";
                return false;
            }

            bool firstTrick = TrickNumber == 0;
            if (!HeartsRules.IsLegal(_hands[player], _trickOrder, HeartsBroken, firstTrick, card))
            {
                error = "That card cannot be played.";
                return false;
            }

            Remove(_hands[player], card);
            _trickBySeat[player] = card;
            _trickOrder.Add(card);
            if (card.IsHeart)
                HeartsBroken = true;

            if (_trickOrder.Count < HeartsRules.PlayerCount)
            {
                CurrentPlayer = HeartsRules.NextSeat(CurrentPlayer);
                error = null;
                return true;
            }

            LastTrickWinner = HeartsRules.TrickWinner(_trickOrder, LeadPlayer);
            AwaitingTrickConfirm = true;
            error = null;
            return true;
        }

        public bool TryConfirmTrick(out string error)
        {
            if (!AwaitingTrickConfirm)
            {
                error = "There is no completed trick.";
                return false;
            }

            RoundScores[LastTrickWinner] += HeartsRules.TrickPoints(_trickOrder);
            ClearTrick();
            AwaitingTrickConfirm = false;
            TrickNumber++;
            LeadPlayer = LastTrickWinner;
            CurrentPlayer = LastTrickWinner;
            if (TrickNumber >= HeartsRules.TrickCount)
                FinishRound();
            error = null;
            return true;
        }

        public bool TryContinue(out string error)
        {
            if (Phase == MatchPhase.MatchOver)
            {
                error = "The match is over.";
                return false;
            }
            if (Phase != MatchPhase.RoundEnd)
            {
                error = "The round is still in progress.";
                return false;
            }
            error = null;
            return true;
        }

        public List<CardId> LegalCardsFor(int player)
        {
            if (Phase != MatchPhase.Playing || player != CurrentPlayer)
                return new List<CardId>();
            return HeartsRules.LegalCards(_hands[player], _trickOrder, HeartsBroken, TrickNumber == 0);
        }

        internal static HeartsMatch FromHands(IReadOnlyList<IReadOnlyList<CardId>> hands, int leader, int trickNumber, bool heartsBroken)
        {
            var match = new HeartsMatch();
            var seen = new HashSet<int>();
            for (int p = 0; p < HeartsRules.PlayerCount; p++)
            {
                for (int i = 0; i < hands[p].Count; i++)
                {
                    if (!seen.Add(hands[p][i].Index))
                        throw new InvalidOperationException("Duplicate card.");
                    match._hands[p].Add(hands[p][i]);
                }
            }
            match.Phase = MatchPhase.Playing;
            match.LeadPlayer = leader;
            match.CurrentPlayer = leader;
            match.TrickNumber = trickNumber;
            match.HeartsBroken = heartsBroken;
            return match;
        }

        void ApplyPasses()
        {
            var received = new List<CardId>[HeartsRules.PlayerCount];
            for (int i = 0; i < HeartsRules.PlayerCount; i++)
                received[i] = new List<CardId>(HeartsRules.PassCount);

            for (int i = 0; i < HeartsRules.PlayerCount; i++)
            {
                int target = HeartsRules.NextSeat(i);
                for (int c = 0; c < _pendingPasses[i].Count; c++)
                {
                    Remove(_hands[i], _pendingPasses[i][c]);
                    received[target].Add(_pendingPasses[i][c]);
                }
            }

            for (int i = 0; i < HeartsRules.PlayerCount; i++)
            {
                _hands[i].AddRange(received[i]);
                SortHand(_hands[i]);
                _pendingPasses[i].Clear();
            }

            BeginPlayingAfterDeal();
        }

        void BeginPlayingAfterDeal()
        {
            ClearTrick();
            TrickNumber = 0;
            HeartsBroken = false;
            LastTrickWinner = -1;
            int leader = FindTwoOfClubs();
            LeadPlayer = leader;
            CurrentPlayer = leader;
            Phase = MatchPhase.Playing;
        }

        void FinishRound()
        {
            for (int i = 0; i < HeartsRules.PlayerCount; i++)
                TotalScores[i] += RoundScores[i];

            RoundIndex++;
            int highest = 0;
            for (int i = 1; i < HeartsRules.PlayerCount; i++)
            {
                if (TotalScores[i] > TotalScores[highest])
                    highest = i;
            }

            if (TotalScores[highest] >= HeartsRules.LosingScore)
            {
                Loser = highest;
                Phase = MatchPhase.MatchOver;
            }
            else
            {
                Phase = MatchPhase.RoundEnd;
            }
        }

        int FindTwoOfClubs()
        {
            for (int p = 0; p < HeartsRules.PlayerCount; p++)
            {
                for (int i = 0; i < _hands[p].Count; i++)
                {
                    if (_hands[p][i].IsTwoOfClubs)
                        return p;
                }
            }
            return 0;
        }

        void ClearTrick()
        {
            _trickOrder.Clear();
            for (int i = 0; i < _trickBySeat.Length; i++)
                _trickBySeat[i] = null;
        }

        static void Shuffle(List<CardId> deck, int seed)
        {
            var random = new Random(seed);
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                CardId swap = deck[i];
                deck[i] = deck[j];
                deck[j] = swap;
            }
        }

        static void SortHand(List<CardId> hand)
        {
            hand.Sort((a, b) =>
            {
                int suit = a.Suit.CompareTo(b.Suit);
                return suit != 0 ? suit : a.Rank.CompareTo(b.Rank);
            });
        }

        static bool Contains(List<CardId> hand, CardId card)
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] == card)
                    return true;
            }
            return false;
        }

        static void Remove(List<CardId> hand, CardId card)
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] == card)
                {
                    hand.RemoveAt(i);
                    return;
                }
            }
        }
    }
}
