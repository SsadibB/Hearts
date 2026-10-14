using System.Collections.Generic;
using System.Text;
using Fusion;
using Hearts.Core;
using UnityEngine;

namespace Hearts.Networking
{
    public class NetworkHeartsMatch : NetworkBehaviour
    {
        [SerializeField] NetworkHeartsPlayer[] seats = new NetworkHeartsPlayer[HeartsRules.PlayerCount];

        readonly HeartsMatch _match = new HeartsMatch();
        float _autoPlayAt = -1f;
        float _confirmTrickAt = -1f;
        int _dealtRound = -1;

        [Networked] public int Phase { get; set; }
        [Networked] public int CurrentPlayer { get; set; }
        [Networked] public int TrickNumber { get; set; }
        [Networked] public NetworkBool HeartsBroken { get; set; }
        [Networked] public int RoundIndex { get; set; }
        [Networked] public int LastTrickWinner { get; set; }
        [Networked] public int Loser { get; set; }
        [Networked] public int ConnectedCount { get; set; }
        [Networked] public NetworkString<_64> Status { get; set; }
        [Networked, Capacity(4)] public NetworkArray<int> RoundScores => default;
        [Networked, Capacity(4)] public NetworkArray<int> TotalScores => default;
        [Networked, Capacity(4)] public NetworkArray<int> TrickCards => default;

        public NetworkHeartsPlayer[] Seats => seats;

        public override void Spawned()
        {
            LastTrickWinner = -1;
            Loser = -1;
            for (int i = 0; i < 4; i++)
                TrickCards.Set(i, -1);
            if (HasStateAuthority)
                Status = "Waiting for four players";
        }

        public void ApplyProfile(int seat, string playerName, int avatarIndex)
        {
            if (!HasStateAuthority || seat < 0 || seat >= seats.Length || seats[seat] == null)
                return;
            seats[seat].DisplayName = Sanitize(playerName);
            seats[seat].AvatarIndex = Mathf.Clamp(avatarIndex, 0, 12);
        }

        public void ServerSeatJoined(NetworkHeartsPlayer seat)
        {
            if (!HasStateAuthority || seat == null)
                return;
            seat.Occupied = true;
            seat.Connected = true;
            RefreshCount();
            if (ConnectedCount >= HeartsRules.PlayerCount && Phase == (int)MatchPhase.Waiting)
                StartRound();
            else if (seat.CardsRemaining > 0 && _match.Phase != MatchPhase.Waiting)
                SendHand(seat.Seat);
        }

        public void ServerSeatLeft(int seat)
        {
            if (!HasStateAuthority || seat < 0 || seat >= seats.Length || seats[seat] == null)
                return;
            seats[seat].Connected = false;
            RefreshCount();
            Status = seats[seat].DisplayName + " disconnected";
            _autoPlayAt = Time.unscaledTime + 1f;
        }

        public void SubmitPass(NetworkHeartsPlayer player, int first, int second, int third, int requestId)
        {
            if (!HasStateAuthority || player == null || !player.AcceptRequest(requestId))
                return;
            var cards = new List<CardId>
            {
                CardId.FromIndex(first),
                CardId.FromIndex(second),
                CardId.FromIndex(third)
            };
            if (!_match.TryPass(player.Seat, cards, out string error))
            {
                player.RpcReject(error);
                return;
            }

            Publish();
            if (_match.Phase == MatchPhase.Playing)
            {
                Status = "Cards passed left";
                SendAllHands();
            }
        }

        public void SubmitPlay(NetworkHeartsPlayer player, int cardIndex, int requestId)
        {
            if (!HasStateAuthority || player == null || !player.AcceptRequest(requestId))
                return;
            if (cardIndex < 0 || cardIndex > 51)
            {
                player.RpcReject("Unknown card.");
                return;
            }
            if (!_match.TryPlay(player.Seat, CardId.FromIndex(cardIndex), out string error))
            {
                player.RpcReject(error);
                return;
            }

            Publish();
            SendAllHands();
            if (_match.AwaitingTrickConfirm)
            {
                Status = SeatName(_match.LastTrickWinner) + " takes the trick";
                _confirmTrickAt = Time.unscaledTime + 1.25f;
            }
            else if (_match.Phase == MatchPhase.RoundEnd)
                Status = "Round complete";
            else if (_match.Phase == MatchPhase.MatchOver)
                Status = "Match over";
        }

