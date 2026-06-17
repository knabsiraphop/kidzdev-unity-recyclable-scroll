using System.Collections.Generic;

namespace KidzDev.Unity.RecyclableScroll.Tests
{
    /// <summary>
    /// Minimal in-memory <see cref="IRecyclableDataSource"/> shared across the test
    /// suite. Records every <see cref="BindItem"/> call so behavioural tests can
    /// assert binding order once the scroll engine drives it.
    /// </summary>
    internal sealed class StubDataSource : IRecyclableDataSource
    {
        private readonly int _count;
        private readonly float _itemSize;

        public StubDataSource(int count, float itemSize = 100f)
        {
            _count = count;
            _itemSize = itemSize;
        }

        /// <summary>Indices passed to <see cref="BindItem"/>, in call order.</summary>
        public readonly List<int> BoundIndices = new List<int>();

        public int ItemCount => _count;

        public void BindItem(int index, RecyclableScrollItem item) => BoundIndices.Add(index);

        public float GetItemSize(int index) => _itemSize;
    }
}
