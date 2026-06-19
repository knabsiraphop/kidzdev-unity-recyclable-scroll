using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.RecyclableScroll.Samples
{
    public class BadgeCard : MonoBehaviour
    {
        [SerializeField] private Image badgeImage;
        [SerializeField] private Text  badgeNameText;

        public void Populate(string badgeName, Color color)
        {
            if (badgeImage    != null) badgeImage.color   = color;
            if (badgeNameText != null) badgeNameText.text = badgeName;
        }
    }
}
