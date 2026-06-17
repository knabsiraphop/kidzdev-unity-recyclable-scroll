using NUnit.Framework;

namespace KidzDev.Unity.RecyclableScroll.Tests
{
    /// <summary>
    /// Guards the <see cref="IRecyclableDataSource"/> contract the scroll engine is
    /// built on. These hold today against <see cref="StubDataSource"/> and document
    /// the shape the Phase 1 engine must respect.
    /// </summary>
    public class DataSourceContractTests
    {
        [Test]
        public void ItemCount_ReflectsConfiguredCount()
        {
            var source = new StubDataSource(count: 250);
            Assert.That(source.ItemCount, Is.EqualTo(250));
        }

        [Test]
        public void GetItemSize_ReturnsConfiguredSize()
        {
            var source = new StubDataSource(count: 1, itemSize: 64f);
            Assert.That(source.GetItemSize(0), Is.EqualTo(64f));
        }

        [Test]
        public void BindItem_RecordsTheRequestedIndexInOrder()
        {
            var source = new StubDataSource(count: 3);

            source.BindItem(2, null);
            source.BindItem(0, null);

            Assert.That(source.BoundIndices, Is.EqualTo(new[] { 2, 0 }));
        }
    }
}
