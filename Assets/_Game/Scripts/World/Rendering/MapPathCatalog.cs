using System;
using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>Local presentation library. Saves contain stable IDs, never texture-array slices.</summary>
    [CreateAssetMenu(menuName="Little Castle/Map/Path catalog")]
    public sealed class MapPathCatalog : ScriptableObject
    {
        public const int MaximumMaterials=32;
        public const string LegacyId="path.dirt";
        [Serializable] public sealed class Entry
        {
            public string id,title;
            public Texture2D thumbnail;
            public Texture2D baseColor,normalMap,ambientOcclusion,height,smoothness;
            [Min(.01f)] public float worldTiling=.5f;
            [Range(0,1)] public float normalStrength=.6f,aoStrength=.45f,heightBlend=.6f;
            [Range(0,1)] public float snowRetention=.65f;
            public Color tint=Color.white;
        }
        public Entry[] entries=Array.Empty<Entry>();
        public Texture2DArray albedo,normal,surface;
        public static MapPathCatalog Default=>Resources.Load<MapPathCatalog>("MapAuthoring/PathCatalog");
        public int Count=>Mathf.Min(MaximumMaterials,entries.Length);
        // A missing future/removed asset remains an ordinary path instead of destroying the edit.
        public int IndexOf(string id)
        {
            if(string.IsNullOrEmpty(id)||id==LegacyId)return 0;
            for(int i=1;i<Count;i++)if(entries[i].id==id)return i;
            return 0;
        }
        public void Bind(MaterialPropertyBlock block)
        {
            bool valid=Count>1&&albedo&&normal&&surface;
            block.SetFloat("_LC_PathCount",valid?Count:0);
            if(!valid)return;
            block.SetTexture("_LC_PathAlbedo",albedo);block.SetTexture("_LC_PathNormal",normal);block.SetTexture("_LC_PathSurface",surface);
            var settings=new Vector4[MaximumMaterials];var tints=new Vector4[MaximumMaterials];
            for(int i=0;i<Count;i++)
            {
                var e=entries[i];settings[i]=new Vector4(e.worldTiling,e.normalStrength,e.heightBlend,e.snowRetention);
                tints[i]=new Vector4(e.tint.r,e.tint.g,e.tint.b,e.aoStrength);
            }
            block.SetVectorArray("_LC_PathSettings",settings);block.SetVectorArray("_LC_PathTints",tints);
        }
    }
}
