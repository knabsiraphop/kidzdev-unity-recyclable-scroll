using System.Collections.Generic;

namespace KidzDev.Unity.RecyclableScroll.Tests
{
    /// <summary>
    /// In-memory <see cref="IRecyclableDataSource"/> shared across the suite. Supports
    /// a uniform item size or an explicit per-index size array, and records every
    /// <see cref="BindItem"/> call so behavioural tests can assert binding order.
    /// </summary>
    internal sealed class StubDataSource : IRecyclableDataSource
    {
        private readonly float[] _sizes;   // null => uniform
        private readonly int _count;
        private readonly float _uniformSize;

        public StubDataSource(int count, float itemSize = 100f)
        {
            _count = count;
            _uniformSize = itemSize;
            _sizes = null;
        }

        public StubDataSource(float[] sizes)
        {
            _sizes = sizes;
            _count = sizes.Length;
        }

        /// <summary>Indices passed to <see cref="BindItem"/>, in call order.</summary>
        public readonly List<int> BoundIndices = new List<int>();

        public int ItemCount => _count;

        public void BindItem(int index, RecyclableScrollItem item) => BoundIndices.Add(index);

        public float GetItemSize(int index) => _sizes != null ? _sizes[index] : _uniformSize;
    }
}
