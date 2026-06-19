using UnityEngine;

namespace KidzDev.Unity.RecyclableScroll.Samples
{
    /// <summary>
    /// Scene 2 — four linear scroll panels, each with a distinct data type and item:
    /// <list type="bullet">
    ///   <item>Top-left: Vertical — Leaderboard (variable row heights, sync).</item>
    ///   <item>Top-right: Vertical + Scrollbar — Contacts (uniform height, sync).</item>
    ///   <item>Bottom-left: Horizontal — News feed (uniform width, sync).</item>
    ///   <item>Bottom-right: Horizontal + Scrollbar — Achievements (uniform width, sync).</item>
    /// </list>
    /// </summary>
    public class MultiScrollShowcase : MonoBehaviour
    {
        [Header("Controllers")]
        [SerializeField] private RecyclableScrollController leaderboardCtrl;
        [SerializeField] private RecyclableScrollController contactCtrl;
        [SerializeField] private RecyclableScrollController newsCtrl;
        [SerializeField] private RecyclableScrollController achievementCtrl;

        [Header("Counts")]
        [SerializeField] private int leaderboardCount = 200;
        [SerializeField] private int contactCount     = 100;
        [SerializeField] private int newsCount        = 80;
        [SerializeField] private int achievementCount = 50;

        // ---- static sample data ----

        private static readonly string[] s_first =
            { "Alex","Jordan","Morgan","Taylor","Casey","Riley","Quinn","Avery","Blake","Drew" };
        private static readonly string[] s_last  =
            { "Smith","Chen","Patel","Kim","Garcia","Müller","Johansson","Park","Santos","Nguyen" };

        private static readonly string[] s_headlines =
        {
            "Researchers discover unexpected link between sleep and memory",
            "New battery tech promises 48-hour phone life",
            "Local team secures historic championship title",
            "Markets rally as quarterly earnings exceed forecast",
            "Ancient shipwreck found off the coast of Sicily",
            "Streaming platform announces record subscriber growth",
            "Scientists map the deepest ocean trench in detail",
            "City council approves urban green-space expansion",
            "AI model passes medical licensing exam with top score",
            "Climate summit reaches landmark emissions agreement",
        };

        private static readonly string[] s_cats = { "Tech","Science","Sports","Business","Culture","Health" };
        private static readonly Color[] s_catColors =
        {
            new Color(0.20f,0.50f,0.90f), new Color(0.20f,0.75f,0.55f),
            new Color(0.95f,0.40f,0.20f), new Color(0.55f,0.35f,0.85f),
            new Color(0.90f,0.70f,0.15f), new Color(0.25f,0.75f,0.40f),
        };

        private static readonly string[] s_achievements =
        {
            "First Steps","Explorer","Collector","Speedrunner","Veteran",
            "Perfectionist","Socialite","Challenger","Trailblazer","Champion",
        };

        // ---- lifecycle ----

        private void Start()
        {
            SetupLeaderboard();
            SetupContacts();
            SetupNews();
            SetupAchievements();
        }

        // ---- setup methods ----

        private void SetupLeaderboard()
        {
            if (leaderboardCtrl == null) return;
            var rng   = new System.Random(42);
            int n     = leaderboardCount;
            var names  = new string[n];
            var scores = new int[n];
            var colors = new Color[n];
            for (int i = 0; i < n; i++)
            {
                names[i]  = s_first[rng.Next(s_first.Length)] + " " + s_last[rng.Next(s_last.Length)];
                scores[i] = rng.Next(100, 1001);
                colors[i] = Color.HSVToRGB((i * 0.618034f) % 1f, 0.25f, 0.95f);
            }
            leaderboardCtrl.Init(new RecyclableDataSource<LeaderboardItem>(
                count:   n,
                getSize: i => scores[i] >= 800 ? 110f : scores[i] >= 500 ? 80f : 60f,
                bind:    (item, i) => item.Populate(i, names[i], scores[i], colors[i])));
        }

        private void SetupContacts()
        {
            if (contactCtrl == null) return;
            var rng    = new System.Random(99);
            int n      = contactCount;
            var names  = new string[n];
            var online = new bool[n];
            var colors = new Color[n];
            for (int i = 0; i < n; i++)
            {
                names[i]  = s_first[rng.Next(s_first.Length)] + " " + s_last[rng.Next(s_last.Length)];
                online[i] = rng.NextDouble() > 0.4;
                colors[i] = Color.HSVToRGB((i * 0.618034f) % 1f, 0.40f, 0.88f);
            }
            contactCtrl.Init(new RecyclableDataSource<ContactItem>(
                count:    n,
                itemSize: 72f,
                bind:     (item, i) => item.Populate(names[i], online[i], colors[i])));
        }

        private void SetupNews()
        {
            if (newsCtrl == null) return;
            var rng        = new System.Random(7);
            int n          = newsCount;
            var headlines  = new string[n];
            var cats       = new string[n];
            var accents    = new Color[n];
            for (int i = 0; i < n; i++)
            {
                int c      = rng.Next(s_cats.Length);
                cats[i]    = s_cats[c];
                accents[i] = s_catColors[c];
                headlines[i] = s_headlines[rng.Next(s_headlines.Length)];
            }
            newsCtrl.Init(new RecyclableDataSource<NewsCard>(
                count:    n,
                itemSize: 200f,
                bind:     (card, i) => card.Populate(headlines[i], cats[i], accents[i])));
        }

        private void SetupAchievements()
        {
            if (achievementCtrl == null) return;
            var rng    = new System.Random(55);
            int n      = achievementCount;
            var titles = new string[n];
            var pct    = new int[n];
            var colors = new Color[n];
            for (int i = 0; i < n; i++)
            {
                titles[i] = s_achievements[i % s_achievements.Length]
                           + (i >= s_achievements.Length ? $" {i / s_achievements.Length + 1}" : "");
                pct[i]    = rng.Next(0, 101);
                colors[i] = Color.HSVToRGB((i * 0.618034f) % 1f, 0.60f, 0.85f);
            }
            achievementCtrl.Init(new RecyclableDataSource<AchievementCard>(
                count:    n,
                itemSize: 180f,
                bind:     (card, i) => card.Populate(titles[i], pct[i], colors[i])));
        }
    }
}
