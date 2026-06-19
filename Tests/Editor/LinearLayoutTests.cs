using NUnit.Framework;

namespace KidzDev.Unity.RecyclableScroll.Tests
{
    /// <summary>
    /// Unit tests for the internal <see cref="LinearLayout"/> math — prefix-sum
    /// offsets and the binary-search visible-index lookup — exercised directly
    /// (no live ScrollRect) via InternalsVisibleTo.
    /// </summary>
    public class LinearLayoutTests
    {
        private static LinearLayout Build(IRecyclableDataSource source, float spacing)
        {
            var layout = new LinearLayout();
            layout.Rebuild(source, new LayoutMetrics(mainSpacing: spacing));
            return layout;
        }

        [Test]
        public void TotalLength_UniformItemsWithSpacing()
        {
            var layout = Build(new StubDataSource(count: 5, itemSize: 100f), spacing: 10f);
            Assert.That(layout.Count, Is.EqualTo(5));
            Assert.That(layout.TotalLength, Is.EqualTo(540f).Within(0.001f)); // 5*100 + 4*10
        }

        [Test]
        public void StartsAndSizes_VariableItems()
        {
            // sizes 50,100,25 with spacing 10 -> starts 0,60,170 ; total 195
            var layout = Build(new StubDataSource(new[] { 50f, 100f, 25f }), spacing: 10f);

            Assert.That(layout.TotalLength, Is.EqualTo(195f).Within(0.001f));
            Assert.That(layout.GetStart(0), Is.EqualTo(0f).Within(0.001f));
            Assert.That(layout.GetStart(1), Is.EqualTo(60f).Within(0.001f));
            Assert.That(layout.GetStart(2), Is.EqualTo(170f).Within(0.001f));
            Assert.That(layout.GetSize(0), Is.EqualTo(50f).Within(0.001f));
            Assert.That(layout.GetSize(1), Is.EqualTo(100f).Within(0.001f));
            Assert.That(layout.GetSize(2), Is.EqualTo(25f).Within(0.001f));
        }

        [Test]
        public void IndexAt_FindsItemSpanningOffset()
        {
            var layout = Build(new StubDataSource(new[] { 50f, 100f, 25f }), spacing: 10f);

            Assert.That(layout.IndexAt(-5f), Is.EqualTo(0));
            Assert.That(layout.IndexAt(0f), Is.EqualTo(0));
            Assert.That(layout.IndexAt(59f), Is.EqualTo(0));
            Assert.That(layout.IndexAt(60f), Is.EqualTo(1));
            Assert.That(layout.IndexAt(169f), Is.EqualTo(1));
            Assert.That(layout.IndexAt(170f), Is.EqualTo(2));
            Assert.That(layout.IndexAt(10000f), Is.EqualTo(2)); // clamps past the end
        }

        [Test]
        public void Empty_HasZeroLengthAndSafeIndex()
        {
            var layout = Build(new StubDataSource(count: 0), spacing: 10f);
            Assert.That(layout.Count, Is.EqualTo(0));
            Assert.That(layout.TotalLength, Is.EqualTo(0f));
            Assert.That(layout.IndexAt(123f), Is.EqualTo(0));
        }

        [Test]
        public void SingleItem_HasNoTrailingSpacing()
        {
            var layout = Build(new StubDataSource(new[] { 80f }), spacing: 10f);
            Assert.That(layout.TotalLength, Is.EqualTo(80f).Within(0.001f));
            Assert.That(layout.GetSize(0), Is.EqualTo(80f).Within(0.001f));
            Assert.That(layout.IndexAt(80f), Is.EqualTo(0));
        }
    }
}
