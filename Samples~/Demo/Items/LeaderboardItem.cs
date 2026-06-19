using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.RecyclableScroll.Samples
{
    public class LeaderboardItem : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Text rankText;
        [SerializeField] private Text nameText;
        [SerializeField] private Text scoreText;

        public void Populate(int rank, string playerName, int score, Color color)
        {
            if (background != null) background.color = color;
            if (rankText != null)   rankText.text     = $"#{rank + 1}";
            if (nameText != null)   nameText.text     = playerName;
            if (scoreText != null)  scoreText.text    = score.ToString("N0");
        }
    }
}
