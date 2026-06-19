using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.RecyclableScroll.Samples
{
    /// <summary>
    /// Scene 1 — demonstrates every <see cref="RecyclableScrollController"/> operation
    /// via UI buttons. Wires OnScrolledToStart / OnScrolledToEnd to a status label.
    /// </summary>
    public class ControllerShowcase : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RecyclableScrollController controller;
        [SerializeField] private Text eventLogText;

        // Two contact datasets to swap between
        private string[] _namesA;
        private bool[]   _onlineA;
        private Color[]  _colorsA;

        private string[] _namesB;
        private bool[]   _onlineB;
        private Color[]  _colorsB;

        private const int CountA = 30;
        private const int CountB = 60;

        private static readonly string[] s_pool =
        {
            "Alice Martin","Bob Chen","Carol White","David Park","Eva Torres",
            "Frank Kim","Grace Liu","Henry Brown","Ivy Patel","Jack Wong",
            "Kate Johnson","Leo Garcia","Mia Rodriguez","Noah Smith","Olivia Lee",
            "Paul Davis","Quinn Taylor","Ruby Martinez","Sam Wilson","Tina Nguyen",
            "Uma Scott","Victor Hall","Wendy Adams","Xan Turner","Yara Phillips",
            "Zoe Campbell","Aaron Evans","Bella Cooper","Carlos Rivera","Diana Lewis",
        };

        private void Awake()
        {
            BuildDatasets();
        }

        private void Start()
        {
            controller.OnScrolledToStart.AddListener(() => Log("↑ Reached top"));
            controller.OnScrolledToEnd.AddListener(()   => Log("↓ Reached bottom"));
            controller.Init(MakeSource(CountA, _namesA, _onlineA, _colorsA));
            Log("Init — 30 contacts");
        }

        // ---- button targets (wire via inspector / scene builder) ----

        public void OnInitClicked()
        {
            BuildDatasets();
            controller.Init(MakeSource(CountA, _namesA, _onlineA, _colorsA));
            Log("Init — 30 contacts, scroll reset");
        }

        public void OnRefillClicked()
        {
            controller.Refill(MakeSource(CountB, _namesB, _onlineB, _colorsB));
            Log($"Refill — {CountB} contacts, scroll reset");
        }

        public void OnUpdateDataClicked()
        {
            for (int i = 0; i < _onlineA.Length; i++)
                _onlineA[i] = !_onlineA[i];
            controller.UpdateData(MakeSource(CountA, _namesA, _onlineA, _colorsA));
            Log("UpdateData — statuses toggled, position kept");
        }

        public void OnRefreshClicked()
        {
            controller.Refresh();
            Log("Refresh — in-place rebind");
        }

        public void OnClearClicked()
        {
            controller.Clear();
            Log("Clear");
        }

        public void OnScrollToStartClicked() => controller.ScrollToStart();
        public void OnScrollToEndClicked()   => controller.ScrollToEnd();

        public void OnJumpRandomClicked()
        {
            if (!controller.IsInitialized) return;
            int idx = Random.Range(0, CountA);
            controller.JumpToIndex(idx);
            Log($"Jump → #{idx}");
        }

        // ---- helpers ----

        private static IRecyclableDataSource MakeSource(
            int count, string[] names, bool[] online, Color[] colors)
        {
            return new RecyclableDataSource<ContactItem>(
                count:    count,
                itemSize: 72f,
                bind:     (item, i) => item.Populate(names[i], online[i], colors[i]));
        }

        private void BuildDatasets()
        {
            var rng = new System.Random(1337);

            _namesA  = (string[])s_pool.Clone();
            _onlineA = new bool[CountA];
            _colorsA = new Color[CountA];
            for (int i = 0; i < CountA; i++)
            {
                _onlineA[i] = rng.NextDouble() > 0.4;
                _colorsA[i] = Color.HSVToRGB((i * 0.618034f) % 1f, 0.35f, 0.90f);
            }

            _namesB  = new string[CountB];
            _onlineB = new bool[CountB];
            _colorsB = new Color[CountB];
            for (int i = 0; i < CountB; i++)
            {
                _namesB[i]  = s_pool[i % s_pool.Length] + (i >= s_pool.Length ? $" {i / s_pool.Length + 1}" : "");
                _onlineB[i] = rng.NextDouble() > 0.5;
                _colorsB[i] = Color.HSVToRGB((i * 0.618034f * 1.3f) % 1f, 0.45f, 0.85f);
            }
        }

        private void Log(string msg)
        {
            if (eventLogText != null) eventLogText.text = msg;
            Debug.Log($"[ControllerShowcase] {msg}");
        }
    }
}
