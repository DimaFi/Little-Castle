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

        public ChunkCoordinate Coordinate { get; private set; }

        public void Initialize(
            ChunkCoordinate coordinate,
            Mesh mesh)
        {
            Coordinate = coordinate;
            ownedMesh = mesh;
        }

        public void ReleaseOwnedResources()
        {
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
