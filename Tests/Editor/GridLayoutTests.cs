using NUnit.Framework;

namespace KidzDev.Unity.RecyclableScroll.Tests
{
    public class GridLayoutTests
    {
        private static GridLayout Build(IRecyclableDataSource source, int columns, float spacing = 0f, float crossAxisSize = 300f)
        {
            var layout = new GridLayout(columns);
            layout.Rebuild(source, new LayoutMetrics(mainSpacing: spacing, crossExtent: crossAxisSize));
            return layout;
        }

        [Test]
        public void TotalLength_UniformItems_RowsTimeHeight()
        {
            // 6 items, 2 cols → 3 rows × 100px + 2 gaps × 10px = 320
            var layout = Build(new StubDataSource(count: 6, itemSize: 100f), columns: 2, spacing: 10f);
            Assert.That(layout.Count, Is.EqualTo(6));
            Assert.That(layout.TotalLength, Is.EqualTo(320f).Within(0.001f));
        }

        [Test]
        public void Columns_ReturnsConfiguredValue()
        {
            var layout = Build(new StubDataSource(count: 1, itemSize: 50f), columns: 4);
            Assert.That(layout.Columns, Is.EqualTo(4));
        }

        [Test]
        public void GetStart_SameForAllItemsInSameRow()
        {
            // 4 items, 2 cols, 100px each, spacing 10 → row0=0, row1=110
            var layout = Build(new StubDataSource(count: 4, itemSize: 100f), columns: 2, spacing: 10f);
            Assert.That(layout.GetStart(0), Is.EqualTo(0f).Within(0.001f));
            Assert.That(layout.GetStart(1), Is.EqualTo(0f).Within(0.001f));  // same row
            Assert.That(layout.GetStart(2), Is.EqualTo(110f).Within(0.001f));
            Assert.That(layout.GetStart(3), Is.EqualTo(110f).Within(0.001f)); // same row
        }

        [Test]
        public void GetSize_MaxPerRow_VariableSizes()
        {
            // row0: items 0(50),1(80) → max=80   row1: item 2(30) → 30
            var layout = Build(new StubDataSource(new[] { 50f, 80f, 30f }), columns: 2);
            Assert.That(layout.GetSize(0), Is.EqualTo(80f).Within(0.001f));
            Assert.That(layout.GetSize(1), Is.EqualTo(80f).Within(0.001f));
            Assert.That(layout.GetSize(2), Is.EqualTo(30f).Within(0.001f));
        }

        [Test]
        public void GetCrossStart_SpreadAcrossViewport()
        {
            // crossAxisSize=300, 3 cols → cellWidth=100
            var layout = Build(new StubDataSource(count: 6, itemSize: 100f), columns: 3, crossAxisSize: 300f);
            Assert.That(layout.GetCrossStart(0), Is.EqualTo(0f).Within(0.001f));
            Assert.That(layout.GetCrossStart(1), Is.EqualTo(100f).Within(0.001f));
            Assert.That(layout.GetCrossStart(2), Is.EqualTo(200f).Within(0.001f));
            Assert.That(layout.GetCrossStart(3), Is.EqualTo(0f).Within(0.001f));  // next row, col 0
        }

        [Test]
        public void GetCrossSize_EqualSlicesOfViewport()
        {
            var layout = Build(new StubDataSource(count: 4, itemSize: 100f), columns: 4, crossAxisSize: 400f);
            Assert.That(layout.GetCrossSize(0), Is.EqualTo(100f).Within(0.001f));
            Assert.That(layout.GetCrossSize(3), Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void IndexAt_ReturnsFirstItemOfContainingRow()
        {
            // 6 items, 2 cols, 100px rows, 0 spacing → row0=[0,200), row1=[200,400), row2=[400,600)
            var layout = Build(new StubDataSource(count: 6, itemSize: 100f), columns: 2, spacing: 0f);
            Assert.That(layout.IndexAt(-1f),  Is.EqualTo(0));  // before start → row 0
            Assert.That(layout.IndexAt(0f),   Is.EqualTo(0));
            Assert.That(layout.IndexAt(99f),  Is.EqualTo(0));  // still in row 0
            Assert.That(layout.IndexAt(100f), Is.EqualTo(2));  // row 1 first item
            Assert.That(layout.IndexAt(199f), Is.EqualTo(2));
            Assert.That(layout.IndexAt(200f), Is.EqualTo(4));  // row 2 first item
            Assert.That(layout.IndexAt(9999f),Is.EqualTo(4));  // clamps to last row start
        }

        [Test]
        public void PartialLastRow_CountedCorrectly()
        {
            // 5 items, 3 cols → rows: [0,1,2], [3,4]  (row 1 has only 2 items)
            var layout = Build(new StubDataSource(count: 5, itemSize: 100f), columns: 3, spacing: 0f);
            Assert.That(layout.Count, Is.EqualTo(5));
            Assert.That(layout.TotalLength, Is.EqualTo(200f).Within(0.001f)); // 2 rows × 100
            Assert.That(layout.GetStart(4), Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void Empty_SafeDefaults()
        {
            var layout = Build(new StubDataSource(count: 0), columns: 3);
            Assert.That(layout.Count, Is.EqualTo(0));
            Assert.That(layout.TotalLength, Is.EqualTo(0f));
            Assert.That(layout.IndexAt(100f), Is.EqualTo(0));
        }
    }
}
