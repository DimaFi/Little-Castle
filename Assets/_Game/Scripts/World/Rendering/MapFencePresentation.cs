using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace LittleCastle.Rendering
{
    /// <summary>Disposable, chunk-batched view. The saved state contains only fence recipes.</summary>
    public sealed class MapFencePresentation : IDisposable
    {
        sealed class Batch
        {
            public MapFenceCatalog.Entry entry;public Vector3 origin;
            public MapFenceGeometry.Buffer high=new MapFenceGeometry.Buffer(),low=new MapFenceGeometry.Buffer();
        }
        readonly MapEditSession session;
        readonly MapFenceCatalog catalog;
        readonly Dictionary<Mesh,MapFenceGeometry.Source> sources=new Dictionary<Mesh,MapFenceGeometry.Source>();
        readonly List<GameObject> roots=new List<GameObject>();
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly float distance;
        readonly Scene destination;
        bool dirty=true,disposed;
        Vector2Int focusCell=new Vector2Int(int.MinValue,int.MinValue);
        public int VisibleSpans {get;private set;}
        public int VisibleBatches=>roots.Count;
        public MapFencePresentation(MapEditSession session,MapFenceCatalog catalog,float distance=160,Scene destination=default)
        {this.session=session;this.catalog=catalog;this.distance=distance;this.destination=destination;session.Changed+=Changed;}
        void Changed(MapRegionChange change)
        {if(!change.preview&&(change.kind&(MapChangeKind.Fences|MapChangeKind.Height|MapChangeKind.Rivers))!=0)dirty=true;}
        MapFenceGeometry.Source Source(Mesh mesh)
        {if(!sources.TryGetValue(mesh,out var result)){result=new MapFenceGeometry.Source(mesh);sources.Add(mesh,result);}return result;}
        public void Tick(Vector3 focus)
        {
            if(disposed||!catalog)return;var cell=new Vector2Int(Mathf.FloorToInt(focus.x/32),Mathf.FloorToInt(focus.z/32));
            if(cell!=focusCell){focusCell=cell;dirty=true;}if(!dirty)return;dirty=false;Rebuild(new Vector2(focus.x,focus.z));
        }
        void Rebuild(Vector2 focus)
        {
            Clear();VisibleSpans=0;var batches=new SortedDictionary<string,Batch>(StringComparer.Ordinal);
            var posts=new HashSet<Vector2Int>();var area=new Rect(focus.x-distance,focus.y-distance,distance*2,distance*2);
            var paths=session.QueryFencePaths(area);paths.Sort((a,b)=>{int d=Distance(a.bounds,focus).CompareTo(Distance(b.bounds,focus));return d!=0?d:a.record.id.CompareTo(b.record.id);});
            Batch Get(MapFenceCatalog.Entry entry,Vector2 point)
            {
                int x=Mathf.FloorToInt(point.x/32),z=Mathf.FloorToInt(point.y/32);string key=entry.id+"/"+x+"/"+z;
                if(!batches.TryGetValue(key,out var batch)){batch=new Batch{entry=entry,origin=new Vector3(x*32,0,z*32)};batches.Add(key,batch);}return batch;
            }
            foreach(var path in paths)
            {
                var entry=catalog.Resolve(path.record.variantId);if(entry==null||!entry.Complete)continue;
                for(int i=0;i<path.PostCount;i++)
                {
                    var f=path.At(i*path.ActualSpacing);if(!area.Contains(f.position))continue;
                    var key=new Vector2Int(Mathf.RoundToInt(f.position.x*1000),Mathf.RoundToInt(f.position.y*1000));
                    if(!posts.Add(key))continue;var batch=Get(entry,f.position);float h=session.SampleHeight(f.position.x,f.position.y);
                    MapFenceGeometry.Post(batch.high,Source(entry.postLOD0),f,h,batch.origin,path.record.scale);
                    MapFenceGeometry.Post(batch.low,Source(entry.postLOD1),f,h,batch.origin,path.record.scale);
                }
                for(int i=0;i<path.SpanCount;i++)
                {
                    float start=i*path.ActualSpacing,end=(i+1)*path.ActualSpacing;var middle=path.At((start+end)*.5f);
                    if(!area.Contains(middle.position)||VisibleSpans>=2048)continue;
                    var batch=Get(entry,middle.position);
                    foreach(float mounting in new[]{.41f,.86f})
                    {
                        MapFenceGeometry.Rail(batch.high,Source(entry.railLOD0),path,start,end,mounting,session.SampleHeight,batch.origin);
                        MapFenceGeometry.Rail(batch.low,Source(entry.railLOD1),path,start,end,mounting,session.SampleHeight,batch.origin);
                    }
                    VisibleSpans++;
                }
                if(VisibleSpans>=2048)break;
            }
            foreach(var pair in batches)
            {
                var batch=pair.Value;var root=new GameObject("Fence preview · "+pair.Key){hideFlags=HideFlags.HideAndDontSave};
                if(destination.IsValid())SceneManager.MoveGameObjectToScene(root,destination);
                roots.Add(root);root.transform.position=batch.origin;var renderers=new Renderer[2];
                for(int lod=0;lod<2;lod++)
                {
                    var mesh=(lod==0?batch.high:batch.low).ToMesh("Derived fence chunk "+pair.Key+" LOD"+lod);meshes.Add(mesh);
                    var child=new GameObject("LOD"+lod){hideFlags=HideFlags.HideAndDontSave};child.transform.SetParent(root.transform,false);
                    child.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=batch.entry.material;
                    renderer.shadowCastingMode=lod==0?ShadowCastingMode.On:ShadowCastingMode.Off;renderer.receiveShadows=lod==0;renderers[lod]=renderer;
                }
                var group=root.AddComponent<LODGroup>();group.SetLODs(new[]{new LOD(.15f,new[]{renderers[0]}),new LOD(.004f,new[]{renderers[1]})});group.RecalculateBounds();
            }
        }
        void Clear(){foreach(var root in roots)Destroy(root);roots.Clear();foreach(var mesh in meshes)Destroy(mesh);meshes.Clear();}
        static float Distance(Rect r,Vector2 p)=>(p-new Vector2(Mathf.Clamp(p.x,r.xMin,r.xMax),Mathf.Clamp(p.y,r.yMin,r.yMax))).sqrMagnitude;
        static void Destroy(Object value){if(!value)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
        public void Dispose(){if(disposed)return;disposed=true;session.Changed-=Changed;Clear();}
    }
}
