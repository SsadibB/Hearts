using System;
using System.Collections.Generic;
using Hearts.Core;
using UnityEngine;

namespace Hearts.Gameplay
{
    public class SinglePlayerMatch : MonoBehaviour, IHeartsTable
    {
        static readonly string[] BotNames = { "Alex", "Riley", "Jordan" };

        readonly HeartsMatch _match = new HeartsMatch();
        readonly List<int> _localCards = new List<int>(HeartsRules.HandSize);
        readonly string[] _names = { "Player", "Alex", "Riley", "Jordan" };
        readonly int[] _avatars = new int[HeartsRules.PlayerCount];

        string _status = "";
        bool _running;
        float _botAt = -1f;
        float _confirmAt = -1f;

        public bool IsActive => _running;
        public int Phase => _running ? (int)_match.Phase : (int)MatchPhase.Waiting;
        public int CurrentPlayer => _match.CurrentPlayer;
        public int RoundIndex => _match.RoundIndex;
        public int Loser => _match.Loser;
        public int LocalSeat => 0;
        public string Status => _status;
        public bool CanRequestNextRound => _running && _match.Phase == MatchPhase.RoundEnd;
        public IReadOnlyList<int> LocalCards => _localCards;

        public event Action HandChanged;
        public event Action<string> MoveRejected;

        public void Begin(string playerName, int avatarIndex)
        {
            _names[0] = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
            if (_names[0].Length > 24)
                _names[0] = _names[0].Substring(0, 24);
            _avatars[0] = Mathf.Max(0, avatarIndex);
            for (int i = 0; i < BotNames.Length; i++)
            {
                _names[i + 1] = BotNames[i];
                _avatars[i + 1] = (_avatars[0] + i + 1) % 13;
            }
            _running = true;
            _botAt = -1f;
            _confirmAt = -1f;
            StartRound();
        }

        public void StopMatch()
        {
            _running = false;
            _botAt = -1f;
            _confirmAt = -1f;
            _localCards.Clear();
            _status = "";
            HandChanged?.Invoke();
        }

        public int TrickCard(int seat)
        {
            if (!_running || seat < 0 || seat >= HeartsRules.PlayerCount)
                return -1;
            CardId? card = _match.TrickCard(seat);
            return card.HasValue ? card.Value.Index : -1;
        }

        public int RoundScore(int seat) => Score(_match.RoundScores, seat);

        public int TotalScore(int seat) => Score(_match.TotalScores, seat);

        public string PlayerName(int seat)
        {
            if (seat < 0 || seat >= _names.Length)
                return "Player";
            return _names[seat];
        }

        public int AvatarIndex(int seat)
        {
            if (seat < 0 || seat >= _avatars.Length)
                return 0;
            return _avatars[seat];
        }

        public int CardsRemaining(int seat)
        {
            if (!_running || seat < 0 || seat >= HeartsRules.PlayerCount)
                return 0;
            return _match.Hand(seat).Count;
        }

        public bool IsConnected(int seat) => _running && seat >= 0 && seat < HeartsRules.PlayerCount;

        public void SubmitPass(int first, int second, int third)
        {
            if (!_running)
                return;
            var cards = new List<CardId>
            {
                CardId.FromIndex(first),
                CardId.FromIndex(second),
                CardId.FromIndex(third)
            };
            if (!_match.TryPass(LocalSeat, cards, out string error))
            {
                _status = error;
                MoveRejected?.Invoke(error);
                return;
            }
            CopyLocalHand();
            AfterAction();
        }

        public void SubmitPlay(int cardIndex)
        {
            if (!_running || cardIndex < 0 || cardIndex > 51)
                return;
            if (!_match.TryPlay(LocalSeat, CardId.FromIndex(cardIndex), out string error))
            {
                _status = error;
                MoveRejected?.Invoke(error);
                return;
            }
            CopyLocalHand();
            AfterAction();
        }

        public void RequestNextRound()
        {
            if (!_running)
                return;
            if (!_match.TryContinue(out string error))
            {
                _status = error;
                return;
            }
            StartRound();
        }

        void Update()
        {
            if (!_running)
                return;
            if (_confirmAt >= 0f && Time.unscaledTime >= _confirmAt)
            {
                _confirmAt = -1f;
                if (_match.TryConfirmTrick(out _))
                {
                    CopyLocalHand();
                    DescribePhase();
                    ScheduleBot();
                }
                return;
            }

            if (_botAt < 0f || Time.unscaledTime < _botAt)
                return;
            _botAt = -1f;
            ActBots();
        }

        void StartRound()
        {
            int seed = UnityEngine.Random.Range(1, int.MaxValue);
            if (!_match.TryStartRound(seed, out string error))
            {
                _status = error;
                return;
            }
            CopyLocalHand();
            _status = "Pass three cards to the left";
            _botAt = Time.unscaledTime + 0.8f;
        }

        void ActBots()
        {
            if (_match.Phase == MatchPhase.Passing)
            {
                for (int seat = 1; seat < HeartsRules.PlayerCount; seat++)
                {
                    if (_match.PassSubmitted(seat))
                        continue;
                    var pass = HeartsBot.ChoosePass(_match.Hand(seat));
                    _match.TryPass(seat, pass, out _);
                }
                CopyLocalHand();
                AfterAction();
                return;
            }

            if (_match.Phase != MatchPhase.Playing || _match.CurrentPlayer == LocalSeat || _match.AwaitingTrickConfirm)
                return;
            var legal = _match.LegalCardsFor(_match.CurrentPlayer);
            if (legal.Count == 0)
                return;
            bool leading = _match.TrickInOrder.Count == 0;
            _match.TryPlay(_match.CurrentPlayer, HeartsBot.ChoosePlay(legal, leading), out _);
            AfterAction();
        }

        void AfterAction()
        {
            if (_match.AwaitingTrickConfirm)
            {
                _status = PlayerName(_match.LastTrickWinner) + " takes the trick";
                _confirmAt = Time.unscaledTime + 1.25f;
                _botAt = -1f;
                return;
            }
            DescribePhase();
            ScheduleBot();
        }

        void ScheduleBot()
        {
            if (_match.Phase == MatchPhase.Playing && !_match.AwaitingTrickConfirm && _match.CurrentPlayer != LocalSeat)
                _botAt = Time.unscaledTime + 0.7f;
        }

        void DescribePhase()
        {
            if (_match.Phase == MatchPhase.Playing)
                _status = _match.CurrentPlayer == LocalSeat ? "Your turn" : PlayerName(_match.CurrentPlayer) + " is playing";
            else if (_match.Phase == MatchPhase.Passing)
                _status = "Pass three cards to the left";
            else if (_match.Phase == MatchPhase.RoundEnd)
                _status = "Round complete";
            else if (_match.Phase == MatchPhase.MatchOver)
                _status = "Match over";
        }

        void CopyLocalHand()
        {
            var hand = _running ? _match.Hand(LocalSeat) : null;
            int count = hand != null ? hand.Count : 0;
            bool changed = count != _localCards.Count;
            if (!changed)
            {
                for (int i = 0; i < count; i++)
                {
                    if (_localCards[i] != hand[i].Index)
                        changed = true;
                }
            }
            if (!changed)
                return;
            _localCards.Clear();
            for (int i = 0; i < count; i++)
                _localCards.Add(hand[i].Index);
            HandChanged?.Invoke();
        }

        static int Score(int[] scores, int seat)
        {
            if (scores == null || seat < 0 || seat >= scores.Length)
                return 0;
            return scores[seat];
        }
    }
}
