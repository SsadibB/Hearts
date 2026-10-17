using System;
using System.Collections.Generic;

namespace Hearts.Gameplay
{
    public interface IHeartsTable
    {
        bool IsActive { get; }
        int Phase { get; }
        int CurrentPlayer { get; }
        int LastTrickWinner { get; }
        int RoundIndex { get; }
        int Loser { get; }
        int LocalSeat { get; }
        string Status { get; }
        bool CanRequestNextRound { get; }
        bool CanRestartMatch { get; }
        IReadOnlyList<int> LocalCards { get; }
        int TrickCard(int seat);
        int RoundScore(int seat);
        int TotalScore(int seat);
        int LoggedRounds { get; }
        int LoggedScore(int round, int seat);
        string PlayerName(int seat);
        int AvatarIndex(int seat);
        int CardsRemaining(int seat);
        bool IsConnected(int seat);
        event Action HandChanged;
        event Action<string> MoveRejected;
        void SubmitPass(int first, int second, int third);
        void SubmitPlay(int cardIndex);
        void RequestNextRound();
        void RestartMatch();
        void FinishTrickPresentation();
    }
}
