using System.Collections;
using System.Collections.Generic;
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
    /// Regression tests for the v0.5.0 item-kind pooling (<see cref="IKindedDataSource"/> /
    /// <see cref="IKindedItemInstantiator"/>): the view must instantiate the right prefab per
    /// kind (kind 0 -&gt; itemPrefab, others -&gt; kindPrefabs[kind]) and never hand a recycled
    /// instance of one kind back for a different kind.
    /// </summary>
    public class RecyclableScrollKindedPlayTests
    {
        private sealed class KindedSource : IKindedDataSource
        {
            private readonly int[] _kinds;
            public readonly List<int> Bound = new List<int>();
            public KindedSource(int[] kinds) => _kinds = kinds;
            public int ItemCount => _kinds.Length;
            public void BindItem(int index, GameObject item) => Bound.Add(index);
            public float GetItemSize(int index) => 80f;
            public int GetItemKind(int index) => _kinds[index];
        }

        private sealed class KindedAsyncInstantiator : IKindedItemInstantiator
        {
            private readonly GameObject[] _prefabsByKind;
            public readonly List<int> Requested = new List<int>();
            public int Destroyed;
            public KindedAsyncInstantiator(GameObject[] prefabsByKind) => _prefabsByKind = prefabsByKind;

            public bool IsEnabled => true;

            public async UniTask<GameObject> InstantiateAsync(int kind, CancellationToken ct)
            {
                Requested.Add(kind);
                await UniTask.Yield(ct);
                return Object.Instantiate(_prefabsByKind[kind]);
            }

            public void Destroy(GameObject item)
            {
                Destroyed++;
                if (item != null) Object.Destroy(item);
            }
        }

        private GameObject _root;
        private readonly List<GameObject> _prefabs = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            foreach (GameObject p in _prefabs)
                if (p != null) Object.DestroyImmediate(p);
            _root = null;
            _prefabs.Clear();
        }

        private GameObject MakePrefab(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.SetActive(false);
            _prefabs.Add(go);
            return go;
        }

        private RecyclableScrollView BuildView(Vector2 viewportSize, GameObject itemPrefab, GameObject[] kindPrefabs)
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

            var view = svGO.AddComponent<RecyclableScrollView>();
            SetPrivate(view, "viewport", viewportRT);
            SetPrivate(view, "content", (RectTransform)contentGO.transform);
            SetPrivate(view, "itemPrefab", itemPrefab);
            SetPrivate(view, "kindPrefabs", kindPrefabs);
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

        private static IEnumerable<Transform> ActiveChildren(RectTransform content) =>
            Enumerable.Range(0, content.childCount)
                .Select(content.GetChild)
                .Where(c => c.gameObject.activeSelf);

        [Test]
        public void SyncKindedSource_InstantiatesCorrectPrefabPerKind()
        {
            GameObject kind0Prefab = MakePrefab("Kind0");
            GameObject kind1Prefab = MakePrefab("Kind1");
            var view = BuildView(new Vector2(400f, 600f), kind0Prefab, new[] { null, kind1Prefab });

            int[] kinds = Enumerable.Range(0, 20).Select(i => i % 2).ToArray();
            view.SetDataSource(new KindedSource(kinds));

            RectTransform content = Content(view);
            Assert.That(ActiveChildren(content).Count(), Is.GreaterThan(0), "precondition: items realized");
            foreach (Transform child in ActiveChildren(content))
            {
                var item = child.GetComponent<RecyclableScrollItem>();
                string expectedName = item.Kind == 0 ? "Kind0" : "Kind1";
                Assert.That(child.gameObject.name, Does.StartWith(expectedName),
                    $"index {item.Index} (kind {item.Kind}) instantiated from the wrong prefab");
            }
        }

        [Test]
        public void ScrollAwayAndBack_NeverHandsBackWrongKindInstance()
        {
            GameObject kind0Prefab = MakePrefab("Kind0");
            GameObject kind1Prefab = MakePrefab("Kind1");
            var view = BuildView(new Vector2(400f, 600f), kind0Prefab, new[] { null, kind1Prefab });

            int[] kinds = Enumerable.Range(0, 100).Select(i => i % 2).ToArray();
            view.SetDataSource(new KindedSource(kinds));

            view.ScrollToIndex(50);
            view.ScrollToIndex(0);

            RectTransform content = Content(view);
            foreach (Transform child in ActiveChildren(content))
            {
                var item = child.GetComponent<RecyclableScrollItem>();
                string expectedName = item.Kind == 0 ? "Kind0" : "Kind1";
                Assert.That(child.gameObject.name, Does.StartWith(expectedName),
                    "a pool swap must never place a wrong-kind instance at this index");
                Assert.That(item.Kind, Is.EqualTo(kinds[item.Index]),
                    "item.Kind must match the data source's kind for its currently bound index");
            }
        }

        [Test]
        public void MissingKindPrefabEntry_LogsOnceAndFallsBackToItemPrefab()
        {
            LogAssert.Expect(LogType.Error, new Regex("no kindPrefabs entry for kind 1"));

            GameObject kind0Prefab = MakePrefab("Kind0");
            var view = BuildView(new Vector2(400f, 600f), kind0Prefab, System.Array.Empty<GameObject>());

            int[] kinds = { 0, 1, 0, 1, 0, 1, 0, 1, 0, 1 };
            view.SetDataSource(new KindedSource(kinds));

            RectTransform content = Content(view);
            foreach (Transform child in ActiveChildren(content))
                Assert.That(child.gameObject.name, Does.StartWith("Kind0"), "missing-prefab kind falls back to itemPrefab");
        }

        [UnityTest]
        public IEnumerator KindedAsyncInstantiator_ReceivesRequestedKindPerIndex() =>
            UniTask.ToCoroutine(async () =>
            {
                GameObject kind0Prefab = MakePrefab("Kind0");
                GameObject kind1Prefab = MakePrefab("Kind1");
                var view = BuildView(new Vector2(400f, 600f), kind0Prefab, System.Array.Empty<GameObject>());

                int[] kinds = Enumerable.Range(0, 20).Select(i => i % 2).ToArray();
                var src = new KindedSource(kinds);
                var instantiator = new KindedAsyncInstantiator(new[] { kind0Prefab, kind1Prefab });
                view.SetDataSource(src, instantiator);

                for (int f = 0; f < 4; f++) await UniTask.Yield();

                RectTransform content = Content(view);
                Assert.That(instantiator.Requested.Count, Is.GreaterThan(0), "instantiator was called");
                foreach (Transform child in ActiveChildren(content))
                {
                    var item = child.GetComponent<RecyclableScrollItem>();
                    string expectedName = item.Kind == 0 ? "Kind0" : "Kind1";
                    Assert.That(child.gameObject.name, Does.StartWith(expectedName),
                        "kinded async instantiator must produce the prefab matching its requested kind");
                }
            });
    }
}
