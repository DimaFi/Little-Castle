using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    public sealed partial class MapEditSession
    {
        struct HeightChange {public int x,z;public float before,after;}
        MapHeightField heightField;
        Dictionary<ChunkCoordinate,HeightChange> pendingHeights;
        float heightRadius,heightStep;
        Rect pendingHeightBounds;
        public long HeightRevision {get;private set;}
        public MapHeightFieldData HeightSettings=>Clone(heightField.Definition);
        public float HeightSpacing=>heightField.Definition.spacing;
        public float MinimumHeight=>heightField.Definition.minimumHeight;
        public float MaximumHeight=>heightField.Definition.maximumHeight;
        public int EditedHeightSamples=>heightField.EditCount;
        public float SampleHeight(float x,float z)=>ComposedHeight(x,z);
        public Vector3 SampleNormal(float x,float z)=>new Vector3(SampleHeight(x-HeightSpacing,z)-SampleHeight(x+HeightSpacing,z),2*HeightSpacing,SampleHeight(x,z-HeightSpacing)-SampleHeight(x,z+HeightSpacing)).normalized;
        public MapAuthoringDocument ExportMap()
        {
            CommitStroke();var result=ExportAuthored();result.surface.strokes.AddRange(ExportEdits().strokes);result.heightField=heightField.Bake();result.trees=ExportTrees();result.rivers=ExportRivers();result.rockSections=ExportRockSections();return result;
        }
        void RequireTerrainAccess(){if((Access&MapEditAccess.Terrain)==0)throw new UnauthorizedAccessException("Terrain editing is disabled for this role.");}
        public void BeginHeightStroke(float radius,float deltaPerStamp)
        {
            RequireTerrainAccess();if(!Finite(radius)||radius<.5f||radius>32||!Finite(deltaPerStamp)||Mathf.Abs(deltaPerStamp)>2||deltaPerStamp==0)
                throw new InvalidDataException("Invalid terrain brush radius/strength.");
            CommitStroke();pendingHeights=new Dictionary<ChunkCoordinate,HeightChange>();heightRadius=radius;heightStep=deltaPerStamp;pendingHeightBounds=default;
        }
        public void AppendHeightPoint(Vector2 center)
        {
            RequireTerrainAccess();ValidatePoint(center);if(pendingHeights==null)throw new InvalidOperationException("Begin a height stroke first.");
            Vector2 lo=heightField.Grid(center.x-heightRadius,center.y-heightRadius),hi=heightField.Grid(center.x+heightRadius,center.y+heightRadius);
            Rect dirty=default;var b=Bounds;
            for(int z=Mathf.CeilToInt(lo.y);z<=Mathf.FloorToInt(hi.y);z++)for(int x=Mathf.CeilToInt(lo.x);x<=Mathf.FloorToInt(hi.x);x++)
            {
                Vector2 p=heightField.Position(x,z);float distance=Vector2.Distance(p,center);
                if(distance>=heightRadius||p.x<b.x||p.y<b.y||p.x>b.x+b.z||p.y>b.y+b.w)continue;
                bool blocked=false;foreach(var area in heightField.Definition.protectedAreas)
                {var padded=area;padded.xMin-=HeightSpacing;padded.yMin-=HeightSpacing;padded.xMax+=HeightSpacing;padded.yMax+=HeightSpacing;if(padded.Contains(p)){blocked=true;break;}}
                if(blocked)continue;
                float before=heightField.Sample(x,z),after=Mathf.Clamp(before+heightStep*(1-Mathf.SmoothStep(0,1,distance/heightRadius)),MinimumHeight,MaximumHeight);
                if(before==after)continue;var key=new ChunkCoordinate(x,z);
                if(!pendingHeights.TryGetValue(key,out var change))change=new HeightChange{x=x,z=z,before=before};
                change.after=after;pendingHeights[key]=change;heightField.Set(x,z,after);
                dirty=Union(dirty,new Rect(p.x-HeightSpacing*2,p.y-HeightSpacing*2,HeightSpacing*4,HeightSpacing*4));
            }
            pendingHeightBounds=Union(pendingHeightBounds,dirty);if(dirty.width>0)HeightRevision++;InvalidateComposedHeight(dirty);Notify(dirty,true,MapChangeKind.Height);
        }
        void CommitHeightStroke()
        {
            if(pendingHeights==null)return;
            try{RequireTerrainAccess();}catch{CancelHeightStroke();throw;}
            var command=new Command{bounds=pendingHeightBounds,kind=MapChangeKind.Height};
            foreach(var change in pendingHeights.Values)if(change.before!=change.after)command.heights.Add(change);
            pendingHeights=null;pendingHeightBounds=default;
            if(command.heights.Count>0)Execute(command);
        }
        void CancelHeightStroke()
        {
            if(pendingHeights==null)return;foreach(var c in pendingHeights.Values)heightField.Set(c.x,c.z,c.before);
            var dirty=pendingHeightBounds;pendingHeights=null;pendingHeightBounds=default;if(dirty.width>0)HeightRevision++;InvalidateComposedHeight(dirty);Notify(dirty,false,MapChangeKind.Height);
        }
        Rect HeightEditsBounds(List<MapHeightSample> values)
        {
            Rect result=default;foreach(var s in values){var p=heightField.Position(s.x,s.z);result=Union(result,new Rect(p.x-HeightSpacing*2,p.y-HeightSpacing*2,HeightSpacing*4,HeightSpacing*4));}return result;
        }
        void ValidateHeightEdits(List<MapHeightSample> values)
        {
            if(values==null||values.Count>4000000)throw new InvalidDataException("Invalid height delta.");
            var seen=new HashSet<ChunkCoordinate>();var b=Bounds;
            foreach(var sample in values)
            {
                Vector2 p=heightField.Position(sample.x,sample.z);
                if(!Finite(sample.height)||sample.height<MinimumHeight||sample.height>MaximumHeight||p.x<b.x||p.y<b.y||p.x>b.x+b.z||p.y>b.y+b.w||
                    !seen.Add(new ChunkCoordinate(sample.x,sample.z)))throw new InvalidDataException("Invalid/duplicate/out-of-bounds height sample.");
            }
        }
        // Exactly the pre-height document layout, for migration of already-written v2 surface saves.
        [Serializable] sealed class LegacySurfaceBase
        {
            public int version;public string mapId;public int revision,decorationRulesVersion;public Vector4 bounds;public LittleCastle.Rendering.SurfacePaintDocument surface;
        }
        string LegacyFingerprint()=>Fingerprint(new LegacySurfaceBase{version=authored.version,mapId=MapId,revision=authored.revision,
            decorationRulesVersion=authored.decorationRulesVersion,bounds=Bounds,surface=authored.surface});
    }
}
