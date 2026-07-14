using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KidzDev.Unity.RecyclableScroll
{
    // Build phases (incremental):
    //   Phase 1: linear layout, per-index sizes, synchronous binding, both orientations.
    //   Phase 2: async item loading via IAsyncRecyclableDataSource + UniTask.
    //   Phase 3: grid layout (lines of K items) behind the same layout seam.
    //   Phase 4: loop mode (infinite wrap-around scrolling).
    //   Phase 5: windowed owned-scroll engine (no ScrollRect): the content rect never
    //            grows to the data length; instead this component owns the scroll
    //            position and places only the realized window of items at small local
    //            coordinates (itemLocal = absoluteStart - scrollPos). Drag, inertia,
    //            mouse-wheel and edge-elastic are handled here, so item coordinates
    //            stay tiny no matter how long the data set is.

    /// <summary>
    /// A recyclable scrolling list. Reuses a small pool of item views to display an
    /// arbitrarily long data set backed by an <see cref="IRecyclableDataSource"/>.
    /// <para>
    /// Unlike a uGUI <c>ScrollRect</c>, the content rect stays viewport-sized: this
    /// component owns the scroll offset and positions a viewport-worth of pooled items
    /// directly, so coordinates never blow up for huge lists. It handles drag, inertia,
    /// mouse-wheel and elastic edges itself — it needs a raycast-target <c>Graphic</c>
    /// (e.g. an <c>Image</c>) somewhere over the viewport to receive pointer events.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class RecyclableScrollView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public enum Orientation
        {
            Vertical,
            Horizontal
        }

        [Header("References")]
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private GameObject itemPrefab;
        [Tooltip("Prefab per item kind for IKindedDataSource consumers, indexed by IKindedDataSource.GetItemKind(). " +
                 "Kind 0 always resolves to itemPrefab above (index 0 of this array is unused) so non-kinded " +
                 "consumers are unaffected; give kind 0 to whichever shape you want itemPrefab to represent.")]
        [SerializeField] private GameObject[] kindPrefabs;

        [Header("Layout")]
        [SerializeField] private Orientation orientation = Orientation.Vertical;
        [Tooltip("When enabled, scrolling past either end wraps to the other end seamlessly.")]
        [SerializeField] private bool loop;
        [Tooltip("Gap between items. X = gap along the horizontal axis, Y = gap along the vertical axis.")]
        [SerializeField] private Vector2 spacing;
        [Tooltip("Inner padding around the content edges (in pixels).")]
        [SerializeField] private RectOffset padding = new RectOffset();
        [Tooltip("Items per row (vertical) or column (horizontal). 1 = linear list, >1 = grid.")]
        [SerializeField, Min(1)] private int columns = 1;
        [Tooltip("Extra items kept realized on each side of the viewport.")]
        [SerializeField, Min(0)] private int buffer = 1;

        [Header("Scrolling")]
        [Tooltip("Keep moving with momentum after a flick.")]
        [SerializeField] private bool inertia = true;
        [Tooltip("Velocity falloff per second while coasting (uGUI default 0.135).")]
        [SerializeField, Min(0f)] private float decelerationRate = 0.135f;
        [Tooltip("Rubber-band stiffness at the ends. 0 = hard stop (no overscroll).")]
        [SerializeField, Min(0f)] private float elasticity = 0.1f;
        [Tooltip("Pixels scrolled per mouse-wheel notch.")]
        [SerializeField, Min(0f)] private float scrollSensitivity = 30f;

        private IScrollLayout _layout;
        private readonly Dictionary<int, RecyclableScrollItem> _active = new Dictionary<int, RecyclableScrollItem>();
        private readonly Dictionary<int, CancellationTokenSource> _pendingBinds = new Dictionary<int, CancellationTokenSource>();
        private readonly Dictionary<int, Stack<RecyclableScrollItem>> _pools = new Dictionary<int, Stack<RecyclableScrollItem>>();
        private readonly List<int> _indicesToRecycle = new List<int>();
        private readonly HashSet<int> _warnedMissingKindPrefabs = new HashSet<int>();

        private IRecyclableDataSource _dataSource;
        private IKindedDataSource _kindedSource;
        private IItemInstantiator _instantiator;
        private IKindedItemInstantiator _kindedInstantiator;
        private int _firstActive;
        private int _lastActive = -1;

        // Owned scroll state (main-axis scalar; positive = scrolled toward the end).
        private float _scrollPos;
        private float _velocity;
        private bool _dragging;
        private bool _refreshing;
        private bool _dragPointerValid;
        private float _pointerStartMain;
        private float _scrollStartPos;
        private float _prevScrollPos;

        private bool IsVertical => orientation == Orientation.Vertical;

        /// <summary>Total main-axis length of the data set (what a full-size content rect would be).</summary>
        public float TotalContentLength => _layout?.TotalLength ?? 0f;

        /// <summary>Current scroll offset along the main axis, from the leading edge.</summary>
        public float ScrollPosition => _scrollPos;

        /// <summary>Maximum scroll offset; zero when the content fits entirely within the viewport.</summary>
        public float MaxScrollPosition => MaxScroll;

        /// <summary>True when the scroll is at or before the leading edge.</summary>
        public bool IsAtStart => _scrollPos <= 0f;

        /// <summary>True when the scroll is at or past the trailing edge, or when content fits in the viewport.</summary>
        public bool IsAtEnd => _layout == null || _scrollPos >= MaxScroll - 0.5f;

        private void Awake()
        {
            if (padding == null) padding = new RectOffset();
        }

        private void OnDestroy()
        {
            foreach (CancellationTokenSource cts in _pendingBinds.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _pendingBinds.Clear();

            if (_kindedInstantiator != null)
            {
                foreach (Stack<RecyclableScrollItem> pool in _pools.Values)
                    foreach (RecyclableScrollItem item in pool)
                        _kindedInstantiator.Destroy(item.gameObject);
                foreach (KeyValuePair<int, RecyclableScrollItem> kv in _active)
                    _kindedInstantiator.Destroy(kv.Value.gameObject);
            }
            else if (_instantiator != null)
            {
                foreach (Stack<RecyclableScrollItem> pool in _pools.Values)
                    foreach (RecyclableScrollItem item in pool)
                        _instantiator.Destroy(item.gameObject);
                foreach (KeyValuePair<int, RecyclableScrollItem> kv in _active)
                    _instantiator.Destroy(kv.Value.gameObject);
            }
        }

        /// <summary>
        /// Assign the data source that feeds this view; resets to the top and rebuilds. Auto-detects
        /// <see cref="IKindedDataSource"/>, and either <see cref="IItemInstantiator"/> or
        /// <see cref="IKindedItemInstantiator"/> (the non-kinded one takes precedence if the source
        /// implements both, for backward compatibility with existing single-shape consumers).
        /// </summary>
        public void SetDataSource(IRecyclableDataSource dataSource)
        {
            _dataSource = dataSource;
            _kindedSource = dataSource as IKindedDataSource;
            _instantiator = dataSource is IItemInstantiator i && i.IsEnabled ? i : null;
            _kindedInstantiator = _instantiator == null && dataSource is IKindedItemInstantiator ki && ki.IsEnabled ? ki : null;
            _scrollPos = 0f;
            _velocity = 0f;
            Refresh();
        }

        /// <summary>
        /// Assign the data source and an explicit instantiator in one call. The instantiator
        /// takes precedence over any <see cref="IItemInstantiator"/> the data source might
        /// implement.
        /// </summary>
        public void SetDataSource(IRecyclableDataSource dataSource, IItemInstantiator instantiator)
        {
            _kindedSource = dataSource as IKindedDataSource;
            _instantiator = instantiator is { IsEnabled: true } ? instantiator : null;
            _kindedInstantiator = null;
            _dataSource = dataSource;
            _scrollPos = 0f;
            _velocity = 0f;
            Refresh();
        }

        /// <summary>
        /// Assign the data source and an explicit kind-aware instantiator in one call. The
        /// instantiator takes precedence over any <see cref="IKindedItemInstantiator"/> the data
        /// source might implement.
        /// </summary>
        public void SetDataSource(IRecyclableDataSource dataSource, IKindedItemInstantiator instantiator)
        {
            _kindedSource = dataSource as IKindedDataSource;
            _kindedInstantiator = instantiator is { IsEnabled: true } ? instantiator : null;
            _instantiator = null;
            _dataSource = dataSource;
            _scrollPos = 0f;
            _velocity = 0f;
            Refresh();
        }

        /// <summary>
        /// Override the item instantiator without replacing the data source. Pass <c>null</c>
        /// to revert to the default <c>Instantiate(itemPrefab)</c> behaviour. Clears any
        /// <see cref="IKindedItemInstantiator"/> previously set via
        /// <see cref="SetDataSource(IRecyclableDataSource,IKindedItemInstantiator)"/>.
        /// </summary>
        public void SetInstantiator(IItemInstantiator instantiator)
        {
            _instantiator = instantiator is { IsEnabled: true } ? instantiator : null;
            _kindedInstantiator = null;
        }

        /// <summary>Rebuild the visible window from scratch against the current data source.</summary>
        public void Refresh()
        {
            if (viewport == null || content == null) return;
            // Guard against re-entrant calls (e.g. a sizer's OnSizeChanged firing again
            // during Canvas.ForceUpdateCanvases below). The outer call already reads the
            // updated viewport in BuildMetrics after the canvas flush, so the inner call
            // would produce the same result and must not be allowed to recycle/re-acquire
            // items while the outer call is mid-flight.
            if (_refreshing) return;

            _refreshing = true;
            try
            {
                EnsureLayout();
                ApplyContentLayout();
                // Deliberate full canvas flush: Rebuild reads the viewport's cross-axis size below,
                // which is only accurate once pending layout has been applied. Cost is acceptable
                // here because Refresh runs on data-source changes, not per frame.
                Canvas.ForceUpdateCanvases();

                _layout.Rebuild(_dataSource, BuildMetrics());
                if (!loop) _scrollPos = Mathf.Clamp(_scrollPos, 0f, MaxScroll);

                RecycleAll();
                UpdateVisibleWindow(force: true);
                RepositionActive();
            }
            finally
            {
                _refreshing = false;
            }
        }

        /// <summary>Scroll so the item at <paramref name="index"/> aligns to the viewport start.</summary>
        public void ScrollToIndex(int index)
        {
            if (_dataSource == null || _layout == null || _layout.Count == 0) return;
            index = Mathf.Clamp(index, 0, _layout.Count - 1);
            ScrollToOffset(_layout.GetStart(index));
        }

        /// <summary>Jump to an absolute main-axis offset (clamped to the scrollable range unless looping).</summary>
        public void ScrollToOffset(float mainOffset)
        {
            if (_layout == null) return;
            _velocity = 0f;
            _scrollPos = loop ? mainOffset : Mathf.Clamp(mainOffset, 0f, MaxScroll);
            UpdateVisibleWindow(force: true);
            RepositionActive();
        }

        /// <summary>
        /// Call after appending items to the end of the backing data source. Re-layouts without
        /// moving the view or rebinding already-active items — appended items don't shift earlier
        /// offsets/indices, so nothing currently on screen needs to change.
        /// </summary>
        public void AppendData()
        {
            if (_dataSource == null || _layout == null) return;
            EnsureLayout();
            _layout.Rebuild(_dataSource, BuildMetrics());
            UpdateVisibleWindow(force: true);
            RepositionActive();
        }

        /// <summary>
        /// Call after inserting <paramref name="count"/> items at the front of the backing data
        /// source (e.g. a page of older chat history). Re-layouts and keeps the currently visible
        /// content pinned at the same screen position — no visual jump. Not supported in loop mode
        /// (falls back to <see cref="Refresh"/>). Defined precisely for a linear (single-column)
        /// layout or a row-aligned <paramref name="count"/> on a grid layout.
        /// </summary>
        public void PrependData(int count)
        {
            if (_dataSource == null || _layout == null || count <= 0) return;
            if (loop)
            {
                Debug.LogError("RecyclableScrollView: PrependData is not supported in loop mode; falling back to Refresh().", this);
                Refresh();
                return;
            }

            // Nothing existed before this insert (e.g. first page load) — no on-screen content to
            // anchor, so a plain rebuild is both correct and cheaper.
            if (_dataSource.ItemCount <= count)
            {
                Refresh();
                return;
            }

            EnsureLayout();
            _layout.Rebuild(_dataSource, BuildMetrics());

            // Main-axis length of the newly inserted block (spacing included): the leading edge of
            // what is now item `count` is exactly how far the previously-first item moved.
            float added = _layout.GetStart(count);
            _scrollPos += added;
            // Keep an in-flight drag stable — both are baselines the drag/inertia code reads from
            // on the next frame; without shifting them the next drag delta snaps the view back.
            _scrollStartPos += added;
            _prevScrollPos += added;

            RecycleAll();
            UpdateVisibleWindow(force: true);
            RepositionActive();
        }

        private void EnsureLayout()
        {
            int cols = Mathf.Max(1, columns);
            if (_layout == null || _layout.Columns != cols)
                _layout = cols > 1 ? (IScrollLayout)new GridLayout(cols) : new LinearLayout();
        }

        // --- metrics / axis mapping ------------------------------------------

        private float ViewportMain  => viewport == null ? 0f : (IsVertical ? viewport.rect.height : viewport.rect.width);
        private float ViewportCross => viewport == null ? 0f : (IsVertical ? viewport.rect.width  : viewport.rect.height);

        // Padding split onto the scroll (main) and cross axes per orientation.
        private float MainLeadingPad  => IsVertical ? padding.top    : padding.left;
        private float MainTrailingPad => IsVertical ? padding.bottom : padding.right;
        private float CrossLeadingPad  => IsVertical ? padding.left  : padding.top;
        private float CrossTrailingPad => IsVertical ? padding.right : padding.bottom;
        private float MainSpacing  => IsVertical ? spacing.y : spacing.x;
        private float CrossSpacing => IsVertical ? spacing.x : spacing.y;

        private float MaxScroll => _layout == null ? 0f : Mathf.Max(0f, _layout.TotalLength - ViewportMain);

        private LayoutMetrics BuildMetrics() => new LayoutMetrics(
            mainSpacing: MainSpacing,
            crossSpacing: CrossSpacing,
            mainLeadingPad: MainLeadingPad,
            mainTrailingPad: MainTrailingPad,
            crossLeadingPad: CrossLeadingPad,
            crossExtent: ViewportCross - CrossLeadingPad - CrossTrailingPad);

        /// <summary>The cross-axis size every linear (non-grid) item root is stretched to by
        /// <see cref="PositionItem"/> — viewport cross size minus both cross-axis paddings. 0 until
        /// the viewport has a valid rect (e.g. before the first layout pass). Exposed so a data
        /// source can measure content (e.g. text wrap width) against the exact width its item will
        /// actually render at, instead of duplicating this padding math.
        /// <para><b>Axis warning:</b> "cross-axis" means the item's <c>RectTransform.rect.width</c>
        /// when <see cref="orientation"/> is <see cref="Orientation.Vertical"/> (the item stretches
        /// horizontally), but its <c>rect.height</c> when <see cref="Orientation.Horizontal"/> (the
        /// item stretches vertically instead). A caller measuring an item's on-screen width must
        /// account for this — this value is only interchangeable with "item width" for a vertical
        /// scroll view.</para></summary>
        public float LinearItemCrossSize => BuildMetrics().CrossExtent;

        // --- virtual-index helpers (loop) ------------------------------------
        //
        // Index vocabulary used throughout the loop path:
        //   data index    — the real index into the data source, always in [0, Count).
        //   virtual index — an unbounded index that encodes the lap (which copy of the list)
        //                   plus the data index: virtual = lap * Count + data. May be negative
        //                   or exceed Count. The realized window (_firstActive/_lastActive) and
        //                   _active keys are virtual indices when looping.
        //   physical      — alias for data index when disambiguating from virtual in placement.
        // ToDataIndex collapses a virtual index back to its data index.

        // Loop: split a possibly-out-of-range offset into a lap (which full copy of the
        // list) and a physical offset within one copy, then combine to a virtual index.
        private int LoopIndexAt(float offset)
        {
            float total = _layout.TotalLength;
            if (total <= 0f) return 0;
            int lap = Mathf.FloorToInt(offset / total);
            float physOffset = offset - lap * total;
            return lap * _layout.Count + _layout.IndexAt(physOffset);
        }

        // Virtual index → data index (wraps any integer into [0, count)).
        private int ToDataIndex(int virtualIndex)
        {
            int count = _layout.Count;
            int lap = Mathf.FloorToInt((float)virtualIndex / count);
            return virtualIndex - lap * count;
        }

        // Absolute main-axis start of a (possibly virtual) index, including lap offset.
        private float AbsoluteStart(int virtualIndex)
        {
            if (loop && _layout.Count > 0)
            {
                int lap = Mathf.FloorToInt((float)virtualIndex / _layout.Count);
                int phys = virtualIndex - lap * _layout.Count;
                return lap * _layout.TotalLength + _layout.GetStart(phys);
            }
            return _layout.GetStart(virtualIndex);
        }

        // --- layout / recycling ----------------------------------------------

        // Content stays pinned to the viewport (it never moves); items carry the scroll.
        private void ApplyContentLayout()
        {
            if (content == null) return;
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
        }

        private void UpdateVisibleWindow(bool force)
        {
            if (_dataSource == null || content == null || viewport == null || itemPrefab == null || _layout == null)
                return;

            int count = _layout.Count;
            if (count == 0)
            {
                if (_active.Count > 0) RecycleAll();
                return;
            }

            float scroll = _scrollPos;
            int cols = _layout.Columns;
            int first, last;

            if (loop)
            {
                // Virtual indices — may be negative or exceed count.
                int firstLine = LoopIndexAt(scroll);
                int lastLine  = LoopIndexAt(scroll + ViewportMain);
                first = firstLine - buffer * cols;
                // Snap to the start of the containing line (floor division for negatives).
                first = Mathf.FloorToInt((float)first / cols) * cols;
                last  = lastLine + (buffer + 1) * cols - 1;
            }
            else
            {
                int firstLine = _layout.IndexAt(scroll);
                int lastLine  = _layout.IndexAt(scroll + ViewportMain);
                // Go back <buffer> lines and snap to the start of that line.
                first = Mathf.Clamp(firstLine - buffer * cols, 0, count - 1);
                first = (first / cols) * cols;
                // Go forward: rest of the last visible line + <buffer> lines.
                last  = Mathf.Clamp(lastLine + (buffer + 1) * cols - 1, 0, count - 1);
            }

            if (!force && first == _firstActive && last == _lastActive)
                return;

            // Release everything that fell outside the new window.
            _indicesToRecycle.Clear();
            foreach (KeyValuePair<int, RecyclableScrollItem> kv in _active)
                if (kv.Key < first || kv.Key > last)
                    _indicesToRecycle.Add(kv.Key);
            for (int i = 0; i < _indicesToRecycle.Count; i++)
                Release(_indicesToRecycle[i]);

            // Realize everything newly inside the window. Skip indices already realized or
            // with an async acquire in flight, so a slow instantiator can't be double-started.
            for (int i = first; i <= last; i++)
                if (!_active.ContainsKey(i) && !_pendingBinds.ContainsKey(i))
                    Acquire(i);

            _firstActive = first;
            _lastActive = last;
        }

        // Re-place every realized item for the current scroll position. Cheap: the
        // active set is only a viewport-worth of items.
        private void RepositionActive()
        {
            foreach (KeyValuePair<int, RecyclableScrollItem> kv in _active)
                PositionItem((RectTransform)kv.Value.transform, kv.Key);
        }

        // Realize the item at <index>. Fully synchronous data sources (no async bind, no custom
        // instantiator) take an allocation-free fast path; anything async goes through the
        // pipeline below with a per-item CancellationTokenSource tracked in _pendingBinds.
        private void Acquire(int index)
        {
            if (_instantiator == null && _kindedInstantiator == null && _dataSource is not IAsyncRecyclableDataSource)
                AcquireSync(index);
            else
                AcquireAsync(index).Forget();
        }

        // Get-or-create the pool for a kind. Non-kinded sources always use kind 0.
        private Stack<RecyclableScrollItem> GetPool(int kind)
        {
            if (!_pools.TryGetValue(kind, out Stack<RecyclableScrollItem> pool))
            {
                pool = new Stack<RecyclableScrollItem>();
                _pools[kind] = pool;
            }
            return pool;
        }

        // Kind 0 always resolves to itemPrefab (preserves exact behaviour for non-kinded
        // consumers). Other kinds index into kindPrefabs; a missing entry logs once and falls
        // back to itemPrefab so a misconfigured kind never hard-fails the view.
        private GameObject ResolveKindPrefab(int kind)
        {
            if (kind == 0) return itemPrefab;

            if (kindPrefabs != null && kind >= 0 && kind < kindPrefabs.Length && kindPrefabs[kind] != null)
                return kindPrefabs[kind];

            if (_warnedMissingKindPrefabs.Add(kind))
                Debug.LogError($"RecyclableScrollView: no kindPrefabs entry for kind {kind}; falling back to itemPrefab.", this);
            return itemPrefab;
        }

        /// <summary>Public read-only lookup of the prefab a given <see cref="IKindedDataSource"/>
        /// kind resolves to, for consumers that need to inspect the prefab asset itself (e.g. to
        /// measure a child's layout) without instantiating it. Same kind-0/fallback semantics as
        /// the internal resolver this wraps.</summary>
        public GameObject GetPrefabForKind(int kind) => ResolveKindPrefab(kind);

        // Allocation-free realize+bind for fully synchronous data sources.
        private void AcquireSync(int index)
        {
            int dataIndex = loop ? ToDataIndex(index) : index;
            int kind = _kindedSource?.GetItemKind(dataIndex) ?? 0;
            Stack<RecyclableScrollItem> pool = GetPool(kind);

            RecyclableScrollItem item;
            if (pool.Count > 0)
            {
                item = pool.Pop();
            }
            else
            {
                item = NewItem(Instantiate(ResolveKindPrefab(kind), content));
                item.Kind = kind;
            }

            if (item.transform.parent != content)
                item.transform.SetParent(content, false);

            PlaceAndActivate(item, index);

            item.Index = dataIndex;
            ReplaceActive(index, item);

            try
            {
                _dataSource.BindItem(dataIndex, item.gameObject);
                item.InvokeOnBind();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        private async UniTaskVoid AcquireAsync(int index)
        {
            var cts = new CancellationTokenSource();
            _pendingBinds[index] = cts;

            try
            {
                int dataIndex = loop ? ToDataIndex(index) : index;
                int kind = _kindedSource?.GetItemKind(dataIndex) ?? 0;
                Stack<RecyclableScrollItem> pool = GetPool(kind);

                RecyclableScrollItem item;
                if (pool.Count > 0)
                {
                    item = pool.Pop();
                }
                else if (_kindedInstantiator != null)
                {
                    GameObject go = await _kindedInstantiator.InstantiateAsync(kind, cts.Token);
                    // Cancelled while the object was loading: destroy it and bail.
                    if (cts.IsCancellationRequested)
                    {
                        _kindedInstantiator.Destroy(go);
                        return;
                    }
                    go.transform.SetParent(content, false);
                    item = NewItem(go);
                    item.Kind = kind;
                }
                else if (_instantiator != null)
                {
                    GameObject go = await _instantiator.InstantiateAsync(cts.Token);
                    // Cancelled while the object was loading: destroy it and bail.
                    if (cts.IsCancellationRequested)
                    {
                        _instantiator.Destroy(go);
                        return;
                    }
                    go.transform.SetParent(content, false);
                    item = NewItem(go);
                    item.Kind = kind;
                }
                else
                {
                    item = NewItem(Instantiate(ResolveKindPrefab(kind), content));
                    item.Kind = kind;
                }

                if (item.transform.parent != content)
                    item.transform.SetParent(content, false);

                PlaceAndActivate(item, index);

                item.Index = dataIndex;
                ReplaceActive(index, item);

                if (_dataSource is IAsyncRecyclableDataSource asyncSource)
                    await asyncSource.BindItemAsync(dataIndex, item.gameObject, cts.Token);
                else
                    _dataSource.BindItem(dataIndex, item.gameObject);

                // Skip the bound-callback if the item scrolled out while binding.
                if (!cts.IsCancellationRequested)
                    item.InvokeOnBind();
            }
            catch (System.OperationCanceledException) { /* item scrolled out mid-flight */ }
            catch (System.Exception e) { Debug.LogException(e); }
            finally
            {
                ClearPending(index, cts);
            }
        }

        // Get-or-add the item component on a freshly created GameObject.
        private static RecyclableScrollItem NewItem(GameObject go)
            => go.GetComponent<RecyclableScrollItem>() ?? go.AddComponent<RecyclableScrollItem>();

        // Position the item for its (virtual) index and show it.
        private void PlaceAndActivate(RecyclableScrollItem item, int index)
        {
            PositionItem((RectTransform)item.transform, index);
            item.gameObject.SetActive(true);
        }

        // Insert into the active set, recycling any stale occupant of the same slot first
        // (defends against a re-entrant realize of the same index).
        private void ReplaceActive(int index, RecyclableScrollItem item)
        {
            if (_active.TryGetValue(index, out RecyclableScrollItem existing) && existing != item)
            {
                existing.gameObject.SetActive(false);
                GetPool(existing.Kind).Push(existing);
            }
            _active[index] = item;
        }

        // Remove this task's pending-bind entry only if it still owns the slot, then dispose.
        // A released-then-reacquired index installs a newer CTS we must not clobber.
        private void ClearPending(int index, CancellationTokenSource cts)
        {
            if (_pendingBinds.TryGetValue(index, out CancellationTokenSource current) && current == cts)
                _pendingBinds.Remove(index);
            cts.Dispose();
        }

        private void Release(int index)
        {
            if (_pendingBinds.TryGetValue(index, out CancellationTokenSource cts))
            {
                cts.Cancel();
                _pendingBinds.Remove(index);
            }

            if (!_active.TryGetValue(index, out RecyclableScrollItem item)) return;
            _active.Remove(index);
            item.gameObject.SetActive(false);
            GetPool(item.Kind).Push(item);
        }

        private void RecycleAll()
        {
            foreach (CancellationTokenSource cts in _pendingBinds.Values)
                cts.Cancel();
            _pendingBinds.Clear();

            if (_active.Count > 0)
            {
                _indicesToRecycle.Clear();
                foreach (KeyValuePair<int, RecyclableScrollItem> kv in _active)
                    _indicesToRecycle.Add(kv.Key);
                for (int i = 0; i < _indicesToRecycle.Count; i++)
                    Release(_indicesToRecycle[i]);
            }

            _firstActive = 0;
            _lastActive = -1;
        }

        private void PositionItem(RectTransform rt, int virtualIndex)
        {
            int phys      = loop && _layout.Count > 0 ? ToDataIndex(virtualIndex) : virtualIndex;
            float mainLocal = AbsoluteStart(virtualIndex) - _scrollPos;
            float size      = _layout.GetSize(phys);
            float crossSize = _layout.GetCrossSize(phys);
            bool  fixedCross = crossSize >= 0f;  // false = linear stretch, true = grid cell

            if (IsVertical)
            {
                if (fixedCross)
                {
                    float crossStart = _layout.GetCrossStart(phys);
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot            = new Vector2(0f, 1f);
                    rt.sizeDelta        = new Vector2(crossSize, size);
                    rt.anchoredPosition = new Vector2(crossStart, -mainLocal);
                }
                else
                {
                    float cl = CrossLeadingPad, cr = CrossTrailingPad;
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot            = new Vector2(0.5f, 1f);
                    rt.sizeDelta        = new Vector2(-(cl + cr), size);
                    rt.anchoredPosition = new Vector2((cl - cr) * 0.5f, -mainLocal);
                }
            }
            else
            {
                if (fixedCross)
                {
                    float crossStart = _layout.GetCrossStart(phys);
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot            = new Vector2(0f, 1f);
                    rt.sizeDelta        = new Vector2(size, crossSize);
                    rt.anchoredPosition = new Vector2(mainLocal, -crossStart);
                }
                else
                {
                    float cl = CrossLeadingPad, cr = CrossTrailingPad;
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot            = new Vector2(0f, 0.5f);
                    rt.sizeDelta        = new Vector2(size, -(cl + cr));
                    rt.anchoredPosition = new Vector2(mainLocal, (cr - cl) * 0.5f);
                }
            }
        }

        // --- scroll engine (drag + inertia + wheel + elastic) ----------------

        void IBeginDragHandler.OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _velocity = 0f;
            _dragPointerValid = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport, eventData.position, eventData.pressEventCamera, out Vector2 local);
            _pointerStartMain = IsVertical ? local.y : local.x;
            _scrollStartPos = _scrollPos;
            _prevScrollPos = _scrollPos;
            _dragging = true;
        }

        void IDragHandler.OnDrag(PointerEventData eventData)
        {
            if (!_dragging || !_dragPointerValid || _layout == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    viewport, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            float cur = IsVertical ? local.y : local.x;
            float pointerDelta = cur - _pointerStartMain;
            // Vertical: dragging up (+y) reveals later items. Horizontal: dragging left (-x) does.
            float raw = _scrollStartPos + (IsVertical ? pointerDelta : -pointerDelta);
            SetScrollPos(ApplyOverscroll(raw));
        }

        void IEndDragHandler.OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _dragging = false;
        }

        void IScrollHandler.OnScroll(PointerEventData eventData)
        {
            if (_layout == null) return;
            _velocity = 0f;
            // Wheel up (positive y) scrolls toward the start of the list.
            float delta = eventData.scrollDelta.y;
            SetScrollPos(ApplyOverscroll(_scrollPos - delta * scrollSensitivity));
        }

        private void LateUpdate()
        {
            if (_dataSource == null || _layout == null) return;
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            if (_dragging)
            {
                // Track velocity so a flick keeps coasting after release.
                float newVelocity = (_scrollPos - _prevScrollPos) / dt;
                _velocity = Mathf.Lerp(_velocity, newVelocity, dt * 10f);
                _prevScrollPos = _scrollPos;
                return;
            }

            if (loop)
            {
                if (inertia && Mathf.Abs(_velocity) > 1f)
                {
                    _velocity *= Mathf.Pow(decelerationRate, dt);
                    if (Mathf.Abs(_velocity) < 1f) _velocity = 0f;
                    SetScrollPos(_scrollPos + _velocity * dt);
                }
                return;
            }

            float min = 0f, max = MaxScroll;
            bool outOfBounds = _scrollPos < min || _scrollPos > max;

            if (!outOfBounds && (!inertia || Mathf.Abs(_velocity) <= 1f))
            {
                _velocity = 0f;
                return;
            }

            float target;
            if (outOfBounds && elasticity > 0f)
            {
                float bound = _scrollPos < min ? min : max;
                target = Mathf.SmoothDamp(_scrollPos, bound, ref _velocity, elasticity, Mathf.Infinity, dt);
                if (Mathf.Abs(target - bound) < 1f) { target = bound; _velocity = 0f; }
            }
            else if (outOfBounds)
            {
                target = Mathf.Clamp(_scrollPos, min, max);
                _velocity = 0f;
            }
            else
            {
                _velocity *= Mathf.Pow(decelerationRate, dt);
                if (Mathf.Abs(_velocity) < 1f) _velocity = 0f;
                target = _scrollPos + _velocity * dt;
                if (elasticity <= 0f) target = Mathf.Clamp(target, min, max);
            }

            SetScrollPos(target);
        }

        // Apply the new scroll position, then refresh the realized window and re-place items.
        private void SetScrollPos(float value)
        {
            _scrollPos = value;
            UpdateVisibleWindow(force: false);
            RepositionActive();
        }

        // Rubber-band an out-of-range position back toward the edge (uGUI's formula).
        // With elasticity == 0 we hard-clamp (no overscroll). Looping never clamps.
        private float ApplyOverscroll(float pos)
        {
            if (loop) return pos;
            float min = 0f, max = MaxScroll;
            if (pos < min) return elasticity > 0f ? min - RubberDelta(min - pos) : min;
            if (pos > max) return elasticity > 0f ? max + RubberDelta(pos - max) : max;
            return pos;
        }

        private float RubberDelta(float overStretch)
        {
            float view = ViewportMain;
            if (view <= 0f) return 0f;
            return (1f - 1f / (overStretch / view + 1f)) * view;
        }
    }
}
