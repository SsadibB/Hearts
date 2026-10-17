using System.Collections.Generic;
using Hearts.Core;
using NUnit.Framework;

namespace Hearts.Tests
{
    public class HeartsRulesTests
    {
        static CardId C(Suit suit, Rank rank) => new CardId(suit, rank);

        static List<CardId> Hand(params CardId[] cards) => new List<CardId>(cards);

        [Test]
        public void DeckHasFiftyTwoUniqueCards()
        {
            var seen = new HashSet<int>();
            for (int i = 0; i < 52; i++)
                Assert.IsTrue(seen.Add(CardId.FromIndex(i).Index));
            Assert.AreEqual(52, seen.Count);
            Assert.IsTrue(CardId.FromIndex(0).IsTwoOfClubs);
            Assert.AreEqual(13, C(Suit.Spades, Rank.Queen).PenaltyPoints);
            Assert.AreEqual(5, C(Suit.Hearts, Rank.Ace).PenaltyPoints);
        }

        [Test]
        public void FirstLeadMustBeTwoOfClubs()
        {
            var hand = Hand(C(Suit.Clubs, Rank.Two), C(Suit.Clubs, Rank.Ace), C(Suit.Hearts, Rank.Ace));
            var legal = HeartsRules.LegalCards(hand, new List<CardId>(), false, true);
            Assert.AreEqual(1, legal.Count);
            Assert.IsTrue(legal[0].IsTwoOfClubs);
        }

        [Test]
        public void MustFollowSuit()
        {
            var hand = Hand(C(Suit.Hearts, Rank.Five), C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.King));
            var trick = new List<CardId> { C(Suit.Hearts, Rank.Two) };
            var legal = HeartsRules.LegalCards(hand, trick, true, false);
            Assert.AreEqual(2, legal.Count);
            Assert.IsFalse(HeartsRules.IsLegal(hand, trick, true, false, C(Suit.Spades, Rank.Ace)));
        }

        [Test]
        public void HeartsCannotBeLedUntilBrokenUnlessOnlyHeartsRemain()
        {
            var mixed = Hand(C(Suit.Clubs, Rank.Four), C(Suit.Hearts, Rank.Ace));
            var legal = HeartsRules.LegalCards(mixed, new List<CardId>(), false, false);
            Assert.AreEqual(1, legal.Count);
            Assert.AreEqual(Suit.Clubs, legal[0].Suit);

            var onlyHearts = Hand(C(Suit.Hearts, Rank.Two), C(Suit.Hearts, Rank.Ace));
            var forced = HeartsRules.LegalCards(onlyHearts, new List<CardId>(), false, false);
            Assert.AreEqual(2, forced.Count);
        }

        [Test]
        public void FirstTrickBlocksPenaltyDiscardsWhenASafeCardExists()
        {
            var hand = Hand(C(Suit.Diamonds, Rank.Four), C(Suit.Hearts, Rank.Ace), C(Suit.Spades, Rank.Queen));
            var trick = new List<CardId> { C(Suit.Clubs, Rank.Two) };
            var legal = HeartsRules.LegalCards(hand, trick, false, true);
            Assert.AreEqual(1, legal.Count);
            Assert.AreEqual(Suit.Diamonds, legal[0].Suit);
        }

        [Test]
        public void FirstTrickAllowsPenaltyCardsWhenNoSafeCardExists()
        {
            var hand = Hand(C(Suit.Hearts, Rank.Three), C(Suit.Spades, Rank.Queen));
            var trick = new List<CardId> { C(Suit.Clubs, Rank.Two) };
            var legal = HeartsRules.LegalCards(hand, trick, false, true);
            Assert.AreEqual(2, legal.Count);
        }

        [Test]
        public void FirstTrickAvoidsQueenWhenAnotherSpadeCanFollow()
        {
            var hand = Hand(C(Suit.Spades, Rank.Queen), C(Suit.Spades, Rank.Five), C(Suit.Hearts, Rank.Two));
            var trick = new List<CardId> { C(Suit.Spades, Rank.Ace) };
            var legal = HeartsRules.LegalCards(hand, trick, false, true);
            Assert.AreEqual(1, legal.Count);
            Assert.AreEqual(Rank.Five, legal[0].Rank);
        }

