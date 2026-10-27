using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable] public sealed class MapHeightTile
    {
        public int x,z;
        public float[] samples=new float[256];
    }
    [Serializable] public sealed class MapHeightFieldData
    {
        public int version=1;
        public Vector2 origin;
        public float spacing=1;
        // Study defaults, configurable per authored map; not production world limits.
        public float minimumHeight=-4,maximumHeight=12,baseHeight;
        public List<MapHeightTile> tiles=new List<MapHeightTile>();
        public List<Rect> protectedAreas=new List<Rect>();
    }
    [Serializable] public struct MapHeightSample
    {
        public int x,z;
        public float height;
        public MapHeightSample(int x,int z,float height){this.x=x;this.z=z;this.height=height;}
    }
    /// <summary>Canonical world lattice, sparse base tiles, sparse final overrides. No Unity objects.</summary>
    public sealed class MapHeightField
    {
        public const int TileSize=16;
        public MapHeightFieldData Definition {get;}
        readonly Dictionary<ChunkCoordinate,MapHeightTile> tiles=new Dictionary<ChunkCoordinate,MapHeightTile>();
        readonly Dictionary<ChunkCoordinate,float> edits=new Dictionary<ChunkCoordinate,float>();
        public int EditCount=>edits.Count;
        public MapHeightField(MapHeightFieldData data)
        {
            Definition=data??new MapHeightFieldData();Validate(Definition);
            foreach(var tile in Definition.tiles)tiles.Add(new ChunkCoordinate(tile.x,tile.z),tile);
        }
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        static void Validate(MapHeightFieldData data)
        {
            if(data.version!=1||!Finite(data.spacing)||data.spacing<.25f||data.spacing>8||!Finite(data.origin.x)||!Finite(data.origin.y)||
                !Finite(data.minimumHeight)||!Finite(data.maximumHeight)||data.minimumHeight>=data.maximumHeight||!Finite(data.baseHeight)||
                data.baseHeight<data.minimumHeight||data.baseHeight>data.maximumHeight||data.tiles==null||data.protectedAreas==null)
                throw new InvalidDataException("Invalid authored height field/limits.");
            var seen=new HashSet<ChunkCoordinate>();
            foreach(var tile in data.tiles)
            {
                if(tile==null||tile.samples==null||tile.samples.Length!=256||!seen.Add(new ChunkCoordinate(tile.x,tile.z)))throw new InvalidDataException("Invalid/duplicate height tile.");
                foreach(float h in tile.samples)if(!Finite(h)||h<data.minimumHeight||h>data.maximumHeight)throw new InvalidDataException("Base height exceeds map limits.");
            }
            foreach(var r in data.protectedAreas)if(!Finite(r.x)||!Finite(r.y)||!Finite(r.width)||!Finite(r.height)||r.width<0||r.height<0)
                throw new InvalidDataException("Invalid protected terrain area.");
        }
        public Vector2 Position(int x,int z)=>Definition.origin+new Vector2(x,z)*Definition.spacing;
        public Vector2 Grid(float x,float z)=>(new Vector2(x,z)-Definition.origin)/Definition.spacing;
        public float BaseSample(int x,int z)
        {
            int tx=Mathf.FloorToInt(x/16f),tz=Mathf.FloorToInt(z/16f);
            return tiles.TryGetValue(new ChunkCoordinate(tx,tz),out var tile)?tile.samples[(z-tz*16)*16+x-tx*16]:Definition.baseHeight;
        }
        public float Sample(int x,int z)=>edits.TryGetValue(new ChunkCoordinate(x,z),out float h)?h:BaseSample(x,z);
        public void Set(int x,int z,float value)
        {
            var key=new ChunkCoordinate(x,z);
            if(value==BaseSample(x,z))edits.Remove(key);else edits[key]=value;
        }
        public float Height(float x,float z)
        {
            Vector2 grid=Grid(x,z);int ix=Mathf.FloorToInt(grid.x),iz=Mathf.FloorToInt(grid.y);float u=grid.x-ix,v=grid.y-iz;
            // Same diagonal as the existing terrain mesh. Roots sit on triangles, not a different bilinear surface.
            return u+v<=1?Sample(ix,iz)+(Sample(ix+1,iz)-Sample(ix,iz))*u+(Sample(ix,iz+1)-Sample(ix,iz))*v:
                Sample(ix+1,iz+1)+(Sample(ix,iz+1)-Sample(ix+1,iz+1))*(1-u)+(Sample(ix+1,iz)-Sample(ix+1,iz+1))*(1-v);
        }
        public Vector3 Normal(float x,float z)
        {float d=Definition.spacing;return new Vector3(Height(x-d,z)-Height(x+d,z),2*d,Height(x,z-d)-Height(x,z+d)).normalized;}
        public List<MapHeightSample> ExportEdits()
        {
            var result=new List<MapHeightSample>();foreach(var p in edits)result.Add(new MapHeightSample(p.Key.x,p.Key.z,p.Value));
            result.Sort((a,b)=>{int z=a.z.CompareTo(b.z);return z!=0?z:a.x.CompareTo(b.x);});return result;
        }
        public void ReplaceEdits(List<MapHeightSample> values){edits.Clear();foreach(var s in values)Set(s.x,s.z,s.height);}
        public MapHeightFieldData Bake()
        {
            var baked=JsonUtility.FromJson<MapHeightFieldData>(JsonUtility.ToJson(Definition));
            foreach(var sample in ExportEdits())SetBase(baked,sample.x,sample.z,sample.height);
            return baked;
        }
        public static void SetBase(MapHeightFieldData data,int x,int z,float value)
        {
            int tx=Mathf.FloorToInt(x/16f),tz=Mathf.FloorToInt(z/16f);MapHeightTile found=null;
            foreach(var tile in data.tiles)if(tile.x==tx&&tile.z==tz){found=tile;break;}
            if(found==null){found=new MapHeightTile{x=tx,z=tz};for(int i=0;i<256;i++)found.samples[i]=data.baseHeight;data.tiles.Add(found);}
            found.samples[(z-tz*16)*16+x-tx*16]=value;
        }
    }
}