        public void RequestNextRound()
        {
            if (!HasStateAuthority)
                return;
            if (!_match.TryContinue(out string error))
            {
                Status = error;
                return;
            }
            StartRound();
        }

        void Update()
        {
            if (Object == null || !Object.IsValid || !HasStateAuthority)
                return;
            if (_confirmTrickAt >= 0f && Time.unscaledTime >= _confirmTrickAt)
            {
                _confirmTrickAt = -1f;
                if (_match.TryConfirmTrick(out _))
                {
                    Publish();
                    SendAllHands();
                    if (_match.Phase == MatchPhase.RoundEnd)
                        Status = "Round complete";
                    else if (_match.Phase == MatchPhase.MatchOver)
                        Status = "Match over";
                    else if (!seats[_match.CurrentPlayer].Connected)
                        _autoPlayAt = Time.unscaledTime + 0.75f;
                }
            }

            if (_autoPlayAt < 0f || Time.unscaledTime < _autoPlayAt)
                return;
            _autoPlayAt = -1f;
            AutoActForDisconnected();
        }

        void StartRound()
        {
            int seed = Random.Range(1, int.MaxValue);
            if (!_match.TryStartRound(seed, out string error))
            {
                Status = error;
                return;
            }
            _dealtRound++;
            Publish();
            SendAllHands();
            Status = "Pass three cards to the left";
        }

        void AutoActForDisconnected()
        {
            if (_match.Phase == MatchPhase.Passing)
            {
                for (int i = 0; i < seats.Length; i++)
                {
                    if (seats[i] == null || seats[i].Connected || _match.PassSubmitted(i))
                        continue;
                    var hand = _match.Hand(i);
                    if (hand.Count < 3)
                        continue;
                    _match.TryPass(i, new List<CardId> { hand[0], hand[1], hand[2] }, out _);
                }
                Publish();
                if (_match.Phase == MatchPhase.Playing)
                    SendAllHands();
                return;
            }

            if (_match.Phase != MatchPhase.Playing)
                return;
            int seat = _match.CurrentPlayer;
            if (seat < 0 || seats[seat] == null || seats[seat].Connected)
                return;
            var legal = _match.LegalCardsFor(seat);
            if (legal.Count == 0)
                return;
            _match.TryPlay(seat, legal[0], out _);
            Publish();
            SendAllHands();
            if (_match.Phase == MatchPhase.Playing && !seats[_match.CurrentPlayer].Connected)
                _autoPlayAt = Time.unscaledTime + 0.75f;
        }

        void SendAllHands()
        {
            for (int i = 0; i < HeartsRules.PlayerCount; i++)
                SendHand(i);
        }

        void SendHand(int seat)
        {
            if (seats[seat] == null)
                return;
            var hand = _match.Hand(seat);
            var builder = new StringBuilder();
            for (int i = 0; i < hand.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append(hand[i].Index);
            }
            seats[seat].CardsRemaining = hand.Count;
            seats[seat].RpcReceiveHand(builder.ToString());
        }

        void Publish()
        {
            Phase = (int)_match.Phase;
            CurrentPlayer = _match.CurrentPlayer;
            TrickNumber = _match.TrickNumber;
            HeartsBroken = _match.HeartsBroken;
            RoundIndex = _match.RoundIndex;
            LastTrickWinner = _match.LastTrickWinner;
            Loser = _match.Loser;
            for (int i = 0; i < 4; i++)
            {
                RoundScores.Set(i, _match.RoundScores[i]);
                TotalScores.Set(i, _match.TotalScores[i]);
                CardId? card = _match.TrickCard(i);
                int index = card.HasValue ? card.Value.Index : -1;
                TrickCards.Set(i, index);
                if (seats[i] != null)
                    seats[i].CardsRemaining = _match.Hand(i).Count;
            }
            RefreshCount();
        }

        void RefreshCount()
        {
            int count = 0;
            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i] != null && seats[i].Occupied && seats[i].Connected)
                    count++;
            }
            ConnectedCount = count;
        }

        string SeatName(int seat)
        {
            if (seat < 0 || seat >= seats.Length || seats[seat] == null)
                return "Player";
            string name = seats[seat].DisplayName.ToString();
            return string.IsNullOrEmpty(name) ? "Player " + (seat + 1) : name;
        }

        static string Sanitize(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
                return "Player";
            playerName = playerName.Trim();
            return playerName.Length > 24 ? playerName.Substring(0, 24) : playerName;
        }
    }
}
