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
        readonly int[] _shownTrick = { -2, -2, -2, -2 };
        readonly Vector2[] _shownPos = new Vector2[52];
        readonly bool[] _shownFace = new bool[52];
        string _handKey = "";
        int _phase = -2;
        IHeartsTable _hooked;
        bool _dealing;
        bool _passAnimating;
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
                cards[i].Clicked += OnCardClicked;
                cards[i].SetInteractable(false);
                cards[i].gameObject.SetActive(false);
                _shownPos[i] = new Vector2(float.NaN, float.NaN);
            }
            if (cardsRoot != null)
                cardsRoot.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (ui != null && ui.PassButton != null)
                ui.PassButton.onClick.AddListener(SubmitPass);
            if (ui != null && ui.NextRoundButton != null)
                ui.NextRoundButton.onClick.AddListener(RequestNextRound);
        }

        void OnDisable()
        {
            if (ui != null && ui.PassButton != null)
                ui.PassButton.onClick.RemoveListener(SubmitPass);
            if (ui != null && ui.NextRoundButton != null)
                ui.NextRoundButton.onClick.RemoveListener(RequestNextRound);
            Unsubscribe();
        }

        void Update()
        {
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
            else if (!_dealing && !_passAnimating && (handKey != _handKey || trickChanged || table.Phase != _phase))
            {
                _handKey = handKey;
                _phase = table.Phase;
                for (int i = 0; i < 4; i++)
                    _shownTrick[i] = table.TrickCard(i);
                if (trickChanged)
                    _selected.Clear();
                LayoutCards(table);
            }

            RefreshHuds(table);
            RefreshButtons(table);
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
                Vector2 target = AreaPosition(1, i);
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
                }, delay);
            }
        }

        void RequestNextRound()
        {
            var table = ActiveTable();
            if (table != null)
                table.RequestNextRound();
        }

        void LayoutCards(IHeartsTable table)
        {
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

            var spare = new List<int>();
            for (int i = 0; i < cards.Length; i++)
            {
                if (!localSet.Contains(i) && !trickSeat.ContainsKey(i))
                    spare.Add(i);
            }

            var visualSeat = new Dictionary<int, int>();
            int spareIndex = 0;
            int localSeat = table.LocalSeat;
            for (int seat = 0; seat < 4; seat++)
            {
                if (seat == localSeat)
                    continue;
                int count = table.CardsRemaining(seat);
                for (int n = 0; n < count && spareIndex < spare.Count; n++)
                    visualSeat[spare[spareIndex++]] = seat;
            }

            var handOrder = new List<int>();
            for (int i = 0; i < table.LocalCards.Count; i++)
                handOrder.Add(table.LocalCards[i]);

            for (int i = 0; i < cards.Length; i++)
            {
                CardView view = cards[i];
                if (view == null)
                    continue;
                bool face = false;
                bool critical = trickSeat.ContainsKey(i);
                Vector2 target = DeckPosition(i);

                if (trickSeat.TryGetValue(i, out int playedSeat))
                {
                    face = true;
                    target = TrickPosition(ScreenOf(playedSeat, localSeat));
                }
                else if (localSet.Contains(i))
                {
                    face = true;
                    int order = handOrder.IndexOf(i);
                    target = HandPosition(order, handOrder.Count);
                    if (_selected.Contains(i))
                        target += new Vector2(0f, 28f);
                }
                else if (visualSeat.TryGetValue(i, out int owner))
                {
                    int stack = 0;
                    foreach (var pair in visualSeat)
                    {
                        if (pair.Value == owner && pair.Key < i)
                            stack++;
                    }
                    target = AreaPosition(ScreenOf(owner, localSeat), stack);
                }

                view.SetSelected(_selected.Contains(i));
                bool canClick = localSet.Contains(i) && !critical &&
                                (table.Phase == (int)MatchPhase.Passing ||
                                 (table.Phase == (int)MatchPhase.Playing && table.CurrentPlayer == localSeat));
                view.SetInteractable(canClick && (animations == null || !animations.InputLocked));

                bool moved = float.IsNaN(_shownPos[i].x) || Vector2.Distance(_shownPos[i], target) > 0.5f;
                bool reveal = face && localSet.Contains(i) && !_shownFace[i];
                _shownPos[i] = target;
                _shownFace[i] = face;
                if (reveal && animations != null)
                {
                    view.ShowFace(false);
                    animations.Move(view.Rect, target, true, () => animations.Flip(view.Rect, () => view.ShowFace(true), null));
                }
                else if (moved && animations != null)
                {
                    view.ShowFace(face);
                    animations.Move(view.Rect, target, critical, null);
                }
                else if (moved)
                {
                    view.ShowFace(face);
                    view.Rect.anchoredPosition = target;
                }
                else
                    view.ShowFace(face);
            }
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
                    screenHuds[screen].Show(playerName, avatar, table.CardsRemaining(seat), status, turn, table.IsConnected(seat));
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

        void RefreshButtons(IHeartsTable table)
        {
            if (ui != null)
                ui.ShowPassArrow(table.Phase == (int)MatchPhase.Passing && !_dealing && !_passAnimating, _selected.Count == HeartsRules.PassCount);
            if (ui != null && ui.NextRoundButton != null)
                ui.NextRoundButton.gameObject.SetActive(table.CanRequestNextRound);
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
            _cardsHidden = true;
            _visualVersion++;
            _dealing = false;
            _passAnimating = false;
            _phase = -2;
            _handKey = "";
            if (cardsRoot != null)
                cardsRoot.gameObject.SetActive(false);
            if (ui != null)
                ui.ShowPassArrow(false, false);
        }

        void BeginDeal(IHeartsTable table)
        {
            _dealing = true;
            _passAnimating = false;
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
                view.Rect.localScale = Vector3.one;
                view.Rect.localRotation = Quaternion.identity;
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
                    cards[localOrder[i]].Rect.anchoredPosition = HandPosition(i, localOrder.Count);
                    cards[localOrder[i]].ShowFace(true);
                    _shownFace[localOrder[i]] = true;
                }
                _dealing = false;
                return;
            }

            int pending = localOrder.Count;
            for (int order = 0; order < localOrder.Count; order++)
            {
                int index = localOrder[order];
                Vector2 target = HandPosition(order, localOrder.Count);
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
                        _dealing = false;
                }, delay);
            }
        }

        void PlaceOpponents(IHeartsTable table, HashSet<int> localSet)
        {
            var spare = new List<int>();
            for (int i = 0; i < cards.Length; i++)
            {
                if (!localSet.Contains(i))
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
                    Vector2 target = AreaPosition(ScreenOf(seat, localSeat), n);
                    _shownPos[index] = target;
                    if (animations != null)
                        animations.Move(cards[index].Rect, target, false, null, n * 0.02f);
                    else
                        cards[index].Rect.anchoredPosition = target;
                }
            }
        }

        static int ScreenOf(int seat, int localSeat) => (seat - localSeat + 4) % 4;

        Vector2 HandPosition(int order, int count)
        {
            if (handArea == null || count <= 0)
                return DeckPosition(order);
            float width = handArea.rect.width;
            if (width < 200f)
                width = handArea.sizeDelta.x > 200f ? handArea.sizeDelta.x : 1400f;
            float step = Mathf.Min(72f, (width - 110f) / Mathf.Max(1, count - 1));
            float start = -step * (count - 1) * 0.5f;
            return ToCards(handArea, new Vector2(start + step * order, 0f));
        }

        Vector2 TrickPosition(int screen)
        {
            Vector2 offset = screen switch
            {
                1 => new Vector2(-70f, 0f),
                2 => new Vector2(0f, 60f),
                3 => new Vector2(70f, 0f),
                _ => new Vector2(0f, -60f)
            };
            return trickArea != null ? ToCards(trickArea, offset) : offset;
        }

        Vector2 AreaPosition(int screen, int stack)
        {
            if (screenAreas == null || screen >= screenAreas.Length || screenAreas[screen] == null)
                return DeckPosition(stack);
            return ToCards(screenAreas[screen], new Vector2(stack * 10f, 0f));
        }

        Vector2 DeckPosition(int index)
        {
            Vector2 offset = new Vector2((index % 13) * 0.4f, (index / 13) * 0.4f);
            return deckPoint != null ? ToCards(deckPoint, offset) : offset;
        }

        Vector2 ToCards(RectTransform area, Vector2 localPoint)
        {
            if (cardsRoot == null || area == null)
                return localPoint;
            Vector3 world = area.TransformPoint(localPoint);
            return cardsRoot.InverseTransformPoint(world);
        }
    }
}
