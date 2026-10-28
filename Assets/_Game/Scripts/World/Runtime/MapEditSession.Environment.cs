using System;
using System.Collections.Generic;
using System.IO;
using LittleCastle.World.Environment;
using UnityEngine;

namespace LittleCastle.World
{
    public sealed partial class MapEditSession
    {
        sealed class EnvironmentTileChange
        {
            public int x,z;
            public List<ThemeLayerSnapshot> beforeThemes,afterThemes;
            public ScalarTileSnapshot beforeSnow,afterSnow,beforeClear,afterClear;
        }
        sealed class EnvironmentStroke
        {
            public int theme;
            public bool snow,erase,smooth;
            public Rect bounds;
            public readonly Dictionary<ChunkCoordinate,EnvironmentTileChange> tiles=new Dictionary<ChunkCoordinate,EnvironmentTileChange>();
        }
        EnvironmentStroke environmentStroke;
        bool applyingEnvironment;
        public BiomeInfluenceField EnvironmentThemes {get;private set;}
        public SnowAccumulationField EnvironmentSnow {get;private set;}
        SnowAccumulationField environmentSnowClear;
        MapEnvironmentSettings environmentSettings;
        public MapEnvironmentSettings EnvironmentSettings=>Clone(environmentSettings);
        public long EnvironmentRevision {get;private set;}
        public long BiomeRevision {get;private set;}
        /// <summary>Committed biome revision, also emitted after Undo/Redo. Consumers request a snapshot.</summary>
        public event Action<long> BiomesCommitted;
        Rect EnvironmentBounds=>new Rect(Bounds.x,Bounds.y,Bounds.z,Bounds.w);

