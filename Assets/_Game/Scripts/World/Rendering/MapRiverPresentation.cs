using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Rendering
{
    /// <summary>Disposable resident ribbons and rigid catalog modules. One owning Tick; no per-rock Update.</summary>
    public sealed class MapRiverPresentation : IDisposable
    {
        sealed class Tile {public long owner;public int index;public Rect area;public GameObject root;public Mesh water,bank;}
        readonly MapEditSession session;
        readonly MapRockCatalog catalog;
        readonly Dictionary<string,Tile> tiles=new Dictionary<string,Tile>();
        readonly Dictionary<long,GameObject> rocks=new Dictionary<long,GameObject>();
        readonly List<string> removeTiles=new List<string>();
        readonly List<long> removeRocks=new List<long>();
        readonly HashSet<string> wantedTiles=new HashSet<string>();
        readonly HashSet<long> wantedRocks=new HashSet<long>();
        bool dirty=true;
        Vector3 lastEye=Vector3.positiveInfinity;
        public float maximumDistance=100;
        public int maximumTiles=48,maximumRocks=192,creationBudget=8;
        public int ResidentTiles=>tiles.Count;
        public int ResidentRocks=>rocks.Count;
        public int LastRebuiltTiles {get;private set;}
        public double LastMeshMilliseconds {get;private set;}
        public MapRiverPresentation(MapEditSession session,MapRockCatalog catalog){this.session=session;this.catalog=catalog;session.RegisterRockCatalog(catalog.Definitions);session.Changed+=Changed;}
        void Changed(MapRegionChange c)
        {
            if((c.kind&(MapChangeKind.Height|MapChangeKind.Rivers))==0)return;dirty=true;
            removeTiles.Clear();foreach(var pair in tiles)if(pair.Value.area.Overlaps(c.bounds))removeTiles.Add(pair.Key);
            foreach(var key in removeTiles){DestroyTile(tiles[key]);tiles.Remove(key);}
            removeRocks.Clear();foreach(var pair in rocks){var p=pair.Value.transform.position;if(c.bounds.Contains(new Vector2(p.x,p.z)))removeRocks.Add(pair.Key);}
            foreach(var id in removeRocks){Destroy(rocks[id]);rocks.Remove(id);}
        }
        public void Tick(Vector3 eye,bool force=false)
        {
            if(!dirty&&!force&&(eye-lastEye).sqrMagnitude<4)return;lastEye=eye;dirty=false;wantedTiles.Clear();wantedRocks.Clear();LastRebuiltTiles=0;
            var watch=System.Diagnostics.Stopwatch.StartNew();int created=0;
            var area=new Rect(eye.x-maximumDistance,eye.z-maximumDistance,maximumDistance*2,maximumDistance*2);
            foreach(var path in session.QueryRiverPaths(area))
            {
                for(int start=0,tileIndex=0;start<path.frames.Count-1;start+=20,tileIndex++)
                {
                    int end=Mathf.Min(start+20,path.frames.Count-1);Rect bounds=FrameBounds(path,start,end);
                    if(!area.Overlaps(bounds))continue;string key=path.record.id+"/"+tileIndex;
                    if(wantedTiles.Count>=maximumTiles)break;wantedTiles.Add(key);
                    if(!tiles.ContainsKey(key)){if(created>=creationBudget){dirty=true;continue;}tiles.Add(key,BuildTile(path,start,end,tileIndex,bounds));created++;LastRebuiltTiles++;}
                }
                foreach(var record in path.record.rocks)
                {
                    if(!area.Contains(record.position)||record.removed||wantedRocks.Count>=maximumRocks)continue;
                    if(path.Nearest(record.position,out var f,out var d)&&(d>=0?f.leftSand:f.rightSand)>.12f)continue;
                    AddRock(record,eye,ref created);
                }
            }
            foreach(var section in session.ExportRockSections())foreach(var record in section.rocks)if(area.Contains(record.position)&&!record.removed&&wantedRocks.Count<maximumRocks)AddRock(record,eye,ref created);
            removeTiles.Clear();foreach(var key in tiles.Keys)if(!wantedTiles.Contains(key))removeTiles.Add(key);
            foreach(var key in removeTiles){DestroyTile(tiles[key]);tiles.Remove(key);}
            removeRocks.Clear();foreach(long id in rocks.Keys)if(!wantedRocks.Contains(id))removeRocks.Add(id);
            foreach(long id in removeRocks){Destroy(rocks[id]);rocks.Remove(id);}
            LastMeshMilliseconds=watch.Elapsed.TotalMilliseconds;
        }
        void AddRock(MapRockInstance r,Vector3 eye,ref int created)
        {
            wantedRocks.Add(r.id);if(rocks.TryGetValue(r.id,out var existing)){EnableCollider(existing,eye);return;}
            if(created>=creationBudget){dirty=true;return;}var entry=catalog.Resolve(r.archetypeId,r.catalogVersion);if(entry==null||!entry.prefab)return;
            var root=UnityEngine.Object.Instantiate(entry.prefab);root.name="River rock "+r.id;root.hideFlags=HideFlags.HideAndDontSave;
            float top=session.SampleBaseHeight(r.position.x,r.position.y)+r.topOffset-entry.definition.embed;
            root.transform.SetPositionAndRotation(new Vector3(r.position.x,top-entry.definition.height*r.scale,r.position.y),Quaternion.Euler(0,r.yaw,0));root.transform.localScale=Vector3.one*r.scale;
            var collider=root.AddComponent<BoxCollider>();collider.center=entry.bounds.center;collider.size=entry.bounds.size*.88f;EnableCollider(root,eye);
            rocks.Add(r.id,root);created++;
        }
        static void EnableCollider(GameObject root,Vector3 eye){var c=root.GetComponent<Collider>();if(c)c.enabled=(root.transform.position-eye).sqrMagnitude<40*40;}
        static Rect FrameBounds(MapRiverPath p,int start,int end)
        {
            Vector2 lo=p.frames[start].center,hi=lo;
            for(int i=start;i<=end;i++){var f=p.frames[i];float extent=f.width*.5f+Mathf.Max(f.leftTransition,f.rightTransition)+1;lo=Vector2.Min(lo,f.center-Vector2.one*extent);hi=Vector2.Max(hi,f.center+Vector2.one*extent);}return Rect.MinMaxRect(lo.x,lo.y,hi.x,hi.y);
        }
        Tile BuildTile(MapRiverPath path,int start,int end,int tileIndex,Rect bounds)
        {
            var root=new GameObject("River ribbon "+path.record.id+"/"+tileIndex){hideFlags=HideFlags.HideAndDontSave};
            var tile=new Tile{owner=path.record.id,index=tileIndex,area=bounds,root=root};
            tile.water=BuildRibbon(path,start,end,true);tile.bank=BuildRibbon(path,start,end,false);
            AddMesh(root,"Water",tile.water,catalog.waterMaterial,false);AddMesh(root,"Bed, bank and earth shoulder",tile.bank,catalog.bankMaterial,true);return tile;
        }
        static void AddMesh(GameObject root,string name,Mesh mesh,Material material,bool opaque)
        {
            var child=new GameObject(name){hideFlags=HideFlags.HideAndDontSave};child.transform.SetParent(root.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.Off;
        }
        Mesh BuildRibbon(MapRiverPath path,int start,int end,bool water)
        {
            // Same stations at tile borders and same global height sampler, no independent border noise.
            const int across=32;int rows=end-start+1,count=rows*(across+1);
            var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];var data=new List<Vector4>(count);var colors=new Color[count];var triangles=new int[(rows-1)*across*6];
            for(int row=0;row<rows;row++)
            {
                var f=path.frames[start+row];float half=f.width*.5f;
                for(int col=0;col<=across;col++)
                {
                    int i=row*(across+1)+col;float fraction=col/(float)across;float signed=water?Mathf.Lerp(-half,half,fraction):Mathf.Lerp(-half-f.rightTransition,half+f.leftTransition,fraction);
                    var p=f.center+f.Left*signed;float h=session.SampleHeight(p.x,p.y);vertices[i]=new Vector3(p.x,water?f.water+.018f:h+.025f,p.y);normals[i]=water?Vector3.up:session.SampleNormal(p.x,p.y);uv[i]=new Vector2(signed/half,f.station);
                    float sand=signed>=0?f.leftSand:f.rightSand,transition=signed>=0?f.leftTransition:f.rightTransition,u=Mathf.Clamp01((Mathf.Abs(signed)-half)/transition);
                    var sandColor=new Color(.68f,.57f,.35f);var rockColor=new Color(.49f,.45f,.36f);var grassColor=new Color(.34f,.43f,.12f);
                    var color=Mathf.Abs(signed)<=half?sandColor:Color.Lerp(rockColor,sandColor,sand);
                    colors[i]=Color.Lerp(color,grassColor,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,u)));
                    colors[i].a=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,u));
                    data.Add(new Vector4(Mathf.Max(0,f.water-h),f.speed,Mathf.Clamp01(1-(f.water-h)),Mathf.Clamp01(f.ford)));
                }
            }
            int t=0;for(int row=0;row<rows-1;row++)for(int col=0;col<across;col++)
            {int a=row*(across+1)+col,b=a+across+1;triangles[t++]=a;triangles[t++]=a+1;triangles[t++]=b;triangles[t++]=a+1;triangles[t++]=b+1;triangles[t++]=b;}
            var mesh=new Mesh{name=water?"Flow strip":"Continuous bank closure",hideFlags=HideFlags.HideAndDontSave};mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.SetUVs(1,data);mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
        }
        public void Dispose(){session.Changed-=Changed;foreach(var tile in tiles.Values)DestroyTile(tile);tiles.Clear();foreach(var root in rocks.Values)Destroy(root);rocks.Clear();}
        static void DestroyTile(Tile tile){Destroy(tile.root);Destroy(tile.water);Destroy(tile.bank);}
        static void Destroy(UnityEngine.Object obj){if(Application.isPlaying)UnityEngine.Object.Destroy(obj);else UnityEngine.Object.DestroyImmediate(obj);}
    }
}
