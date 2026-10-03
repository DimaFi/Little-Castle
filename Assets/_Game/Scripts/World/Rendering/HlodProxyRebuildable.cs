using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// Base class for a presentation-only HLOD proxy builder.
    /// Structural changes are coalesced through HlodRebuildScheduler.
    /// </summary>
    public abstract class HlodProxyRebuildable : MonoBehaviour
    {
        public bool IsHlodRebuildQueued
        {
            get;
            internal set;
        }

        public void MarkHlodDirty()
        {
            HlodRebuildScheduler.Schedule(this);
        }

        protected virtual void OnDisable()
        {
            HlodRebuildScheduler.Cancel(this);
        }

        public abstract void RebuildHlodProxy();
    }
}
