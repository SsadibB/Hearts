using System;
using Hearts.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hearts.Presentation
{
    public class CardView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] Image faceImage;
        [SerializeField] Sprite faceSprite;
        [SerializeField] Sprite backSprite;
        [SerializeField] int cardIndex;

        Vector2 _home;
        bool _selected;
        bool _interactable;

        public CardId Card => CardId.FromIndex(cardIndex);
        public int CardIndex => cardIndex;
        public bool Selected => _selected;
        public bool FaceUp { get; private set; }
        public RectTransform Rect => (RectTransform)transform;
        public event Action<CardView> Clicked;

        void Awake()
        {
            if (faceImage == null)
                faceImage = GetComponent<Image>();
            faceImage.preserveAspect = true;
            ShowFace(false);
        }

        public void ShowFace(bool faceUp)
        {
            FaceUp = faceUp;
            if (faceImage == null)
                return;
            faceImage.sprite = faceUp ? faceSprite : backSprite;
            faceImage.color = Color.white;
        }

        public void SetHome(Vector2 anchoredPosition, bool moveNow)
        {
            _home = anchoredPosition;
            if (moveNow && !_selected)
                Rect.anchoredPosition = anchoredPosition;
        }

        public Vector2 Home => _home;

        public Vector2 SelectedPosition => _home + new Vector2(0f, 28f);

        public void SetSelected(bool selected)
        {
            _selected = selected;
        }

        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
            if (faceImage != null)
                faceImage.raycastTarget = interactable;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_interactable)
                Clicked?.Invoke(this);
        }
    }
}
