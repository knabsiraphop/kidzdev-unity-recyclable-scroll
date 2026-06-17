using NUnit.Framework;
using UnityEngine;

namespace KidzDev.Unity.RecyclableScroll.Tests.Play
{
    /// <summary>
    /// Lifetime and null-safety smoke tests for the scaffolded view. They assert the
    /// public entry points stay exception-free before the Phase 1 engine lands, so
    /// the harness is ready for behavioural tests to drop in.
    /// </summary>
    public class RecyclableScrollViewPlayTests
    {
        private sealed class StubDataSource : IRecyclableDataSource
        {
            public int ItemCount => 100;
            public void BindItem(int index, RecyclableScrollItem item) { }
            public float GetItemSize(int index) => 80f;
        }

        [Test]
        public void Refresh_WithoutDataSource_DoesNotThrow()
        {
            var go = new GameObject("scroll-view");
            try
            {
                var view = go.AddComponent<RecyclableScrollView>();
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.ScrollToIndex(0));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SetDataSource_ThenRefresh_DoesNotThrow()
        {
            var go = new GameObject("scroll-view");
            try
            {
                var view = go.AddComponent<RecyclableScrollView>();
                view.SetDataSource(new StubDataSource());
                Assert.DoesNotThrow(() => view.Refresh());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Item_IndexDefaultsToZero()
        {
            var go = new GameObject("scroll-item");
            try
            {
                var item = go.AddComponent<RecyclableScrollItem>();
                Assert.That(item.Index, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
