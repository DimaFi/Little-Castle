using System;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable] public sealed class MapRiverSectionProfile
    {
        public int version=1;
        public float bankRise=.12f,maximumCut=10,maximumFill=2;
        public void Validate()
        {
            if(version!=1||!Range(bankRise,.03f,.4f)||!Range(maximumCut,.15f,12)||!Range(maximumFill,0,4))
                throw new InvalidDataException("Неверные пределы мягкого профиля русла.");
        }
        static bool Range(float v,float min,float max)=>float.IsFinite(v)&&v>=min&&v<=max;
    }
    /// <summary>One cross section for the terrain, wet footprint, water mesh and depth queries.</summary>
    public static class MapRiverSection
    {
        public const int LayoutVersion=3;
        public const float TerrainStep=.25f;
        public static float Bed(MapRiverFrame f,float distance,float ground)
        {
            float d=Mathf.Abs(distance),half=f.width*.5f;
            float depth=Mathf.Lerp(f.depth,f.shallowDepth,f.ford);
            if(d<=half)return Mathf.Lerp(f.water-depth,f.water,Mathf.SmoothStep(0,1,Mathf.InverseLerp(half*.3f,half,d)));
            float transition=distance>=0?f.leftTransition:f.rightTransition;
            float u=(d-half)/transition;
            if(u>=1)return ground;
            float crest=f.water+f.sectionProfile.bankRise;
            return u<.3f?Mathf.Lerp(f.water,crest,Mathf.SmoothStep(0,1,u/.3f)):
                Mathf.Lerp(crest,ground,Mathf.SmoothStep(0,1,(u-.3f)/.7f));
        }
    }
    // Freeze the exact pre-v10 shape/order for every historical fingerprint.
    [Serializable] public sealed class MapRiverLegacyV9
    {
        public long id;
        public int seed,layoutVersion;
        public System.Collections.Generic.List<MapRiverNode> nodes;
        public System.Collections.Generic.List<MapBankPatch> patches;
        public System.Collections.Generic.List<MapRockInstance> rocks;
        public static System.Collections.Generic.List<MapRiverLegacyV9> Copy(System.Collections.Generic.List<MapRiverRecord> rivers)
        {
            var result=new System.Collections.Generic.List<MapRiverLegacyV9>();
            foreach(var r in rivers)result.Add(new MapRiverLegacyV9{id=r.id,seed=r.seed,layoutVersion=r.layoutVersion,nodes=r.nodes,patches=r.patches,rocks=r.rocks});
            return result;
        }
    }
}
