using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace LittleCastle.Rendering
{
    /// <summary>Shared meshes and material; static instance batches with conservative patch culling.</summary>
    [ExecuteAlways]
    public sealed class MeadowGrassRenderer : MonoBehaviour
    {
        public MeadowGrassData data;
        public Material material;
        [NonSerialized] public MaterialPropertyBlock surfaceOverrides;
        [Min(1)] public float maximumDistance = 128;
        public Vector3 lodDistances = new Vector3(22,70,90);
        public bool forceFullDetail;
        public int visiblePatches, submittedClumps, submittedTriangles, drawCalls;
        readonly Plane[] planes = new Plane[6];
        readonly int[] counts=new int[4];
        readonly Bounds[] bounds=new Bounds[4];
        Matrix4x4[][] batches;
        MeadowGrassData cachedData;
        int capacity;
        static readonly ProfilerMarker Marker = new ProfilerMarker("LC.Meadow.CullAndSubmit");
        void OnEnable() { RenderPipelineManager.beginCameraRendering += Draw; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= Draw; }
        void Draw(ScriptableRenderContext context, Camera camera)
        {
            if(!isActiveAndEnabled || !data || !material || camera.cameraType==CameraType.Preview || !SystemInfo.supportsInstancing) return;
            using(Marker.Auto())
            {
                visiblePatches=submittedClumps=submittedTriangles=drawCalls=0;
                if(cachedData!=data||capacity!=data.authoredClumps||batches==null)
                {cachedData=data;capacity=data.authoredClumps;batches=new Matrix4x4[4][];for(int i=0;i<4;i++)batches[i]=new Matrix4x4[capacity];}
                Array.Clear(counts,0,4);
                GeometryUtility.CalculateFrustumPlanes(camera,planes);
                Vector3 eye=camera.transform.position;
                foreach(var patch in data.patches)
                {
                    if(!GeometryUtility.TestPlanesAABB(planes,patch.bounds))continue;
                    float squareDistance=patch.bounds.SqrDistance(eye);
                    if(squareDistance>maximumDistance*maximumDistance)continue;
                    int lod=forceFullDetail?0:squareDistance<lodDistances.x*lodDistances.x?0:squareDistance<lodDistances.y*lodDistances.y?1:squareDistance<lodDistances.z*lodDistances.z?2:3;
                    if(counts[lod]==0)bounds[lod]=patch.bounds;else bounds[lod].Encapsulate(patch.bounds);
                    Array.Copy(patch.matrices,0,batches[lod],counts[lod],patch.matrices.Length);counts[lod]+=patch.matrices.Length;visiblePatches++;
                }
                for(int lod=0;lod<4;lod++)
                {
                    if(counts[lod]==0)continue;
                    var mesh=data.lodMeshes[lod];var parameters=new RenderParams(material){matProps=surfaceOverrides,camera=camera,worldBounds=bounds[lod],layer=gameObject.layer,shadowCastingMode=ShadowCastingMode.Off,receiveShadows=true,lightProbeUsage=LightProbeUsage.Off};
                    submittedClumps+=counts[lod];submittedTriangles+=counts[lod]*(int)(mesh.GetIndexCount(0)/3);
                    // Merge already-culled patches by LOD, then submit reused instance arrays.
                    for(int start=0;start<counts[lod];start+=500)
                    {Graphics.RenderMeshInstanced(parameters,mesh,0,batches[lod],Mathf.Min(500,counts[lod]-start),start);drawCalls++;}
                }
            }
        }
    }
}