        [Test]
        public void HighestLeadSuitWinsAndOffSuitDoesNot()
        {
            var trick = new List<CardId>
            {
                C(Suit.Diamonds, Rank.Ten),
                C(Suit.Diamonds, Rank.Ace),
                C(Suit.Spades, Rank.Ace),
                C(Suit.Diamonds, Rank.King)
            };
            Assert.AreEqual(1, HeartsRules.TrickWinner(trick, 0));
            Assert.AreEqual(23, HeartsRules.TrickPoints(new List<CardId>
            {
                C(Suit.Hearts, Rank.Two),
                C(Suit.Hearts, Rank.Ace),
                C(Suit.Spades, Rank.Queen)
            }));
        }

        [Test]
        public void DealPassAndPlayCompletesARoundWorthSeventyEight()
        {
            var match = new HeartsMatch();
            Assert.IsTrue(match.TryStartRound(7, out string error), error);
            Assert.AreEqual(MatchPhase.Passing, match.Phase);

            for (int p = 0; p < 4; p++)
            {
                var pass = new List<CardId> { match.Hand(p)[0], match.Hand(p)[1], match.Hand(p)[2] };
                Assert.IsTrue(match.TryPass(p, pass, out error), error);
            }

            Assert.AreEqual(MatchPhase.Playing, match.Phase);
            Assert.IsFalse(match.TryPass(0, new List<CardId> { match.Hand(0)[0], match.Hand(0)[1], match.Hand(0)[2] }, out _));

            for (int trick = 0; trick < 13; trick++)
            {
                for (int turn = 0; turn < 4; turn++)
                {
                    int player = match.CurrentPlayer;
                    var legal = match.LegalCardsFor(player);
                    Assert.Greater(legal.Count, 0);
                    Assert.IsFalse(match.TryPlay(HeartsRules.NextSeat(player), legal[0], out _));
                    Assert.IsTrue(match.TryPlay(player, legal[0], out error), error);
                }
                Assert.IsTrue(match.AwaitingTrickConfirm);
                Assert.IsTrue(match.TryConfirmTrick(out error), error);
            }

            int total = 0;
            for (int i = 0; i < 4; i++)
            {
                total += match.RoundScores[i];
                Assert.AreEqual(0, match.Hand(i).Count);
            }
            Assert.AreEqual(78, total);
            Assert.AreEqual(MatchPhase.RoundEnd, match.Phase);
        }

        [Test]
        public void PassMovesThreeCardsToTheLeftAndRejectsDuplicates()
        {
            var hands = new List<CardId>[4];
            int index = 0;
            for (int p = 0; p < 4; p++)
            {
                hands[p] = new List<CardId>();
                for (int c = 0; c < 13; c++)
                    hands[p].Add(CardId.FromIndex(index++));
            }

            var match = new HeartsMatch();
            Assert.IsTrue(match.TryStartRound(3, out _));
            var original = new List<CardId>(match.Hand(0));
            var pass = new List<CardId> { original[0], original[1], original[2] };
            Assert.IsTrue(match.TryPass(0, pass, out _));
            Assert.IsFalse(match.TryPass(0, pass, out _));
            Assert.IsFalse(match.TryPass(1, new List<CardId> { match.Hand(1)[0], match.Hand(1)[0], match.Hand(1)[1] }, out _));

            for (int p = 1; p < 4; p++)
            {
                var cards = new List<CardId> { match.Hand(p)[0], match.Hand(p)[1], match.Hand(p)[2] };
                Assert.IsTrue(match.TryPass(p, cards, out _));
            }

            for (int i = 0; i < pass.Count; i++)
            {
                Assert.IsFalse(Contains(match.Hand(0), pass[i]));
                Assert.IsTrue(Contains(match.Hand(1), pass[i]));
            }
            Assert.AreEqual(13, match.Hand(0).Count);
        }