        void InitializeEnvironment()
        {
            var data=ValidateEnvironment(authored.environment);
            EnvironmentThemes=new BiomeInfluenceField(data.cellSize,data.tileResolution);
            EnvironmentSnow=new SnowAccumulationField(data.cellSize,data.tileResolution);
            environmentSnowClear=new SnowAccumulationField(data.cellSize,data.tileResolution);
            RestoreEnvironment(data);
            EnvironmentThemes.Changed+=r=>EnvironmentFieldChanged(r,MapChangeKind.Biomes);
            EnvironmentSnow.Changed+=r=>EnvironmentFieldChanged(r,MapChangeKind.Snow);
            environmentSnowClear.Changed+=r=>EnvironmentFieldChanged(r,MapChangeKind.Snow);
        }
        void EnvironmentFieldChanged(Rect area,MapChangeKind kind)
        {
            if(applyingEnvironment)return;
            EnvironmentRevision++;
            Notify(area,environmentStroke!=null,kind);
        }
        public MapEnvironmentData ExportEnvironment()=>new MapEnvironmentData{
            cellSize=EnvironmentThemes.CellSize,tileResolution=EnvironmentThemes.TileResolution,
            themes=EnvironmentThemes.Capture(),snow=EnvironmentSnow.Capture(),snowClear=environmentSnowClear.Capture(),settings=Clone(environmentSettings)};
        MapEnvironmentData ValidateEnvironment(MapEnvironmentData source)
        {
            var d=source==null?new MapEnvironmentData():Clone(source);
            if(d.version!=1||d.cellSize!=.5f||d.tileResolution!=16||d.themes==null||d.snow==null||d.themes.Count>256||d.snow.Count>65536)
                throw new InvalidDataException("Unsupported environment grid or version.");
            d.snowClear=d.snowClear??new List<ScalarTileSnapshot>();
            ValidateEnvironmentSettings(d.settings);
            int count=d.snow.Count+d.snowClear.Count;foreach(var layer in d.themes){if(layer==null||layer.tiles==null)throw new InvalidDataException("Missing biome layer.");count+=layer.tiles.Count;}
            if(count>65536)throw new InvalidDataException("Environment tile limit exceeded.");
            foreach(var tile in d.snow)ValidateEnvironmentTile(tile);foreach(var tile in d.snowClear)ValidateEnvironmentTile(tile);
            foreach(var layer in d.themes)foreach(var tile in layer.tiles)ValidateEnvironmentTile(tile);
            // Validate all weights/duplicates/normalization before mutating a live session.
            var b=new BiomeInfluenceField(d.cellSize,d.tileResolution);b.Restore(d.themes);
            var s=new SnowAccumulationField(d.cellSize,d.tileResolution);s.Restore(d.snow);s.Restore(d.snowClear);
            return d;
        }
        void ValidateEnvironmentTile(ScalarTileSnapshot tile)
        {
            if(tile==null||tile.values==null||tile.values.Length!=256)throw new InvalidDataException("Invalid environment tile.");
            for(int i=0;i<256;i++)if(tile.values[i]!=0){
                double x=((long)tile.tileX*16+i%16+.5)*.5,z=((long)tile.tileZ*16+i/16+.5)*.5;
                var b=Bounds;if(x<b.x||x>b.x+b.z||z<b.y||z>b.y+b.w)throw new InvalidDataException("Environment cell outside map bounds.");}
        }
        static void ValidateEnvironmentSettings(MapEnvironmentSettings s)
        {
            if(s==null||!Finite(s.snowAmount)||s.snowAmount<0||s.snowAmount>1||!Finite(s.snowfallIntensity)||s.snowfallIntensity<0||s.snowfallIntensity>1||
                !Finite(s.accumulationPerSecond)||s.accumulationPerSecond<0||s.accumulationPerSecond>1||!Finite(s.meltingPerSecond)||s.meltingPerSecond<0||s.meltingPerSecond>1)
                throw new InvalidDataException("Invalid snow settings.");
        }
        void RestoreEnvironment(MapEnvironmentData data)
        {
            applyingEnvironment=true;
            try{EnvironmentThemes.Restore(data.themes);EnvironmentSnow.Restore(data.snow);environmentSnowClear.Restore(data.snowClear);environmentSettings=Clone(data.settings);}
            finally{applyingEnvironment=false;}
            EnvironmentRevision++;
        }
        void RequireEnvironmentAccess(bool snow)
        {if((Access&(snow?MapEditAccess.Snow:MapEditAccess.Biomes))==0)throw new UnauthorizedAccessException("Environment editing is disabled for this role.");}
        public void BeginEnvironmentStroke(int themeId,bool snow=false,bool erase=false,bool smooth=false)
        {
            RequireEnvironmentAccess(snow);if(themeId<0||themeId>65535)throw new ArgumentOutOfRangeException(nameof(themeId));
            CommitStroke();environmentStroke=new EnvironmentStroke{theme=themeId,snow=snow,erase=erase,smooth=smooth};
        }
        public void ApplyEnvironmentBrush(Vector2 center,float radius,float strength,float softness)
        {
            var stroke=environmentStroke;if(stroke==null)throw new InvalidOperationException("Begin environment stroke first.");
            RequireEnvironmentAccess(stroke.snow);ValidatePoint(center);
            if(!Finite(radius)||radius<.5f||radius>128||!Finite(strength)||strength<0||strength>1||!Finite(softness)||softness<0||softness>1)
                throw new InvalidDataException("Invalid environment brush.");
            foreach(var key in EnvironmentThemes.GetAffectedTiles(center,radius))
                if(!stroke.tiles.ContainsKey(key))stroke.tiles.Add(key,new EnvironmentTileChange{x=key.x,z=key.z,
                    beforeThemes=stroke.snow?null:EnvironmentThemes.CaptureTile(key.x,key.z),beforeSnow=stroke.snow?EnvironmentSnow.CaptureTile(key.x,key.z):null,beforeClear=stroke.snow?environmentSnowClear.CaptureTile(key.x,key.z):null});
            stroke.bounds=Union(stroke.bounds,new Rect(center.x-radius-.5f,center.y-radius-.5f,2*radius+1,2*radius+1));
            if(stroke.snow){EnvironmentSnow.Paint(center,radius,stroke.erase?-strength:strength,1-softness,EnvironmentBounds);environmentSnowClear.Paint(center,radius,stroke.erase?strength:-strength,1-softness,EnvironmentBounds);}
            else if(stroke.smooth)EnvironmentThemes.Smooth(center,radius,stroke.theme,strength,1-softness,EnvironmentBounds);
            else EnvironmentThemes.Paint(center,radius,stroke.erase?EnvironmentThemeIds.Summer:stroke.theme,strength,1-softness,EnvironmentBounds);
        }
        void CommitEnvironmentStroke()
        {
            var stroke=environmentStroke;if(stroke==null)return;
            try{RequireEnvironmentAccess(stroke.snow);}catch{CancelEnvironmentStroke();throw;}
            environmentStroke=null;
            var command=new Command{bounds=stroke.bounds,kind=stroke.snow?MapChangeKind.Snow:MapChangeKind.Biomes};
            foreach(var tile in stroke.tiles.Values){
                tile.afterThemes=stroke.snow?null:EnvironmentThemes.CaptureTile(tile.x,tile.z);
                tile.afterSnow=stroke.snow?EnvironmentSnow.CaptureTile(tile.x,tile.z):null;tile.afterClear=stroke.snow?environmentSnowClear.CaptureTile(tile.x,tile.z):null;
                if(JsonUtility.ToJson(new TileComparison{themes=tile.beforeThemes,snow=tile.beforeSnow,clear=tile.beforeClear})!=JsonUtility.ToJson(new TileComparison{themes=tile.afterThemes,snow=tile.afterSnow,clear=tile.afterClear}))command.environment.Add(tile);
            }
            if(command.environment.Count>0)Execute(command);
        }
        [Serializable] sealed class TileComparison {public List<ThemeLayerSnapshot> themes;public ScalarTileSnapshot snow,clear;}
        void CancelEnvironmentStroke()
        {
            var stroke=environmentStroke;if(stroke==null)return;environmentStroke=null;
            applyingEnvironment=true;try{foreach(var tile in stroke.tiles.Values)
                if(stroke.snow){EnvironmentSnow.RestoreTile(tile.x,tile.z,tile.beforeSnow);environmentSnowClear.RestoreTile(tile.x,tile.z,tile.beforeClear);}else EnvironmentThemes.RestoreTile(tile.x,tile.z,tile.beforeThemes);}
            finally{applyingEnvironment=false;}
            EnvironmentRevision++;Notify(stroke.bounds,false,stroke.snow?MapChangeKind.Snow:MapChangeKind.Biomes);
        }
        void ApplyEnvironmentCommand(Command command,bool reverse)
        {
            if(command.environment.Count==0&&command.environmentSettingsBefore==null)return;
            applyingEnvironment=true;
            try{foreach(var tile in command.environment){
                if((command.kind&MapChangeKind.Biomes)!=0)EnvironmentThemes.RestoreTile(tile.x,tile.z,reverse?tile.beforeThemes:tile.afterThemes);
                if((command.kind&MapChangeKind.Snow)!=0){EnvironmentSnow.RestoreTile(tile.x,tile.z,reverse?tile.beforeSnow:tile.afterSnow);environmentSnowClear.RestoreTile(tile.x,tile.z,reverse?tile.beforeClear:tile.afterClear);}
            }
            if(command.environmentSettingsBefore!=null)environmentSettings=Clone(reverse?command.environmentSettingsBefore:command.environmentSettingsAfter);}
            finally{applyingEnvironment=false;}
            EnvironmentRevision++;
            if((command.kind&MapChangeKind.Biomes)!=0){BiomeRevision++;BiomesCommitted?.Invoke(BiomeRevision);}
        }
        public void SetEnvironmentSettings(MapEnvironmentSettings value)
        {
            RequireEnvironmentAccess(true);ValidateEnvironmentSettings(value);CommitStroke();
            if(JsonUtility.ToJson(value)==JsonUtility.ToJson(environmentSettings))return;
            Execute(new Command{bounds=EnvironmentBounds,kind=MapChangeKind.Snow,environmentSettingsBefore=Clone(environmentSettings),environmentSettingsAfter=Clone(value)});
        }
        public float SampleEnvironmentSnow(float x,float z)
        {
            float coverage=EnvironmentSnow.Sample(x,z);
            if(environmentSettings.winterSnowPreview)coverage=Mathf.Max(coverage,EnvironmentThemes.Sample(EnvironmentThemeIds.Winter,x,z));
            return Mathf.Clamp01(coverage*(1-environmentSnowClear.Sample(x,z))*environmentSettings.snowAmount);
        }
        public void AdvanceLocalSnow(float seconds,Rect area)
        {
            if(!Finite(seconds)||seconds<0||seconds>3600)throw new ArgumentOutOfRangeException(nameof(seconds));
            if(!environmentSettings.simulateWeather)return;
            var b=EnvironmentBounds;area=Rect.MinMaxRect(Mathf.Max(area.xMin,b.xMin),Mathf.Max(area.yMin,b.yMin),Mathf.Min(area.xMax,b.xMax),Mathf.Min(area.yMax,b.yMax));
            // Keep client-side weather bounded even if a caller passes the entire strategic map.
            if(area.width<=0||area.height<=0)return;
            if(area.width>256||area.height>256)throw new ArgumentOutOfRangeException(nameof(area));
            EnvironmentSnow.Advance(area,seconds,environmentSettings.snowfallIntensity*environmentSettings.accumulationPerSecond,
                environmentSettings.meltingPerSecond,EnvironmentThemes,EnvironmentThemeIds.Winter);
        }
        public MapBiomeSnapshot ExportBiomeSnapshot()=>new MapBiomeSnapshot{mapId=MapId,bounds=Bounds,revision=BiomeRevision,
            cellSize=EnvironmentThemes.CellSize,tileResolution=EnvironmentThemes.TileResolution,themes=EnvironmentThemes.Capture()};
        /// <summary>Call only with a trusted server snapshot. Transport/ownership remains a gameplay concern.</summary>
        public void ApplyBiomeSnapshot(MapBiomeSnapshot snapshot)
        {
            if(snapshot==null||snapshot.version!=1||snapshot.mapId!=MapId||snapshot.bounds!=Bounds||snapshot.revision<BiomeRevision)
                throw new InvalidDataException("Wrong map or stale biome snapshot.");
            var validated=ValidateEnvironment(new MapEnvironmentData{cellSize=snapshot.cellSize,tileResolution=snapshot.tileResolution,themes=snapshot.themes});
            if(HasPending)throw new InvalidOperationException("Finish the active stroke before applying a server snapshot.");
            applyingEnvironment=true;try{EnvironmentThemes.Restore(validated.themes);}finally{applyingEnvironment=false;}
            BiomeRevision=snapshot.revision;EnvironmentRevision++;undo.Clear();redo.Clear();Notify(EnvironmentBounds,false,MapChangeKind.Biomes);
        }
        bool HasEnvironmentEdits=>JsonUtility.ToJson(ExportEnvironment())!=JsonUtility.ToJson(authored.environment??new MapEnvironmentData());
        void AddEnvironmentReset(Command command)
        {
            if(!HasEnvironmentEdits)return;
            var target=new MapEditSession(ExportAuthored());
            bool biomesChanged=JsonUtility.ToJson(new EnvironmentFieldSnapshot{themes=EnvironmentThemes.Capture()})!=JsonUtility.ToJson(new EnvironmentFieldSnapshot{themes=target.EnvironmentThemes.Capture()});
            bool snowChanged=JsonUtility.ToJson(new EnvironmentFieldSnapshot{snow=EnvironmentSnow.Capture()})!=JsonUtility.ToJson(new EnvironmentFieldSnapshot{snow=target.EnvironmentSnow.Capture()});
            bool clearingChanged=JsonUtility.ToJson(new EnvironmentFieldSnapshot{snow=environmentSnowClear.Capture()})!=JsonUtility.ToJson(new EnvironmentFieldSnapshot{snow=target.environmentSnowClear.Capture()});
            snowChanged|=clearingChanged;
            bool settingsChanged=JsonUtility.ToJson(environmentSettings)!=JsonUtility.ToJson(target.EnvironmentSettings);
            var keys=new HashSet<ChunkCoordinate>();
            if(biomesChanged){foreach(var layer in EnvironmentThemes.Capture())foreach(var t in layer.tiles)keys.Add(new ChunkCoordinate(t.tileX,t.tileZ));
            foreach(var layer in target.EnvironmentThemes.Capture())foreach(var t in layer.tiles)keys.Add(new ChunkCoordinate(t.tileX,t.tileZ));}
            if(snowChanged){foreach(var t in EnvironmentSnow.Capture())keys.Add(new ChunkCoordinate(t.tileX,t.tileZ));
            foreach(var t in target.EnvironmentSnow.Capture())keys.Add(new ChunkCoordinate(t.tileX,t.tileZ));
            foreach(var t in environmentSnowClear.Capture())keys.Add(new ChunkCoordinate(t.tileX,t.tileZ));foreach(var t in target.environmentSnowClear.Capture())keys.Add(new ChunkCoordinate(t.tileX,t.tileZ));}
            foreach(var key in keys)command.environment.Add(new EnvironmentTileChange{x=key.x,z=key.z,
                beforeThemes=EnvironmentThemes.CaptureTile(key.x,key.z),afterThemes=target.EnvironmentThemes.CaptureTile(key.x,key.z),
                beforeSnow=EnvironmentSnow.CaptureTile(key.x,key.z),afterSnow=target.EnvironmentSnow.CaptureTile(key.x,key.z),beforeClear=environmentSnowClear.CaptureTile(key.x,key.z),afterClear=target.environmentSnowClear.CaptureTile(key.x,key.z)});
            if(settingsChanged){command.environmentSettingsBefore=Clone(environmentSettings);command.environmentSettingsAfter=target.EnvironmentSettings;}
            if(biomesChanged)command.kind|=MapChangeKind.Biomes;
            if(snowChanged||settingsChanged)command.kind|=MapChangeKind.Snow;
            command.bounds=Union(command.bounds,EnvironmentBounds);
        }
        [Serializable] sealed class LegacyFeatureBase
        {
            public int version;public string mapId;public int revision,decorationRulesVersion;public Vector4 bounds;
            public Rendering.SurfacePaintDocument surface;public MapHeightFieldData heightField;public List<MapTreeInstance> trees;
            public List<Rect> placementExclusions;public List<MapRiverRecord> rivers;public List<MapRockSection> rockSections;
        }
        string LegacyFeatureFingerprint()=>Fingerprint(new LegacyFeatureBase{version=authored.version,mapId=MapId,revision=authored.revision,
            decorationRulesVersion=authored.decorationRulesVersion,bounds=Bounds,surface=authored.surface,heightField=authored.heightField,
            trees=authored.trees,placementExclusions=authored.placementExclusions,rivers=authored.rivers,rockSections=authored.rockSections});
        string EnvironmentBaseFingerprint()=>authored.environment==null?LegacyFeatureFingerprint():Fingerprint(authored);
    }
}
