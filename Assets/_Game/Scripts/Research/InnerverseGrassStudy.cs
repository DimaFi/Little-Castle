using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Research
{
    /// <summary>Presentation-only research fixture. Authored positions are fixed in the saved scene.</summary>
    [ExecuteAlways]
    public sealed class InnerverseGrassStudy : MonoBehaviour
    {
        public Mesh[] lodMeshes;
        public Material grassMaterial;
        [System.NonSerialized] public MaterialPropertyBlock surfaceOverrides;
        public Matrix4x4[] authoredMatrices;
        [Min(1)] public float maximumDistance = 95;
        public bool receiveGroundShadows;
        public int lastVisibleClumps;
        public int lastSubmittedTriangles;
        private struct Instance { public Matrix4x4 objectToWorld; }
        private readonly List<Instance>[] groups = { new List<Instance>(), new List<Instance>(), new List<Instance>(), new List<Instance>() };
        private void OnEnable()
        {
            Camera.onPreCull += DrawBuiltIn;
            RenderPipelineManager.beginCameraRendering += DrawSrp;
        }
        private void OnDisable()
        {
            Camera.onPreCull -= DrawBuiltIn;
            RenderPipelineManager.beginCameraRendering -= DrawSrp;
        }
        private void DrawBuiltIn(Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline == null) Draw(camera);
        }
        private void DrawSrp(ScriptableRenderContext context, Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline != null) Draw(camera);
        }
        private void Draw(Camera camera)
        {
            if (!isActiveAndEnabled || camera == null || grassMaterial == null || authoredMatrices == null || lodMeshes == null || lodMeshes.Length != 4) return;
            if (camera.cameraType == CameraType.Preview || !SystemInfo.supportsInstancing) return;
            foreach (var list in groups) list.Clear();
            lastVisibleClumps = lastSubmittedTriangles = 0;
            Vector3 eye = camera.transform.position;
            foreach (var matrix in authoredMatrices)
            {
                Vector3 position = matrix.GetColumn(3);
                float distance = Vector3.Distance(eye, position);
                if (distance > maximumDistance) continue;
                int lod = distance < 18 ? 0 : distance < 35 ? 1 : distance < 60 ? 2 : 3;
                groups[lod].Add(new Instance { objectToWorld = matrix });
            }
            var parameters = new RenderParams(grassMaterial) { matProps=surfaceOverrides,camera = camera,
                shadowCastingMode = ShadowCastingMode.Off, receiveShadows = receiveGroundShadows,
                lightProbeUsage = LightProbeUsage.Off, layer = gameObject.layer,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 500) };
            for (int lod = 0; lod < 4; lod++)
            {
                if (lodMeshes[lod] == null) continue;
                var list = groups[lod];
                lastVisibleClumps += list.Count;
                lastSubmittedTriangles += list.Count * (int)(lodMeshes[lod].GetIndexCount(0) / 3);
                for (int start = 0; start < list.Count; start += 500)
                    Graphics.RenderMeshInstanced(parameters, lodMeshes[lod], 0, list, Math.Min(500, list.Count - start), start);
            }
        }
    }
}
