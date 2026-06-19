using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KidzDev.Unity.RecyclableScroll.Tests.Play
{
    public class RecyclableScrollAsyncPlayTests
    {
        private sealed class AsyncDataSource : IAsyncRecyclableDataSource
        {
            public readonly List<int> Bound = new List<int>();
            public int ItemCount => 100;
            public void BindItem(int index, GameObject item) { }
            public float GetItemSize(int index) => 80f;

            public async UniTask BindItemAsync(int index, GameObject item, CancellationToken cancellationToken)
            {
                await UniTask.Yield(cancellationToken);
                Bound.Add(index);
            }
        }

        private sealed class CancelTrackingDataSource : IAsyncRecyclableDataSource
        {
            public readonly List<int> Started = new List<int>();
            public readonly List<int> Completed = new List<int>();
            public int ItemCount => 100;
            public void BindItem(int index, GameObject item) { }
            public float GetItemSize(int index) => 80f;

            public async UniTask BindItemAsync(int index, GameObject item, CancellationToken cancellationToken)
            {
                Started.Add(index);
                // Two-frame delay so we have time to cancel between start and completion.
                await UniTask.Yield(cancellationToken);
                await UniTask.Yield(cancellationToken);
                Completed.Add(index);
            }
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
            Assert.That(f, Is.Not.Null, $"serialized field '{field}' not found");
            f.SetValue(target, value);
        }

        private static int ActiveChildCount(RectTransform content) =>
            Enumerable.Range(0, content.childCount).Count(i => content.GetChild(i).gameObject.activeSelf);

        [UnityTest]
        public IEnumerator AsyncBind_ItemsArePositionedImmediately_AndBoundAfterYield() =>
            UniTask.ToCoroutine(async () =>
            {
                var src = new AsyncDataSource();
                var view = BuildView(new Vector2(400f, 600f));
                view.SetDataSource(src);

                var content = Content(view);

                // Items are activated (positioned) synchronously even though binding is async.
                // window = [0..8] = 9 items: IndexAt(600)=7 (item 7 starts at 560) + buffer 1 = last 8,
                // so items 0..8 are realized (80px items, 600px viewport, buffer=1).
                Assert.That(ActiveChildCount(content), Is.EqualTo(9), "items activated before bind completes");
                Assert.That(src.Bound.Count, Is.EqualTo(0), "no binds completed yet");

                // After one yield, BindItemAsync resumes and records completions.
                await UniTask.Yield();
                await UniTask.Yield(); // second yield to let all in-flight tasks complete

                Assert.That(src.Bound.Count, Is.EqualTo(9), "all visible items bound after yield");
            });

        [UnityTest]
        public IEnumerator AsyncBind_Cancellation_ItemScrolledOutBeforeBindCompletes() =>
            UniTask.ToCoroutine(async () =>
            {
                var src = new CancelTrackingDataSource();
                var view = BuildView(new Vector2(400f, 600f));
                view.SetDataSource(src);

                // Async binds for the initial window are in-flight (two-frame delay).
                // Immediately refresh with zero items to cancel them all via RecycleAll.
                var emptySource = new EmptyDataSource();
                view.SetDataSource(emptySource);

                // Wait for any queued UniTask continuations to drain.
                await UniTask.Yield();
                await UniTask.Yield();
                await UniTask.Yield();

                // Binds were started but then cancelled — Completed must be empty.
                Assert.That(src.Started.Count, Is.GreaterThan(0), "binds were started");
                Assert.That(src.Completed.Count, Is.EqualTo(0), "cancelled binds never completed");
            });

        private sealed class EmptyDataSource : IRecyclableDataSource
        {
            public int ItemCount => 0;
            public void BindItem(int index, GameObject item) { }
            public float GetItemSize(int index) => 80f;
        }
    }
}
