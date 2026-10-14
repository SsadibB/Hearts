using System.Collections.Generic;
using Hearts.Core;
using NUnit.Framework;

namespace Hearts.Tests
{
    public class HeartsBotTests
    {
        [Test]
        public void BotPassesThreeOwnedCardsAndCanFinishARound()
        {
            var match = new HeartsMatch();
            Assert.IsTrue(match.TryStartRound(3, out string error), error);

            for (int player = 0; player < HeartsRules.PlayerCount; player++)
            {
                var pass = HeartsBot.ChoosePass(match.Hand(player));
                Assert.AreEqual(HeartsRules.PassCount, pass.Count);
                Assert.IsTrue(match.TryPass(player, pass, out error), error);
            }

            Assert.AreEqual(MatchPhase.Playing, match.Phase);
            int guard = 0;
            while (match.Phase == MatchPhase.Playing && guard++ < 80)
            {
                var legal = match.LegalCardsFor(match.CurrentPlayer);
                Assert.Greater(legal.Count, 0);
                bool leading = match.TrickInOrder.Count == 0;
                Assert.IsTrue(match.TryPlay(match.CurrentPlayer, HeartsBot.ChoosePlay(legal, leading), out error), error);
                if (match.AwaitingTrickConfirm)
                    Assert.IsTrue(match.TryConfirmTrick(out error), error);
            }

            Assert.AreEqual(MatchPhase.RoundEnd, match.Phase);
            int total = 0;
            for (int i = 0; i < HeartsRules.PlayerCount; i++)
                total += match.RoundScores[i];
            Assert.AreEqual(78, total);
        }

        [Test]
        public void BotPassPrefersTheQueenOfSpades()
        {
            var hand = new List<CardId>
            {
                new CardId(Suit.Clubs, Rank.Two),
                new CardId(Suit.Diamonds, Rank.Three),
                new CardId(Suit.Hearts, Rank.Four),
                new CardId(Suit.Spades, Rank.Queen)
            };
            var pass = HeartsBot.ChoosePass(hand);
            bool hasQueen = false;
            for (int i = 0; i < pass.Count; i++)
            {
                if (pass[i].IsQueenOfSpades)
                    hasQueen = true;
            }
            Assert.IsTrue(hasQueen);
        }
    }
}
