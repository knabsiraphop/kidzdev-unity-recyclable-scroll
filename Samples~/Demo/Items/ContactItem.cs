using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.RecyclableScroll.Samples
{
    public class ContactItem : MonoBehaviour
    {
        [SerializeField] private Image avatarImage;
        [SerializeField] private Text  nameText;
        [SerializeField] private Text  statusText;
        [SerializeField] private Image statusDot;

        private static readonly Color OnlineColor  = new Color(0.22f, 0.82f, 0.42f);
        private static readonly Color OfflineColor = new Color(0.55f, 0.55f, 0.55f);

        public void Populate(string contactName, bool isOnline, Color avatarColor)
        {
            if (avatarImage != null) avatarImage.color = avatarColor;
            if (nameText    != null) nameText.text     = contactName;
            if (statusText  != null) statusText.text   = isOnline ? "Online" : "Offline";
            if (statusDot   != null) statusDot.color   = isOnline ? OnlineColor : OfflineColor;
        }
    }
}
