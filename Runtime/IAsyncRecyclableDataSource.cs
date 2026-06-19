using System.Threading;
using Cysharp.Threading.Tasks;

namespace KidzDev.Unity.RecyclableScroll
{
    /// <summary>
    /// Optional extension of <see cref="IRecyclableDataSource"/> for data sources that
    /// bind items asynchronously (e.g. loading a sprite or texture across frame boundaries).
    /// When a <see cref="RecyclableScrollView"/> detects this interface on its data source
    /// it uses <see cref="BindItemAsync"/> instead of <see cref="IRecyclableDataSource.BindItem"/>.
    /// The synchronous <see cref="IRecyclableDataSource.BindItem"/> may be left as a no-op.
    /// </summary>
    public interface IAsyncRecyclableDataSource : IRecyclableDataSource
    {
        /// <summary>
        /// Bind data at <paramref name="index"/> into <paramref name="item"/> asynchronously.
        /// The <paramref name="cancellationToken"/> is cancelled when the item scrolls out of
        /// view before binding finishes — observe it early to avoid wasted work.
        /// </summary>
        UniTask BindItemAsync(int index, RecyclableScrollItem item, CancellationToken cancellationToken);
    }
}
