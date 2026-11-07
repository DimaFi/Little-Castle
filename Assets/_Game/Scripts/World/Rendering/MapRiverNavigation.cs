using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;
using UnityEngine.AI;

namespace LittleCastle.Rendering
{
    /// <summary>Bounded navigation acceptance adapter. Reads resident collision geometry and authoritative water.</summary>
    public sealed class MapRiverNavigation : IDisposable
    {
        readonly List<Mesh> derived=new List<Mesh>();
        NavMeshData data;
        NavMeshDataInstance instance;
        public int SourceCount {get;private set;}
        public double BuildMilliseconds {get;private set;}
        public void Build(MapEditSession session,GameObject bridge,Bounds bounds)
        {
            if (!bridge || !bridge.scene.IsValid()) throw new InvalidOperationException("An isolated scene bridge is required for the navigation fixture.");
            Dispose();var sources=new List<NavMeshBuildSource>();var watch=System.Diagnostics.Stopwatch.StartNew();
            foreach(var filter in Resources.FindObjectsOfTypeAll<MeshFilter>())
            {
                if(filter.gameObject.scene!=bridge.scene||!filter.sharedMesh||!filter.name.StartsWith("Terrain view "))continue;
                var mesh=filter.sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;var allowed=new List<int>();
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var center=filter.transform.TransformPoint((vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3);
                    if(!bounds.Contains(center))continue;
                    if(session.QueryWater(new Vector2(center.x,center.z),out _,out var depth,out _)&&depth>.45f)continue;
                    allowed.Add(triangles[i]);allowed.Add(triangles[i+1]);allowed.Add(triangles[i+2]);
                }
                if(allowed.Count==0)continue;var collision=UnityEngine.Object.Instantiate(mesh);collision.name="Derived walkable bank triangles";collision.triangles=allowed.ToArray();derived.Add(collision);
                sources.Add(new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Mesh,sourceObject=collision,transform=filter.transform.localToWorldMatrix,area=0});
            }
            if(bridge)foreach(var collider in bridge.GetComponentsInChildren<MeshCollider>(true))if(collider.sharedMesh)sources.Add(new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Mesh,sourceObject=collider.sharedMesh,transform=collider.transform.localToWorldMatrix,area=0});
            var settings=NavMesh.GetSettingsByIndex(0);settings.agentRadius=.3f;settings.agentHeight=1.7f;settings.agentClimb=.35f;settings.agentSlope=32;settings.overrideVoxelSize=true;settings.voxelSize=.12f;settings.overrideTileSize=true;settings.tileSize=128;
            SourceCount=sources.Count;data=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);if(!data)throw new InvalidOperationException("Bounded river navigation build returned no data.");
            instance=NavMesh.AddNavMeshData(data);BuildMilliseconds=watch.Elapsed.TotalMilliseconds;
        }
        public bool Path(Vector3 from,Vector3 to,out NavMeshPath path)
        {
            path=new NavMeshPath();return NavMesh.SamplePosition(from,out var a,1,NavMesh.AllAreas)&&NavMesh.SamplePosition(to,out var b,1,NavMesh.AllAreas)&&NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
        }
        public void Dispose(){if(instance.valid)instance.Remove();if(data){Destroy(data);data=null;}foreach(var mesh in derived)Destroy(mesh);derived.Clear();}
        static void Destroy(UnityEngine.Object value){if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
}
