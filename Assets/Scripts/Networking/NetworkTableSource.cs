using System;
using System.Collections.Generic;
using Hearts.Core;
using Hearts.Gameplay;
using UnityEngine;

namespace Hearts.Networking
{
    public class NetworkTableSource : MonoBehaviour, IHeartsTable
    {
        static readonly List<int> EmptyCards = new List<int>();

        [SerializeField] NetworkHeartsMatch match;
        [SerializeField] HeartsSession session;

        NetworkHeartsPlayer _hooked;

        public bool IsActive => match != null && match.Object != null && match.Object.IsValid;
        public int Phase => match != null ? match.Phase : (int)MatchPhase.Waiting;
        public int CurrentPlayer => match != null ? match.CurrentPlayer : -1;
        public int LastTrickWinner => match != null ? match.LastTrickWinner : -1;
        public int RoundIndex => match != null ? match.RoundIndex : 0;
        public int Loser => match != null ? match.Loser : -1;
        public int LocalSeat => Local() != null ? Local().Seat : 0;
        public string Status => match != null && match.Object != null && match.Object.IsValid ? match.Status.ToString() : "";
        public bool CanRequestNextRound => match != null && match.Phase == (int)MatchPhase.RoundEnd && match.HasStateAuthority;
        public bool CanRestartMatch => match != null && match.HasStateAuthority && (match.Phase == (int)MatchPhase.RoundEnd || match.Phase == (int)MatchPhase.MatchOver);
        public IReadOnlyList<int> LocalCards => Local() != null ? Local().LocalCards : EmptyCards;

        public event Action HandChanged;
        public event Action<string> MoveRejected;

        public int TrickCard(int seat)
        {
            if (match == null || seat < 0 || seat >= HeartsRules.PlayerCount)
                return -1;
            return match.TrickCards[seat];
        }

        public int RoundScore(int seat) => match != null && seat >= 0 && seat < HeartsRules.PlayerCount ? match.RoundScores[seat] : 0;

        public int TotalScore(int seat) => match != null && seat >= 0 && seat < HeartsRules.PlayerCount ? match.TotalScores[seat] : 0;

        public string PlayerName(int seat)
        {
            var player = Seat(seat);
            if (player == null)
                return "Waiting";
            string name = player.DisplayName.ToString();
            return string.IsNullOrEmpty(name) ? "Waiting" : name;
        }

        public int AvatarIndex(int seat)
        {
            var player = Seat(seat);
            return player != null ? player.AvatarIndex : 0;
        }

        public int CardsRemaining(int seat)
        {
            var player = Seat(seat);
            return player != null ? player.CardsRemaining : 0;
        }

        public bool IsConnected(int seat)
        {
            var player = Seat(seat);
            return player == null || player.Connected;
        }

        public void SubmitPass(int first, int second, int third)
        {
            var local = Local();
            if (local != null)
                local.SubmitPass(first, second, third);
        }

        public void SubmitPlay(int cardIndex)
        {
            var local = Local();
            if (local != null)
                local.SubmitPlay(cardIndex);
        }

        public void RequestNextRound()
        {
            if (match != null && match.HasStateAuthority)
                match.RequestNextRound();
        }

        public void RestartMatch()
        {
            if (match != null && match.HasStateAuthority)
                match.RestartMatch();
        }

        public void FinishTrickPresentation()
        {
            if (match != null && match.HasStateAuthority)
                match.FinishTrickPresentation();
        }

        void Update()
        {
            var local = Local();
            if (local == _hooked)
                return;
            if (_hooked != null)
            {
                _hooked.HandChanged -= ForwardHand;
                _hooked.MoveRejected -= ForwardReject;
            }
            _hooked = local;
            if (_hooked != null)
            {
                _hooked.HandChanged += ForwardHand;
                _hooked.MoveRejected += ForwardReject;
            }
        }

        void OnDisable()
        {
            if (_hooked == null)
                return;
            _hooked.HandChanged -= ForwardHand;
            _hooked.MoveRejected -= ForwardReject;
            _hooked = null;
        }

        void ForwardHand() => HandChanged?.Invoke();

        void ForwardReject(string reason) => MoveRejected?.Invoke(reason);

        NetworkHeartsPlayer Local() => session != null ? session.LocalSeat() : null;

        NetworkHeartsPlayer Seat(int seat)
        {
            if (match == null || match.Seats == null || seat < 0 || seat >= match.Seats.Length)
                return null;
            return match.Seats[seat];
        }
    }
}
