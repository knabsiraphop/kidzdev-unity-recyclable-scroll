using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.RecyclableScroll.Samples
{
    /// <summary>
    /// Shows a vertical list, a multi-column grid, and a horizontal list side-by-side
    /// in one scene. All three share the same data source so recycling behaviour is
    /// easy to compare.
    /// </summary>
    public class RecyclableScrollShowcase : MonoBehaviour, IRecyclableDataSource
    {
        [Header("Views")]
        [SerializeField] private RecyclableScrollView verticalView;
        [SerializeField] private RecyclableScrollView gridView;
        [SerializeField] private RecyclableScrollView horizontalView;

        [Header("Data")]
        [SerializeField] private int itemCount = 500;
        [SerializeField] private float itemSize = 100f;

        private void Start()
        {
            verticalView?.SetDataSource(this);
            gridView?.SetDataSource(this);
            horizontalView?.SetDataSource(this);
        }

        public int ItemCount => itemCount;
        public void BindItem(int index, RecyclableScrollItem item)
        {
            var label = item.GetComponentInChildren<Text>(true);
            if (label != null) label.text = index.ToString();

            var bg = item.GetComponent<Image>();
            if (bg != null) bg.color = IndexToColor(index);
        }

        public float GetItemSize(int index) => itemSize;

        private static Color IndexToColor(int index)
        {
            float h = (index * 0.618034f) % 1f;
            return Color.HSVToRGB(h, 0.35f, 0.92f);
        }
    }
}
