using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KidzDev.Unity.RecyclableScroll.Tests.Play
{
    /// <summary>
    /// Regression tests for the production-hardening pass: the <c>OnBind</c> hook fires
    /// (B1), a slow async instantiator never double-spawns or leaks an index (B2/B3), and
    /// a one-shot instantiator failure does not permanently block its index (B4).
    /// </summary>
    public class RecyclableScrollLifecyclePlayTests
    {
        // Counts how many times the view invoked the bound-callback on this item.
        private sealed class CountingItem : RecyclableScrollItem
        {
            public int BindCount;
            protected override void OnBind() => BindCount++;
        }

        private sealed class SyncSource : IRecyclableDataSource
        {
            public int ItemCount => 100;
            public void BindItem(int index, GameObject item) { }
            public float GetItemSize(int index) => 80f;
        }

        private sealed class AsyncBindSource : IAsyncRecyclableDataSource
        {
            public int ItemCount => 100;
            public void BindItem(int index, GameObject item) { }
            public float GetItemSize(int index) => 80f;

            public async UniTask BindItemAsync(int index, GameObject item, CancellationToken ct)
                => await UniTask.Yield(ct);
        }

        // Async custom instantiator with a one-frame delay, so the visible window can be
        // re-driven while instantiation is still in flight. Counts attempts and destroys.
        private sealed class DelayedInstantiatorSource : IRecyclableDataSource, IItemInstantiator
        {
            private readonly GameObject _prefab;
            public int Attempts;
            public int Destroyed;
            public DelayedInstantiatorSource(GameObject prefab) => _prefab = prefab;

            public int ItemCount => 1000;
            public void BindItem(int index, GameObject item) { }
            public float GetItemSize(int index) => 80f;

            bool IItemInstantiator.IsEnabled => true;

            async UniTask<GameObject> IItemInstantiator.InstantiateAsync(CancellationToken ct)
            {
                Attempts++;                 // count before the await: one per realize attempt
                await UniTask.Yield(ct);
                return Object.Instantiate(_prefab);
            }

            void IItemInstantiator.Destroy(GameObject go)
            {
                Destroyed++;
                if (go != null) Object.Destroy(go);
            }
        }

        // Throws on the first instantiation only, then succeeds on every later call.
        private sealed class FlakyInstantiatorSource : IRecyclableDataSource, IItemInstantiator
        {
            private readonly GameObject _prefab;
            private bool _thrown;
            public FlakyInstantiatorSource(GameObject prefab) => _prefab = prefab;

            public int ItemCount => 5;
            public void BindItem(int index, GameObject item) { }
            public float GetItemSize(int index) => 80f;

            bool IItemInstantiator.IsEnabled => true;

            async UniTask<GameObject> IItemInstantiator.InstantiateAsync(CancellationToken ct)
            {
                await UniTask.Yield(ct);
                if (!_thrown) { _thrown = true; throw new System.InvalidOperationException("boom"); }
                return Object.Instantiate(_prefab);
            }

            void IItemInstantiator.Destroy(GameObject go) { if (go != null) Object.Destroy(go); }
        }

        private GameObject _root;
        private GameObject _itemPrefab;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_itemPrefab != null) Object.DestroyImmediate(_itemPrefab);
            _root = null;
            _itemPrefab = null;
        }

        private RecyclableScrollView BuildView(Vector2 viewportSize, out GameObject itemPrefab)
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

            // Item prefab carries a CountingItem so OnBind invocations are observable.
            _itemPrefab = new GameObject("Item", typeof(RectTransform));
            _itemPrefab.AddComponent<CountingItem>();
            _itemPrefab.SetActive(false);
            itemPrefab = _itemPrefab;

            var view = svGO.AddComponent<RecyclableScrollView>();
            SetPrivate(view, "viewport", viewportRT);
            SetPrivate(view, "content", (RectTransform)contentGO.transform);
            SetPrivate(view, "itemPrefab", _itemPrefab);
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

        // --- B1: OnBind fires --------------------------------------------------

        [Test]
        public void OnBind_FiresOncePerItem_OnSyncPath()
        {
            var view = BuildView(new Vector2(400f, 600f), out _);
            view.SetDataSource(new SyncSource());   // sync path realizes + binds synchronously
            RectTransform content = Content(view);

            int active = ActiveChildCount(content);
            Assert.That(active, Is.GreaterThan(0), "items realized");
            foreach (int i in Enumerable.Range(0, content.childCount))
            {
                var item = content.GetChild(i).GetComponent<CountingItem>();
                if (item.gameObject.activeSelf)
                    Assert.That(item.BindCount, Is.EqualTo(1), $"child {i} bound exactly once");
            }
        }

        [UnityTest]
        public IEnumerator OnBind_FiresAfterAsyncBindCompletes_NotBefore() =>
            UniTask.ToCoroutine(async () =>
            {
                var view = BuildView(new Vector2(400f, 600f), out _);
                view.SetDataSource(new AsyncBindSource());
                RectTransform content = Content(view);

                // Items are positioned synchronously; the async bind has not resumed yet.
                Assert.That(content.GetChild(0).GetComponent<CountingItem>().BindCount, Is.EqualTo(0),
                    "OnBind must not fire before the async bind completes");

                await UniTask.Yield();
                await UniTask.Yield();

                foreach (int i in Enumerable.Range(0, content.childCount))
                {
                    var item = content.GetChild(i).GetComponent<CountingItem>();
                    if (item.gameObject.activeSelf)
                        Assert.That(item.BindCount, Is.EqualTo(1), $"child {i} bound once after yield");
                }
            });

        // --- B2/B3: no duplicate spawn, no orphan leak -------------------------

        [UnityTest]
        public IEnumerator SlowInstantiator_ReDrivenWindow_RealizesEachIndexOnce() =>
            UniTask.ToCoroutine(async () =>
            {
                var view = BuildView(new Vector2(400f, 600f), out GameObject prefab);
                var src = new DelayedInstantiatorSource(prefab);
                view.SetDataSource(src);   // starts instantiating the initial window (pending)

                // Re-drive the same window several times while instantiation is in flight.
                // Without the _pendingBinds guard each pass re-acquires every index, doubling
                // attempts and orphaning the first GameObject of each index.
                view.ScrollToOffset(0f);
                view.ScrollToOffset(0f);
                view.ScrollToOffset(0f);

                for (int f = 0; f < 5; f++) await UniTask.Yield();

                RectTransform content = Content(view);
                int active = ActiveChildCount(content);

                Assert.That(active, Is.GreaterThan(0), "window realized");
                Assert.That(src.Attempts, Is.EqualTo(active),
                    "each realized index was instantiated exactly once (no duplicate spawns)");
                Assert.That(content.childCount, Is.EqualTo(active),
                    "no orphaned GameObjects left parented to content");
                Assert.That(src.Destroyed, Is.EqualTo(0), "nothing scrolled out, nothing destroyed");
            });

        // --- B4: a one-shot failure does not permanently block an index -------

        [UnityTest]
        public IEnumerator InstantiatorThrowsOnce_IndexRecoversOnNextPass() =>
            UniTask.ToCoroutine(async () =>
            {
                // The first instantiation logs an exception; expect it so the test still passes.
                LogAssert.Expect(LogType.Exception, new Regex("boom"));

                var view = BuildView(new Vector2(400f, 600f), out GameObject prefab);
                var src = new FlakyInstantiatorSource(prefab);
                view.SetDataSource(src);   // 5 items realize; the first to resume throws

                for (int f = 0; f < 4; f++) await UniTask.Yield();

                RectTransform content = Content(view);
                Assert.That(ActiveChildCount(content), Is.EqualTo(4),
                    "four items realized; the failed index is not yet present");

                // Re-drive the window: the previously-failed index must be re-acquirable
                // (its pending entry was cleared on failure, not left dangling).
                view.ScrollToOffset(0f);
                for (int f = 0; f < 4; f++) await UniTask.Yield();

                Assert.That(ActiveChildCount(content), Is.EqualTo(5),
                    "the failed index recovered on the next realize pass");
            });
    }
}
