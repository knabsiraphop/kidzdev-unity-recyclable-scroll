using UnityEngine;

namespace KidzDev.Unity.RecyclableScroll.Samples
{
    /// <summary>
    /// Minimal demo data source: 1000 dummy string rows wired into a
    /// <see cref="RecyclableScrollView"/>. Shows the intended integration shape
    /// even though the scroll engine itself is still a Phase 1 stub.
    /// </summary>
    public class RecyclableScrollDemo : MonoBehaviour, IRecyclableDataSource
    {
        [SerializeField] private RecyclableScrollView scrollView;
        [SerializeField] private int rowCount = 1000;
        [SerializeField] private float rowSize = 80f;

        private string[] _rows;

        private void Awake()
        {
            _rows = new string[rowCount];
            for (int i = 0; i < rowCount; i++)
                _rows[i] = $"Row {i}";
        }

        private void Start()
        {
            if (scrollView == null)
            {
                Debug.LogWarning($"{nameof(RecyclableScrollDemo)}: no {nameof(RecyclableScrollView)} assigned.");
                return;
            }

            scrollView.SetDataSource(this);
            scrollView.Refresh();
        }

        // IRecyclableDataSource ------------------------------------------------

        public int ItemCount => _rows?.Length ?? 0;

        public void BindItem(int index, RecyclableScrollItem item)
        {
            // A real item view would expose a label/setter; for the demo we just
            // log so the wiring is observable while the engine is a stub.
            Debug.Log($"Bind item {index}: {_rows[index]} -> {item.name}");
        }

        public float GetItemSize(int index) => rowSize;
    }
}
