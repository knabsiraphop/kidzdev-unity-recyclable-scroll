using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.RecyclableScroll
{
    // Build phases (incremental):
    //   Phase 1 (this): linear layout, per-index sizes, synchronous binding, both
    //                   orientations, driven by ScrollRect.onValueChanged.
    //   Phase 2: async item loading (instantiate / bind over multiple frames).
    //   Phase 3: grid layout (lines of K items) behind the same layout seam.
    //   Phase 4: loop mode (infinite wrap-around scrolling).

    /// <summary>
    /// A recyclable scrolling list. Reuses a small pool of item views to display an
    /// arbitrarily long data set backed by an <see cref="IRecyclableDataSource"/>.
    /// Sits on a uGUI Scroll View and recycles items in response to the
    /// <see cref="ScrollRect"/>'s scroll events, so only a viewport-worth of
    /// GameObjects ever exist regardless of item count.
    /// </summary>
    [DisallowMultipleComponent]
    public class RecyclableScrollView : MonoBehaviour
    {
        public enum Orientation
        {
            Vertical,
            Horizontal
        }

        [Header("References")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private RecyclableScrollItem itemPrefab;

        [Header("Layout")]
        [SerializeField] private Orientation orientation = Orientation.Vertical;
        [Tooltip("Reserved for Phase 4 (loop mode); ignored today.")]
        [SerializeField] private bool loop;
        [SerializeField] private float spacing;
        [Tooltip("Extra items kept realized on each side of the viewport.")]
        [SerializeField, Min(0)] private int buffer = 1;

        private readonly LinearLayout _layout = new LinearLayout();
        private readonly Dictionary<int, RecyclableScrollItem> _active = new Dictionary<int, RecyclableScrollItem>();
        private readonly Stack<RecyclableScrollItem> _pool = new Stack<RecyclableScrollItem>();
        private readonly List<int> _scratch = new List<int>();

        private IRecyclableDataSource _dataSource;
        private bool _subscribed;
        private int _firstActive;
        private int _lastActive = -1;

        private bool IsVertical => orientation == Orientation.Vertical;

        private void Awake()
        {
            if (scrollRect == null) scrollRect = GetComponent<ScrollRect>();
            if (viewport == null && scrollRect != null) viewport = scrollRect.viewport;
            if (content == null && scrollRect != null) content = scrollRect.content;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        /// <summary>Assign the data source that feeds this view; rebuilds immediately.</summary>
        public void SetDataSource(IRecyclableDataSource dataSource)
        {
            _dataSource = dataSource;
            Refresh();
        }

        /// <summary>Rebuild the visible window from scratch against the current data source.</summary>
        public void Refresh()
        {
            if (scrollRect == null) scrollRect = GetComponent<ScrollRect>();
            Subscribe();

            RecycleAll();
            _layout.Rebuild(_dataSource, spacing);
            ApplyContentSize();

            // Item-size queries and content resize can run before the canvas has laid
            // out the viewport; force an update so the first window is measured correctly.
            Canvas.ForceUpdateCanvases();
            UpdateVisibleWindow(force: true);
        }

        /// <summary>Scroll so the item at <paramref name="index"/> aligns to the viewport start.</summary>
        public void ScrollToIndex(int index)
        {
            if (_dataSource == null || content == null || viewport == null || _layout.Count == 0)
                return;

            index = Mathf.Clamp(index, 0, _layout.Count - 1);
            float max = Mathf.Max(0f, _layout.TotalLength - ViewportMain);
            SetScrollOffset(Mathf.Clamp(_layout.GetStart(index), 0f, max));
            UpdateVisibleWindow(force: true);
        }

        // --- scroll plumbing -------------------------------------------------

        private void Subscribe()
        {
            if (_subscribed || scrollRect == null) return;
            scrollRect.onValueChanged.AddListener(OnScrolled);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || scrollRect == null) return;
            scrollRect.onValueChanged.RemoveListener(OnScrolled);
            _subscribed = false;
        }

        private void OnScrolled(Vector2 _) => UpdateVisibleWindow(force: false);

        private float ViewportMain => viewport == null ? 0f : (IsVertical ? viewport.rect.height : viewport.rect.width);

        // Scroll distance from the content's leading edge, measured along the main axis.
        private float ScrollOffset
        {
            get
            {
                if (content == null) return 0f;
                Vector2 ap = content.anchoredPosition;
                return IsVertical ? ap.y : -ap.x;
            }
        }

        private void SetScrollOffset(float main)
        {
            if (content == null) return;
            Vector2 ap = content.anchoredPosition;
            if (IsVertical) ap.y = main; else ap.x = -main;
            content.anchoredPosition = ap;
        }

        // --- layout / recycling ----------------------------------------------

        private void ApplyContentSize()
        {
            if (content == null) return;

            if (IsVertical)
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.sizeDelta = new Vector2(0f, _layout.TotalLength);
            }
            else
            {
                content.anchorMin = new Vector2(0f, 0f);
                content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 0.5f);
                content.sizeDelta = new Vector2(_layout.TotalLength, 0f);
            }
        }

        private void UpdateVisibleWindow(bool force)
        {
            if (_dataSource == null || content == null || viewport == null || itemPrefab == null)
                return;

            int count = _layout.Count;
            if (count == 0)
            {
                if (_active.Count > 0) RecycleAll();
                return;
            }

            float scroll = ScrollOffset;
            int first = Mathf.Clamp(_layout.IndexAt(scroll) - buffer, 0, count - 1);
            int last = Mathf.Clamp(_layout.IndexAt(scroll + ViewportMain) + buffer, 0, count - 1);

            if (!force && first == _firstActive && last == _lastActive)
                return;

            // Release everything that fell outside the new window.
            _scratch.Clear();
            foreach (KeyValuePair<int, RecyclableScrollItem> kv in _active)
                if (kv.Key < first || kv.Key > last)
                    _scratch.Add(kv.Key);
            for (int i = 0; i < _scratch.Count; i++)
                Release(_scratch[i]);

            // Realize everything newly inside the window.
            for (int i = first; i <= last; i++)
                if (!_active.ContainsKey(i))
                    Acquire(i);

            _firstActive = first;
            _lastActive = last;
        }

        private void Acquire(int index)
        {
            RecyclableScrollItem item = _pool.Count > 0 ? _pool.Pop() : Instantiate(itemPrefab, content);
            if (item.transform.parent != content)
                item.transform.SetParent(content, false);

            PositionItem((RectTransform)item.transform, index);
            item.gameObject.SetActive(true);
            item.Index = index;
            _active[index] = item;

            _dataSource.BindItem(index, item);
            item.InvokeOnBind();
        }

        private void Release(int index)
        {
            if (!_active.TryGetValue(index, out RecyclableScrollItem item)) return;
            _active.Remove(index);
            item.gameObject.SetActive(false);
            _pool.Push(item);
        }

        private void RecycleAll()
        {
            if (_active.Count > 0)
            {
                _scratch.Clear();
                foreach (KeyValuePair<int, RecyclableScrollItem> kv in _active)
                    _scratch.Add(kv.Key);
                for (int i = 0; i < _scratch.Count; i++)
                    Release(_scratch[i]);
            }

            _firstActive = 0;
            _lastActive = -1;
        }

        private void PositionItem(RectTransform rt, int index)
        {
            float start = _layout.GetStart(index);
            float size = _layout.GetSize(index);

            if (IsVertical)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, size);
                rt.anchoredPosition = new Vector2(0f, -start);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(size, 0f);
                rt.anchoredPosition = new Vector2(start, 0f);
            }
        }
    }
}
