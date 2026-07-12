using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.RecyclableScroll
{
    /// <summary>
    /// Optional seam for custom, kind-aware item instantiation on a <see cref="RecyclableScrollView"/>.
    /// Unlike <see cref="IItemInstantiator"/>, this variant is told which kind (see
    /// <see cref="IKindedDataSource.GetItemKind"/>) it is being asked to produce, so a single
    /// instantiator can serve multiple item shapes — e.g. loading different prefabs per kind via
    /// Addressables.
    /// <para>
    /// Set via <see cref="RecyclableScrollView.SetDataSource(IRecyclableDataSource,IKindedItemInstantiator)"/>.
    /// Takes precedence over <see cref="IItemInstantiator"/> and the default <c>kindPrefabs</c> array.
    /// </para>
    /// </summary>
    public interface IKindedItemInstantiator
    {
        /// <summary>
        /// Whether this instantiator is active. Return <c>false</c> to fall back to the view's
        /// default per-kind prefab behaviour.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Produce a new item <see cref="GameObject"/> for <paramref name="kind"/>. Observe
        /// <paramref name="cancellationToken"/>: throw or return early on cancellation. The
        /// returned object must NOT already be parented to the scroll content — the view handles
        /// parenting after this call returns.
        /// </summary>
        UniTask<GameObject> InstantiateAsync(int kind, CancellationToken cancellationToken);

        /// <summary>
        /// Destroy or release an item that is no longer needed — called on pool flush and when
        /// the view is destroyed.
        /// </summary>
        void Destroy(GameObject item);
    }
}
