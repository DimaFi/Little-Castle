using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Scene-side owner of presentation resources for one streamed chunk.
    ///
    /// Generated data is owned by WorldChunkCache. This component owns only
    /// transient Unity presentation resources such as the runtime mesh and
    /// distance-tier state for generated model colliders.
    /// </summary>
    public sealed class StreamedChunkView : MonoBehaviour
    {
        private sealed class ManagedCollider
        {
            public Collider collider;
            public bool authoredEnabled;
        }

        private Mesh ownedMesh;
        private MeshCollider terrainCollider;

        private readonly List<ManagedCollider> generatedObjectColliders =
            new List<ManagedCollider>();

        private bool generatedColliderCacheValid;

        public ChunkCoordinate Coordinate { get; private set; }
        public bool IsPlayableChunk { get; private set; }

        public bool TerrainColliderEnabled =>
            terrainCollider != null &&
            terrainCollider.enabled;

        public int ActiveGeneratedObjectColliderCount
        {
            get
            {
                EnsureGeneratedColliderCache();

                int count = 0;

                for (int i = 0;
                     i < generatedObjectColliders.Count;
                     i++)
                {
                    Collider collider =
                        generatedObjectColliders[i].collider;

                    if (collider != null &&
                        collider.enabled)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void Initialize(
            ChunkCoordinate coordinate,
            Mesh mesh,
            bool isPlayableChunk = true)
        {
            Coordinate = coordinate;
            IsPlayableChunk = isPlayableChunk;
            ownedMesh = mesh;
            generatedColliderCacheValid = false;
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

        public void InvalidateGeneratedObjectColliderCache()
        {
            generatedObjectColliders.Clear();
            generatedColliderCacheValid = false;
        }

        public void SetGeneratedObjectCollidersEnabled(
            bool enabled)
        {
            if (!IsPlayableChunk)
                enabled = false;

            EnsureGeneratedColliderCache();

            for (int i = 0;
                 i < generatedObjectColliders.Count;
                 i++)
            {
                ManagedCollider managed =
                    generatedObjectColliders[i];

                if (managed.collider == null)
                    continue;

                managed.collider.enabled =
                    enabled &&
                    managed.authoredEnabled;
            }
        }

        public void ReleaseOwnedResources()
        {
            SetGeneratedObjectCollidersEnabled(
                false);

            generatedObjectColliders.Clear();
            generatedColliderCacheValid = false;

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

        private void EnsureGeneratedColliderCache()
        {
            if (generatedColliderCacheValid)
                return;

            generatedObjectColliders.Clear();

            Collider[] colliders =
                GetComponentsInChildren<
                    Collider>(true);

            for (int i = 0;
                 i < colliders.Length;
                 i++)
            {
                Collider collider =
                    colliders[i];

                if (collider == null ||
                    collider == terrainCollider ||
                    collider.gameObject ==
                        gameObject)
                {
                    continue;
                }

                generatedObjectColliders.Add(
                    new ManagedCollider
                    {
                        collider = collider,
                        authoredEnabled =
                            collider.enabled
                    });
            }

            generatedColliderCacheValid = true;
        }
    }
}
