using System.Runtime.CompilerServices;

// Expose internals (LinearLayout and friends) to the test assemblies so the layout
// math can be unit-tested without a live ScrollRect.
[assembly: InternalsVisibleTo("KidzDev.Unity.RecyclableScroll.Tests")]
[assembly: InternalsVisibleTo("KidzDev.Unity.RecyclableScroll.PlayTests")]
