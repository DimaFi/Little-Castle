using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable] public sealed class MapTreeDefinition
    {
        public string archetypeId;
        public int version=1;
        public bool authorOnly,playerPlaceable=true;
        public float minimumScale=.85f,maximumScale=1.2f,minimumSpacing=4,maxSlopeDegrees=35,groundOffset;
    }
    [Serializable] public sealed class MapTreePackEntry {public string archetypeId;public float weight=1;}
    [Serializable] public sealed class MapTreePack
    {
        public string id;
        public int version=1;
        public bool authorOnly=true;
        public List<MapTreePackEntry> entries=new List<MapTreePackEntry>();
    }
    /// <summary>Final authoritative instance; prefab/thumbnail references belong only to presentation.</summary>
    [Serializable] public sealed class MapTreeInstance
    {
        public long stableId;
        public string archetypeId;
        public int catalogVersion=1;
        public Vector2 position;
        public float yawDegrees,uniformScale=1,groundOffset,clearanceRadius=2;
        public bool playerEditable=true;
        public WorldSpawnData ToWorldSpawn(float height)=>new WorldSpawnData(stableId,archetypeId,SpawnCategory.Tree,
            new Vector3(position.x,height+groundOffset,position.y),yawDegrees,uniformScale);
    }
    public static class MapObjectIds
    {
        public static long FromKey(string key)
        {ulong h=14695981039346656037UL;foreach(char c in key){h^=c;h=unchecked(h*1099511628211UL);}return h==0?1:unchecked((long)h);}
        public static long New()=>FromKey(Guid.NewGuid().ToString("N"));
    }
}
