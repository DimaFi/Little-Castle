using UnityEngine;

namespace LittleCastle.Building
{
    /// <summary>
    /// Scene-side identity for a structural tower derived from one wall path.
    /// The tower transform is deterministic and is not stored independently.
    /// </summary>
    public sealed class WallTowerHandle : MonoBehaviour
    {
        [SerializeField] private long wallId;
        [SerializeField] private long towerId;
        [SerializeField] private int towerIndex;
        [SerializeField] private bool startTower;

        public long WallId => wallId;
        public long TowerId => towerId;
        public int TowerIndex => towerIndex;
        public bool IsStartTower => startTower;

        public void Initialize(
            long wallId,
            long towerId,
            int towerIndex,
            bool startTower)
        {
            this.wallId = wallId;
            this.towerId = towerId;
            this.towerIndex = towerIndex;
            this.startTower = startTower;
        }
    }
}
