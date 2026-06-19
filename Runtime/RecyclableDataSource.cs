using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.RecyclableScroll
{
    /// <summary>
    /// Synchronous generic data source. Drives a <see cref="RecyclableScrollView"/> from a
    /// fixed count, a per-index size function, and a typed bind action — no subclassing needed.
    /// <para>
    /// <typeparamref name="TItem"/> is the <see cref="Component"/> on each item prefab that
    /// your bind action receives. It does not need to inherit from <see cref="RecyclableScrollItem"/>.
    /// </para>
    /// <para>
    /// Supply an <paramref name="instantiate"/> delegate to replace the view's default
    /// <c>Instantiate(prefab)</c> call with any async strategy (Addressables, Resources, etc.).
    /// When provided this class also acts as an <see cref="IItemInstantiator"/> and is detected
    /// automatically by <see cref="RecyclableScrollView.SetDataSource(IRecyclableDataSource)"/>.
    /// </para>
    /// </summary>
    public sealed class RecyclableDataSource<TItem> : IRecyclableDataSource, IItemInstantiator
        where TItem : Component
    {
        private readonly int _count;
        private readonly Func<int, float> _getSize;
        private readonly Action<TItem, int> _bind;
        private readonly Func<CancellationToken, UniTask<TItem>> _instantiate;
        private readonly Action<GameObject> _destroy;

        /// <param name="count">Total number of items.</param>
        /// <param name="getSize">Returns the main-axis size for item at a given index.</param>
        /// <param name="bind">Called each time an item is recycled into view.</param>
        /// <param name="instantiate">
        /// Optional async factory that produces a new item component. When <c>null</c> the
        /// view falls back to <c>Instantiate(itemPrefab)</c>.
        /// </param>
        /// <param name="destroy">
        /// Optional cleanup called when a pooled item is permanently released (view destroyed,
        /// pool flush). Defaults to <c>Object.Destroy</c>; use
        /// <c>Addressables.ReleaseInstance</c> when <paramref name="instantiate"/> loads via
        /// Addressables.
        /// </param>
        public RecyclableDataSource(
            int count,
            Func<int, float> getSize,
            Action<TItem, int> bind,
            Func<CancellationToken, UniTask<TItem>> instantiate = null,
            Action<GameObject> destroy = null)
        {
            _count = count;
            _getSize = getSize;
            _bind = bind;
            _instantiate = instantiate;
            _destroy = destroy;
        }

        /// <summary>Convenience overload for a uniform item size.</summary>
        public RecyclableDataSource(
            int count,
            float itemSize,
            Action<TItem, int> bind,
            Func<CancellationToken, UniTask<TItem>> instantiate = null,
            Action<GameObject> destroy = null)
            : this(count, _ => itemSize, bind, instantiate, destroy) { }

        public int ItemCount => _count;
        public float GetItemSize(int index) => _getSize(index);
        public void BindItem(int index, GameObject item) => _bind(item.GetComponent<TItem>(), index);

        // IItemInstantiator — only active when an instantiate delegate was supplied
        bool IItemInstantiator.IsEnabled => _instantiate != null;

        async UniTask<GameObject> IItemInstantiator.InstantiateAsync(CancellationToken ct)
            => (await _instantiate(ct)).gameObject;

        void IItemInstantiator.Destroy(GameObject item)
        {
            if (_destroy != null) _destroy(item);
            else UnityEngine.Object.Destroy(item);
        }
    }

    /// <summary>
    /// Async generic data source. Same as <see cref="RecyclableDataSource{TItem}"/> but the
    /// bind action is a <see cref="UniTask"/> so it can span frame boundaries (e.g. asset loads).
    /// In-flight binds are cancelled automatically when an item scrolls out of view.
    /// <para>
    /// Supply an <paramref name="instantiate"/> delegate to replace the view's default
    /// <c>Instantiate(prefab)</c> call with any async strategy (Addressables, Resources, etc.).
    /// </para>
    /// </summary>
    public sealed class AsyncRecyclableDataSource<TItem> : IAsyncRecyclableDataSource, IItemInstantiator
        where TItem : Component
    {
        private readonly int _count;
        private readonly Func<int, float> _getSize;
        private readonly Func<TItem, int, CancellationToken, UniTask> _bind;
        private readonly Func<CancellationToken, UniTask<TItem>> _instantiate;
        private readonly Action<GameObject> _destroy;

        /// <param name="count">Total number of items.</param>
        /// <param name="getSize">Returns the main-axis size for item at a given index.</param>
        /// <param name="bind">Async action called each time an item is recycled into view.</param>
        /// <param name="instantiate">
        /// Optional async factory that produces a new item component. When <c>null</c> the
        /// view falls back to <c>Instantiate(itemPrefab)</c>.
        /// </param>
        /// <param name="destroy">
        /// Optional cleanup called when a pooled item is permanently released. Defaults to
        /// <c>Object.Destroy</c>.
        /// </param>
        public AsyncRecyclableDataSource(
            int count,
            Func<int, float> getSize,
            Func<TItem, int, CancellationToken, UniTask> bind,
            Func<CancellationToken, UniTask<TItem>> instantiate = null,
            Action<GameObject> destroy = null)
        {
            _count = count;
            _getSize = getSize;
            _bind = bind;
            _instantiate = instantiate;
            _destroy = destroy;
        }

        /// <summary>Convenience overload for a uniform item size.</summary>
        public AsyncRecyclableDataSource(
            int count,
            float itemSize,
            Func<TItem, int, CancellationToken, UniTask> bind,
            Func<CancellationToken, UniTask<TItem>> instantiate = null,
            Action<GameObject> destroy = null)
            : this(count, _ => itemSize, bind, instantiate, destroy) { }

        public int ItemCount => _count;
        public float GetItemSize(int index) => _getSize(index);
        public void BindItem(int index, GameObject item) { }
        public UniTask BindItemAsync(int index, GameObject item, CancellationToken ct)
            => _bind(item.GetComponent<TItem>(), index, ct);

        // IItemInstantiator — only active when an instantiate delegate was supplied
        bool IItemInstantiator.IsEnabled => _instantiate != null;

        async UniTask<GameObject> IItemInstantiator.InstantiateAsync(CancellationToken ct)
            => (await _instantiate(ct)).gameObject;

        void IItemInstantiator.Destroy(GameObject item)
        {
            if (_destroy != null) _destroy(item);
            else UnityEngine.Object.Destroy(item);
        }
    }
}
