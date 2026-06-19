using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.RecyclableScroll.Samples
{
    public class ProductCard : MonoBehaviour
    {
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private Text  nameText;
        [SerializeField] private Text  priceText;

        public void Populate(string productName, float price, Color color)
        {
            if (thumbnailImage != null) thumbnailImage.color = color;
            if (nameText       != null) nameText.text        = productName;
            if (priceText      != null) priceText.text       = $"${price:F2}";
        }
    }
}
