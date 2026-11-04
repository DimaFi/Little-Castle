using System;
using System.Collections.Generic;
using LittleCastle.Rendering;
using LittleCastle.World;
using LittleCastle.World.Environment;
using UnityEngine;

namespace LittleCastle.Tests
{
    /// <summary>
    /// Frozen authored schema from before fences/networks. Tests must not generate
    /// a historical fingerprint by serializing a root that gains new fields.
    /// Intentionally independent of production compatibility DTOs and hash helpers.
    /// </summary>
    static class MapLegacyTestDocument
    {
        [Serializable] sealed class V6
        {
            public int version;
            public string mapId;
            public int revision, decorationRulesVersion;
            public Vector4 bounds;
            public SurfacePaintDocument surface;
            public MapHeightFieldData heightField;
            public List<MapTreeInstance> trees;
            public List<Rect> placementExclusions;
            public List<MapRiverRecord> rivers;
            public List<MapRockSection> rockSections;
            public MapEnvironmentData environment;
        }
        public static string V6Json(MapAuthoringDocument map) => JsonUtility.ToJson(new V6 {
            version=map.version,mapId=map.mapId,revision=map.revision,decorationRulesVersion=map.decorationRulesVersion,
            bounds=map.bounds,surface=map.surface,heightField=map.heightField,trees=map.trees,placementExclusions=map.placementExclusions,
            rivers=map.rivers,rockSections=map.rockSections,environment=map.environment });
    }
}
