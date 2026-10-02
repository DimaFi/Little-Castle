using UnityEngine;

namespace LittleCastle.Building
{
    /// <summary>
    /// Scene-side identity for one derived rigid wall module.
    ///
    /// Later damage/repair code can address sectionId without making section
    /// transforms part of the save/network payload.
    /// </summary>
    public sealed class WallSectionHandle : MonoBehaviour
    {
        [SerializeField] private long wallId;
        [SerializeField] private long sectionId;
        [SerializeField] private int sectionIndex;

        public long WallId => wallId;
        public long SectionId => sectionId;
        public int SectionIndex => sectionIndex;

        public void Initialize(
            long wallId,
            long sectionId,
            int sectionIndex)
        {
            this.wallId = wallId;
            this.sectionId = sectionId;
            this.sectionIndex = sectionIndex;
        }
    }
}
