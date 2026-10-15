using UnityEngine;
using UnityEngine.UI;

namespace Hearts.Presentation
{
    public class PlayerHudView : MonoBehaviour
    {
        [SerializeField] Image avatarImage;
        [SerializeField] Text nameText;
        [SerializeField] Text cardsText;
        [SerializeField] Text statusText;
        [SerializeField] Image turnImage;

        public RectTransform AvatarRect => avatarImage != null ? avatarImage.rectTransform : null;

        public void Show(string playerName, Sprite avatar, int cardsRemaining, int roundScore, int totalScore, string status, bool turn, bool connected, int screen)
        {
            if (nameText != null)
                nameText.text = string.IsNullOrEmpty(playerName) ? "Waiting" : playerName;
            if (cardsText != null)
                cardsText.text = roundScore + " / " + totalScore;
            if (statusText != null)
            {
                if (!connected)
                    statusText.text = "Disconnected";
                else if (!string.IsNullOrEmpty(status))
                    statusText.text = status;
                else
                    statusText.text = cardsRemaining + " cards";
            }
            if (avatarImage != null && avatar != null)
                avatarImage.sprite = avatar;
            if (turnImage != null)
                turnImage.color = turn ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(1f, 1f, 1f, 0.25f);
        }

        public void Arrange(int screen)
        {
            var root = (RectTransform)transform;
            float width = root.rect.width;
            float height = root.rect.height;
            if (width < 8f || height < 8f)
                return;

            var background = GetComponent<Image>();
            if (background != null)
            {
                background.raycastTarget = false;
                background.color = new Color(0f, 0f, 0f, 0.62f);
            }

            if (screen == 2 || screen == 0)
            {
                ArrangeNameLeft(width, height, screen == 0);
                return;
            }

            ArrangeBelow(width, height, false);
        }

        void ArrangeNameLeft(float width, float height, bool local)
        {
            float avatar = Mathf.Clamp(height - 8f, local ? 52f : 40f, local ? 112f : 92f);
            avatar = Mathf.Min(avatar, width * 0.48f);
            PlaceAvatar(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, 0f), avatar);

            float textW = Mathf.Max(24f, width - avatar - 16f);
            float nameH = Mathf.Clamp(height * 0.4f, 18f, local ? 40f : 32f);
            float scoreH = Mathf.Clamp(height * 0.24f, 12f, local ? 22f : 18f);
            float statusH = Mathf.Clamp(height * 0.2f, 12f, local ? 18f : 16f);
            float block = nameH + scoreH + statusH;
            float room = Mathf.Max(28f, height - 8f);
            if (block > room)
            {
                float scale = room / block;
                nameH *= scale;
                scoreH *= scale;
                statusH *= scale;
                block = nameH + scoreH + statusH;
            }

            float top = (height - block) * 0.5f;
            PlaceLeft(nameText, 6f, textW, top, nameH, local ? 36 : 28);
            PlaceLeft(cardsText, 6f, textW, top + nameH, scoreH, local ? 20 : 16);
            PlaceLeft(statusText, 6f, textW, top + nameH + scoreH, statusH, local ? 16 : 14);
            PlaceTurn(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -4f));
        }

        void ArrangeBelow(float width, float height, bool local)
        {
            float nameH = Mathf.Clamp(height * 0.2f, local ? 24f : 16f, local ? 44f : 30f);
            float scoreH = Mathf.Clamp(height * 0.14f, 14f, local ? 26f : 20f);
            float statusH = Mathf.Clamp(height * 0.12f, 12f, local ? 22f : 18f);
            float textBudget = Mathf.Max(36f, height - (local ? 56f : 40f) - 8f);
            float text = nameH + scoreH + statusH;
            if (text > textBudget)
            {
                float scale = textBudget / text;
                nameH *= scale;
                scoreH *= scale;
                statusH *= scale;
            }

            float avatar = Mathf.Clamp(height - nameH - scoreH - statusH - 10f, local ? 48f : 36f, local ? 128f : 92f);
            avatar = Mathf.Min(avatar, width - 8f);
            PlaceAvatar(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), avatar);

            float y = 6f + avatar;
            PlaceCentered(nameText, y, width - 8f, nameH, local ? 40 : 26);
            y += nameH;
            PlaceCentered(cardsText, y, width - 8f, scoreH, local ? 24 : 18);
            y += scoreH;
            PlaceCentered(statusText, y, width - 8f, statusH, local ? 18 : 15);
            PlaceTurn(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4f, -4f));
        }

        void PlaceAvatar(Vector2 anchor, Vector2 pivot, Vector2 position, float size)
        {
            if (avatarImage == null)
                return;
            avatarImage.preserveAspect = true;
            avatarImage.raycastTarget = false;
            var avatarRect = avatarImage.rectTransform;
            avatarRect.anchorMin = avatarRect.anchorMax = anchor;
            avatarRect.pivot = pivot;
            avatarRect.sizeDelta = new Vector2(size, size);
            avatarRect.anchoredPosition = position;
        }

        void PlaceTurn(Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            if (turnImage == null)
                return;
            turnImage.raycastTarget = false;
            var turnRect = turnImage.rectTransform;
            turnRect.anchorMin = turnRect.anchorMax = anchor;
            turnRect.pivot = pivot;
            turnRect.anchoredPosition = position;
            turnRect.sizeDelta = new Vector2(14f, 14f);
        }

        static void PlaceLeft(Text text, float x, float width, float yFromTop, float height, int maxFont)
        {
            if (text == null)
                return;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -yFromTop);
            rect.sizeDelta = new Vector2(Mathf.Max(24f, width), height);
            text.alignment = TextAnchor.MiddleRight;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = maxFont;
            text.raycastTarget = false;
            var outline = text.GetComponent<Outline>();
            if (outline == null)
                outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
        }

        static void PlaceCentered(Text text, float yFromTop, float width, float height, int maxFont)
        {
            if (text == null)
                return;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -yFromTop);
            rect.sizeDelta = new Vector2(Mathf.Max(24f, width), height);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = maxFont;
            text.raycastTarget = false;
            var outline = text.GetComponent<Outline>();
            if (outline == null)
                outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
        }

    }
}
