using UnityEngine;

namespace KidzDev.Unity.RecyclableScroll.Samples
{
    /// <summary>
    /// Scene 3 — four grid scroll panels, each with a distinct data type and item:
    /// <list type="bullet">
    ///   <item>Top-left: Vertical grid — Colour gallery (GalleryCard, 3 columns).</item>
    ///   <item>Top-right: Vertical grid + Scrollbar — Products (ProductCard, 3 columns).</item>
    ///   <item>Bottom-left: Horizontal grid — Profiles (ProfileCard, 3 rows).</item>
    ///   <item>Bottom-right: Horizontal grid + Scrollbar — Badges (BadgeCard, 3 rows).</item>
    /// </list>
    /// </summary>
    public class GridScrollShowcase : MonoBehaviour
    {
        [Header("Controllers")]
        [SerializeField] private RecyclableScrollController galleryCtrl;
        [SerializeField] private RecyclableScrollController productCtrl;
        [SerializeField] private RecyclableScrollController profileCtrl;
        [SerializeField] private RecyclableScrollController badgeCtrl;

        [Header("Counts")]
        [SerializeField] private int galleryCount = 300;
        [SerializeField] private int productCount = 120;
        [SerializeField] private int profileCount = 150;
        [SerializeField] private int badgeCount   = 60;

        // ---- static data ----

        private static readonly string[] s_products =
        {
            "Sword","Shield","Potion","Map","Boots",
            "Helmet","Ring","Staff","Bow","Arrow",
        };

        private static readonly string[] s_userPre  = { "cool","fast","blue","dark","neon","star","epic","bold","wild","sky" };
        private static readonly string[] s_userSuf  = { "fox","hawk","wolf","lion","bear","tiger","eagle","panda","raven","shark" };

        private static readonly string[] s_badges =
        {
            "Gold Star","Diamond","Champion","Legend","Elite",
            "Warrior","Scholar","Pioneer","Guardian","Hero",
        };

        // ---- lifecycle ----

        private void Start()
        {
            SetupGallery();
            SetupProducts();
            SetupProfiles();
            SetupBadges();
        }

        // ---- setup methods ----

        private void SetupGallery()
        {
            if (galleryCtrl == null) return;
            int n      = galleryCount;
            var colors = new Color[n];
            for (int i = 0; i < n; i++)
                colors[i] = Color.HSVToRGB((i * 0.618034f) % 1f, 0.55f, 0.88f);

            galleryCtrl.Init(new RecyclableDataSource<GalleryCard>(
                count:    n,
                itemSize: 100f,
                bind:     (card, i) => card.Populate(i, colors[i], $"#{i:D3}")));
        }

        private void SetupProducts()
        {
            if (productCtrl == null) return;
            var rng    = new System.Random(11);
            int n      = productCount;
            var names  = new string[n];
            var prices = new float[n];
            var colors = new Color[n];
            for (int i = 0; i < n; i++)
            {
                names[i]  = s_products[i % s_products.Length]
                           + (i >= s_products.Length ? $" +{i / s_products.Length}" : "");
                prices[i] = rng.Next(100, 9999) / 100f;
                colors[i] = Color.HSVToRGB((i * 0.618034f) % 1f, 0.50f, 0.85f);
            }
            productCtrl.Init(new RecyclableDataSource<ProductCard>(
                count:    n,
                itemSize: 120f,
                bind:     (card, i) => card.Populate(names[i], prices[i], colors[i])));
        }

        private void SetupProfiles()
        {
            if (profileCtrl == null) return;
            var rng    = new System.Random(22);
            int n      = profileCount;
            var users  = new string[n];
            var colors = new Color[n];
            for (int i = 0; i < n; i++)
            {
                users[i]  = "@" + s_userPre[rng.Next(s_userPre.Length)]
                               + "_" + s_userSuf[rng.Next(s_userSuf.Length)]
                               + rng.Next(10, 99);
                colors[i] = Color.HSVToRGB((i * 0.618034f) % 1f, 0.45f, 0.90f);
            }
            profileCtrl.Init(new RecyclableDataSource<ProfileCard>(
                count:    n,
                itemSize: 110f,
                bind:     (card, i) => card.Populate(users[i], colors[i])));
        }

        private void SetupBadges()
        {
            if (badgeCtrl == null) return;
            int n      = badgeCount;
            var names  = new string[n];
            var colors = new Color[n];
            for (int i = 0; i < n; i++)
            {
                names[i]  = s_badges[i % s_badges.Length]
                           + (i >= s_badges.Length ? $" {i / s_badges.Length + 1}" : "");
                colors[i] = Color.HSVToRGB((i * 0.618034f) % 1f, 0.70f, 0.88f);
            }
            badgeCtrl.Init(new RecyclableDataSource<BadgeCard>(
                count:    n,
                itemSize: 100f,
                bind:     (card, i) => card.Populate(names[i], colors[i])));
        }
    }
}
