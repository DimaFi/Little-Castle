using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;
namespace LittleCastle.Rendering
{
    public sealed class MapRockCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry {public string title,sourcePath;public MapRockDefinition definition;public GameObject prefab;public Bounds bounds;}
        public Entry[] entries=Array.Empty<Entry>();
        public Material waterMaterial,bankMaterial;
        [Tooltip("Optional visual settings; an owned default is used when unset.")] public RiverVisualQualityProfile riverVisualQuality;
        public static MapRockCatalog Default=>Resources.Load<MapRockCatalog>("MapAuthoring/MeadowRockCatalog");
        public IEnumerable<MapRockDefinition> Definitions {get{foreach(var e in entries)yield return e.definition;}}
        public Entry Resolve(string id,int version){foreach(var e in entries)if(e.definition.id==id&&e.definition.version==version)return e;return null;}
    }
}
