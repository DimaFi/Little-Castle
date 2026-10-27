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
        sealed class Tile {public long owner;public int index;public Rect area;public GameObject root;public Mesh water,bank;public Texture2D flow;}
        readonly MapEditSession session;
        readonly MapRockCatalog catalog;
        readonly MapRockCatalog rockCatalog;
        readonly bool waterOnly;
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
        public Material WaterMaterial=>catalog.waterMaterial;
        public bool WaterOnly=>waterOnly;
        public int ResidentTiles=>tiles.Count;
        public int ResidentRocks=>rocks.Count;
        public IEnumerable<MeshRenderer> WaterRenderers {get{foreach(var tile in tiles.Values)if(tile.root){var child=tile.root.transform.Find("Water");if(child)yield return child.GetComponent<MeshRenderer>();}}}
        public int LastRebuiltTiles {get;private set;}
        public double LastMeshMilliseconds {get;private set;}
        public MapRiverPresentation(MapEditSession session,MapRockCatalog catalog,bool waterOnly=false,MapRockCatalog legacyRockCatalog=null)
        {
            this.session=session;this.catalog=catalog;this.waterOnly=waterOnly;
            // A water material override must not remove the existing rigid-module authoring catalog.
            rockCatalog=legacyRockCatalog?legacyRockCatalog:waterOnly?MapRockCatalog.Default:catalog;
            if(rockCatalog)session.RegisterRockCatalog(rockCatalog.Definitions);
            session.Changed+=Changed;
        }
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
                    if(waterOnly&&path.record.layoutVersion!=1&&!record.manual)continue;
                    if(!area.Contains(record.position)||record.removed||wantedRocks.Count>=maximumRocks)continue;
                    if(!record.manual&&path.Nearest(record.position,out var f,out var d)&&(d>=0?f.leftSand:f.rightSand)>.12f)continue;
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
            if(created>=creationBudget){dirty=true;return;}var entry=rockCatalog?rockCatalog.Resolve(r.archetypeId,r.catalogVersion):null;if(entry==null||!entry.prefab)return;
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
            tile.water=BuildRibbon(path,start,end,true);
            AddMesh(root,"Water",tile.water,catalog.waterMaterial,false);
            if(!waterOnly||path.record.layoutVersion==1&&rockCatalog)
            {
                tile.bank=BuildRibbon(path,start,end,false);
                AddMesh(root,"Bed, bank and earth shoulder",tile.bank,waterOnly?rockCatalog.bankMaterial:catalog.bankMaterial,true);
            }
            if(path.record.layoutVersion>=2)
            {
                tile.flow=RiverBankMeshBuilder.FlowMap(path,bounds,out var flowBounds);var block=new MaterialPropertyBlock();block.SetTexture("_LC_RiverFlowMap",tile.flow);block.SetVector("_LC_FlowBounds",flowBounds);block.SetFloat("_LC_UseFlowMap",1);root.transform.Find("Water").GetComponent<MeshRenderer>().SetPropertyBlock(block);
            }
            return tile;
        }
        static void AddMesh(GameObject root,string name,Mesh mesh,Material material,bool opaque)
        {
            var child=new GameObject(name){hideFlags=HideFlags.HideAndDontSave};child.transform.SetParent(root.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;
            if(!opaque)child.layer=4;
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
                    int i=row*(across+1)+col;float fraction=col/(float)across;
                    float extraLeft=water&&path.record.layoutVersion>=2?Mathf.Clamp(f.leftTransition*.3f,.6f,3):0,extraRight=water&&path.record.layoutVersion>=2?Mathf.Clamp(f.rightTransition*.3f,.6f,3):0;
                    float signed=water?Mathf.Lerp(-half-extraRight,half+extraLeft,fraction):Mathf.Lerp(-half-f.rightTransition,half+f.leftTransition,fraction);
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
            var mesh=new Mesh{name=water?"Flow strip":"Continuous bank closure",hideFlags=HideFlags.HideAndDontSave};mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.SetUVs(1,data);mesh.colors=colors;mesh.triangles=triangles;
            if(path.record.layoutVersion>=2&&water){if(start==0)RoundWaterEnd(mesh,path.frames[0],-1,0);if(end==path.frames.Count-1)RoundWaterEnd(mesh,path.frames[end],1,(rows-1)*(across+1));}
            if(water)
            {
                // Uber's tangent-space normal lighting needs a real basis. Derive it from
                // the global path so neighboring tiles and round caps agree exactly.
                var finalVertices=mesh.vertices;var tangents=new Vector4[finalVertices.Length];
                for(int i=0;i<finalVertices.Length;i++){path.Nearest(new Vector2(finalVertices[i].x,finalVertices[i].z),out var frame,out _);var left=frame.Left;tangents[i]=new Vector4(left.x,0,left.y,1);}
                mesh.tangents=tangents;
            }
            mesh.RecalculateBounds();if(water&&catalog.waterMaterial&&catalog.waterMaterial.IsKeywordEnabled("_ENABLEWAVE"))
            {
                float height=Mathf.Abs(catalog.waterMaterial.GetFloat("_1st_Wave_Height"))+Mathf.Abs(catalog.waterMaterial.GetFloat("_2nd_Wave_Height"));
                float lateral=(Mathf.Abs(catalog.waterMaterial.GetFloat("_1st_Wave_Sharpness")*catalog.waterMaterial.GetFloat("_1st_Wave_Length"))+Mathf.Abs(catalog.waterMaterial.GetFloat("_2nd_Wave_Sharpness")*catalog.waterMaterial.GetFloat("_2nd_Wave_Length")))/(2*Mathf.PI);
                var bounds=mesh.bounds;bounds.Expand(new Vector3(lateral*2+.1f,height*2+.1f,lateral*2+.1f));mesh.bounds=bounds;
            }return mesh;
        }
        void RoundWaterEnd(Mesh mesh,MapRiverFrame f,int end,int baseRow)
        {
            // Match the existing 32-column border, then sample the cap densely enough
            // for Gerstner displacement. A single large triangle fan aliases the waves.
            const int across=32;float half=f.width*.5f,left=half+Mathf.Clamp(f.leftTransition*.3f,.6f,3),right=half+Mathf.Clamp(f.rightTransition*.3f,.6f,3),radius=Mathf.Max(left,right);
            int rows=Mathf.Max(2,Mathf.CeilToInt(radius/.5f)),previousRow=baseRow;
            var vertices=new List<Vector3>(mesh.vertices);var normals=new List<Vector3>(mesh.normals);var uv=new List<Vector2>(mesh.uv);var colors=new List<Color>(mesh.colors);var data=new List<Vector4>();mesh.GetUVs(1,data);var indices=new List<int>(mesh.triangles);
            void Triangle(int a,int b,int c){if(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).y<0){int swap=b;b=c;c=swap;}indices.Add(a);indices.Add(b);indices.Add(c);}
            void Vertex(float signed,float along)
            {
                var p=f.center+f.Left*signed+f.tangent*(along*end);float h=session.SampleHeight(p.x,p.y);
                vertices.Add(new Vector3(p.x,f.water+.018f,p.y));normals.Add(Vector3.up);uv.Add(new Vector2(signed/half,f.station+along*end));colors.Add(new Color(0,1,0,0));data.Add(new Vector4(Mathf.Max(0,f.water-h),f.speed,Mathf.Clamp01(1-(f.water-h)),f.ford));
            }
            for(int row=1;row<rows;row++)
            {
                float along=radius*row/rows,scale=Mathf.Sqrt(1-(along*along)/(radius*radius));int current=vertices.Count;
                for(int col=0;col<=across;col++)Vertex(Mathf.Lerp(-right,left,col/(float)across)*scale,along);
                for(int col=0;col<across;col++){Triangle(previousRow+col,previousRow+col+1,current+col);Triangle(previousRow+col+1,current+col+1,current+col);}
                previousRow=current;
            }
            int tip=vertices.Count;Vertex(0,radius);for(int col=0;col<across;col++)Triangle(previousRow+col,previousRow+col+1,tip);
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetUVs(1,data);mesh.SetColors(colors);mesh.SetTriangles(indices,0);
        }
        public void Dispose(){session.Changed-=Changed;foreach(var tile in tiles.Values)DestroyTile(tile);tiles.Clear();foreach(var root in rocks.Values)Destroy(root);rocks.Clear();}
        static void DestroyTile(Tile tile){Destroy(tile.root);Destroy(tile.water);Destroy(tile.bank);if(tile.flow)Destroy(tile.flow);}
        static void Destroy(UnityEngine.Object obj){if(Application.isPlaying)UnityEngine.Object.Destroy(obj);else UnityEngine.Object.DestroyImmediate(obj);}
    }
}
