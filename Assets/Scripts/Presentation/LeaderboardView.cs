using UnityEngine;
using UnityEngine.UI;

namespace Hearts.Presentation
{
    public class LeaderboardView : MonoBehaviour
    {
        [SerializeField] Image[] avatars;
        [SerializeField] Text[] names;
        [SerializeField] Text[] roundScores;
        [SerializeField] Text[] totalScores;
        [SerializeField] Text titleText;

        public void ShowRound(string title, string[] playerNames, Sprite[] avatarSprites, int[] round, int[] total)
        {
            if (titleText != null)
                titleText.text = title;
            for (int i = 0; i < 4; i++)
            {
                if (names != null && i < names.Length && names[i] != null)
                    names[i].text = playerNames[i];
                if (roundScores != null && i < roundScores.Length && roundScores[i] != null)
                    roundScores[i].text = round[i].ToString();
                if (totalScores != null && i < totalScores.Length && totalScores[i] != null)
                    totalScores[i].text = total[i].ToString();
                if (avatars != null && i < avatars.Length && avatars[i] != null && avatarSprites[i] != null)
                    avatars[i].sprite = avatarSprites[i];
            }
        }
    }
}
