using System;
using UnityEngine;

namespace LittleCastle.Rendering
{
    [CreateAssetMenu(menuName="Little Castle/Map/Fence Catalog")]
    public sealed class MapFenceCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string id,title;
            public Material material;
            public Mesh postLOD0,railLOD0,postLOD1,railLOD1;
            public bool Complete=>material&&postLOD0&&railLOD0&&postLOD1&&railLOD1;
        }
        public Entry[] entries=Array.Empty<Entry>();
        public int meshAdapterVersion;
        public static MapFenceCatalog Default=>Resources.Load<MapFenceCatalog>("MapAuthoring/FenceCatalog");
        public Entry Resolve(string id){foreach(var e in entries)if(e.id==id)return e;return null;}
    }
}
