using UnityEngine;

namespace KidzDev.Unity.RecyclableScroll
{
    /// <summary>
    /// Base class for a recyclable list item view. Derive from this and override
    /// <see cref="OnBind"/> to refresh your visuals when the item is reused for a
    /// new data index.
    /// </summary>
    public class RecyclableScrollItem : MonoBehaviour
    {
        /// <summary>Data index this item view currently represents.</summary>
        public int Index { get; internal set; }

        /// <summary>
        /// Called by the view after <see cref="Index"/> has been assigned and the
        /// data source has bound this item. Override to update visuals.
        /// </summary>
        protected virtual void OnBind() { }

        /// <summary>
        /// Internal bridge that lets the owning <see cref="RecyclableScrollView"/>
        /// trigger the protected <see cref="OnBind"/> after the data source has bound
        /// this item. Not part of the public API.
        /// </summary>
        internal void InvokeOnBind() => OnBind();
    }
}
