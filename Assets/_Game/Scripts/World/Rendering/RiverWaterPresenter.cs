using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Owns the one runtime river-water mesh under a streamed chunk view.
    /// Destroying or explicitly releasing this presenter frees that mesh.
    /// </summary>
    public sealed class RiverWaterPresenter : MonoBehaviour
    {
        private Mesh ownedMesh;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        public Mesh OwnedMesh => ownedMesh;

        public static RiverWaterPresenter Populate(
            Transform chunkRoot,
            WorldChunkData chunk,
            float chunkWorldSize,
            MacroWorldPlan plan,
            Material material)
        {
            if (chunkRoot == null || chunk == null ||
                plan == null || material == null)
                return null;

            // Chunk roots may be parked inactive during streaming. Avoid
            // creating a second water object when a parked presenter exists.
            RiverWaterPresenter existing =
                chunkRoot.GetComponentInChildren<RiverWaterPresenter>(true);
            if (existing != null && existing.OwnedMesh != null)
            {
                existing.ResolveComponents();
                if (existing.meshFilter != null &&
                    existing.meshRenderer != null)
                {
                    existing.meshRenderer.sharedMaterial = material;
                    return existing;
                }
            }

            Mesh mesh = RiverWaterMeshBuilder.Build(
                chunk, chunkWorldSize, plan);
            if (mesh == null)
                return null;

            RiverWaterPresenter presenter = existing;
            if (presenter == null)
            {
                var waterObject = new GameObject(
                    $"RiverWater_{chunk.Coordinate.x}_{chunk.Coordinate.z}");
                waterObject.transform.SetParent(chunkRoot, false);
                presenter = waterObject.AddComponent<RiverWaterPresenter>();
                presenter.meshFilter = waterObject.AddComponent<MeshFilter>();
                presenter.meshRenderer = waterObject.AddComponent<MeshRenderer>();
            }

            presenter.ResolveComponents();
            if (presenter.meshFilter == null ||
                presenter.meshRenderer == null)
            {
                // Do not orphan a transient mesh when a legacy presenter
                // has missing components. Recycle safely without releasing
                // another presenter's still-owned mesh.
                if (Application.isPlaying)
                    Destroy(mesh);
                else
                    DestroyImmediate(mesh);
                return null;
            }

            presenter.ownedMesh = mesh;
            presenter.meshFilter.sharedMesh = mesh;
            presenter.meshRenderer.sharedMaterial = material;
            presenter.meshRenderer.enabled = true;
            presenter.meshRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            presenter.meshRenderer.receiveShadows = false;
            return presenter;
        }

        private void ResolveComponents()
        {
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();
        }

        public void ReleaseOwnedResources()
        {
            ResolveComponents();
            if (meshFilter != null)
                meshFilter.sharedMesh = null;
            if (meshRenderer != null)
                meshRenderer.enabled = false;
            if (ownedMesh == null)
                return;

            Mesh released = ownedMesh;
            ownedMesh = null;
            if (Application.isPlaying)
                Destroy(released);
            else
                DestroyImmediate(released);
        }

        private void OnDestroy()
        {
            ReleaseOwnedResources();
        }
    }
}
