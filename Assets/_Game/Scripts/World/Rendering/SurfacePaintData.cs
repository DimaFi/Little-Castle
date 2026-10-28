using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Rendering
{
    public enum MeadowScatterProfile
    {
        Mixed = 0,
        Flowers = 1,
        ForestFloor = 2,
        MeadowGrass = 3,
        Stones = 4,
        Understory = 5
    }
    public static class MeadowScatterProfiles
    {
        public static readonly string[] Names={"РЎРјРµС€Р°РЅРЅС‹Р№","Р¦РІРµС‚С‹","Р›РµСЃРЅР°СЏ РјРµР»РѕС‡СЊ","РўСЂР°РІР°","РљР°РјРЅРё","РџРѕРґР»РµСЃРѕРє"};
        public const int Count=6;
    }

    /// <summary>Plain visual surface edits; cache textures are never the save format.</summary>
    [Serializable] public sealed class SurfacePaintDocument
    {
        public int version=1;
        public List<SurfacePaintStroke> strokes=new List<SurfacePaintStroke>();
    }
    [Serializable] public sealed class SurfacePaintStroke
    {
        public string id;
        public bool road=true;
        public bool scatter;
        // Zero preserves old JSON strokes which did not store a scatter profile.
        public MeadowScatterProfile scatterProfile=MeadowScatterProfile.Mixed;
        public bool erase;
        public float radius=1.5f;
        public float strength=1;
        public float brushSoftness; // 0 = legacy; otherwise softness + .001
        public float density=1;
        public List<Vector2> points=new List<Vector2>();
        public LittleCastle.World.WorldRoadData ToRoadData()
        {
            // Stable FNV hash, independent of process/.NET string hash randomization.
            ulong hash=14695981039346656037UL;foreach(char c in id??""){hash^=c;hash=unchecked(hash*1099511628211UL);}
            return new LittleCastle.World.WorldRoadData{stableId=unchecked((long)hash),roadKind=LittleCastle.World.RoadKind.DirtRoad,width=radius*1.24f,centerline=new List<Vector2>(points)};
        }
    }
    public sealed class SurfacePaintData : ScriptableObject
    {
        public string mapId="meadow-20261025";
        public int revision=1;
        public LittleCastle.World.MapHeightFieldData heightField;
        // Unity may instantiate a newly-added serializable class on old assets.
        // An explicit marker distinguishes that empty default from an authored base.
        public bool hasHeightBase;
        public bool hasTreeBase;
        public bool hasEnvironmentBase;
        public LittleCastle.World.Environment.MapEnvironmentData environment;
        public List<LittleCastle.World.MapRiverRecord> rivers=new List<LittleCastle.World.MapRiverRecord>();
        public List<LittleCastle.World.MapRockSection> rockSections=new List<LittleCastle.World.MapRockSection>();
        public List<LittleCastle.World.MapTreeInstance> treeInstances=new List<LittleCastle.World.MapTreeInstance>();
        public List<Rect> placementExclusions=new List<Rect>();
        public Vector4 bounds=new Vector4(-48,-40,96,104);
        public SurfacePaintDocument authored=new SurfacePaintDocument();
    }
}
