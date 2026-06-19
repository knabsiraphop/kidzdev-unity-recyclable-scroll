using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.RecyclableScroll.Samples
{
    public class ProfileCard : MonoBehaviour
    {
        [SerializeField] private Image avatarImage;
        [SerializeField] private Text  usernameText;

        public void Populate(string username, Color avatarColor)
        {
            if (avatarImage  != null) avatarImage.color = avatarColor;
            if (usernameText != null) usernameText.text = username;
        }
    }
}
