using UnityEngine;

namespace KidzDev.Unity.RecyclableScroll
{
    /// <summary>
    /// Supplies data and binding logic to a <see cref="RecyclableScrollView"/>.
    /// Implement this on whatever owns your list data.
    /// </summary>
    public interface IRecyclableDataSource
    {
        /// <summary>Total number of items in the list.</summary>
        int ItemCount { get; }

        /// <summary>Push the data at <paramref name="index"/> into the recycled item <see cref="GameObject"/>.</summary>
        void BindItem(int index, GameObject item);

        /// <summary>
        /// Size (height for vertical, width for horizontal) of the item at
        /// <paramref name="index"/>, in the content's local units.
        /// </summary>
        float GetItemSize(int index);
    }
}
