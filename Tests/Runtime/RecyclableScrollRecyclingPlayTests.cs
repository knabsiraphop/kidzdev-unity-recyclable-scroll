using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.RecyclableScroll.Tests.Play
{
    /// <summary>
    /// Integration tests that build a real uGUI Scroll View in code and prove the
    /// recycler only realizes a viewport-sized window, reuses its pool when scrolling,
    /// and sizes content along the correct axis for each orientation.
    /// </summary>
    public class RecyclableScrollRecyclingPlayTests
    {
        private sealed class RecordingDataSource : IRecyclableDataSource
        {
            private readonly int _count;
            private readonly float _size;

            public RecordingDataSource(int count, float size)
            {
                _count = count;
                _size = size;
            }

            public readonly List<int> Bound = new List<int>();
            public int ItemCount => _count;
            public void BindItem(int index, RecyclableScrollItem item) => Bound.Add(index);
            public float GetItemSize(int index) => _size;
        }

        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
        }

        private RecyclableScrollView BuildView(RecyclableScrollView.Orientation orientation, Vector2 viewportSize)
        {
            _root = new GameObject("Canvas", typeof(Canvas));
            ((RectTransform)_root.transform).sizeDelta = new Vector2(2000f, 2000f);

            var svGO = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            svGO.transform.SetParent(_root.transform, false);

            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGO.transform.SetParent(svGO.transform, false);
            var viewportRT = (RectTransform)viewportGO.transform;
            viewportRT.anchorMin = viewportRT.anchorMax = new Vector2(0.5f, 0.5f);
            viewportRT.sizeDelta = viewportSize; // fixed-anchor rect == sizeDelta, no layout pass needed

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRT = (RectTransform)contentGO.transform;

            var scrollRect = svGO.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRT;
            scrollRect.content = contentRT;

            // Inactive template so Instantiate clones an inactive item.
            var itemGO = new GameObject("Item", typeof(RectTransform), typeof(RecyclableScrollItem));
            itemGO.transform.SetParent(_root.transform, false);
            itemGO.SetActive(false);

            // Adding the view runs Awake, which adopts the ScrollRect + its viewport/content.
            var view = svGO.AddComponent<RecyclableScrollView>();
            SetPrivate(view, "itemPrefab", itemGO.GetComponent<RecyclableScrollItem>());
            SetPrivate(view, "orientation", orientation);
            return view;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(f, Is.Not.Null, $"serialized field '{field}' not found (renamed?)");
            f.SetValue(target, value);
        }

        private static int ActiveChildCount(RectTransform content) =>
            Enumerable.Range(0, content.childCount).Count(i => content.GetChild(i).gameObject.activeSelf);

        [Test]
        public void Refresh_RealizesOnlyAWindow_AndSizesContent()
        {
            var view = BuildView(RecyclableScrollView.Orientation.Vertical, new Vector2(400f, 600f));
            var src = new RecordingDataSource(1000, 100f);
            view.SetDataSource(src);

            RectTransform content = view.GetComponent<ScrollRect>().content;

            // 1000 items * 100 each = 100000 content height.
            Assert.That(content.sizeDelta.y, Is.EqualTo(100000f).Within(0.1f));
            // window = [0 .. IndexAt(600)+buffer] = [0..7] = 8 items (not 1000).
            Assert.That(ActiveChildCount(content), Is.EqualTo(8));
            Assert.That(src.Bound, Is.EquivalentTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }));
        }

        [Test]
        public void ScrollToIndex_RecyclesPoolInsteadOfInstantiating()
        {
            var view = BuildView(RecyclableScrollView.Orientation.Vertical, new Vector2(400f, 600f));
            view.SetDataSource(new RecordingDataSource(1000, 100f));
            RectTransform content = view.GetComponent<ScrollRect>().content;

            Assert.That(content.childCount, Is.EqualTo(8), "precondition: initial window realized");

            view.ScrollToIndex(500);

            // window at 500: [499 .. IndexAt(50600)+buffer] = [499..507] = 9 items.
            // The 8 original items are recycled from the pool; only 1 new instance is
            // created, so total children stay at 9 (would be 17 without pooling).
            Assert.That(content.childCount, Is.EqualTo(9), "pool reuse: at most one new instance");
            Assert.That(ActiveChildCount(content), Is.EqualTo(9));

            bool has500 = Enumerable.Range(0, content.childCount)
                .Select(i => content.GetChild(i).GetComponent<RecyclableScrollItem>())
                .Any(it => it != null && it.gameObject.activeSelf && it.Index == 500);
            Assert.That(has500, Is.True, "item 500 must be realized after scrolling to it");
        }

        [Test]
        public void HorizontalOrientation_SizesContentOnXAxis()
        {
            var view = BuildView(RecyclableScrollView.Orientation.Horizontal, new Vector2(600f, 400f));
            view.SetDataSource(new RecordingDataSource(1000, 100f));
            RectTransform content = view.GetComponent<ScrollRect>().content;

            Assert.That(content.sizeDelta.x, Is.EqualTo(100000f).Within(0.1f));
            Assert.That(ActiveChildCount(content), Is.EqualTo(8)); // IndexAt(600)=6, +1 buffer
        }
    }
}
