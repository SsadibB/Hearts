using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Hearts.Networking
{
    public class NetworkHeartsPlayer : NetworkBehaviour
    {
        [SerializeField] int seatIndex;

        readonly List<int> _localCards = new List<int>(13);
        int _requestId = 1;
        int _lastAcceptedRequest;

        [Networked] public NetworkString<_32> DisplayName { get; set; }
        [Networked] public int AvatarIndex { get; set; }
        [Networked] public NetworkBool Occupied { get; set; }
        [Networked] public NetworkBool Connected { get; set; }
        [Networked] public int CardsRemaining { get; set; }
        [Networked] public int Seat { get; set; }

        public IReadOnlyList<int> LocalCards => _localCards;
        public event Action HandChanged;
        public event Action<string> MoveRejected;

        public override void Spawned()
        {
            if (HasStateAuthority)
                Seat = seatIndex;
        }

        public void SetLocalProfile(string playerName, int avatarIndex)
        {
            if (!HasInputAuthority)
                return;
            RpcSetProfile(playerName ?? "Player", avatarIndex);
        }

        public void SubmitPass(int first, int second, int third)
        {
            if (!HasInputAuthority)
                return;
            RpcSubmitPass(first, second, third, _requestId++);
        }

        public void SubmitPlay(int cardIndex)
        {
            if (!HasInputAuthority)
                return;
            RpcSubmitPlay(cardIndex, _requestId++);
        }

        public bool AcceptRequest(int requestId)
        {
            if (requestId <= _lastAcceptedRequest)
                return false;
            _lastAcceptedRequest = requestId;
            return true;
        }

        public void ReplaceLocalHand(string packed)
        {
            _localCards.Clear();
            if (!string.IsNullOrEmpty(packed))
            {
                string[] parts = packed.Split(',');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (int.TryParse(parts[i], out int index))
                        _localCards.Add(index);
                }
            }
            HandChanged?.Invoke();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        void RpcSetProfile(string playerName, int avatarIndex)
        {
            var match = FindFirstObjectByType<NetworkHeartsMatch>();
            if (match != null)
                match.ApplyProfile(Seat, playerName, avatarIndex);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        void RpcSubmitPass(int first, int second, int third, int requestId)
        {
            var match = FindFirstObjectByType<NetworkHeartsMatch>();
            if (match != null)
                match.SubmitPass(this, first, second, third, requestId);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        void RpcSubmitPlay(int cardIndex, int requestId)
        {
            var match = FindFirstObjectByType<NetworkHeartsMatch>();
            if (match != null)
                match.SubmitPlay(this, cardIndex, requestId);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        public void RpcReceiveHand(string packed)
        {
            ReplaceLocalHand(packed);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        public void RpcReject(string reason)
        {
            MoveRejected?.Invoke(reason);
        }
    }
}
