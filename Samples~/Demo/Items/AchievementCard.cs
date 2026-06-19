using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.RecyclableScroll.Samples
{
    public class AchievementCard : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Text  titleText;
        [SerializeField] private Text  progressText;

        public void Populate(string title, int progressPct, Color iconColor)
        {
            if (iconImage    != null) iconImage.color   = iconColor;
            if (titleText    != null) titleText.text    = title;
            if (progressText != null) progressText.text = $"{progressPct}%";
        }
    }
}
