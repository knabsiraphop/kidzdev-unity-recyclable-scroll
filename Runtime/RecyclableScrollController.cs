using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KidzDev.Unity.RecyclableScroll
{
    /// <summary>
    /// High-level bridge component that exposes named scroll operations over a
    /// <see cref="RecyclableScrollView"/>. Wire <see cref="scrollView"/> in the
    /// inspector or place this component on the same GameObject as the view.
    /// <para>
    /// Optionally wire a uGUI <see cref="Scrollbar"/> to <c>scrollbar</c>: the
    /// controller keeps it in sync with the scroll position and routes bar drags
    /// back into the view.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class RecyclableScrollController : MonoBehaviour
    {
        [SerializeField] private RecyclableScrollView scrollView;
        [SerializeField] private Scrollbar scrollbar;

        [Header("Events")]
        [SerializeField] private UnityEvent onScrolledToStart = new UnityEvent();
        [SerializeField] private UnityEvent onScrolledToEnd   = new UnityEvent();

        private bool _initialized;
        private bool _wasAtStart;
        private bool _wasAtEnd;
        private bool _settingScrollbar; // prevents feedback loop when we write scrollbar.value

        /// <summary>Fires once each time the scroll reaches or passes the leading edge.</summary>
        public UnityEvent OnScrolledToStart => onScrolledToStart;

        /// <summary>Fires once each time the scroll reaches or passes the trailing edge.</summary>
        public UnityEvent OnScrolledToEnd => onScrolledToEnd;

        /// <summary>
        /// True after <see cref="Init"/>, <see cref="Refill"/>, <see cref="UpdateData"/>,
        /// or <see cref="Clear"/> has been called at least once.
        /// </summary>
        public bool IsInitialized => _initialized;

        private void Awake()
        {
            if (scrollView == null)
                scrollView = GetComponent<RecyclableScrollView>();

            if (scrollbar != null)
                scrollbar.onValueChanged.AddListener(OnScrollbarChanged);
        }

        private void OnDestroy()
        {
            if (scrollbar != null)
                scrollbar.onValueChanged.RemoveListener(OnScrollbarChanged);
        }

        private void LateUpdate()
        {
            if (!_initialized || scrollView == null) return;

            bool atStart = scrollView.IsAtStart;
            bool atEnd   = scrollView.IsAtEnd;

            if (atStart && !_wasAtStart) onScrolledToStart.Invoke();
            if (atEnd   && !_wasAtEnd)  onScrolledToEnd.Invoke();

            _wasAtStart = atStart;
            _wasAtEnd   = atEnd;

            SyncScrollbar();
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>Assign a data source for the first time and reset scroll to the top.</summary>
        public void Init(IRecyclableDataSource dataSource)
        {
            _initialized = true;
            ResetEdgeTracking();
            scrollView.SetDataSource(dataSource);
        }

        /// <summary>Remove all items and leave the scroll empty.</summary>
        public void Clear()
        {
            _initialized = true;
            ResetEdgeTracking();
            scrollView.SetDataSource(EmptyDataSource.Instance);
        }

        /// <summary>Replace the data set with a new one and reset scroll to the top.</summary>
        public void Refill(IRecyclableDataSource dataSource)
        {
            _initialized = true;
            ResetEdgeTracking();
            scrollView.SetDataSource(dataSource);
        }

        /// <summary>
        /// Replace the data set while preserving the current scroll position.
        /// Use when the list contents change but the user should stay at the same location.
        /// </summary>
        public void UpdateData(IRecyclableDataSource dataSource)
        {
            _initialized = true;
            float savedPos = scrollView.ScrollPosition;
            scrollView.SetDataSource(dataSource);
            scrollView.ScrollToOffset(savedPos);
        }

        /// <summary>
        /// Re-bind all visible items against the current data source without resetting
        /// scroll position. Use when data values change in-place without affecting item
        /// count or sizes.
        /// </summary>
        public void Refresh()
        {
            scrollView.Refresh();
        }

        /// <summary>Scroll to the leading edge of the list.</summary>
        public void ScrollToStart()
        {
            scrollView.ScrollToOffset(0f);
        }

        /// <summary>Scroll to the trailing edge of the list.</summary>
        public void ScrollToEnd()
        {
            scrollView.ScrollToOffset(scrollView.MaxScrollPosition);
        }

        /// <summary>Scroll so the item at <paramref name="index"/> aligns to the viewport start.</summary>
        public void JumpToIndex(int index)
        {
            scrollView.ScrollToIndex(index);
        }

        // -------------------------------------------------------------------------
        // Scrollbar sync
        // -------------------------------------------------------------------------

        private void SyncScrollbar()
        {
            if (scrollbar == null) return;

            float total = scrollView.TotalContentLength;
            float max   = scrollView.MaxScrollPosition;

            _settingScrollbar = true;

            // Handle proportion: how much of the content fits in the viewport.
            scrollbar.size  = total > 0f ? (total - max) / total : 1f;
            // Normalized scroll position [0, 1].
            scrollbar.value = max > 0f ? scrollView.ScrollPosition / max : 0f;

            _settingScrollbar = false;
        }

        private void OnScrollbarChanged(float value)
        {
            if (_settingScrollbar) return;
            scrollView.ScrollToOffset(value * scrollView.MaxScrollPosition);
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private void ResetEdgeTracking()
        {
            _wasAtStart = false;
            _wasAtEnd   = false;
        }

        private sealed class EmptyDataSource : IRecyclableDataSource
        {
            internal static readonly EmptyDataSource Instance = new EmptyDataSource();
            public int ItemCount => 0;
            public float GetItemSize(int index) => 0f;
            public void BindItem(int index, GameObject item) { }
        }
    }
}
