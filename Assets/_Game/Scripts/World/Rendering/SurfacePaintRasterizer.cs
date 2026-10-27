using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>One raster algorithm for authored previews, resident tiles and the legacy Meadow view.</summary>
    public static class SurfacePaintRasterizer
    {
        public static void ApplyRiverExclusions(byte[] data,Vector4 bounds,int width,int height,MapEditSession session,RectInt dirty)
        {
            if(session.RiverCount==0&&session.ExportRockSections().Count==0)return;
            for(int y=dirty.yMin;y<dirty.yMax;y++)for(int x=dirty.xMin;x<dirty.xMax;x++)
            {
                float wx=bounds.x+(x+.5f)*bounds.z/width,wz=bounds.y+(y+.5f)*bounds.w/height;
                if(!session.VegetationAllowed(wx,wz)){int i=(y*width+x)*2;data[i]=0;data[i+1]=0;}
            }
        }
        public static RectInt PixelRect(Vector4 bounds,int width,int height,Rect area)
        {
            int x0=Mathf.Clamp(Mathf.FloorToInt((area.xMin-bounds.x)*width/bounds.z),0,width);
            int y0=Mathf.Clamp(Mathf.FloorToInt((area.yMin-bounds.y)*height/bounds.w),0,height);
            int x1=Mathf.Clamp(Mathf.CeilToInt((area.xMax-bounds.x)*width/bounds.z)+1,0,width);
            int y1=Mathf.Clamp(Mathf.CeilToInt((area.yMax-bounds.y)*height/bounds.w)+1,0,height);
            return new RectInt(x0,y0,Mathf.Max(0,x1-x0),Mathf.Max(0,y1-y0));
        }
        public static void Rebuild(byte[] data,Vector4 bounds,int width,int height,List<SurfacePaintStroke> strokes,RectInt dirty)
        {
            for(int y=dirty.yMin;y<dirty.yMax;y++)for(int x=dirty.xMin;x<dirty.xMax;x++)
            {int i=(y*width+x)*2;data[i]=0;data[i+1]=255;}
            foreach(var stroke in strokes)for(int p=0;p<stroke.points.Count;p++)
                Stamp(data,bounds,width,height,stroke.points[Mathf.Max(0,p-1)],stroke.points[p],stroke,dirty);
        }
        static void Stamp(byte[] data,Vector4 bounds,int width,int height,Vector2 a,Vector2 b,SurfacePaintStroke s,RectInt dirty)
        {
            float r=s.radius;
            var area=Rect.MinMaxRect(Mathf.Min(a.x,b.x)-r,Mathf.Min(a.y,b.y)-r,Mathf.Max(a.x,b.x)+r,Mathf.Max(a.y,b.y)+r);
            var pixels=PixelRect(bounds,width,height,area);
            int x0=Mathf.Max(pixels.xMin,dirty.xMin),x1=Mathf.Min(pixels.xMax,dirty.xMax);
            int y0=Mathf.Max(pixels.yMin,dirty.yMin),y1=Mathf.Min(pixels.yMax,dirty.yMax);
            for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)
            {
                var point=new Vector2(bounds.x+(x+.5f)*bounds.z/width,bounds.y+(y+.5f)*bounds.w/height);
                float distance=ChunkTerrainVertexMasks.DistanceToSegment(point,a,b,out _);
                float coverage=MapBrushFalloff.SurfaceWeight(s,distance);int i=(y*width+x)*2;
                if(!s.erase)data[i]=(byte)Mathf.RoundToInt((s.road?Mathf.Max(data[i]/255f,coverage):Mathf.Min(data[i]/255f,1-coverage))*255);
                data[i+1]=(byte)Mathf.RoundToInt((s.road||s.erase?Mathf.Min(data[i+1]/255f,1-coverage):Mathf.Max(data[i+1]/255f,coverage))*255);
            }
        }
    }

    /// <summary>Lazy CPU tiles; edits invalidate resident tiles only. No complete-map texture allocation.</summary>
    public sealed class MapSurfaceTileCache : IDisposable
    {
        public sealed class Tile
        {
            public readonly byte[] pixels;
            public readonly int size;
            public readonly Vector4 bounds;
            public int revision {get;internal set;}
            internal Tile(int interiorSize,Rect region)
            {
                size=interiorSize+2;float texel=MapEditSession.RegionSize/interiorSize;
                bounds=new Vector4(region.x-texel,region.y-texel,region.width+texel*2,region.height+texel*2);
                pixels=new byte[size*size*2];
            }
        }
        readonly MapEditSession session;
        readonly Dictionary<ChunkCoordinate,Tile> tiles=new Dictionary<ChunkCoordinate,Tile>();
        readonly int resolution;
        public int ResidentCount=>tiles.Count;
        public int LastUpdatedTiles {get;private set;}
        public event Action<ChunkCoordinate,Tile> TileUpdated;
        public MapSurfaceTileCache(MapEditSession session,int resolution=128)
        {if(resolution<2||resolution>2048)throw new ArgumentOutOfRangeException(nameof(resolution));this.session=session;this.resolution=resolution;session.Changed+=Changed;}
        public Tile Get(ChunkCoordinate key)
        {
            if(!tiles.TryGetValue(key,out var tile)){tile=new Tile(resolution,MapEditSession.RegionBounds(key));tiles.Add(key,tile);Rebuild(key,tile);}
            return tile;
        }
        public void Release(ChunkCoordinate key)=>tiles.Remove(key);
        void Changed(MapRegionChange change)
        {
            LastUpdatedTiles=0;if((change.kind&MapChangeKind.Surface)==0)return;
            foreach(var pair in tiles)
            {
                var b=pair.Value.bounds;
                if(new Rect(b.x,b.y,b.z,b.w).Overlaps(change.bounds)){Rebuild(pair.Key,pair.Value);LastUpdatedTiles++;}
            }
        }
        void Rebuild(ChunkCoordinate key,Tile tile)
        {
            var b=tile.bounds;var r=new Rect(b.x,b.y,b.z,b.w);
            SurfacePaintRasterizer.Rebuild(tile.pixels,b,tile.size,tile.size,session.QuerySurface(r),new RectInt(0,0,tile.size,tile.size));
            SurfacePaintRasterizer.ApplyRiverExclusions(tile.pixels,b,tile.size,tile.size,session,new RectInt(0,0,tile.size,tile.size));
            tile.revision++;TileUpdated?.Invoke(key,tile);
        }
        public void Dispose(){session.Changed-=Changed;tiles.Clear();}
    }

    /// <summary>
    /// Resident-view GPU adapter for existing _SurfacePaint/_SurfacePaintBounds shaders.
    /// Request one binding per resident terrain/grass draw group; release when that view unloads.
    /// No world-sized texture, light globals or material mutation.
    /// </summary>
    public sealed class MapSurfaceGpuCache : IDisposable
    {
        public sealed class Binding
        {
            public Texture2D texture {get;internal set;}
            public MaterialPropertyBlock properties {get;internal set;}
            public int revision {get;internal set;}
        }
        readonly MapSurfaceTileCache cpu;
        readonly Dictionary<ChunkCoordinate,Binding> bindings=new Dictionary<ChunkCoordinate,Binding>();
        public int ResidentCount=>bindings.Count;
        public MapSurfaceGpuCache(MapEditSession session,int resolution=128)
        {cpu=new MapSurfaceTileCache(session,resolution);cpu.TileUpdated+=UploadResident;}
        public Binding Get(ChunkCoordinate key)
        {
            if(bindings.TryGetValue(key,out var binding))return binding;
            var tile=cpu.Get(key);
            binding=new Binding{texture=new Texture2D(tile.size,tile.size,TextureFormat.RG16,false,true){
                name="Map surface "+key,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave},properties=new MaterialPropertyBlock()};
            var b=tile.bounds;binding.properties.SetTexture("_SurfacePaint",binding.texture);
            binding.properties.SetVector("_SurfacePaintBounds",new Vector4(b.x,b.y,1/b.z,1/b.w));binding.properties.SetFloat("_UseSurfacePaint",1);
            bindings.Add(key,binding);UploadResident(key,tile);return binding;
        }
        void UploadResident(ChunkCoordinate key,MapSurfaceTileCache.Tile tile)
        {
            if(!bindings.TryGetValue(key,out var binding))return;
            binding.texture.SetPixelData(tile.pixels,0);binding.texture.Apply(false,false);binding.revision=tile.revision;
        }
        public void Release(ChunkCoordinate key)
        {
            if(bindings.TryGetValue(key,out var binding)){DestroyTexture(binding.texture);bindings.Remove(key);}cpu.Release(key);
        }
        static void DestroyTexture(Texture2D texture)
        {if(Application.isPlaying)UnityEngine.Object.Destroy(texture);else UnityEngine.Object.DestroyImmediate(texture);}
        public void Dispose()
        {cpu.TileUpdated-=UploadResident;cpu.Dispose();foreach(var binding in bindings.Values)DestroyTexture(binding.texture);bindings.Clear();}
    }
}
