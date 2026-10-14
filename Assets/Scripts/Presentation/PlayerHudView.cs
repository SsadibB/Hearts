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

        public void Show(string playerName, Sprite avatar, int cardsRemaining, string status, bool turn, bool connected)
        {
            if (nameText != null)
                nameText.text = string.IsNullOrEmpty(playerName) ? "Waiting" : playerName;
            if (cardsText != null)
                cardsText.text = cardsRemaining + " cards";
            if (statusText != null)
                statusText.text = connected ? status : "Disconnected";
            if (avatarImage != null && avatar != null)
                avatarImage.sprite = avatar;
            if (turnImage != null)
                turnImage.color = turn ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(1f, 1f, 1f, 0.25f);
        }
    }
}
