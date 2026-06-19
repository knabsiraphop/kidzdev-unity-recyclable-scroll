using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.RecyclableScroll
{
    /// <summary>
    /// Optional seam for custom item instantiation on a <see cref="RecyclableScrollView"/>.
    /// Implement this interface (or pass an <c>instantiate</c> delegate to
    /// <see cref="RecyclableDataSource{TItem}"/> / <see cref="AsyncRecyclableDataSource{TItem}"/>)
    /// to replace the default <c>Instantiate(prefab)</c> call with any async strategy —
    /// Addressables, Resources.LoadAsync, a custom object pool, etc.
    /// <para>
    /// Set via <see cref="RecyclableScrollView.SetInstantiator"/> or by passing it directly to
    /// <see cref="RecyclableScrollView.SetDataSource(IRecyclableDataSource,IItemInstantiator)"/>.
    /// Alternatively, if the data source itself implements <see cref="IItemInstantiator"/> and
    /// <see cref="IsEnabled"/> is true, it is detected automatically in
    /// <see cref="RecyclableScrollView.SetDataSource(IRecyclableDataSource)"/>.
    /// </para>
    /// </summary>
    public interface IItemInstantiator
    {
        /// <summary>
        /// Whether this instantiator is active. Return <c>false</c> to fall back to the view's
        /// default <c>Instantiate(prefab)</c> behaviour. Always <c>true</c> for standalone
        /// implementations; the generic data-source helpers set it based on whether an
        /// <c>instantiate</c> delegate was supplied.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Produce a new item <see cref="GameObject"/>. Observe <paramref name="ct"/>:
        /// throw or return early on cancellation. The returned object must NOT already be
        /// parented to the scroll content — the view handles parenting after this call returns.
        /// </summary>
        UniTask<GameObject> InstantiateAsync(CancellationToken ct);

        /// <summary>
        /// Destroy or release an item that is no longer needed — called on pool flush and when
        /// the view is destroyed. Use <c>Addressables.ReleaseInstance</c> here when the
        /// <see cref="InstantiateAsync"/> implementation loads via Addressables.
        /// </summary>
        void Destroy(GameObject item);
    }
}
