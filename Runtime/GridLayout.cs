using System;

namespace KidzDev.Unity.RecyclableScroll
{
    /// <summary>
    /// K-column grid layout. Items are packed left-to-right into rows of
    /// <see cref="Columns"/> cells. Each row's main-axis size is the maximum
    /// item size in that row; cross-axis cells share the usable cross extent
    /// (viewport cross minus cross padding) evenly, separated by cross spacing.
    /// Deals only in scalars — no Unity types — so it can be unit-tested directly.
    /// </summary>
    internal sealed class GridLayout : IScrollLayout
    {
        private float[] _rowStarts = Array.Empty<float>();  // _rowStarts[r] = leading edge of row r (incl. leading pad)
        private float[] _rowSizes = Array.Empty<float>();
        private int _count;
        private int _columns;
        private int _rowCount;
        private float _total;
        private float _cellCross;        // cross size of one cell
        private float _crossSpacing;
        private float _crossLeadingPad;

        public int Count => _count;
        public float TotalLength => _total;
        public int Columns => _columns;

        public GridLayout(int columns) => _columns = Math.Max(1, columns);

        public void Rebuild(IRecyclableDataSource source, in LayoutMetrics metrics)
        {
            _count = source?.ItemCount ?? 0;
            _crossSpacing = metrics.CrossSpacing;
            _crossLeadingPad = metrics.CrossLeadingPad;

            float usableCross = metrics.CrossExtent - _crossSpacing * (_columns - 1);
            _cellCross = _columns > 0 ? Math.Max(0f, usableCross) / _columns : 0f;
            _rowCount = _count == 0 ? 0 : (_count + _columns - 1) / _columns;

            // Grow-only by design: row arrays are reused across rebuilds and never shrink, to
            // avoid realloc churn when the item count fluctuates. Deliberate retention, not a leak.
            if (_rowStarts.Length < _rowCount)
            {
                _rowStarts = new float[_rowCount];
                _rowSizes = new float[_rowCount];
            }

            float cursor = metrics.MainLeadingPad;
            for (int row = 0; row < _rowCount; row++)
            {
                float rowMain = 0f;
                for (int col = 0; col < _columns; col++)
                {
                    int idx = row * _columns + col;
                    if (idx >= _count) break;
                    float s = source.GetItemSize(idx);
                    if (s < 0f) s = 0f;
                    if (s > rowMain) rowMain = s;
                }
                _rowStarts[row] = cursor;
                _rowSizes[row] = rowMain;
                cursor += rowMain;
                if (row < _rowCount - 1) cursor += metrics.MainSpacing;
            }

            _total = _rowCount == 0 ? 0f : cursor + metrics.MainTrailingPad;
        }

        public float GetStart(int index) => _rowStarts[index / _columns];
        public float GetSize(int index) => _rowSizes[index / _columns];
        public float GetCrossStart(int index) => _crossLeadingPad + (index % _columns) * (_cellCross + _crossSpacing);
        public float GetCrossSize(int index) => _cellCross;

        /// <summary>
        /// Returns the index of the first item (column 0) in the row that contains
        /// <paramref name="offset"/>. Clamps to [0, Count-1].
        /// </summary>
        public int IndexAt(float offset)
        {
            if (_rowCount == 0) return 0;
            if (offset <= _rowStarts[0]) return 0;
            if (offset >= _total) return (_rowCount - 1) * _columns;

            int lo = 0, hi = _rowCount - 1, res = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (_rowStarts[mid] <= offset) { res = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return res * _columns;
        }
    }
}
