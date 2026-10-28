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

            RiverWaterPresenter existing =
                chunkRoot.GetComponentInChildren<RiverWaterPresenter>();
            if (existing != null && existing.OwnedMesh != null)
                return existing;

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

            presenter.ownedMesh = mesh;
            presenter.meshFilter.sharedMesh = mesh;
            presenter.meshRenderer.sharedMaterial = material;
            presenter.meshRenderer.enabled = true;
            presenter.meshRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            presenter.meshRenderer.receiveShadows = false;
            return presenter;
        }

        public void ReleaseOwnedResources()
        {
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
