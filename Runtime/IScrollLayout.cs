namespace KidzDev.Unity.RecyclableScroll
{
    /// <summary>
    /// Spacing and padding resolved onto the layout's own axes. The view turns its
    /// orientation-specific <c>spacing</c> (a <see cref="UnityEngine.Vector2"/>) and
    /// <c>padding</c> (a <see cref="UnityEngine.RectOffset"/>) into these axis-neutral
    /// scalars so the layouts stay orientation-agnostic and unit-testable.
    /// </summary>
    internal readonly struct LayoutMetrics
    {
        /// <summary>Gap between consecutive lines along the scroll (main) axis.</summary>
        public readonly float MainSpacing;

        /// <summary>Gap between columns along the cross axis (grid only).</summary>
        public readonly float CrossSpacing;

        /// <summary>Padding before the first line along the main axis.</summary>
        public readonly float MainLeadingPad;

        /// <summary>Padding after the last line along the main axis.</summary>
        public readonly float MainTrailingPad;

        /// <summary>Padding before the first column along the cross axis.</summary>
        public readonly float CrossLeadingPad;

        /// <summary>Usable cross-axis extent: the viewport cross size minus both cross pads.</summary>
        public readonly float CrossExtent;

        public LayoutMetrics(
            float mainSpacing = 0f,
            float crossSpacing = 0f,
            float mainLeadingPad = 0f,
            float mainTrailingPad = 0f,
            float crossLeadingPad = 0f,
            float crossExtent = 0f)
        {
            MainSpacing = mainSpacing < 0f ? 0f : mainSpacing;
            CrossSpacing = crossSpacing < 0f ? 0f : crossSpacing;
            MainLeadingPad = mainLeadingPad;
            MainTrailingPad = mainTrailingPad;
            CrossLeadingPad = crossLeadingPad;
            CrossExtent = crossExtent < 0f ? 0f : crossExtent;
        }
    }

    /// <summary>
    /// Seam between the recycler and the layout engine. Deals only in main-axis
    /// scalars plus cross-axis placement, so the recycler is orientation-agnostic
    /// and grid layouts can slot in without the recycler changing.
    /// </summary>
    internal interface IScrollLayout
    {
        int Count { get; }
        float TotalLength { get; }

        /// <summary>Items per line along the cross axis (1 for linear/single-column).</summary>
        int Columns { get; }

        /// <summary>Recompute the layout from <paramref name="source"/> and <paramref name="metrics"/>.</summary>
        void Rebuild(IRecyclableDataSource source, in LayoutMetrics metrics);

        /// <summary>Leading-edge main-axis offset of <paramref name="index"/> (includes leading pad).</summary>
        float GetStart(int index);

        /// <summary>Main-axis size of <paramref name="index"/> (spacing excluded).</summary>
        float GetSize(int index);

        /// <summary>Leading-edge cross-axis offset of <paramref name="index"/>. Zero for linear.</summary>
        float GetCrossStart(int index);

        /// <summary>
        /// Cross-axis size of <paramref name="index"/>. Negative means "stretch to fill the
        /// cross axis" (linear behaviour); a non-negative value is the exact cell size (grid).
        /// </summary>
        float GetCrossSize(int index);

        /// <summary>
        /// Index of the first item whose line contains <paramref name="offset"/>.
        /// For linear this is simply the item at that position; for grid it is the
        /// first item (column 0) of the row that spans <paramref name="offset"/>.
        /// Clamps to [0, Count-1].
        /// </summary>
        int IndexAt(float offset);
    }
}
