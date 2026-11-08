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
        sealed class Tile
        {
            public GameObject root;
            public Vector4 bounds;
            public int columns,rows;
            public float[] height;
            public System.Collections.BitArray faces;
        }
        readonly Dictionary<Vector2Int,Tile> tiles=new Dictionary<Vector2Int,Tile>();
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
            if(tiles.TryGetValue(key,out var previous)){Destroy(previous.root.GetComponent<MeshFilter>().sharedMesh);Destroy(previous.root);tiles.Remove(key);}
            var map=session.Bounds;
            float left=Mathf.Max(map.x,key.x*TileSize),bottom=Mathf.Max(map.y,key.y*TileSize);
            float right=Mathf.Min(map.x+map.z,(key.x+1)*TileSize),top=Mathf.Min(map.y+map.w,(key.y+1)*TileSize);
            if(right<=left||top<=bottom)return;
            var b=new Vector4(left,bottom,right-left,top-bottom);var strokes=session.QuerySurface(new Rect(left,bottom,b.z,b.w));
            if(!strokes.Exists(s=>s.road&&!s.erase&&s.PathReliefHeight>0&&catalog.IndexOf(s.pathMaterialId)>0))return;
            int cols=Mathf.CeilToInt(b.z/Step),rows=Mathf.CeilToInt(b.w/Step),width=cols+1,height=rows+1;
            // Raster texel centres coincide with geometry vertices, including shared tile borders.
            float dx=b.z/cols,dz=b.w/rows;var rasterBounds=new Vector4(left-dx*.5f,bottom-dz*.5f,dx*width,dz*height);
            var weights=new byte[MapPathRasterizer.SliceCount(catalog.Count)][];
            for(int i=0;i<weights.Length;i++)weights[i]=new byte[width*height*4];
            MapPathRasterizer.Rebuild(weights,rasterBounds,width,height,strokes,catalog,new RectInt(0,0,width,height),reliefHeights:true);
            var vertices=new Vector3[width*height];var coverage=new float[vertices.Length];var colors=new Color[vertices.Length];
            var surfaceHeight=new float[vertices.Length];
            for(int z=0;z<height;z++)for(int x=0;x<width;x++)
            {
                int p=z*width+x;float wx=left+x*dx,wz=bottom+z*dz,raised=0,total=0;
                for(int layer=1;layer<catalog.Count;layer++)
                {
                    float w=weights[layer/4][p*4+layer%4]/255f;if(w<=0)continue;
                    var entry=catalog.entries[layer];float h=Height(layer,wx*entry.worldTiling,wz*entry.worldTiling);
                    raised+=(.115f+.885f*Mathf.SmoothStep(0,1,h))*w*SurfacePaintStroke.MaximumPathReliefHeight;total+=w;
                }
                coverage[p]=Mathf.Clamp01(total);
                surfaceHeight[p]=session.SampleHeight(wx,wz)+.003f+raised;
                vertices[p]=new Vector3(wx,surfaceHeight[p],wz);
                colors[p]=Color.black;
            }
            var indices=new List<int>();
            var faces=new System.Collections.BitArray(cols*rows);
            for(int z=0;z<rows;z++)for(int x=0;x<cols;x++)
            {
                int p=z*width+x;if(Mathf.Max(coverage[p],coverage[p+1],coverage[p+width],coverage[p+width+1])<.009f)continue;
                faces[z*cols+x]=true;
                indices.AddRange(new[]{p,p+width,p+1,p+1,p+width,p+width+1});
            }
            if(indices.Count==0)return;
            var mesh=new Mesh{name="Derived path relief "+key,hideFlags=HideFlags.HideAndDontSave};
            mesh.vertices=vertices;mesh.colors=colors;mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var root=new GameObject(mesh.name){hideFlags=HideFlags.HideAndDontSave};root.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.SetPropertyBlock(properties);
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.Off;
            tiles.Add(key,new Tile{root=root,bounds=b,columns=cols,rows=rows,height=surfaceHeight,faces=faces});
        }
        public bool TrySampleHeight(float x,float z,out float height)
        {
            height=0;
            var key=new Vector2Int(Mathf.FloorToInt(x/TileSize),Mathf.FloorToInt(z/TileSize));
            if(!tiles.TryGetValue(key,out var tile))return false;
            float u=(x-tile.bounds.x)/tile.bounds.z*tile.columns;
            float v=(z-tile.bounds.y)/tile.bounds.w*tile.rows;
            if(u<0||v<0||u>tile.columns||v>tile.rows)return false;
            int col=Mathf.Min((int)u,tile.columns-1),row=Mathf.Min((int)v,tile.rows-1);
            if(!tile.faces[row*tile.columns+col])return false;
            int width=tile.columns+1,index=row*width+col;
            float a=u-col,b=v-row;
            var y=tile.height;
            // Match the diagonal used by Build, including the path's actual tessellated height.
            height=a+b<=1
                ? y[index]*(1-a-b)+y[index+1]*a+y[index+width]*b
                : y[index+width+1]*(a+b-1)+y[index+1]*(1-b)+y[index+width]*(1-a);
            return true;
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
        {foreach(var tile in tiles.Values)commands.DrawMesh(tile.root.GetComponent<MeshFilter>().sharedMesh,Matrix4x4.identity,material,0,pass,properties);}
        public void Dispose(){session.Changed-=Changed;foreach(var tile in tiles.Values){Destroy(tile.root.GetComponent<MeshFilter>().sharedMesh);Destroy(tile.root);}tiles.Clear();}
    }
}
