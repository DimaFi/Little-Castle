using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Rendering
{
    /// <summary>Derived near-ground geometry from the actual path height maps. Never changes the authored height field.</summary>
    public sealed class MapPathReliefPresentation : IDisposable
    {
        const float TileSize=12,Step=.125f;
        readonly MapEditSession session;
        readonly MapPathCatalog catalog;
        readonly Material material;
        readonly MaterialPropertyBlock properties;
        readonly byte[][] heights;
        readonly int heightSize;
        readonly Dictionary<Vector2Int,GameObject> tiles=new Dictionary<Vector2Int,GameObject>();
        public MapPathReliefPresentation(MapEditSession session,MapPathCatalog catalog,Material material,MaterialPropertyBlock properties)
        {
            this.session=session;this.catalog=catalog;this.material=material;this.properties=properties;
            if(!catalog.surface||!catalog.surface.isReadable)return;
            heightSize=catalog.surface.width;heights=new byte[catalog.Count][];
            for(int layer=1;layer<catalog.Count;layer++)
            {
                var colors=catalog.surface.GetPixels32(layer);heights[layer]=new byte[colors.Length];
                for(int i=0;i<colors.Length;i++)heights[layer][i]=colors[i].g;
            }
            var b=session.Bounds;Rebuild(new Rect(b.x,b.y,b.z,b.w));session.Changed+=Changed;
        }
        void Changed(MapRegionChange change)
        {if((change.kind&(MapChangeKind.Surface|MapChangeKind.Height))!=0)Rebuild(change.bounds);}
        void Rebuild(Rect dirty)
        {
            for(int z=Mathf.FloorToInt(dirty.yMin/TileSize);z<=Mathf.FloorToInt(dirty.yMax/TileSize);z++)
            for(int x=Mathf.FloorToInt(dirty.xMin/TileSize);x<=Mathf.FloorToInt(dirty.xMax/TileSize);x++)Build(new Vector2Int(x,z));
        }
        void Build(Vector2Int key)
        {
            if(tiles.TryGetValue(key,out var previous)){Destroy(previous.GetComponent<MeshFilter>().sharedMesh);Destroy(previous);tiles.Remove(key);}
            var map=session.Bounds;
            float left=Mathf.Max(map.x,key.x*TileSize),bottom=Mathf.Max(map.y,key.y*TileSize);
            float right=Mathf.Min(map.x+map.z,(key.x+1)*TileSize),top=Mathf.Min(map.y+map.w,(key.y+1)*TileSize);
            if(right<=left||top<=bottom)return;
            var b=new Vector4(left,bottom,right-left,top-bottom);var strokes=session.QuerySurface(new Rect(left,bottom,b.z,b.w));
            if(!strokes.Exists(s=>s.road&&!s.erase&&catalog.IndexOf(s.pathMaterialId)>0))return;
            int cols=Mathf.CeilToInt(b.z/Step),rows=Mathf.CeilToInt(b.w/Step),width=cols+1,height=rows+1;
            // Raster texel centres coincide with geometry vertices, including shared tile borders.
            float dx=b.z/cols,dz=b.w/rows;var rasterBounds=new Vector4(left-dx*.5f,bottom-dz*.5f,dx*width,dz*height);
            var weights=new byte[MapPathRasterizer.SliceCount(catalog.Count)][];
            for(int i=0;i<weights.Length;i++)weights[i]=new byte[width*height*4];
            MapPathRasterizer.Rebuild(weights,rasterBounds,width,height,strokes,catalog,new RectInt(0,0,width,height));
            var vertices=new Vector3[width*height];var normals=new Vector3[vertices.Length];var coverage=new float[vertices.Length];var colors=new Color[vertices.Length];
            for(int z=0;z<height;z++)for(int x=0;x<width;x++)
            {
                int p=z*width+x;float wx=left+x*dx,wz=bottom+z*dz,raised=0,total=0;
                for(int layer=1;layer<catalog.Count;layer++)
                {
                    float w=weights[layer/4][p*4+layer%4]/255f;if(w<=0)continue;
                    var entry=catalog.entries[layer];float h=Height(layer,wx*entry.worldTiling,wz*entry.worldTiling);
                    raised+=(.018f+Mathf.SmoothStep(0,1,h)*.14f)*w;total+=w;
                }
                coverage[p]=Mathf.Clamp01(total);
                vertices[p]=new Vector3(wx,session.SampleHeight(wx,wz)+.003f+raised,wz);
                normals[p]=session.SampleNormal(wx,wz);colors[p]=Color.black;
            }
            var indices=new List<int>();
            for(int z=0;z<rows;z++)for(int x=0;x<cols;x++)
            {
                int p=z*width+x;if(Mathf.Max(coverage[p],coverage[p+1],coverage[p+width],coverage[p+width+1])<.025f)continue;
                indices.AddRange(new[]{p,p+width,p+1,p+1,p+width,p+width+1});
            }
            if(indices.Count==0)return;
            var mesh=new Mesh{name="Derived path relief "+key,hideFlags=HideFlags.HideAndDontSave};
            mesh.vertices=vertices;mesh.colors=colors;mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var root=new GameObject(mesh.name){hideFlags=HideFlags.HideAndDontSave};root.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.SetPropertyBlock(properties);
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.Off;tiles.Add(key,root);
        }
        float Height(int layer,float u,float v)
        {
            u=(u-Mathf.Floor(u))*heightSize-.5f;v=(v-Mathf.Floor(v))*heightSize-.5f;
            int x=Mathf.FloorToInt(u),y=Mathf.FloorToInt(v);float fx=u-x,fy=v-y;
            int Wrap(int n)=>(n%heightSize+heightSize)%heightSize;
            float Read(int xx,int yy)=>heights[layer][Wrap(yy)*heightSize+Wrap(xx)]/255f;
            return Mathf.Lerp(Mathf.Lerp(Read(x,y),Read(x+1,y),fx),Mathf.Lerp(Read(x,y+1),Read(x+1,y+1),fx),fy);
        }
        static void Destroy(UnityEngine.Object value){if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        public void Draw(CommandBuffer commands,int pass)
        {foreach(var tile in tiles.Values)commands.DrawMesh(tile.GetComponent<MeshFilter>().sharedMesh,Matrix4x4.identity,material,0,pass,properties);}
        public void Dispose(){session.Changed-=Changed;foreach(var tile in tiles.Values){Destroy(tile.GetComponent<MeshFilter>().sharedMesh);Destroy(tile);}tiles.Clear();}
    }
}
