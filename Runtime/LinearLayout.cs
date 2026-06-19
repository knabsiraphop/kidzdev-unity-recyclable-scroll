using System;

namespace KidzDev.Unity.RecyclableScroll
{
    /// <summary>
    /// Single-lane linear layout: one item per line along the scroll (main) axis,
    /// each sized by <see cref="IRecyclableDataSource.GetItemSize"/>. Precomputes
    /// per-item start offsets so the visible window can be located in O(log n).
    /// <para>
    /// Deals only in main-axis scalars — no Unity types — so the same instance serves
    /// both orientations and can be unit-tested directly. Cross-axis sizing is left to
    /// the view (linear items stretch to fill the viewport minus cross padding).
    /// </para>
    /// </summary>
    internal sealed class LinearLayout : IScrollLayout
    {
        private float[] _starts = Array.Empty<float>();  // _starts[i] = leading edge of item i (incl. leading pad)
        private float[] _sizes = Array.Empty<float>();
        private int _count;
        private float _total;

        public int Count => _count;
        public float TotalLength => _total;
        public int Columns => 1;

        /// <summary>Recompute start offsets and sizes from the data source, spacing and padding.</summary>
        public void Rebuild(IRecyclableDataSource source, in LayoutMetrics metrics)
        {
            _count = source?.ItemCount ?? 0;

            if (_starts.Length < _count)
            {
                _starts = new float[_count];
                _sizes = new float[_count];
            }

            float cursor = metrics.MainLeadingPad;
            for (int i = 0; i < _count; i++)
            {
                float size = source.GetItemSize(i);
                if (size < 0f) size = 0f;
                _starts[i] = cursor;
                _sizes[i] = size;
                cursor += size;
                if (i < _count - 1)
                    cursor += metrics.MainSpacing;
            }

            _total = _count == 0 ? 0f : cursor + metrics.MainTrailingPad;
        }

        public float GetCrossStart(int index) => 0f;
        public float GetCrossSize(int index) => -1f;

        public float GetStart(int index) => _starts[index];
        public float GetSize(int index) => _sizes[index];

        /// <summary>
        /// Largest index whose leading edge is at or before <paramref name="offset"/>.
        /// Clamps to 0 before the first item and Count-1 past the end.
        /// </summary>
        public int IndexAt(float offset)
        {
            if (_count == 0) return 0;
            if (offset <= _starts[0]) return 0;
            if (offset >= _total) return _count - 1;

            int lo = 0, hi = _count - 1, res = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (_starts[mid] <= offset) { res = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return res;
        }
    }
}
