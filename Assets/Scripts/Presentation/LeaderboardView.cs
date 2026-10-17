using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hearts.Presentation
{
    public class LeaderboardView : MonoBehaviour
    {
        const float Header = 40f;
        const float RowHeight = 64f;
        const float NameWidth = 220f;
        const float TotalWidth = 96f;
        const float ColumnWidth = 104f;
        const float Top = -56f;
        const float Side = 16f;

        static readonly Color[] RowColors =
        {
            new Color(0.86f, 0.93f, 1f, 0.96f),
            new Color(0.86f, 0.96f, 0.88f, 0.96f),
            new Color(1f, 0.94f, 0.82f, 0.96f),
            new Color(0.93f, 0.88f, 0.98f, 0.96f)
        };

        static readonly Color Ink = new Color(0.13f, 0.16f, 0.22f, 1f);
        static readonly Color HeaderInk = new Color(0.93f, 0.95f, 0.98f, 1f);

        [SerializeField] Image[] avatars;
        [SerializeField] Text[] names;
        [SerializeField] Text[] roundScores;
        [SerializeField] Text[] totalScores;
        [SerializeField] Text titleText;

        RectTransform _table;
        RectTransform _content;
        RoundDrag _drag;
        Text[] _totals;
        float _columnWidth = ColumnWidth;
        int _shownRounds = -1;
        int _scoreHash;
        bool _built;

        void Awake()
        {
            EnsureBoard();
        }

        public void ShowRounds(string[] playerNames, Sprite[] avatarSprites, int rounds, int[] scores, int[] total)
        {
            if (titleText != null)
                titleText.text = "Scores";
            EnsureBoard();
            HideLegacyScores();
            PlaceIdentities(playerNames, avatarSprites);
            ShowTotals(total);
            int hash = ScoreHash(scores);
            if (rounds == _shownRounds && hash == _scoreHash)
                return;
            _shownRounds = rounds;
            _scoreHash = hash;
            RebuildRounds(rounds, scores);
            Canvas.ForceUpdateCanvases();
            if (_drag != null)
                _drag.ShowLatest();
        }

        static int ScoreHash(int[] scores)
        {
            if (scores == null)
                return 0;
            int hash = scores.Length;
            for (int i = 0; i < scores.Length; i++)
                hash = (hash * 31) + scores[i];
            return hash;
        }

        void EnsureBoard()
        {
            if (_built)
                return;
            _built = true;

            var panel = (RectTransform)transform;
            float width = panel.rect.width > 80f ? panel.rect.width : panel.sizeDelta.x;
            if (width < 80f)
                width = 760f;
            float tableWidth = width - Side * 2f;
            float tableHeight = Header + RowHeight * 4f;

            _table = CreateRect("ScoreTable", panel);
            _table.anchorMin = _table.anchorMax = new Vector2(0f, 1f);
            _table.pivot = new Vector2(0f, 1f);
            _table.anchoredPosition = new Vector2(Side, Top);
            _table.sizeDelta = new Vector2(tableWidth, tableHeight);

            var header = CreateRect("HeaderRow", _table);
            header.anchorMin = header.anchorMax = new Vector2(0f, 1f);
            header.pivot = new Vector2(0f, 1f);
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(tableWidth, Header);
            var headerImage = header.gameObject.AddComponent<Image>();
            headerImage.color = new Color(1f, 1f, 1f, 0.08f);
            headerImage.raycastTarget = false;

            var playersHeader = AddLabel(header, "Players", new Vector2(12f, 0f), new Vector2(NameWidth - 16f, Header), 16, FontStyle.Bold, HeaderInk);
            playersHeader.alignment = TextAnchor.MiddleLeft;
            var totalHeader = AddLabel(header, "Total", new Vector2(tableWidth - TotalWidth, 0f), new Vector2(TotalWidth, Header), 16, FontStyle.Bold, HeaderInk);
            totalHeader.alignment = TextAnchor.MiddleCenter;

            for (int seat = 0; seat < 4; seat++)
            {
                var row = CreateRect("PlayerRow" + seat, _table);
                row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
                row.pivot = new Vector2(0f, 1f);
                row.anchoredPosition = new Vector2(0f, -(Header + seat * RowHeight));
                row.sizeDelta = new Vector2(tableWidth, RowHeight);
                var band = row.gameObject.AddComponent<Image>();
                band.color = RowColors[seat];
                band.raycastTarget = false;
                AdoptIdentity(row, seat);

                var totalLabel = AddLabel(row, "0", new Vector2(tableWidth - TotalWidth, 0f), new Vector2(TotalWidth, RowHeight), 22, FontStyle.Bold, Ink);
                if (_totals == null)
                    _totals = new Text[4];
                _totals[seat] = totalLabel;
            }

            float scrollWidth = Mathf.Max(80f, tableWidth - NameWidth - TotalWidth);
            _columnWidth = scrollWidth / 4f;
            CreateRoundsScroll(_table, new Vector2(NameWidth, 0f), new Vector2(scrollWidth, tableHeight));

            var next = panel.Find("NextRoundButton") as RectTransform;
            if (next != null)
            {
                next.anchorMin = next.anchorMax = new Vector2(0.5f, 0f);
                next.pivot = new Vector2(0.5f, 0f);
                next.anchoredPosition = new Vector2(0f, 16f);
                next.SetAsLastSibling();
            }
        }

        void CreateRoundsScroll(RectTransform parent, Vector2 position, Vector2 size)
        {
            var scrollGo = new GameObject("RoundsScroll", typeof(RectTransform));
            scrollGo.transform.SetParent(parent, false);
            var scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = scrollRect.anchorMax = new Vector2(0f, 1f);
            scrollRect.pivot = new Vector2(0f, 1f);
            scrollRect.anchoredPosition = position;
            scrollRect.sizeDelta = size;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollRect, false);
            var viewport = viewportGo.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            var viewportImage = viewportGo.GetComponent<Image>();
            viewportImage.sprite = WhiteSprite();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            viewportImage.raycastTarget = false;
            viewportGo.GetComponent<RectMask2D>().enabled = true;

            _content = FindNamed(transform, "Rounds");
            bool placed = _content != null;
            if (!placed)
            {
                var roundsGo = new GameObject("Rounds", typeof(RectTransform), typeof(Image));
                roundsGo.transform.SetParent(viewport, false);
                _content = roundsGo.GetComponent<RectTransform>();
                _content.anchorMin = _content.anchorMax = new Vector2(0f, 1f);
                _content.pivot = new Vector2(0f, 1f);
                _content.anchoredPosition = Vector2.zero;
                _content.sizeDelta = new Vector2(size.x, size.y);
                var contentImage = roundsGo.GetComponent<Image>();
                contentImage.sprite = WhiteSprite();
                contentImage.color = new Color(1f, 1f, 1f, 0.01f);
                contentImage.raycastTarget = true;
            }
            else if (_content.GetComponent<Image>() == null)
            {
                var contentImage = _content.gameObject.AddComponent<Image>();
                contentImage.sprite = WhiteSprite();
                contentImage.color = new Color(1f, 1f, 1f, 0.01f);
                contentImage.raycastTarget = true;
            }

            _drag = _content.GetComponent<RoundDrag>();
            if (_drag == null)
                _drag = _content.gameObject.AddComponent<RoundDrag>();
            _drag.Bind(_content, viewport);
        }

        static RectTransform FindNamed(Transform root, string objectName)
        {
            if (root == null)
                return null;
            if (root.name == objectName)
                return root as RectTransform;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindNamed(root.GetChild(i), objectName);
                if (found != null)
                    return found;
            }
            return null;
        }

        static Sprite _whiteSprite;

        static Sprite WhiteSprite()
        {
            if (_whiteSprite != null)
                return _whiteSprite;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return _whiteSprite;
        }

        void AdoptIdentity(RectTransform row, int seat)
        {
            RectTransform oldRow = null;
            if (names != null && seat < names.Length && names[seat] != null)
            {
                oldRow = names[seat].rectTransform.parent as RectTransform;
                names[seat].rectTransform.SetParent(row, false);
            }

            if (avatars != null && seat < avatars.Length && avatars[seat] != null)
                avatars[seat].rectTransform.SetParent(row, false);

            if (oldRow != null && oldRow != row && oldRow != _table && oldRow != (RectTransform)transform)
                oldRow.gameObject.SetActive(false);
        }

        void PlaceIdentities(string[] playerNames, Sprite[] avatarSprites)
        {
            for (int seat = 0; seat < 4; seat++)
            {
                if (names != null && seat < names.Length && names[seat] != null)
                {
                    names[seat].text = playerNames != null && seat < playerNames.Length ? playerNames[seat] : "Player";
                    names[seat].color = Ink;
                    names[seat].alignment = TextAnchor.MiddleLeft;
                    names[seat].horizontalOverflow = HorizontalWrapMode.Overflow;
                    names[seat].verticalOverflow = VerticalWrapMode.Truncate;
                    var nameRect = names[seat].rectTransform;
                    nameRect.anchorMin = nameRect.anchorMax = new Vector2(0f, 0.5f);
                    nameRect.pivot = new Vector2(0f, 0.5f);
                    nameRect.anchoredPosition = new Vector2(68f, 0f);
                    nameRect.sizeDelta = new Vector2(NameWidth - 80f, RowHeight);
                }

                if (avatars == null || seat >= avatars.Length || avatars[seat] == null)
                    continue;
                if (avatarSprites != null && seat < avatarSprites.Length && avatarSprites[seat] != null)
                    avatars[seat].sprite = avatarSprites[seat];
                avatars[seat].color = Color.white;
                var avatarRect = avatars[seat].rectTransform;
                avatarRect.anchorMin = avatarRect.anchorMax = new Vector2(0f, 0.5f);
                avatarRect.pivot = new Vector2(0f, 0.5f);
                avatarRect.anchoredPosition = new Vector2(12f, 0f);
                avatarRect.sizeDelta = new Vector2(48f, 48f);
            }
        }

        void RebuildRounds(int rounds, int[] scores)
        {
            if (_content == null)
                return;
            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);

            int count = Mathf.Max(0, rounds);
            float columnWidth = _columnWidth > 1f ? _columnWidth : ColumnWidth;
            float tableHeight = Header + RowHeight * 4f;
            var contentSize = _content.sizeDelta;
            contentSize.x = Mathf.Max(columnWidth, count * columnWidth);
            if (contentSize.y < 1f)
                contentSize.y = tableHeight;
            _content.sizeDelta = contentSize;
            for (int round = 0; round < count; round++)
            {
                var column = CreateRect("Round" + (round + 1), _content);
                column.anchorMin = column.anchorMax = new Vector2(0f, 1f);
                column.pivot = new Vector2(0f, 1f);
                column.anchoredPosition = new Vector2(round * columnWidth, 0f);
                column.sizeDelta = new Vector2(columnWidth, tableHeight);
                AddLabel(column, "Round " + (round + 1), Vector2.zero, new Vector2(columnWidth, Header), 16, FontStyle.Bold, HeaderInk);
                for (int seat = 0; seat < 4; seat++)
                {
                    int index = round * 4 + seat;
                    int value = scores != null && index >= 0 && index < scores.Length ? scores[index] : 0;
                    AddLabel(column, value.ToString(), new Vector2(0f, -(Header + seat * RowHeight)), new Vector2(columnWidth, RowHeight), 22, FontStyle.Normal, Ink);
                }
            }
        }

        void ShowTotals(int[] total)
        {
            if (_totals == null)
                return;
            for (int seat = 0; seat < _totals.Length; seat++)
            {
                if (_totals[seat] == null)
                    continue;
                int value = total != null && seat < total.Length ? total[seat] : 0;
                _totals[seat].text = value.ToString();
            }
        }

        void HideLegacyScores()
        {
            Hide(roundScores);
            Hide(totalScores);
        }

        static void Hide(Text[] labels)
        {
            if (labels == null)
                return;
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null)
                    labels[i].gameObject.SetActive(false);
            }
        }

        static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        Text AddLabel(RectTransform parent, string value, Vector2 position, Vector2 size, int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.font = titleText != null && titleText.font != null
                ? titleText.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }
    }

    public sealed class RoundDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
        {
            RectTransform _content;
            RectTransform _viewport;
            Vector2 _startPointer;
            Vector2 _startContent;
            bool _dragging;

            public void Bind(RectTransform content, RectTransform viewport)
            {
                _content = content;
                _viewport = viewport;
            }

            public void ShowLatest()
            {
                if (_content == null)
                    return;
                var pos = _content.anchoredPosition;
                pos.x = -Overflow();
                _content.anchoredPosition = pos;
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                if (_content == null || _viewport == null)
                    return;
                _dragging = true;
                _startPointer = LocalPoint(eventData);
                _startContent = _content.anchoredPosition;
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (!_dragging || _content == null)
                    return;
                float dx = LocalPoint(eventData).x - _startPointer.x;
                var pos = _startContent;
                pos.x = Mathf.Clamp(_startContent.x + dx, -Overflow(), 0f);
                _content.anchoredPosition = pos;
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                _dragging = false;
            }

            public void OnScroll(PointerEventData eventData)
            {
                if (_dragging || _content == null)
                    return;
                var pos = _content.anchoredPosition;
                pos.x = Mathf.Clamp(pos.x + eventData.scrollDelta.y * 36f, -Overflow(), 0f);
                _content.anchoredPosition = pos;
            }

            float Overflow()
            {
                if (_content == null || _viewport == null)
                    return 0f;
                return Mathf.Max(0f, _content.rect.width - _viewport.rect.width);
            }

            Vector2 LocalPoint(PointerEventData eventData)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position, eventData.pressEventCamera, out var local);
                return local;
            }
        }
}
