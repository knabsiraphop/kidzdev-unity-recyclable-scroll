using System;

namespace KidzDev.Unity.RecyclableScroll
{
    /// <summary>
    /// Single-lane linear layout: one item per line along the scroll (main) axis,
    /// each sized by <see cref="IRecyclableDataSource.GetItemSize"/>. Precomputes
    /// prefix-sum offsets so the visible window can be located in O(log n).
    /// <para>
    /// Deals only in main-axis scalars — no Unity types — so the same instance serves
    /// both orientations and can be unit-tested directly. It lives behind this seam so
    /// a future grid layout can replace it without the recycler changing.
    /// </para>
    /// </summary>
    internal sealed class LinearLayout
    {
        // _offsets[i] = leading-edge position of item i; _offsets[Count] = total length.
        private float[] _offsets = Array.Empty<float>();
        private int _count;
        private float _spacing;

        public int Count => _count;

        public float TotalLength => _count == 0 ? 0f : _offsets[_count];

        /// <summary>Recompute offsets from the data source's per-item sizes and spacing.</summary>
        public void Rebuild(IRecyclableDataSource source, float spacing)
        {
            _count = source?.ItemCount ?? 0;
            _spacing = spacing < 0f ? 0f : spacing;

            if (_offsets.Length < _count + 1)
                _offsets = new float[_count + 1];

            float cursor = 0f;
            for (int i = 0; i < _count; i++)
            {
                _offsets[i] = cursor;
                float size = source.GetItemSize(i);
                cursor += size < 0f ? 0f : size;
                if (i < _count - 1)
                    cursor += _spacing;
            }

            if (_count > 0)
                _offsets[_count] = cursor;
        }

        /// <summary>Leading-edge offset of <paramref name="index"/> along the main axis.</summary>
        public float GetStart(int index) => _offsets[index];

        /// <summary>Main-axis size of <paramref name="index"/> (spacing excluded).</summary>
        public float GetSize(int index)
        {
            float span = _offsets[index + 1] - _offsets[index];
            return index < _count - 1 ? span - _spacing : span;
        }

        /// <summary>
        /// Largest index whose leading edge is at or before <paramref name="offset"/>.
        /// Clamps to 0 before the first item and Count-1 past the end.
        /// </summary>
        public int IndexAt(float offset)
        {
            if (_count == 0) return 0;
            if (offset <= 0f) return 0;
            if (offset >= _offsets[_count]) return _count - 1;

            int lo = 0, hi = _count - 1, res = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (_offsets[mid] <= offset) { res = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return res;
        }
    }
}
