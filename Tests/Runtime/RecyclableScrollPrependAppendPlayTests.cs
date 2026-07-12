using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KidzDev.Unity.RecyclableScroll.Tests.Play
{
    /// <summary>
    /// Regression tests for the v0.5.0 <see cref="RecyclableScrollView.PrependData"/> /
    /// <see cref="RecyclableScrollView.AppendData"/> anchor behaviour: prepending older items
    /// (e.g. loading chat history) must not visually shift what's currently on screen, and
    /// appending new items must not disturb already-realized ones.
    /// </summary>
    public class RecyclableScrollPrependAppendPlayTests
    {
        private sealed class MutableSource : IRecyclableDataSource
        {
            public readonly List<float> Sizes = new List<float>();
            public readonly List<int> BoundIndices = new List<int>();
            public int ItemCount => Sizes.Count;
            public void BindItem(int index, GameObject item) => BoundIndices.Add(index);
            public float GetItemSize(int index) => Sizes[index];
        }

        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
        }

        private RecyclableScrollView BuildView(Vector2 viewportSize)
        {
            _root = new GameObject("Canvas", typeof(Canvas));
            ((RectTransform)_root.transform).sizeDelta = new Vector2(2000f, 2000f);

            var svGO = new GameObject("ScrollView", typeof(RectTransform), typeof(Image));
            svGO.transform.SetParent(_root.transform, false);

            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGO.transform.SetParent(svGO.transform, false);
            var viewportRT = (RectTransform)viewportGO.transform;
            viewportRT.anchorMin = viewportRT.anchorMax = new Vector2(0.5f, 0.5f);
            viewportRT.sizeDelta = viewportSize;

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);

            var itemGO = new GameObject("Item", typeof(RectTransform));
            itemGO.transform.SetParent(_root.transform, false);
            itemGO.SetActive(false);

            var view = svGO.AddComponent<RecyclableScrollView>();
            SetPrivate(view, "viewport", viewportRT);
            SetPrivate(view, "content", (RectTransform)contentGO.transform);
            SetPrivate(view, "itemPrefab", itemGO);
            return view;
        }

        private static RectTransform Content(RecyclableScrollView view) =>
            (RectTransform)typeof(RecyclableScrollView)
                .GetField("content", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(view);

        private static void SetPrivate(object target, string field, object value)
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(f, Is.Not.Null, $"field '{field}' not found");
            f.SetValue(target, value);
        }

        private static float GetPrivateFloat(object target, string field)
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(f, Is.Not.Null, $"field '{field}' not found");
            return (float)f.GetValue(target);
        }

        private static IEnumerable<Transform> ActiveChildren(RectTransform content) =>
            Enumerable.Range(0, content.childCount)
                .Select(content.GetChild)
                .Where(c => c.gameObject.activeSelf);

        [Test]
        public void PrependData_KeepsPreviouslyVisibleItemAtSameScreenPosition()
        {
            var view = BuildView(new Vector2(400f, 600f));
            var src = new MutableSource();
            for (int i = 0; i < 20; i++) src.Sizes.Add(80f);
            view.SetDataSource(src);

            RectTransform content = Content(view);
            RectTransform firstItemRT = (RectTransform)ActiveChildren(content)
                .First(c => c.GetComponent<RecyclableScrollItem>().Index == 0);
            float beforeY = firstItemRT.anchoredPosition.y;

            // Simulate loading 3 older messages (80px each) prepended to the front.
            src.Sizes.InsertRange(0, new[] { 80f, 80f, 80f });
            view.PrependData(3);

            // The item that used to be data-index 0 is now data-index 3; its on-screen
            // anchoredPosition must be unchanged even though the object backing it may have
            // been swapped via the pool (recycle-and-rebind).
            RectTransform stillItemRT = (RectTransform)ActiveChildren(content)
                .First(c => c.GetComponent<RecyclableScrollItem>().Index == 3);
            Assert.That(stillItemRT.anchoredPosition.y, Is.EqualTo(beforeY).Within(0.01f),
                "prepending older items must not visually shift already-visible content");
        }

        [Test]
        public void PrependData_ShiftsDragBaselines_SoAnInFlightDragDoesNotSnap()
        {
            var view = BuildView(new Vector2(400f, 600f));
            var src = new MutableSource();
            for (int i = 0; i < 20; i++) src.Sizes.Add(80f);
            view.SetDataSource(src);

            // Simulate a drag in progress: baselines were captured at the current scroll pos.
            SetPrivate(view, "_scrollPos", 40f);
            SetPrivate(view, "_scrollStartPos", 40f);
            SetPrivate(view, "_prevScrollPos", 40f);
            SetPrivate(view, "_dragging", true);

            src.Sizes.InsertRange(0, new[] { 80f, 80f, 80f });
            view.PrependData(3);

            const float added = 240f; // 3 * 80f
            Assert.That(GetPrivateFloat(view, "_scrollPos"), Is.EqualTo(40f + added).Within(0.01f));
            Assert.That(GetPrivateFloat(view, "_scrollStartPos"), Is.EqualTo(40f + added).Within(0.01f),
                "drag start baseline must shift by the prepended block's length, or the next drag frame snaps back");
            Assert.That(GetPrivateFloat(view, "_prevScrollPos"), Is.EqualTo(40f + added).Within(0.01f));
        }

        [Test]
        public void AppendData_DoesNotRebindAlreadyActiveItems()
        {
            var view = BuildView(new Vector2(400f, 600f));
            var src = new MutableSource();
            for (int i = 0; i < 5; i++) src.Sizes.Add(80f);
            view.SetDataSource(src);

            var boundBeforeAppend = new List<int>(src.BoundIndices); // [0,1,2,3,4]
            Assert.That(boundBeforeAppend, Is.EquivalentTo(new[] { 0, 1, 2, 3, 4 }), "precondition");

            src.Sizes.Add(80f);
            src.Sizes.Add(80f);
            view.AppendData();

            foreach (int idx in boundBeforeAppend)
                Assert.That(src.BoundIndices.Count(i => i == idx), Is.EqualTo(1),
                    $"index {idx} was already active and must not be rebound by AppendData");

            Assert.That(src.BoundIndices, Does.Contain(5), "newly appended item that now fits on screen must be bound");
            Assert.That(src.BoundIndices, Does.Contain(6), "newly appended item that now fits on screen must be bound");
        }

        [Test]
        public void PrependData_InLoopMode_LogsErrorAndFallsBackToRefresh()
        {
            LogAssert.Expect(LogType.Error, new Regex("PrependData is not supported in loop mode"));

            var view = BuildView(new Vector2(400f, 600f));
            SetPrivate(view, "loop", true);
            var src = new MutableSource();
            for (int i = 0; i < 20; i++) src.Sizes.Add(80f);
            view.SetDataSource(src);

            src.Sizes.InsertRange(0, new[] { 80f, 80f, 80f });
            Assert.DoesNotThrow(() => view.PrependData(3));
        }

        [Test]
        public void PrependData_WhenNothingExistedBefore_FallsBackToRefreshWithoutThrowing()
        {
            var view = BuildView(new Vector2(400f, 600f));
            var src = new MutableSource(); // starts empty
            view.SetDataSource(src);

            src.Sizes.AddRange(new[] { 80f, 80f, 80f });
            Assert.DoesNotThrow(() => view.PrependData(3));

            RectTransform content = Content(view);
            Assert.That(ActiveChildren(content).Count(), Is.EqualTo(3),
                "all 3 freshly-added items realized via the Refresh fallback");
        }
    }
}
