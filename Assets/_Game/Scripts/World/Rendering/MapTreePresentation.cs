using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>Bounded resident tree views, existing prefab LODs and shared materials. No full-map spawn.</summary>
    public sealed class MapTreePresentation : IDisposable
    {
        sealed class View {public GameObject root;public string source;public CapsuleCollider collider;public Bounds bounds;}
        readonly MapEditSession session;
        readonly MapTreeCatalog catalog;
        readonly Dictionary<long,View> views=new Dictionary<long,View>();
        readonly Dictionary<GameObject,bool> sourceStates=new Dictionary<GameObject,bool>();
        readonly List<MapTreeInstance> wanted=new List<MapTreeInstance>();
        readonly HashSet<long> wantedIds=new HashSet<long>();
        readonly List<long> removals=new List<long>();
        bool dirty=true;
        float nextRefresh;
        public float maximumDistance=120,colliderDistance=28;
        public int maximumResident=96,creationBudget=8;
        public int ResidentCount=>views.Count;
        public int MissingSources {get;private set;}
        public MapTreePresentation(MapEditSession session,MapTreeCatalog catalog,bool hideSceneSources=true)
        {
            this.session=session;this.catalog=catalog;
            if(hideSceneSources)foreach(var root in catalog.SceneSources()){sourceStates.Add(root,root.activeSelf);root.SetActive(false);}
            session.Changed+=Changed;
        }
        public void Tick(Vector3 eye,bool forceRefresh=false)
        {
            if(forceRefresh||dirty||Time.realtimeSinceStartup>=nextRefresh)
            {
                nextRefresh=Time.realtimeSinceStartup+.2f;dirty=false;MissingSources=0;wanted.Clear();wantedIds.Clear();
                var candidates=session.QueryTrees(new Rect(eye.x-maximumDistance,eye.z-maximumDistance,maximumDistance*2,maximumDistance*2));
                candidates.Sort((a,b)=>{float da=(a.position-new Vector2(eye.x,eye.z)).sqrMagnitude,db=(b.position-new Vector2(eye.x,eye.z)).sqrMagnitude;int d=da.CompareTo(db);return d!=0?d:a.stableId.CompareTo(b.stableId);});
                foreach(var record in candidates)
                {
                    if(wanted.Count>=maximumResident)break;if(!session.VegetationAllowed(record.position.x,record.position.y,record.clearanceRadius))continue;if((record.position-new Vector2(eye.x,eye.z)).sqrMagnitude>maximumDistance*maximumDistance)continue;
                    var entry=catalog.Resolve(record.archetypeId);if(entry==null||!entry.prefab){MissingSources++;continue;}wanted.Add(record);wantedIds.Add(record.stableId);
                }
                removals.Clear();foreach(var pair in views)if(!wantedIds.Contains(pair.Key)||!session.HasTree(pair.Key))removals.Add(pair.Key);
                foreach(long id in removals){Destroy(views[id].root);views.Remove(id);}
                foreach(var pair in views){var p=pair.Value.root.transform.position;pair.Value.collider.enabled=(new Vector2(p.x-eye.x,p.z-eye.z)).sqrMagnitude<colliderDistance*colliderDistance;}
            }
            int created=0;
            foreach(var target in wanted)
            {
                if(views.ContainsKey(target.stableId)||!session.HasTree(target.stableId))continue;if(created>=creationBudget)break;
                var record=session.GetTree(target.stableId);var entry=catalog.Resolve(record.archetypeId);if(entry==null||!entry.prefab)continue;
                var root=UnityEngine.Object.Instantiate(entry.prefab);root.name=entry.title+" · "+record.stableId;root.hideFlags=HideFlags.HideAndDontSave;root.SetActive(true);
                root.AddComponent<MapTreeInstanceView>().stableId=record.stableId;
                // Simple nearby trunk collision; prefab's visual LODs and shared material contracts stay intact.
                foreach(var old in root.GetComponentsInChildren<Collider>(true))old.enabled=false;
                var collider=root.AddComponent<CapsuleCollider>();collider.radius=.35f;collider.height=2.5f;collider.center=Vector3.up*1.25f;
                var view=new View{root=root,source=record.archetypeId,collider=collider};views.Add(record.stableId,view);Sync(record,view);
                collider.enabled=(record.position-new Vector2(eye.x,eye.z)).sqrMagnitude<colliderDistance*colliderDistance;created++;
            }
        }
        void Changed(MapRegionChange change)
        {
            bool objects=(change.kind&MapChangeKind.Objects)!=0;
            bool height=(change.kind&MapChangeKind.Height)!=0;
            if(!objects&&!height&&(change.kind&MapChangeKind.Rivers)==0)return;
            if((change.kind&MapChangeKind.Rivers)!=0)dirty=true;
            if(objects)dirty=true;
            removals.Clear();
            foreach(var pair in views)
            {
                if(!session.HasTree(pair.Key)){pair.Value.root.SetActive(false);removals.Add(pair.Key);continue;}
                var record=session.GetTree(pair.Key);
                if(pair.Value.source!=record.archetypeId){removals.Add(pair.Key);continue;}
                if(change.bounds.Contains(record.position))Sync(record,pair.Value);
            }
            foreach(long id in removals){Destroy(views[id].root);views.Remove(id);}
        }
        void Sync(MapTreeInstance record,View view)
        {
            var entry=catalog.Resolve(record.archetypeId);var spawn=record.ToWorldSpawn(session.SampleHeight(record.position.x,record.position.y));
            view.root.transform.SetPositionAndRotation(spawn.worldPosition,Quaternion.Euler(0,spawn.yawDegrees,0));view.root.transform.localScale=entry.prefab.transform.localScale*spawn.uniformScale;
            view.bounds=TransformBounds(entry.localBounds,view.root.transform.localToWorldMatrix);
        }
        public bool Pick(Ray ray,out long id)
        {
            id=0;float closest=float.PositiveInfinity;foreach(var pair in views)
            {
                var view=pair.Value;if(!session.HasTree(pair.Key)||!view.bounds.IntersectRay(ray,out _))continue;
                var local=catalog.Resolve(view.source).localBounds;var inverse=view.root.transform.worldToLocalMatrix;
                var origin=inverse.MultiplyPoint3x4(ray.origin);var direction=inverse.MultiplyVector(ray.direction);
                var center=local.center;center.y=local.min.y+local.size.y*.62f;
                var radius=new Vector3(Mathf.Max(.1f,local.extents.x),Mathf.Max(.1f,local.size.y*.38f),Mathf.Max(.1f,local.extents.z));
                var o=origin-center;o=new Vector3(o.x/radius.x,o.y/radius.y,o.z/radius.z);
                var d=new Vector3(direction.x/radius.x,direction.y/radius.y,direction.z/radius.z);
                float a=Vector3.Dot(d,d),b=Vector3.Dot(o,d),c=Vector3.Dot(o,o)-1,disc=b*b-a*c,distance=float.PositiveInfinity;
                if(a>1e-8f&&disc>=0){float near=(-b-Mathf.Sqrt(disc))/a,far=(-b+Mathf.Sqrt(disc))/a;distance=near>=0?near:far>=0?far:distance;}
                // A slender trunk box avoids selecting empty corners of a rotated crown AABB.
                var trunk=new Bounds(new Vector3(0,local.min.y+local.size.y*.325f,0),new Vector3(.8f,local.size.y*.65f,.8f));
                var localRay=new Ray(origin,direction);if(trunk.IntersectRay(localRay,out float hit))distance=Mathf.Min(distance,Vector3.Distance(ray.origin,view.root.transform.TransformPoint(localRay.GetPoint(hit))));
                if(distance<closest){closest=distance;id=pair.Key;}
            }
            return id!=0;
        }
        public Vector3 Position(long id)=>views.TryGetValue(id,out var view)?view.root.transform.position:throw new InvalidOperationException("Tree view is not resident.");
        public GameObject Root(long id)=>views.TryGetValue(id,out var view)?view.root:null;
        static Bounds TransformBounds(Bounds local,Matrix4x4 matrix)
        {
            Vector3 center=matrix.MultiplyPoint3x4(local.center),e=local.extents,x=matrix.MultiplyVector(Vector3.right*e.x),y=matrix.MultiplyVector(Vector3.up*e.y),z=matrix.MultiplyVector(Vector3.forward*e.z);
            return new Bounds(center,2*new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z)));
        }
        public void Dispose(){session.Changed-=Changed;foreach(var view in views.Values)Destroy(view.root);views.Clear();foreach(var pair in sourceStates)if(pair.Key)pair.Key.SetActive(pair.Value);sourceStates.Clear();}
        static void Destroy(UnityEngine.Object obj){if(Application.isPlaying)UnityEngine.Object.Destroy(obj);else UnityEngine.Object.DestroyImmediate(obj);}
    }
}