        [Test]
        public void EveryRoundPassesThreeCardsClockwise()
        {
            var match = new HeartsMatch();
            Assert.IsTrue(match.TryStartRound(11, out string error), error);
            PlayRound(match);
            Assert.AreEqual(MatchPhase.RoundEnd, match.Phase);
            Assert.AreEqual(1, match.LoggedRounds);
            Assert.IsTrue(match.TryContinue(out error), error);
            Assert.IsTrue(match.TryStartRound(19, out error), error);
            Assert.AreEqual(MatchPhase.Passing, match.Phase);

            var passed = new List<CardId>[4];
            for (int player = 0; player < 4; player++)
            {
                passed[player] = new List<CardId>
                {
                    match.Hand(player)[0],
                    match.Hand(player)[1],
                    match.Hand(player)[2]
                };
                Assert.IsTrue(match.TryPass(player, passed[player], out error), error);
            }

            Assert.AreEqual(MatchPhase.Playing, match.Phase);
            for (int player = 0; player < 4; player++)
            {
                int receiver = HeartsRules.NextSeat(player);
                for (int i = 0; i < passed[player].Count; i++)
                    Assert.IsTrue(Contains(match.Hand(receiver), passed[player][i]));
            }
            Assert.AreEqual(1, match.LoggedRounds);
        }

        static void PlayRound(HeartsMatch match)
        {
            string error;
            for (int player = 0; player < 4; player++)
            {
                var pass = new List<CardId> { match.Hand(player)[0], match.Hand(player)[1], match.Hand(player)[2] };
                Assert.IsTrue(match.TryPass(player, pass, out error), error);
            }

            for (int trick = 0; trick < 13; trick++)
            {
                for (int turn = 0; turn < 4; turn++)
                {
                    int player = match.CurrentPlayer;
                    var legal = match.LegalCardsFor(player);
                    Assert.IsTrue(match.TryPlay(player, legal[0], out error), error);
                }
                Assert.IsTrue(match.TryConfirmTrick(out error), error);
            }
        }

        [Test]
        public void ConstructedFirstTrickRejectsQueenWhenADiamondRemains()
        {
            var hands = EmptyHands();
            hands[0].Add(C(Suit.Clubs, Rank.Two));
            hands[1].Add(C(Suit.Diamonds, Rank.Three));
            hands[1].Add(C(Suit.Spades, Rank.Queen));
            hands[1].Add(C(Suit.Hearts, Rank.Four));
            hands[2].Add(C(Suit.Clubs, Rank.Three));
            hands[3].Add(C(Suit.Clubs, Rank.Four));
            var match = HeartsMatch.FromHands(hands, 0, 0, false);
            Assert.IsTrue(match.TryPlay(0, C(Suit.Clubs, Rank.Two), out _));
            Assert.IsFalse(match.TryPlay(1, C(Suit.Spades, Rank.Queen), out _));
            Assert.IsTrue(match.TryPlay(1, C(Suit.Diamonds, Rank.Three), out string error), error);
        }

        [Test]
        public void MatchEndsWhenAPlayerReachesOneThousand()
        {
            var match = new HeartsMatch();
            int seed = 1;
            while (match.Phase != MatchPhase.MatchOver && seed < 80)
            {
                Assert.IsTrue(match.TryStartRound(seed, out string error), error);
                for (int p = 0; p < 4; p++)
                {
                    var pass = new List<CardId> { match.Hand(p)[0], match.Hand(p)[1], match.Hand(p)[2] };
                    Assert.IsTrue(match.TryPass(p, pass, out error), error);
                }
                while (match.Phase == MatchPhase.Playing)
                {
                    if (match.AwaitingTrickConfirm)
                    {
                        Assert.IsTrue(match.TryConfirmTrick(out error), error);
                        continue;
                    }
                    int player = match.CurrentPlayer;
                    var legal = match.LegalCardsFor(player);
                    Assert.IsTrue(match.TryPlay(player, legal[0], out error), error);
                }
                seed++;
            }

            Assert.AreEqual(MatchPhase.MatchOver, match.Phase);
            Assert.GreaterOrEqual(match.TotalScores[match.Loser], HeartsRules.LosingScore);
            int best = match.TotalScores[0];
            for (int i = 1; i < 4; i++)
                if (match.TotalScores[i] > best)
                    best = match.TotalScores[i];
            Assert.AreEqual(best, match.TotalScores[match.Loser]);
        }

        static List<CardId>[] EmptyHands()
        {
            var hands = new List<CardId>[4];
            for (int i = 0; i < 4; i++)
                hands[i] = new List<CardId>();
            return hands;
        }

        static bool Contains(IReadOnlyList<CardId> hand, CardId card)
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] == card)
                    return true;
            }
            return false;
        }
    }
}
