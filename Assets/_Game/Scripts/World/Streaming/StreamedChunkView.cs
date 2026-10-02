using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Scene-side owner of presentation resources for one streamed chunk.
    ///
    /// Generated data is owned by WorldChunkCache. This component owns only
    /// transient Unity presentation resources such as the runtime mesh.
    /// </summary>
    public sealed class StreamedChunkView : MonoBehaviour
    {
        private Mesh ownedMesh;
        private MeshCollider terrainCollider;

        public ChunkCoordinate Coordinate { get; private set; }
        public bool IsPlayableChunk { get; private set; }

        public bool TerrainColliderEnabled =>
            terrainCollider != null &&
            terrainCollider.enabled;

        public void Initialize(
            ChunkCoordinate coordinate,
            Mesh mesh,
            bool isPlayableChunk = true)
        {
            Coordinate = coordinate;
            IsPlayableChunk = isPlayableChunk;
            ownedMesh = mesh;
        }

        public void SetTerrainColliderEnabled(
            bool enabled)
        {
            if (!IsPlayableChunk)
                enabled = false;

            if (enabled)
            {
                if (terrainCollider == null)
                {
                    terrainCollider =
                        GetComponent<MeshCollider>();

                    if (terrainCollider == null)
                    {
                        terrainCollider =
                            gameObject.AddComponent<
                                MeshCollider>();
                    }
                }

                if (terrainCollider.sharedMesh !=
                    ownedMesh)
                {
                    terrainCollider.sharedMesh =
                        ownedMesh;
                }

                terrainCollider.enabled =
                    ownedMesh != null;
            }
            else if (terrainCollider != null)
            {
                terrainCollider.enabled = false;
            }
        }

        public void ReleaseOwnedResources()
        {
            if (terrainCollider != null)
            {
                terrainCollider.enabled = false;
                terrainCollider.sharedMesh = null;
            }

            if (ownedMesh == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(ownedMesh);
            else
                Object.DestroyImmediate(ownedMesh);

            ownedMesh = null;
        }
    }
}
