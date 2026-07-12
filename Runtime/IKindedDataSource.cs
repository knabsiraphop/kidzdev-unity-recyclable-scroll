namespace KidzDev.Unity.RecyclableScroll
{
    /// <summary>
    /// Optional extension of <see cref="IRecyclableDataSource"/> for data sources whose items
    /// come in more than one visual shape (e.g. a chat feed mixing text bubbles, stickers and
    /// system rows). When a <see cref="RecyclableScrollView"/> detects this interface on its
    /// data source, items are instantiated and pooled per kind instead of from a single shared
    /// prefab/pool.
    /// </summary>
    public interface IKindedDataSource : IRecyclableDataSource
    {
        /// <summary>
        /// Small non-negative kind id for the item at <paramref name="index"/>, used to select
        /// which prefab (see <see cref="RecyclableScrollView"/>'s <c>kindPrefabs</c>) or
        /// <see cref="IKindedItemInstantiator"/> output to use, and to keep recycled instances of
        /// different kinds in separate pools. Stable per index until the data changes.
        /// </summary>
        int GetItemKind(int index);
    }
}
