using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.RecyclableScroll.Samples
{
    /// <summary>
    /// Minimal demo data source: a list of dummy string rows wired into a
    /// <see cref="RecyclableScrollView"/>. Each recycled row's <see cref="Text"/> is
    /// updated in <see cref="BindItem"/>, so scrolling shows live "Row N" content
    /// from a small pool of reused item views.
    /// </summary>
    public class RecyclableScrollDemo : MonoBehaviour, IRecyclableDataSource
    {
        [SerializeField] private RecyclableScrollView scrollView;
        [SerializeField] private int rowCount = 1000;
        [SerializeField] private float rowSize = 80f;

        private string[] _rows;

        private void Awake()
        {
            _rows = new string[rowCount];
            for (int i = 0; i < rowCount; i++)
                _rows[i] = $"Row {i}";
        }

        private void Start()
        {
            if (scrollView == null)
            {
                Debug.LogWarning($"{nameof(RecyclableScrollDemo)}: no {nameof(RecyclableScrollView)} assigned.");
                return;
            }

            // SetDataSource rebuilds the view; no separate Refresh call needed.
            scrollView.SetDataSource(this);
        }

        // IRecyclableDataSource ------------------------------------------------

        public int ItemCount => _rows?.Length ?? 0;

        public void BindItem(int index, RecyclableScrollItem item)
        {
            var label = item.GetComponentInChildren<Text>();
            if (label != null)
                label.text = _rows[index];
        }

        public float GetItemSize(int index) => rowSize;
    }
}
