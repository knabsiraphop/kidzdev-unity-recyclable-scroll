using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.RecyclableScroll.Samples
{
    public class NewsCard : MonoBehaviour
    {
        [SerializeField] private Image accentStrip;
        [SerializeField] private Text categoryText;
        [SerializeField] private Text headlineText;

        public void Populate(string headline, string category, Color accent)
        {
            if (accentStrip != null)   accentStrip.color  = accent;
            if (categoryText != null)  categoryText.text  = category.ToUpper();
            if (headlineText != null)  headlineText.text  = headline;
        }
    }
}
