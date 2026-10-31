using System;
using System.Collections.Generic;
using System.IO;
using LittleCastle.Rendering;
using UnityEngine;

namespace LittleCastle.World
{
    public sealed partial class MapEditSession
    {
        FireflyMapData fireflies;
        public long FireflyRevision { get; private set; }
        public FireflyMapData ExportFireflies() => Clone(fireflies);

        void InitializeFireflies() => fireflies = ValidateFireflies(authored.fireflies);

        FireflyMapData ValidateFireflies(FireflyMapData source)
        {
            FireflyMapData d=source==null?new FireflyMapData():Clone(source);
            if (d.version!=1 || d.areas==null || d.areas.Count>256 ||
                !Finite(d.densityPer100m2) || d.densityPer100m2<0 || d.densityPer100m2>80 ||
                d.globalDesiredCap<0 || d.globalDesiredCap>50000 ||
                d.perAreaDesiredCap<0 || d.perAreaDesiredCap>20000 ||
                d.cellDesiredCap<0 || d.cellDesiredCap>2000 ||
                !Finite(d.minimumHeight) || !Finite(d.maximumHeight) ||
                d.minimumHeight<0 || d.maximumHeight<d.minimumHeight || d.maximumHeight>100 ||
                !Finite(d.maximumSlopeDegrees) || d.maximumSlopeDegrees<0 || d.maximumSlopeDegrees>90 ||
                !Finite(d.worldSize) || d.worldSize<=0 || d.worldSize>1 ||
                !Finite(d.glow) || d.glow<0 || d.glow>4 ||
                d.desiredNearCount<0 || d.desiredNearCount>48 ||
                !Finite(d.nearRadius) || d.nearRadius<1 || d.nearRadius>50)
                throw new InvalidDataException("Invalid firefly settings.");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var a in d.areas)
            {
                if(a==null||string.IsNullOrEmpty(a.id)||a.id.Length>128||
                   !ids.Add(a.id)||string.IsNullOrWhiteSpace(a.name)||a.name.Length>128||
                   !Finite(a.center.x)||!Finite(a.center.y)||
                   !Finite(a.radius.x)||!Finite(a.radius.y)||a.radius.x<.5f||a.radius.y<.5f||
                   a.radius.x>2048||a.radius.y>2048||
                   !Finite(a.angleDegrees)||!Finite(a.falloff)||a.falloff<0||a.falloff>1||
                   !Finite(a.densityValue)||a.densityValue<0||a.densityValue>100||
                   (int)a.shape<0||(int)a.shape>1||(int)a.densityMode<0||(int)a.densityMode>3||
                   a.polygon==null||a.polygon.Count>64)
                    throw new InvalidDataException("Invalid firefly region.");
                ValidatePoint(a.center);
                if(a.shape==FireflyAreaShape.Polygon)
                {
                    if(a.polygon.Count<3)throw new InvalidDataException("Polygon needs three vertices.");
                    foreach(Vector2 p in a.polygon)ValidatePoint(p);
                }
            }
            return d;
        }

        void RequireFireflyAccess()
        {
            if((Access&MapEditAccess.Fireflies)==0)
                throw new UnauthorizedAccessException("Firefly editing requires author permission.");
        }

        public void SetFireflies(FireflyMapData value)
        {
            RequireFireflyAccess();
            var validated=ValidateFireflies(value);CommitStroke();
            if(JsonUtility.ToJson(validated)==JsonUtility.ToJson(fireflies))return;
            Execute(new Command{
                kind=MapChangeKind.Fireflies,
                bounds=new Rect(Bounds.x,Bounds.y,Bounds.z,Bounds.w),
                fireflyBefore=Clone(fireflies),fireflyAfter=validated
            });
        }

        public void UpsertFireflyArea(FireflyAreaData area)
        {
            RequireFireflyAccess();
            if(area==null)throw new ArgumentNullException(nameof(area));
            var data=ExportFireflies();int at=data.areas.FindIndex(a=>a.id==area.id);
            if(at>=0)data.areas[at]=Clone(area);
            else data.areas.Add(Clone(area));
            SetFireflies(data);
        }

        public void RemoveFireflyArea(string id)
        {
            RequireFireflyAccess();
            var data=ExportFireflies();
            if(data.areas.RemoveAll(a=>a.id==id)>0)SetFireflies(data);
        }

        bool HasFireflyEdits =>
            JsonUtility.ToJson(fireflies)!=JsonUtility.ToJson(ValidateFireflies(authored.fireflies));

        void AddFireflyReset(Command command)
        {
            if(!HasFireflyEdits)return;
            command.fireflyBefore=ExportFireflies();
            command.fireflyAfter=ValidateFireflies(authored.fireflies);
            command.kind|=MapChangeKind.Fireflies;
            command.bounds=Union(command.bounds,new Rect(Bounds.x,Bounds.y,Bounds.z,Bounds.w));
        }

        void ApplyFireflyCommand(Command command,bool reverse)
        {
            if(command.fireflyBefore==null)return;
            fireflies=Clone(reverse?command.fireflyBefore:command.fireflyAfter);
            FireflyRevision++;
        }

        // Exact pre-firefly MapAuthoringDocument field order preserves v6 fingerprints.
        [Serializable] sealed class LegacyEnvironmentFireflyBase
        {
            public int version;
            public string mapId;
            public int revision,decorationRulesVersion;
            public Vector4 bounds;
            public LittleCastle.Rendering.SurfacePaintDocument surface;
            public MapHeightFieldData heightField;
            public List<MapTreeInstance> trees;
            public List<Rect> placementExclusions;
            public List<MapRiverRecord> rivers;
            public List<MapRockSection> rockSections;
            public Environment.MapEnvironmentData environment;
        }

        string LegacyEnvironmentFingerprint()
        {
            if(authored.environment==null)return LegacyFeatureFingerprint();
            return Fingerprint(new LegacyEnvironmentFireflyBase{
                version=authored.version,mapId=MapId,revision=authored.revision,
                decorationRulesVersion=authored.decorationRulesVersion,bounds=Bounds,
                surface=authored.surface,heightField=authored.heightField,
                trees=authored.trees,placementExclusions=authored.placementExclusions,
                rivers=authored.rivers,rockSections=authored.rockSections,
                environment=authored.environment
            });
        }
    }
}
