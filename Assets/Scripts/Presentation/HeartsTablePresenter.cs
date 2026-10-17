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
        readonly Vector2[] _handCardSize = new Vector2[4];
        readonly bool[] _handCardSizeReady = new bool[4];
        string _handKey = "";
        int _phase = -2;
        IHeartsTable _hooked;
        bool _dealing;
        bool _passAnimating;
        bool _clearingTrick;
        bool _cardsHidden = true;
        int _visualVersion;
        int _leaderboardPhase = -2;
        bool _startingNextRound;

        void Awake()
        {
            CaptureHandCardSizes();
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
            if (animations == null || cards == null)
            {
                _selected.Clear();
                table.SubmitPass(picked[0], picked[1], picked[2]);
                return;
            }

            SettlePass(table, picked);
        }

        void SettlePass(IHeartsTable table, List<int> picked)
        {
            EnsureFans(table);
            var oldLocal = new HashSet<int>();
            for (int i = 0; i < table.LocalCards.Count; i++)
                oldLocal.Add(table.LocalCards[i]);

            int local = table.LocalSeat;
            var outgoing = new List<int>[4];
            for (int seat = 0; seat < 4; seat++)
                outgoing[seat] = seat == local ? new List<int>(picked) : Tail(_fans[seat], HeartsRules.PassCount);

            _passAnimating = true;
            table.SubmitPass(picked[0], picked[1], picked[2]);

            var arrived = new List<int>();
            for (int i = 0; i < table.LocalCards.Count; i++)
            {
                if (!oldLocal.Contains(table.LocalCards[i]))
                    arrived.Add(table.LocalCards[i]);
            }

            int right = (local + 3) % 4;
            for (int i = 0; i < arrived.Count && i < outgoing[right].Count; i++)
            {
                int real = arrived[i];
                int slotView = outgoing[right][i];
                if (real == slotView)
                    continue;
                SwapFanIds(real, slotView);
                ExchangePlaces(real, slotView);
                for (int seat = 0; seat < 4; seat++)
                {
                    for (int n = 0; n < outgoing[seat].Count; n++)
                    {
                        if (outgoing[seat][n] == real)
                            outgoing[seat][n] = slotView;
                    }
                }
                outgoing[right][i] = real;
            }

            var finalFan = new List<int>[4];
            for (int seat = 0; seat < 4; seat++)
            {
                finalFan[seat] = new List<int>();
                if (seat == local)
                {
                    for (int i = 0; i < table.LocalCards.Count; i++)
                        finalFan[seat].Add(table.LocalCards[i]);
                    continue;
                }

                var leaving = new HashSet<int>(outgoing[seat]);
                for (int n = 0; n < _fans[seat].Count; n++)
                {
                    if (!leaving.Contains(_fans[seat][n]))
                        finalFan[seat].Add(_fans[seat][n]);
                }

                int giver = (seat + 3) % 4;
                if (giver == local)
                    finalFan[seat].AddRange(picked);
                else
                    finalFan[seat].AddRange(outgoing[giver]);
            }

            for (int seat = 0; seat < 4; seat++)
            {
                _fans[seat].Clear();
                if (seat == local)
                    continue;
                _fans[seat].AddRange(finalFan[seat]);
            }
            _fansReady = true;

            int version = _visualVersion;
            int pending = 0;
            for (int seat = 0; seat < 4; seat++)
            {
                int screen = ScreenOf(seat, local);
                int count = Mathf.Max(1, finalFan[seat].Count);
                for (int index = 0; index < finalFan[seat].Count; index++)
                {
                    int card = finalFan[seat][index];
                    if (card < 0 || card >= cards.Length || cards[card] == null)
                        continue;
                    Vector2 target = HandLocal(screen, index, count, false);
                    float rotation = FanRotation(screen, index, count);
                    bool face = screen == 0;
                    CardView view = cards[card];
                    view.Rect.sizeDelta = CardSizeFor(screen);
                    view.ShowFace(face);
                    _shownPos[card] = target;
                    _shownRot[card] = rotation;
                    _shownFace[card] = face;
                    RectTransform hand = HandContainer(screen);
                    bool parked = hand != null
                        && view.Rect.parent == hand
                        && Vector2.Distance(view.Rect.anchoredPosition, target) <= 0.5f
                        && Mathf.Abs(Mathf.DeltaAngle(view.Rect.localEulerAngles.z, rotation)) <= 0.75f;
                    if (parked || animations == null)
                    {
                        if (hand != null)
                            view.Rect.SetParent(hand, false);
                        view.Rect.anchoredPosition = target;
                        view.Rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
                        continue;
                    }

                    pending++;
                    if (view.Rect.parent != hand)
                        view.Rect.SetParent(hand, true);
                    int captured = card;
                    animations.Move(view.Rect, target, true, () =>
                    {
                        if (version != _visualVersion)
                            return;
                        if (cards[captured] != null)
                            cards[captured].ShowFace(face);
                        pending--;
                        if (pending > 0)
                            return;
                        _passAnimating = false;
                        _selected.Clear();
                    }, (index % 3) * 0.03f, rotation, 0.7f);
                }
            }

            if (pending == 0)
            {
                _passAnimating = false;
                _selected.Clear();
            }
        }

        void SwapFanIds(int a, int b)
        {
            for (int seat = 0; seat < _fans.Length; seat++)
            {
                int atA = _fans[seat].IndexOf(a);
                int atB = _fans[seat].IndexOf(b);
                if (atA >= 0)
                    _fans[seat][atA] = b;
                if (atB >= 0)
                    _fans[seat][atB] = a;
            }
        }

        static List<int> Tail(List<int> source, int count)
        {
            var tail = new List<int>();
            if (source == null || count <= 0)
                return tail;
            int start = Mathf.Max(0, source.Count - count);
            for (int i = start; i < source.Count; i++)
                tail.Add(source[i]);
            return tail;
        }

        void EnsureFans(IHeartsTable table)
        {
            if (_fansReady || cards == null || table == null)
                return;
            for (int seat = 0; seat < _fans.Length; seat++)
                _fans[seat].Clear();
            int local = table.LocalSeat;
            for (int i = 0; i < cards.Length; i++)
            {
                CardView view = cards[i];
                if (view == null || !view.gameObject.activeInHierarchy)
                    continue;
                int screen = ScreenOfParent(view.Rect.parent);
                if (screen < 0)
                    continue;
                _fans[(local + screen) % 4].Add(i);
            }
            _fansReady = true;
        }

        int ScreenOfParent(Transform parent)
        {
            if (parent == null)
                return -1;
            for (int screen = 0; screen < 4; screen++)
            {
                RectTransform hand = HandContainer(screen);
                if (hand != null && parent == hand)
                    return screen;
            }
            return -1;
        }

        void RequestNextRound()
        {
            if (_startingNextRound)
                return;
            var table = ActiveTable();
            if (table == null)
                return;
            _startingNextRound = true;
            void StartRound()
            {
                _startingNextRound = false;
                var active = ActiveTable();
                if (active == null)
                    return;
                if (active.Phase == (int)MatchPhase.MatchOver)
                    active.RestartMatch();
                else
                    active.RequestNextRound();
            }
            if (ui != null)
                ui.CloseLeaderboard(StartRound);
            else
                StartRound();
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
                Vector2 size = view.Rect.sizeDelta;
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
                    size = CardSizeFor(0);
                    rotation = TableLayout.TrickRotation(screen);
                    duration = 1.05f;
                    priority = 400 + playIndex;
                }
                else if (localSet.Contains(i))
                {
                    face = true;
                    int order = handOrder.IndexOf(i);
                    bool selected = _selected.Contains(i);
                    size = CardSizeFor(0);
                    target = HandLocal(0, order, handOrder.Count, selected);
                    duration = 0.65f;
                    priority = 200 + order;
                    home = HandContainer(0);
                    inHand = true;
                    bool sameX = !float.IsNaN(_shownPos[i].x) && Mathf.Abs(_shownPos[i].x - target.x) <= 0.5f;
                    bool sameRot = !float.IsNaN(_shownRot[i]) && Mathf.Abs(Mathf.DeltaAngle(_shownRot[i], rotation)) <= 0.75f;
                    liftOnly = sameX && sameRot && (float.IsNaN(_shownPos[i].y) || Mathf.Abs(_shownPos[i].y - target.y) > 0.5f);
                }
                else if (visualSeat.TryGetValue(i, out int owner))
                {
                    int screen = ScreenOf(owner, localSeat);
                    int count = Mathf.Max(1, fanCount[screen]);
                    target = HandLocal(screen, fanIndex[i], count, false);
                    rotation = FanRotation(screen, fanIndex[i], count);
                    size = CardSizeFor(screen);
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
                total[seat] = table.TotalScore(seat);
            }

            if (tableStatus != null)
                tableStatus.text = table.Status;

            bool roundEnd = table.Phase == (int)MatchPhase.RoundEnd;
            bool over = table.Phase == (int)MatchPhase.MatchOver;
            if (ui != null)
            {
                bool end = roundEnd || over;
                if (end && _leaderboardPhase != table.Phase)
                    ui.OpenLeaderboard();
                _leaderboardPhase = end ? table.Phase : -1;
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
            if (leaderboard != null && table.Phase != (int)MatchPhase.Waiting)
            {
                int rounds = table.LoggedRounds;
                var history = new int[Mathf.Max(0, rounds) * 4];
                for (int logged = 0; logged < rounds; logged++)
                {
                    for (int seat = 0; seat < 4; seat++)
                        history[logged * 4 + seat] = table.LoggedScore(logged, seat);
                }
                leaderboard.ShowRounds(names, sprites, rounds, history, total);
            }
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
                bool over = table.Phase == (int)MatchPhase.MatchOver;
                ui.NextRoundButton.gameObject.SetActive(table.CanRestartMatch);
                var label = ui.NextRoundButton.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = over ? "Play Again" : "Next round";
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
                if (localSet.Contains(i))
                    view.Rect.sizeDelta = CardSizeFor(0);
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
                    cards[index].Rect.sizeDelta = CardSizeFor(screen);
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

        float FanRotation(int screen, int index, int count)
        {
            float along = count <= 1 ? 0f : (index / (float)(count - 1)) * 2f - 1f;
            if (screen == 1)
                return TableLayout.LeftRotation + along * 10f;
            if (screen == 3)
                return TableLayout.RightRotation - along * 10f;
            if (screen == 2)
                return TopFan(index, count).rotation;
            return 0f;
        }

        (Vector2 position, float rotation) TopFan(int index, int count)
        {
            int safeCount = Mathf.Max(1, count);
            int safeIndex = Mathf.Clamp(index, 0, safeCount - 1);
            float along = safeCount <= 1 ? 0f : (safeIndex / (float)(safeCount - 1)) * 2f - 1f;
            float center = (safeCount - 1) * 0.5f;
            Vector2 card = CardSizeFor(2);
            float width = HandContainer(2) != null ? HandContainer(2).rect.width : card.x * 8f;
            float room = Mathf.Max(0f, width - card.x);
            float step = safeCount <= 1 ? 0f : Mathf.Min(card.x * 0.4f, room / (safeCount - 1));
            float bow = Mathf.Min(22f, card.y * 0.07f);
            float x = (safeIndex - center) * step;
            float y = -bow * along * along;
            float slope = safeCount <= 1 || step <= 0.01f ? 0f : (2f * bow * along) / ((safeCount - 1) * 0.5f * step);
            float rotation = Mathf.Clamp(Mathf.Atan(slope) * Mathf.Rad2Deg, -5.5f, 5.5f);
            return (new Vector2(x, y), rotation);
        }

        Vector2 HandLocal(int screen, int index, int count, bool selected)
        {
            int safeCount = Mathf.Max(1, count);
            int safeIndex = Mathf.Clamp(index, 0, safeCount - 1);
            float along = safeCount <= 1 ? 0f : (safeIndex / (float)(safeCount - 1)) * 2f - 1f;
            float center = (safeCount - 1) * 0.5f;
            Vector2 card = CardSizeFor(screen);
            Rect area = HandContainer(screen) != null ? HandContainer(screen).rect : new Rect(0f, 0f, card.x, card.y);
            if (screen == 0)
            {
                float room = Mathf.Max(0f, area.width - card.x);
                float overlap = card.x * 0.82f;
                float fitted = safeCount <= 1 ? 0f : room / (safeCount - 1);
                float step = safeCount <= 1 ? 0f : Mathf.Min(overlap, fitted);
                float lift = selected ? card.y * 0.18f : 0f;
                return new Vector2((safeIndex - center) * step, lift);
            }

            if (screen == 2)
                return TopFan(safeIndex, safeCount).position;

            float sideRoom = Mathf.Max(0f, area.height - card.x);
            float sideStep = Mathf.Min(card.x * 0.42f, safeCount <= 1 ? 0f : sideRoom / (safeCount - 1));
            float bow = card.x * 0.12f * (1f - along * along);
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
            int pending = 4;
            Vector2 shift = GatherOffset(ScreenOf(winner, table.LocalSeat));
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
                Vector2 dest = view.Rect.anchoredPosition + shift;
                animations.Collect(view.Rect, dest, () =>
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
                });
            }
        }

        Vector2 GatherOffset(int screen)
        {
            Vector2 origin = Vector2.zero;
            for (int i = 0; i < 4; i++)
                origin += TrickPosition(i);
            origin *= 0.25f;

            Vector2 toward = origin + SeatDirection(screen) * 220f;
            RectTransform hand = HandContainer(screen);
            if (hand != null && cardsRoot != null)
                toward = cardsRoot.InverseTransformPoint(hand.TransformPoint(hand.rect.center));

            Vector2 delta = toward - origin;
            if (delta.sqrMagnitude < 4f)
                delta = SeatDirection(screen) * 170f;
            float travel = Mathf.Clamp(delta.magnitude * 0.42f, 130f, 200f);
            return delta.normalized * travel;
        }

        static Vector2 SeatDirection(int screen)
        {
            if (screen == 1)
                return Vector2.left;
            if (screen == 2)
                return Vector2.up;
            if (screen == 3)
                return Vector2.right;
            return Vector2.down;
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
            }
        }

        void CaptureHandCardSizes()
        {
            EnsureHandContainers();
            for (int screen = 0; screen < 4; screen++)
            {
                RectTransform hand = _handContainers[screen];
                if (hand == null)
                    continue;
                for (int child = 0; child < hand.childCount; child++)
                {
                    var rect = hand.GetChild(child) as RectTransform;
                    if (rect == null || rect.sizeDelta.x < 8f || rect.sizeDelta.y < 8f)
                        continue;
                    _handCardSize[screen] = rect.sizeDelta;
                    _handCardSizeReady[screen] = true;
                    break;
                }
            }
        }

        Vector2 CardSizeFor(int screen)
        {
            Vector2 local = _handCardSizeReady[0] ? _handCardSize[0] : new Vector2(140f, 196f);
            if (screen == 0)
                return local;
            Vector2 small = local * 0.52f;
            if (screen >= 1 && screen < 4 && _handCardSizeReady[screen])
            {
                Vector2 authored = _handCardSize[screen];
                if (authored.x <= local.x * 0.7f && authored.y <= local.y * 0.7f)
                    return authored;
            }
            return small;
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
            ApplyChrome();
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

        void ApplyChrome()
        {
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
    }
}
