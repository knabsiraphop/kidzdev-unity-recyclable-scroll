using UnityEngine;

namespace KidzDev.Unity.RecyclableScroll
{
    // Build phases (incremental — only the scaffold exists today):
    //   Phase 1: linear layout, fixed item size, synchronous binding.
    //   Phase 2: async item loading (instantiate / bind over multiple frames).
    //   Phase 3: variable item sizes driven by IRecyclableDataSource.GetItemSize.
    //   Phase 4: loop mode (infinite wrap-around scrolling).

    /// <summary>
    /// A recyclable scrolling list. Reuses a small pool of item views to display
    /// an arbitrarily long data set backed by an <see cref="IRecyclableDataSource"/>.
    /// </summary>
    public class RecyclableScrollView : MonoBehaviour
    {
        public enum Orientation
        {
            Vertical,
            Horizontal
        }

        [Header("References")]
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private RecyclableScrollItem itemPrefab;

        [Header("Layout")]
        [SerializeField] private Orientation orientation = Orientation.Vertical;
        [SerializeField] private bool loop;
        [SerializeField] private float spacing;

        private IRecyclableDataSource _dataSource;

        /// <summary>Assign the data source that feeds this view, then call <see cref="Refresh"/>.</summary>
        public void SetDataSource(IRecyclableDataSource dataSource)
        {
            _dataSource = dataSource;
            // TODO: reset internal pool/layout state and trigger an initial Refresh.
        }

        /// <summary>Rebuild the visible window from scratch against the current data source.</summary>
        public void Refresh()
        {
            // TODO: recompute content size, recycle/instantiate visible items, rebind.
        }

        /// <summary>Scroll so the item at <paramref name="index"/> is brought into view.</summary>
        public void ScrollToIndex(int index)
        {
            // TODO: compute the target offset for index and move content accordingly.
        }
    }
}
