using System;
using System.Collections.Generic;
using System.Text;
using Hearts.Core;
using Hearts.Gameplay;
using Hearts.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Hearts.Presentation
{
    public class HeartsTablePresenter : MonoBehaviour
    {
        [SerializeField] CardView[] cards;
        [SerializeField] PlayerHudView[] screenHuds;
        [SerializeField] RectTransform[] screenAreas;
        [SerializeField] RectTransform handArea;
        [SerializeField] RectTransform trickArea;
        [SerializeField] RectTransform deckPoint;
        [SerializeField] RectTransform cardsRoot;
        [SerializeField] CardAnimationController animations;
        [SerializeField] SinglePlayerMatch singlePlayer;
        [SerializeField] NetworkTableSource networkTable;
        [SerializeField] GameUiController ui;
        [SerializeField] LeaderboardView leaderboard;
        [SerializeField] Text tableStatus;

        readonly HashSet<int> _selected = new HashSet<int>();
        readonly HashSet<int> _won = new HashSet<int>();
        readonly HashSet<int> _pulled = new HashSet<int>();
        readonly List<int>[] _fans =
        {
            new List<int>(),
            new List<int>(),
            new List<int>(),
            new List<int>()
        };
        bool _fansReady;
        readonly List<int> _trickPlay = new List<int>();
        readonly int[] _shownTrick = { -2, -2, -2, -2 };
        readonly Vector2[] _shownPos = new Vector2[52];
        readonly float[] _shownRot = new float[52];
        readonly bool[] _shownFace = new bool[52];
        readonly int[] _drawOrder = new int[52];
        readonly int[] _drawPriority = new int[52];
        TableMetrics _metrics;
        RectTransform _canvasRect;
        bool _metricsReady;
        readonly RectTransform[] _handContainers = new RectTransform[4];
        string _handKey = "";
        int _phase = -2;
        IHeartsTable _hooked;
        bool _dealing;
        bool _passAnimating;
        bool _clearingTrick;
        bool _cardsHidden = true;
        int _visualVersion;

        void Awake()
        {
            if (cards == null)
                return;
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null)
                    continue;
                cards[i].SetInteractable(false);
                cards[i].gameObject.SetActive(false);
                _shownPos[i] = new Vector2(float.NaN, float.NaN);
                _shownRot[i] = float.NaN;
            }
            if (cardsRoot != null)
                cardsRoot.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            HookCards();
            if (ui != null && ui.PassButton != null)
                ui.PassButton.onClick.AddListener(SubmitPass);
            if (ui != null && ui.NextRoundButton != null)
                ui.NextRoundButton.onClick.AddListener(RequestNextRound);
        }

        void OnDisable()
        {
            UnhookCards();
            if (ui != null && ui.PassButton != null)
                ui.PassButton.onClick.RemoveListener(SubmitPass);
            if (ui != null && ui.NextRoundButton != null)
                ui.NextRoundButton.onClick.RemoveListener(RequestNextRound);
            Unsubscribe();
        }

        void HookCards()
        {
            if (cards == null)
                return;
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null)
                    continue;
                cards[i].Clicked -= OnCardClicked;
                cards[i].Clicked += OnCardClicked;
            }
        }

        void UnhookCards()
        {
            if (cards == null)
                return;
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null)
                    continue;
                cards[i].Clicked -= OnCardClicked;
            }
        }

        void Update()
        {
            bool resized = EnsureMetrics();
            IHeartsTable table = ActiveTable();
            if (!ReferenceEquals(table, _hooked))
            {
                Unsubscribe();
                _hooked = table;
                Subscribe();
                _handKey = "";
                _phase = -2;
            }

            if (table == null)
            {
                if (ui != null)
                    ui.ShowTable(false);
                HideCards();
                return;
            }

            bool inMatch = table.Phase != (int)MatchPhase.Waiting;
            if (ui != null)
                ui.ShowTable(inMatch);

            string handKey = HandKey(table);
            bool trickChanged = false;
            for (int i = 0; i < 4; i++)
            {
                if (_shownTrick[i] != table.TrickCard(i))
                    trickChanged = true;
            }

            if (resized && !_dealing && !_passAnimating)
                _handKey = "";

            bool newPass = table.Phase == (int)MatchPhase.Passing && _phase != (int)MatchPhase.Passing;
            if (newPass)
            {
                _handKey = handKey;
                _phase = table.Phase;
                for (int i = 0; i < 4; i++)
                    _shownTrick[i] = table.TrickCard(i);
                _selected.Clear();
                BeginDeal(table);
            }
            else if (!_clearingTrick && !_dealing && !_passAnimating && (handKey != _handKey || trickChanged || table.Phase != _phase))
            {
                _handKey = handKey;
                _phase = table.Phase;
                for (int i = 0; i < 4; i++)
                    _shownTrick[i] = table.TrickCard(i);
                if (trickChanged)
                    _selected.Clear();
                LayoutCards(table);
            }

            if (!_dealing && !_passAnimating)
                TryBeginTrickClear(table);

            RefreshHuds(table);
            RefreshButtons(table);
            RefreshClicks(table);
        }

        IHeartsTable ActiveTable()
        {
            if (singlePlayer != null && singlePlayer.IsActive)
                return singlePlayer;
            if (networkTable != null && networkTable.IsActive)
                return networkTable;
            return null;
        }

        void Subscribe()
        {
            if (_hooked == null)
                return;
            _hooked.HandChanged += OnHandChanged;
            _hooked.MoveRejected += OnRejected;
        }

        void Unsubscribe()
        {
            if (_hooked == null)
                return;
            _hooked.HandChanged -= OnHandChanged;
            _hooked.MoveRejected -= OnRejected;
            _hooked = null;
        }

        void OnHandChanged()
        {
            _handKey = "";
            _selected.Clear();
        }

        void OnRejected(string reason)
        {
            if (tableStatus != null)
                tableStatus.text = reason;
        }

        void OnCardClicked(CardView card)
        {
            var table = ActiveTable();
            if (table == null || _dealing || _passAnimating || (animations != null && animations.InputLocked))
                return;
            if (table.Phase == (int)MatchPhase.Waiting)
                return;

            if (table.Phase == (int)MatchPhase.Passing)
            {
                if (!IsInLocalHand(table, card.CardIndex))
                    return;
                if (_selected.Contains(card.CardIndex))
                    _selected.Remove(card.CardIndex);
                else if (_selected.Count < HeartsRules.PassCount)
                    _selected.Add(card.CardIndex);
                LayoutCards(table);
                return;
            }

            if (table.Phase == (int)MatchPhase.Playing && table.CurrentPlayer == table.LocalSeat)
                table.SubmitPlay(card.CardIndex);
        }

        void SubmitPass()
        {
            var table = ActiveTable();
            if (table == null || _dealing || _passAnimating)
                return;
            if (_selected.Count != HeartsRules.PassCount)
            {
                if (tableStatus != null)
                    tableStatus.text = "Select 3 cards";
                return;
            }
            if (animations != null && animations.InputLocked)
                return;

            var picked = new List<int>(_selected);
            var order = new Dictionary<int, int>();
            for (int i = 0; i < table.LocalCards.Count; i++)
                order[table.LocalCards[i]] = i;
            picked.Sort((a, b) => order[a].CompareTo(order[b]));
            if (ui != null)
                ui.ShowPassArrow(false, false);
            if (animations == null)
            {
                _selected.Clear();
                table.SubmitPass(picked[0], picked[1], picked[2]);
                return;
            }

            _passAnimating = true;
            int version = _visualVersion;
            int pending = picked.Count;
            for (int i = 0; i < picked.Count; i++)
            {
                CardView view = cards[picked[i]];
                int passScreen = ScreenOf(HeartsRules.NextSeat(table.LocalSeat), table.LocalSeat);
                Vector2 target = FanPosition(passScreen, i, picked.Count);
                float delay = i * 0.06f;
                animations.Move(view.Rect, target, true, () =>
                {
                    if (version != _visualVersion)
                        return;
                    pending--;
                    if (pending > 0)
                        return;
                    _passAnimating = false;
                    _selected.Clear();
                    table.SubmitPass(picked[0], picked[1], picked[2]);
                }, delay, Facing(passScreen));
            }
        }

        void RequestNextRound()
        {
            var table = ActiveTable();
            if (table != null)
                table.RestartMatch();
        }

        void LayoutCards(IHeartsTable table)
        {
            HookCards();
            EnsureHandContainers();
            var localSet = new HashSet<int>();
            for (int i = 0; i < table.LocalCards.Count; i++)
                localSet.Add(table.LocalCards[i]);

            var trickSeat = new Dictionary<int, int>();
            for (int seat = 0; seat < 4; seat++)
            {
                int index = table.TrickCard(seat);
                if (index >= 0)
                    trickSeat[index] = seat;
            }

            if (table.Phase == (int)MatchPhase.Playing)
                PrepareHands(table, localSet, trickSeat);
            SyncTrickPlay(trickSeat);

            int localSeat = table.LocalSeat;
            var visualSeat = new Dictionary<int, int>();
            var fanCount = new int[4];
            var fanIndex = new Dictionary<int, int>();
            if (_fansReady)
            {
                for (int seat = 0; seat < 4; seat++)
                {
                    if (seat == localSeat)
                        continue;
                    fanCount[ScreenOf(seat, localSeat)] = _fans[seat].Count;
                    for (int n = 0; n < _fans[seat].Count; n++)
                    {
                        visualSeat[_fans[seat][n]] = seat;
                        fanIndex[_fans[seat][n]] = n;
                    }
                }
            }
            else
            {
                var spare = new List<int>();
                for (int i = 0; i < cards.Length; i++)
                {
                    if (!localSet.Contains(i) && !trickSeat.ContainsKey(i) && !_won.Contains(i))
                        spare.Add(i);
                }

                int spareIndex = 0;
                for (int seat = 0; seat < 4; seat++)
                {
                    if (seat == localSeat)
                        continue;
                    int count = table.CardsRemaining(seat);
                    int screen = ScreenOf(seat, localSeat);
                    fanCount[screen] = count;
                    for (int n = 0; n < count && spareIndex < spare.Count; n++)
                    {
                        int index = spare[spareIndex++];
                        visualSeat[index] = seat;
                        fanIndex[index] = n;
                    }
                }
            }

            var handOrder = new List<int>();
            for (int i = 0; i < table.LocalCards.Count; i++)
                handOrder.Add(table.LocalCards[i]);

            int draw = 0;
            for (int i = 0; i < cards.Length; i++)
            {
                CardView view = cards[i];
                if (view == null || _won.Contains(i))
                {
                    if (view != null)
                        view.gameObject.SetActive(false);
                    continue;
                }

                bool face = false;
                bool critical = false;
                bool liftOnly = false;
                float rotation = 0f;
                float duration = 0.65f;
                int priority = draw;
                Vector2 target = DeckPosition(i);
                Vector2 size = _metricsReady ? _metrics.cardSize : view.Rect.sizeDelta;
                RectTransform home = cardsRoot;
                bool inHand = false;

                if (trickSeat.TryGetValue(i, out int playedSeat))
                {
                    face = true;
                    critical = playedSeat == localSeat;
                    int screen = ScreenOf(playedSeat, localSeat);
                    int playIndex = _trickPlay.IndexOf(i);
                    if (playIndex < 0)
                        playIndex = 0;
                    target = TrickPosition(screen);
                    rotation = TableLayout.TrickRotation(screen);
                    duration = 1.05f;
                    priority = 400 + playIndex;
                }
                else if (localSet.Contains(i))
                {
                    face = true;
                    int order = handOrder.IndexOf(i);
                    bool selected = _selected.Contains(i);
                    size = _metricsReady ? _metrics.localCardSize : size;
                    target = HandLocal(0, order, handOrder.Count, selected);
                    duration = 0.65f;
                    priority = 200 + order;
                    home = HandContainer(0);
                    inHand = true;
                    bool sameX = !float.IsNaN(_shownPos[i].x) && Mathf.Abs(_shownPos[i].x - target.x) <= 0.5f;
                    bool sameRot = !float.IsNaN(_shownRot[i]) && Mathf.Abs(Mathf.DeltaAngle(_shownRot[i], 0f)) <= 0.75f;
                    liftOnly = sameX && sameRot && (float.IsNaN(_shownPos[i].y) || Mathf.Abs(_shownPos[i].y - target.y) > 0.5f);
                }
                else if (visualSeat.TryGetValue(i, out int owner))
                {
                    int screen = ScreenOf(owner, localSeat);
                    int count = Mathf.Max(1, fanCount[screen]);
                    target = HandLocal(screen, fanIndex[i], count, false);
                    rotation = FanRotation(screen, fanIndex[i], count);
                    duration = 0.65f;
                    priority = 100 + screen * 20 + fanIndex[i];
                    home = HandContainer(screen);
                    inHand = true;
                }

                bool staying = home != null && view.Rect.parent == home;
                liftOnly = liftOnly && staying;
                if (home != null && view.Rect.parent != home)
                    view.Rect.SetParent(home, true);
                Vector2 localTarget = inHand ? target : ToParent(home, target);

                if (_metricsReady)
                    view.Rect.sizeDelta = size;
                view.SetSelected(_selected.Contains(i));
                view.SetInteractable(CanClick(table, view.CardIndex));

                bool moved = float.IsNaN(_shownPos[i].x) || Vector2.Distance(_shownPos[i], target) > 0.5f;
                bool turned = float.IsNaN(_shownRot[i]) || Mathf.Abs(Mathf.DeltaAngle(_shownRot[i], rotation)) > 0.75f;
                bool reveal = face && localSet.Contains(i) && !_shownFace[i];
                _shownPos[i] = target;
                _shownRot[i] = rotation;
                _shownFace[i] = face;
                _drawOrder[draw] = i;
                _drawPriority[draw] = priority;
                if (reveal && animations != null)
                {
                    view.ShowFace(false);
                    animations.Move(view.Rect, localTarget, true, () => animations.Flip(view.Rect, () => view.ShowFace(true), null), 0f, rotation, duration);
                }
                else if (liftOnly && animations != null)
                {
                    view.ShowFace(face);
                    _shownPos[i] = target;
                    animations.MoveY(view.Rect, localTarget.y);
                }
                else if ((moved || turned) && animations != null)
                {
                    view.ShowFace(face);
                    animations.Move(view.Rect, localTarget, critical, null, 0f, rotation, duration);
                }
                else if (moved || turned)
                {
                    view.ShowFace(face);
                    view.Rect.anchoredPosition = localTarget;
                    view.Rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
                }
                else
                    view.ShowFace(face);
                draw++;
            }

            SortCardDrawOrder(draw);
        }

        void RefreshHuds(IHeartsTable table)
        {
            int localSeat = table.LocalSeat;
            string[] names = new string[4];
            var sprites = new Sprite[4];
            int[] round = new int[4];
            int[] total = new int[4];
            for (int seat = 0; seat < 4; seat++)
            {
                string playerName = table.PlayerName(seat);
                if (string.IsNullOrEmpty(playerName))
                    playerName = "Waiting";
                int screen = ScreenOf(seat, localSeat);
                bool turn = table.Phase == (int)MatchPhase.Playing && table.CurrentPlayer == seat;
                string status = turn ? "Your turn" : table.Phase == (int)MatchPhase.Passing ? "Passing" : "";
                if (screenHuds != null && screen < screenHuds.Length && screenHuds[screen] != null)
                {
                    Sprite avatar = ui != null ? ui.Avatar(table.AvatarIndex(seat)) : null;
                    screenHuds[screen].Show(playerName, avatar, table.CardsRemaining(seat), table.RoundScore(seat), table.TotalScore(seat), status, turn, table.IsConnected(seat), screen);
                }
                names[seat] = playerName;
                sprites[seat] = ui != null ? ui.Avatar(table.AvatarIndex(seat)) : null;
                round[seat] = table.RoundScore(seat);
                total[seat] = table.TotalScore(seat);
            }

            if (tableStatus != null)
                tableStatus.text = table.Status;

            bool roundEnd = table.Phase == (int)MatchPhase.RoundEnd;
            bool over = table.Phase == (int)MatchPhase.MatchOver;
            if (ui != null)
            {
                ui.ShowLeaderboard(roundEnd || over);
                if (over)
                {
                    int loser = Mathf.Max(0, table.Loser);
                    string loserName = table.Loser >= 0 && table.Loser < names.Length ? names[table.Loser] : "A player";
                    ui.ShowGameOver(loserName + " reached " + table.TotalScore(loser) + " and loses the match.");
                }
                else
                {
                    ui.HideGameOver();
                }
            }
            if ((roundEnd || over) && leaderboard != null)
                leaderboard.ShowRound(over ? "Final scores" : "Round " + table.RoundIndex, names, sprites, round, total);
        }

        void RefreshClicks(IHeartsTable table)
        {
            if (cards == null || _dealing || _passAnimating)
                return;
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null)
                    continue;
                cards[i].SetInteractable(CanClick(table, cards[i].CardIndex));
            }
        }

        bool CanClick(IHeartsTable table, int cardIndex)
        {
            if (table == null || _dealing || _passAnimating || _clearingTrick)
                return false;
            if (animations != null && animations.InputLocked)
                return false;
            if (!IsInLocalHand(table, cardIndex))
                return false;
            if (table.Phase == (int)MatchPhase.Passing)
                return true;
            return table.Phase == (int)MatchPhase.Playing && table.CurrentPlayer == table.LocalSeat;
        }

        void RefreshButtons(IHeartsTable table)
        {
            if (ui != null)
                ui.ShowPassArrow(table.Phase == (int)MatchPhase.Passing && !_dealing && !_passAnimating, _selected.Count == HeartsRules.PassCount);
            if (ui != null && ui.NextRoundButton != null)
            {
                ui.NextRoundButton.gameObject.SetActive(table.CanRestartMatch);
                var label = ui.NextRoundButton.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = "Play Again";
            }
        }

        static bool IsInLocalHand(IHeartsTable table, int cardIndex)
        {
            for (int i = 0; i < table.LocalCards.Count; i++)
            {
                if (table.LocalCards[i] == cardIndex)
                    return true;
            }
            return false;
        }

        static string HandKey(IHeartsTable table)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < table.LocalCards.Count; i++)
            {
                builder.Append(table.LocalCards[i]);
                builder.Append(',');
            }
            return builder.ToString();
        }

        void HideCards()
        {
            if (_cardsHidden)
                return;
            ClearCollected();
            _cardsHidden = true;
            _visualVersion++;
            _dealing = false;
            _passAnimating = false;
            _clearingTrick = false;
            _phase = -2;
            _handKey = "";
            if (cardsRoot != null)
                cardsRoot.gameObject.SetActive(false);
            if (ui != null)
                ui.ShowPassArrow(false, false);
        }

        void BeginDeal(IHeartsTable table)
        {
            ClearCollected();
            _dealing = true;
            _passAnimating = false;
            _clearingTrick = false;
            _cardsHidden = false;
            _visualVersion++;
            int version = _visualVersion;
            if (cardsRoot != null)
                cardsRoot.gameObject.SetActive(true);
            if (ui != null)
                ui.ShowPassArrow(false, false);

            var localOrder = new List<int>();
            var localSet = new HashSet<int>();
            for (int i = 0; i < table.LocalCards.Count; i++)
            {
                localOrder.Add(table.LocalCards[i]);
                localSet.Add(table.LocalCards[i]);
            }

            for (int i = 0; i < cards.Length; i++)
            {
                CardView view = cards[i];
                if (view == null)
                    continue;
                view.gameObject.SetActive(true);
                view.ShowFace(false);
                view.SetInteractable(false);
                view.SetSelected(false);
                if (cardsRoot != null && view.Rect.parent != cardsRoot)
                    view.Rect.SetParent(cardsRoot, false);
                if (animations != null)
                    animations.ResetCard(view.Rect);
                else
                    view.Rect.localScale = Vector3.one;
                view.Rect.localRotation = Quaternion.identity;
                if (_metricsReady)
                    view.Rect.sizeDelta = localSet.Contains(i) ? _metrics.localCardSize : _metrics.cardSize;
                _shownRot[i] = 0f;
                Vector2 deck = DeckPosition(i);
                view.Rect.anchoredPosition = deck;
                _shownPos[i] = deck;
                _shownFace[i] = false;
            }

            PlaceOpponents(table, localSet);
            if (localOrder.Count == 0 || animations == null)
            {
                for (int i = 0; i < localOrder.Count; i++)
                {
                    RectTransform hand = HandContainer(0);
                    if (hand != null)
                        cards[localOrder[i]].Rect.SetParent(hand, false);
                    cards[localOrder[i]].Rect.anchoredPosition = HandLocal(0, i, localOrder.Count, false);
                    cards[localOrder[i]].ShowFace(true);
                    _shownFace[localOrder[i]] = true;
                }
                FinishDeal(table);
                return;
            }

            int pending = localOrder.Count;
            for (int order = 0; order < localOrder.Count; order++)
            {
                int index = localOrder[order];
                Vector2 target = HandLocal(0, order, localOrder.Count, false);
                RectTransform hand = HandContainer(0);
                if (hand != null)
                    cards[index].Rect.SetParent(hand, true);
                _shownPos[index] = target;
                float delay = order * 0.045f;
                animations.Move(cards[index].Rect, target, true, () =>
                {
                    if (version != _visualVersion)
                        return;
                    pending--;
                    if (pending == 0)
                        FlipDealt(localOrder, version);
                }, delay);
            }
        }

        void FinishDeal(IHeartsTable table)
        {
            _dealing = false;
            if (table != null)
                LayoutCards(table);
        }

        void FlipDealt(List<int> localOrder, int version)
        {
            if (version != _visualVersion)
                return;
            int pending = localOrder.Count;
            for (int order = 0; order < localOrder.Count; order++)
            {
                CardView view = cards[localOrder[order]];
                int index = localOrder[order];
                float delay = order * 0.035f;
                animations.Flip(view.Rect, () =>
                {
                    if (version != _visualVersion)
                        return;
                    view.ShowFace(true);
                    _shownFace[index] = true;
                }, () =>
                {
                    if (version != _visualVersion)
                        return;
                    pending--;
                    if (pending == 0)
                        FinishDeal(ActiveTable());
                }, delay);
            }
        }

        void PlaceOpponents(IHeartsTable table, HashSet<int> localSet)
        {
            var spare = new List<int>();
            for (int i = 0; i < cards.Length; i++)
            {
                if (!localSet.Contains(i) && !_won.Contains(i))
                    spare.Add(i);
            }

            int spareIndex = 0;
            int localSeat = table.LocalSeat;
            for (int seat = 0; seat < 4; seat++)
            {
                if (seat == localSeat)
                    continue;
                int count = table.CardsRemaining(seat);
                for (int n = 0; n < count && spareIndex < spare.Count; n++)
                {
                    int index = spare[spareIndex++];
                    int screen = ScreenOf(seat, localSeat);
                    Vector2 target = HandLocal(screen, n, count, false);
                    float rotation = FanRotation(screen, n, count);
                    RectTransform hand = HandContainer(screen);
                    if (hand != null)
                        cards[index].Rect.SetParent(hand, true);
                    _shownPos[index] = target;
                    _shownRot[index] = rotation;
                    if (_metricsReady)
                        cards[index].Rect.sizeDelta = _metrics.cardSize;
                    if (animations != null)
                        animations.Move(cards[index].Rect, target, false, null, n * 0.02f, rotation);
                    else
                    {
                        cards[index].Rect.anchoredPosition = target;
                        cards[index].Rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
                    }
                }
            }
        }

        static int ScreenOf(int seat, int localSeat) => (seat - localSeat + 4) % 4;

        static float Facing(int screen)
        {
            if (screen == 1)
                return TableLayout.LeftRotation;
            if (screen == 3)
                return TableLayout.RightRotation;
            return 0f;
        }

        static float FanRotation(int screen, int index, int count)
        {
            float along = count <= 1 ? 0f : (index / (float)(count - 1)) * 2f - 1f;
            if (screen == 1)
                return TableLayout.LeftRotation + along * 10f;
            if (screen == 3)
                return TableLayout.RightRotation - along * 10f;
            if (screen == 2)
                return -along * 8f;
            return 0f;
        }

        Vector2 HandLocal(int screen, int index, int count, bool selected)
        {
            int safeCount = Mathf.Max(1, count);
            int safeIndex = Mathf.Clamp(index, 0, safeCount - 1);
            float along = safeCount <= 1 ? 0f : (safeIndex / (float)(safeCount - 1)) * 2f - 1f;
            float center = (safeCount - 1) * 0.5f;
            Vector2 card = _metricsReady ? _metrics.cardSize : new Vector2(112f, 157f);
            Vector2 local = _metricsReady ? _metrics.localCardSize : new Vector2(134f, 188f);
            if (screen == 0)
            {
                float step = Mathf.Min(local.x * 0.48f, 78f);
                float lift = selected ? (_metricsReady ? _metrics.selectionLift : 28f) : 0f;
                return new Vector2((safeIndex - center) * step, lift);
            }

            if (screen == 2)
            {
                float step = Mathf.Min(card.x * 0.42f, 52f);
                float arc = card.y * 0.12f;
                return new Vector2((safeIndex - center) * step, -arc * (1f - along * along));
            }

            float sideStep = Mathf.Min(card.x * 0.48f, 40f);
            float bow = card.x * 0.16f * (1f - along * along);
            float x = screen == 1 ? bow : -bow;
            return new Vector2(x, (safeIndex - center) * sideStep);
        }

        Vector2 HandPosition(int order, int count, bool selected)
        {
            if (!_metricsReady)
                return DeckPosition(order);
            return ToCards(TableLayout.HandPoint(_metrics, order, count, selected));
        }

        void TryBeginTrickClear(IHeartsTable table)
        {
            if (_clearingTrick || table == null || cards == null)
                return;
            if (table.LastTrickWinner < 0)
                return;

            var taken = new int[4];
            for (int seat = 0; seat < 4; seat++)
            {
                int index = table.TrickCard(seat);
                if (index < 0 || index >= cards.Length || cards[index] == null)
                    return;
                int screen = ScreenOf(seat, table.LocalSeat);
                Vector2 target = TrickPosition(screen);
                if (Vector2.Distance(cards[index].Rect.anchoredPosition, target) > 12f)
                    return;
                taken[seat] = index;
            }

            BeginTrickClear(table, taken);
        }

        void BeginTrickClear(IHeartsTable table, int[] taken)
        {
            if (_clearingTrick || table == null)
                return;
            int winner = table.LastTrickWinner;
            if (winner < 0)
                return;

            _clearingTrick = true;
            int version = _visualVersion;
            int screen = ScreenOf(winner, table.LocalSeat);
            Vector2 dest = AvatarPoint(screen);
            Transform tuck = screenHuds != null && screen < screenHuds.Length && screenHuds[screen] != null
                ? screenHuds[screen].transform
                : null;
            int pending = 4;
            if (animations == null)
            {
                for (int i = 0; i < 4; i++)
                {
                    _won.Add(taken[i]);
                    cards[taken[i]].gameObject.SetActive(false);
                }
                _clearingTrick = false;
                _trickPlay.Clear();
                table.FinishTrickPresentation();
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                int index = taken[i];
                CardView view = cards[index];
                view.SetInteractable(false);
                animations.Collect(view.Rect, dest, tuck, () =>
                {
                    if (version != _visualVersion)
                        return;
                    _won.Add(index);
                    view.gameObject.SetActive(false);
                    pending--;
                    if (pending > 0)
                        return;
                    _clearingTrick = false;
                    _trickPlay.Clear();
                    table.FinishTrickPresentation();
                }, i * 0.04f);
            }
        }

        Vector2 AvatarPoint(int screen)
        {
            if (screenHuds != null && screen >= 0 && screen < screenHuds.Length && screenHuds[screen] != null)
            {
                RectTransform avatar = screenHuds[screen].AvatarRect;
                if (avatar != null && cardsRoot != null)
                    return cardsRoot.InverseTransformPoint(avatar.position);
            }
            return TrickPosition(0);
        }

        void ClearCollected()
        {
            _won.Clear();
            _pulled.Clear();
            _trickPlay.Clear();
            _fansReady = false;
            for (int seat = 0; seat < _fans.Length; seat++)
                _fans[seat].Clear();
        }

        void PrepareHands(IHeartsTable table, HashSet<int> localSet, Dictionary<int, int> trickSeat)
        {
            if (!_fansReady)
                BuildFans(table, localSet, trickSeat);
            foreach (var pair in trickSeat)
            {
                if (!_pulled.Add(pair.Key))
                    continue;
                if (pair.Value != table.LocalSeat)
                    PullFromHand(pair.Key, pair.Value);
            }
        }

        void BuildFans(IHeartsTable table, HashSet<int> localSet, Dictionary<int, int> trickSeat)
        {
            for (int seat = 0; seat < _fans.Length; seat++)
                _fans[seat].Clear();
            var spare = new List<int>();
            for (int i = 0; i < cards.Length; i++)
            {
                if (!localSet.Contains(i) && !_won.Contains(i) && !trickSeat.ContainsKey(i))
                    spare.Add(i);
            }

            int spareIndex = 0;
            int local = table.LocalSeat;
            for (int seat = 0; seat < 4; seat++)
            {
                if (seat == local)
                    continue;
                int count = table.CardsRemaining(seat);
                for (int n = 0; n < count && spareIndex < spare.Count; n++)
                    _fans[seat].Add(spare[spareIndex++]);
            }
            _fansReady = true;
        }

        void PullFromHand(int card, int seat)
        {
            int holder = -1;
            for (int s = 0; s < _fans.Length; s++)
            {
                if (_fans[s].Contains(card))
                    holder = s;
            }

            if (holder < 0)
                return;

            if (holder == seat)
            {
                _fans[seat].Remove(card);
                return;
            }

            if (seat < 0 || seat >= _fans.Length || _fans[seat].Count == 0)
            {
                if (holder >= 0)
                    _fans[holder].Remove(card);
                return;
            }

            int standIn = _fans[seat][_fans[seat].Count - 1];
            if (holder >= 0)
            {
                int at = _fans[holder].IndexOf(card);
                _fans[holder][at] = standIn;
            }
            _fans[seat].RemoveAt(_fans[seat].Count - 1);
            ExchangePlaces(card, standIn);
        }

        void ExchangePlaces(int incoming, int outgoing)
        {
            if (cards == null || incoming < 0 || outgoing < 0 || incoming >= cards.Length || outgoing >= cards.Length)
                return;
            CardView arrive = cards[incoming];
            CardView leave = cards[outgoing];
            if (arrive == null || leave == null)
                return;
            if (animations != null)
            {
                animations.ResetCard(arrive.Rect);
                animations.ResetCard(leave.Rect);
            }

            Vector2 arrivePos = arrive.Rect.position;
            Vector2 leavePos = leave.Rect.position;
            Quaternion arriveRot = arrive.Rect.rotation;
            Quaternion leaveRot = leave.Rect.rotation;
            Transform arriveParent = arrive.Rect.parent;
            Transform leaveParent = leave.Rect.parent;
            arrive.Rect.SetParent(leaveParent, false);
            leave.Rect.SetParent(arriveParent, false);
            arrive.Rect.position = leavePos;
            leave.Rect.position = arrivePos;
            arrive.Rect.rotation = leaveRot;
            leave.Rect.rotation = arriveRot;
            _shownPos[incoming] = arrive.Rect.anchoredPosition;
            _shownPos[outgoing] = leave.Rect.anchoredPosition;
            _shownRot[incoming] = leave.Rect.localEulerAngles.z;
            _shownRot[outgoing] = arrive.Rect.localEulerAngles.z;
        }

        void SyncTrickPlay(Dictionary<int, int> trickSeat)
        {
            for (int i = _trickPlay.Count - 1; i >= 0; i--)
            {
                if (!trickSeat.ContainsKey(_trickPlay[i]))
                    _trickPlay.RemoveAt(i);
            }

            for (int seat = 0; seat < 4; seat++)
            {
                int card = -1;
                foreach (var pair in trickSeat)
                {
                    if (pair.Value != seat)
                        continue;
                    card = pair.Key;
                    break;
                }

                if (card >= 0 && !_trickPlay.Contains(card))
                    _trickPlay.Add(card);
            }
        }

        Vector2 TrickPosition(int playIndex)
        {
            if (!_metricsReady)
                return DeckPosition(playIndex);
            return ToCards(TableLayout.TrickPoint(_metrics, playIndex));
        }

        Vector2 FanPosition(int screen, int stack, int count)
        {
            if (!_metricsReady)
                return DeckPosition(stack);
            if (screen == 2)
                return ToCards(TableLayout.TopPoint(_metrics, stack, count));
            if (screen == 1 || screen == 3)
                return ToCards(TableLayout.SidePoint(_metrics, stack, count, screen == 1));
            return HandPosition(stack, count, false);
        }

        Vector2 DeckPosition(int index)
        {
            Vector2 offset = new Vector2((index % 13) * 0.4f, (index / 13) * 0.4f);
            if (!_metricsReady)
                return deckPoint != null ? ToCards(deckPoint, offset) : offset;
            return ToCards(_metrics.deckCenter + offset);
        }

        Vector2 ToParent(RectTransform parent, Vector2 cardsRootLocal)
        {
            if (parent == null || cardsRoot == null || parent == cardsRoot)
                return cardsRootLocal;
            return parent.InverseTransformPoint(cardsRoot.TransformPoint(cardsRootLocal));
        }

        RectTransform HandContainer(int screen)
        {
            if (screen < 0 || screen > 3)
                return cardsRoot;
            if (_handContainers[screen] == null)
                EnsureHandContainers();
            return _handContainers[screen] != null ? _handContainers[screen] : cardsRoot;
        }

        void EnsureHandContainers()
        {
            for (int screen = 0; screen < 4; screen++)
            {
                RectTransform host = screenAreas != null && screen < screenAreas.Length ? screenAreas[screen] : null;
                if (host == null)
                    host = cardsRoot;
                if (host == null)
                    continue;

                RectTransform hand = _handContainers[screen];
                if (hand == null)
                {
                    Transform found = host.Find("Hand");
                    hand = found as RectTransform;
                    if (hand == null)
                    {
                        var created = new GameObject("Hand", typeof(RectTransform));
                        hand = created.GetComponent<RectTransform>();
                        hand.SetParent(host, false);
                    }
                    _handContainers[screen] = hand;
                }

                hand.localRotation = Quaternion.identity;
                hand.localScale = Vector3.one;
                if (hand.sizeDelta.sqrMagnitude < 4f)
                    hand.sizeDelta = HandSize(screen);
            }
        }

        Vector2 HandCenter(int screen)
        {
            if (!_metricsReady)
                return Vector2.zero;
            if (screen == 1)
                return new Vector2(_metrics.leftCardX, _metrics.sideBaseY + _metrics.sideTravel * 0.5f);
            if (screen == 2)
                return new Vector2((_metrics.topFanLeft + _metrics.topFanRight) * 0.5f, _metrics.topFanY);
            if (screen == 3)
                return new Vector2(_metrics.rightCardX, _metrics.sideBaseY + _metrics.sideTravel * 0.5f);
            return new Vector2((_metrics.handLeft + _metrics.handRight) * 0.5f, _metrics.handCenterY);
        }

        Vector2 HandSize(int screen)
        {
            if (!_metricsReady)
                return new Vector2(160f, 160f);
            if (screen == 1 || screen == 3)
                return new Vector2(_metrics.cardSize.y + _metrics.cardSize.x * 0.45f, Mathf.Max(48f, _metrics.sideTravel + _metrics.cardSize.x));
            if (screen == 2)
                return new Vector2(Mathf.Max(48f, _metrics.topFanRight - _metrics.topFanLeft + _metrics.cardSize.x), _metrics.cardSize.y * 1.35f);
            return new Vector2(Mathf.Max(48f, _metrics.handRight - _metrics.handLeft + _metrics.localCardSize.x), _metrics.localCardSize.y + _metrics.selectionLift);
        }

        Vector2 ToCards(Vector2 bottomLeft)
        {
            if (cardsRoot == null || _canvasRect == null)
                return bottomLeft;
            Vector2 local = bottomLeft - Vector2.Scale(_canvasRect.rect.size, _canvasRect.pivot);
            Vector3 world = _canvasRect.TransformPoint(local);
            return cardsRoot.InverseTransformPoint(world);
        }

        Vector2 ToCards(RectTransform area, Vector2 localPoint)
        {
            if (cardsRoot == null || area == null)
                return localPoint;
            Vector3 world = area.TransformPoint(localPoint);
            return cardsRoot.InverseTransformPoint(world);
        }

        void SortCardDrawOrder(int count)
        {
            for (int i = 1; i < count; i++)
            {
                int card = _drawOrder[i];
                int priority = _drawPriority[i];
                int slot = i - 1;
                while (slot >= 0 && _drawPriority[slot] > priority)
                {
                    _drawOrder[slot + 1] = _drawOrder[slot];
                    _drawPriority[slot + 1] = _drawPriority[slot];
                    slot--;
                }
                _drawOrder[slot + 1] = card;
                _drawPriority[slot + 1] = priority;
            }

            var seen = new Dictionary<Transform, int>();
            for (int i = 0; i < count; i++)
            {
                CardView view = cards[_drawOrder[i]];
                if (view == null)
                    continue;
                Transform parent = view.Rect.parent;
                int slot = 0;
                if (!seen.TryGetValue(parent, out slot))
                    slot = 0;
                if (view.Rect.GetSiblingIndex() != slot)
                    view.Rect.SetSiblingIndex(slot);
                seen[parent] = slot + 1;
            }
        }

        bool EnsureMetrics()
        {
            if (cardsRoot == null)
                return false;
            if (_canvasRect == null)
            {
                var canvas = cardsRoot.GetComponentInParent<Canvas>();
                _canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            }
            if (_canvasRect == null)
                return false;

            Canvas.ForceUpdateCanvases();
            float width = _canvasRect.rect.width;
            float height = _canvasRect.rect.height;
            if (width < 80f || height < 80f)
                return false;

            ReadInsets(width, height, out float left, out float right, out float bottom, out float top);
            bool changed = !_metricsReady
                           || Mathf.Abs(_metrics.canvasWidth - width) > 0.5f
                           || Mathf.Abs(_metrics.canvasHeight - height) > 0.5f
                           || Mathf.Abs(_metrics.insetLeft - left) > 0.5f
                           || Mathf.Abs(_metrics.insetTop - top) > 0.5f;
            if (!changed)
                return false;

            _metrics = TableLayout.Compute(width, height, left, right, bottom, top);
            _metricsReady = true;
            ApplyChrome(_metrics);
            return true;
        }

        void ReadInsets(float width, float height, out float left, out float right, out float bottom, out float top)
        {
            left = right = bottom = top = 0f;
            if (Screen.width < 8 || Screen.height < 8)
                return;
            Rect safe = Screen.safeArea;
            float scaleX = width / Screen.width;
            float scaleY = height / Screen.height;
            left = safe.xMin * scaleX;
            right = (Screen.width - safe.xMax) * scaleX;
            bottom = safe.yMin * scaleY;
            top = (Screen.height - safe.yMax) * scaleY;
        }

        void ApplyChrome(TableMetrics metrics)
        {
            Place(trickArea, metrics.trickCenter, new Vector2(metrics.cardSize.x + metrics.trickOffsetX * 2f, metrics.cardSize.y + metrics.trickOffsetY * 2f));
            Place(deckPoint, metrics.deckCenter, new Vector2(20f, 20f));
            Place(tableStatus != null ? tableStatus.rectTransform : null, metrics.statusCenter, metrics.statusSize);
            Canvas.ForceUpdateCanvases();
            if (tableStatus != null)
            {
                tableStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
                tableStatus.verticalOverflow = VerticalWrapMode.Truncate;
                tableStatus.resizeTextForBestFit = true;
                tableStatus.resizeTextMinSize = 12;
                tableStatus.resizeTextMaxSize = 24;
                tableStatus.raycastTarget = false;
            }
            EnsureHandContainers();
        }

        void Place(RectTransform[] rects, int index, Vector2 center, Vector2 size)
        {
            if (rects == null || index < 0 || index >= rects.Length)
                return;
            Place(rects[index], center, size);
        }

        static void Place(RectTransform rect, Vector2 center, Vector2 size)
        {
            if (rect == null)
                return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }
    }
}
