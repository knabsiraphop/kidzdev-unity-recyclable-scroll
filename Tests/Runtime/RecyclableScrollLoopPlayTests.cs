using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.RecyclableScroll.Tests.Play
{
    /// <summary>
    /// Integration tests for loop mode. All tests use 10 items × 100 px = 1 000 px
    /// total length and a 300 px vertical viewport (buffer=1 unless stated otherwise).
    ///
    /// Virtual-index math at a glance:
    ///   LoopIndexAt(offset) → lap = floor(offset/1000), physIdx = innerLayout.IndexAt(offset % 1000)
    ///                          virtualIdx = lap*10 + physIdx
    ///   DataIndex(vi) → vi - floor(vi/10)*10  (always in [0,10))
    ///   AbsoluteStart(vi) → TotalLength*lap + GetStart(physIdx)
    ///     e.g. vi=-1 → lap=-1, physIdx=9, pos=-1000+900=-100  (sits just above item 0)
    /// </summary>
    public class RecyclableScrollLoopPlayTests
    {
        private sealed class CountingSource : IRecyclableDataSource
        {
            private readonly int _count;
            private readonly float _size;
            public CountingSource(int count, float size) { _count = count; _size = size; }
            public int ItemCount => _count;
            public void BindItem(int index, RecyclableScrollItem item) { }
            public float GetItemSize(int index) => _size;
        }

        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
        }

        private RecyclableScrollView BuildLoopView(int itemCount = 10, float itemSize = 100f, float viewportHeight = 300f)
        {
            _root = new GameObject("Canvas", typeof(Canvas));
            ((RectTransform)_root.transform).sizeDelta = new Vector2(2000f, 2000f);

            var svGO = new GameObject("ScrollView", typeof(RectTransform), typeof(Image));
            svGO.transform.SetParent(_root.transform, false);

            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGO.transform.SetParent(svGO.transform, false);
            var viewportRT = (RectTransform)viewportGO.transform;
            viewportRT.anchorMin = viewportRT.anchorMax = new Vector2(0.5f, 0.5f);
            viewportRT.sizeDelta = new Vector2(400f, viewportHeight);

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);

            var itemGO = new GameObject("Item", typeof(RectTransform), typeof(RecyclableScrollItem));
            itemGO.transform.SetParent(_root.transform, false);
            itemGO.SetActive(false);

            var view = svGO.AddComponent<RecyclableScrollView>();
            SetField(view, "viewport", viewportRT);
            SetField(view, "content", (RectTransform)contentGO.transform);
            SetField(view, "itemPrefab", itemGO.GetComponent<RecyclableScrollItem>());
            SetField(view, "loop", true);
            view.SetDataSource(new CountingSource(itemCount, itemSize));
            return view;
        }

        private static RectTransform Content(RecyclableScrollView view) =>
            (RectTransform)typeof(RecyclableScrollView)
                .GetField("content", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(view);

        private static HashSet<int> DataIndices(RecyclableScrollView view)
        {
            var content = Content(view);
            var result = new HashSet<int>();
            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (child.gameObject.activeSelf)
                {
                    var item = child.GetComponent<RecyclableScrollItem>();
                    if (item != null) result.Add(item.Index);
                }
            }
            return result;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo f = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(f, Is.Not.Null, $"field '{name}' not found");
            f.SetValue(target, value);
        }

        // -------------------------------------------------------------------

        [Test]
        public void LoopMode_AtStart_BackwardBufferContainsLastItem()
        {
            // scroll=0: virtual window = [-1..4], data indices = {9,0,1,2,3,4}.
            // Virtual -1 sits at position GetStart(9) - TotalLength = 900-1000 = -100,
            // just above position 0, making data[9] appear in the backward buffer.
            var view = BuildLoopView();
            var indices = DataIndices(view);

            Assert.That(indices, Does.Contain(9), "backward buffer must include data[9]");
            Assert.That(indices, Does.Contain(0));
            Assert.That(indices.All(i => i >= 0 && i < 10), "all item.Index values must be valid data indices");
        }

        [Test]
        public void LoopMode_ScrollBackPastStart_ShowsEndOfList()
        {
            // scroll=-200: viewport covers [-200, 100].
            // LoopIndexAt(-200) → lap=-1, physOffset=800, physIdx=8, virtual=-2
            // LoopIndexAt(100)  → lap=0,  physOffset=100, physIdx=1, virtual=1
            // first=-3, last=2  →  data indices include 7,8,9,0,1,2.
            var view = BuildLoopView();
            view.ScrollToOffset(-200f);
            var indices = DataIndices(view);

            Assert.That(indices, Does.Contain(8));
            Assert.That(indices, Does.Contain(9));
            Assert.That(indices, Does.Contain(0));
        }

        [Test]
        public void LoopMode_ScrollPastEnd_ShowsStartOfList()
        {
            // scroll=950: viewport covers [950, 1250].
            // LoopIndexAt(950)  → physIdx=9, virtual=9
            // LoopIndexAt(1250) → lap=1, physIdx=2, virtual=12
            // first=8, last=13  →  data indices include 8,9,0,1,2,3.
            var view = BuildLoopView();
            view.ScrollToOffset(950f);
            var indices = DataIndices(view);

            Assert.That(indices, Does.Contain(9));
            Assert.That(indices, Does.Contain(0), "data[0] must appear in the forward wrap");
            Assert.That(indices, Does.Contain(1));
        }

        [Test]
        public void LoopMode_ItemIndex_IsAlwaysInDataRange()
        {
            // At every scroll position, item.Index must lie in [0, count) so that
            // consumers never see raw virtual indices leaking through the public API.
            var view = BuildLoopView(itemCount: 5, itemSize: 80f, viewportHeight: 160f);
            int count = 5;

            foreach (float scroll in new[] { 0f, -80f, -160f, 320f, 400f, 480f })
            {
                view.ScrollToOffset(scroll);
                var indices = DataIndices(view);
                Assert.That(
                    indices.All(i => i >= 0 && i < count),
                    $"scroll={scroll}: got [{string.Join(",", indices)}], expected all in [0,{count})");
            }
        }

        [Test]
        public void LoopMode_ScrollTwoFullLaps_StillCorrect()
        {
            // After scrolling forward by 2×TotalLength items must still be visible
            // and data indices must all be valid. 2000px forward = laps 2.
            // scroll=2000: LoopIndexAt(2000) → lap=2, physIdx=0, virtual=20
            // LoopIndexAt(2300) → lap=2, physIdx=3, virtual=23
            // first=19, last=24  →  data[9,0,1,2,3,4].
            var view = BuildLoopView();
            view.ScrollToOffset(2000f);
            var indices = DataIndices(view);

            Assert.That(indices.Count, Is.GreaterThan(0), "items must be realized after 2 laps");
            Assert.That(indices.All(i => i >= 0 && i < 10), "data indices must stay in [0,10)");
            Assert.That(indices, Does.Contain(0), "data[0] must appear at exactly 2 × TotalLength");
        }

        [Test]
        public void LoopMode_ScrollPastEnd_IsNotClampedToContentLength()
        {
            // The owned engine clamps non-loop scrolling to [0, max] but lets loop mode
            // run unbounded — proving the wrap-around offset survives.
            var view = BuildLoopView();
            view.ScrollToOffset(5000f);
            Assert.That(view.ScrollPosition, Is.EqualTo(5000f).Within(0.001f));
        }
    }
}
