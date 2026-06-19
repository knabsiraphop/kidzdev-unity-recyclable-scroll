using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.RecyclableScroll.Samples
{
    public class GalleryCard : MonoBehaviour
    {
        [SerializeField] private Image swatchImage;
        [SerializeField] private Text labelText;

        public void Populate(int id, Color color, string label)
        {
            if (swatchImage != null) swatchImage.color = color;
            if (labelText != null)   labelText.text    = label;
        }
    }
}
