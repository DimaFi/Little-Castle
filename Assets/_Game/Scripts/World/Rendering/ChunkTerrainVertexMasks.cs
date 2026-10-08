using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Builds terrain shader R (road) and G (riverbank wetness) vertex masks
    /// directly from deterministic world-space macro geometry. All queries
    /// use the absolute vertex X/Z, so shared negative/positive chunk seams
    /// receive the same values with no neighbor cache dependency.
    ///
    /// This is PRESENTATION ONLY. Does not alter SurfaceKind, terrain height,
    /// authoritative roads/rivers, bridge sites, or world version.
    /// </summary>
    public static class ChunkTerrainVertexMasks
    {
        private const float RoadEdgeMeters = 0.85f;
        private const float RiverBankEdgeMeters = 1.8f;

        private struct RoadSegment
        {
            public Vector2 a;
            public Vector2 b;
            public float halfWidth;
        }

        private struct RiverSegment
        {
            public Vector2 a;
            public Vector2 b;
            public float halfStartWidth;
            public float halfEndWidth;
        }

        public static Color32[] Build(
            WorldChunkData chunk,
            float chunkWorldSize,
            MacroWorldPlan plan)
        {
            if (chunk == null || chunkWorldSize <= 0f)
                return null;

            int samples = chunk.SamplesPerSide;
            var result = new Color32[samples * samples];
            if (plan == null)
                return result;

            float minX = chunk.Coordinate.x * chunkWorldSize;
            float minZ = chunk.Coordinate.z * chunkWorldSize;
            float maxX = minX + chunkWorldSize;
            float maxZ = minZ + chunkWorldSize;
            float cellSize = chunkWorldSize / chunk.CellsPerSide;
            var roads = new List<RoadSegment>();
            var rivers = new List<RiverSegment>();

            // Coarse AABB culling happens once per visible chunk (not once
            // for each of its ~4K vertices). No full map materialization.
            for (int i = 0; i < plan.Roads.Count; i++)
            {
                WorldRoadData road = plan.Roads[i];
                if (road == null || road.centerline == null ||
                    road.centerline.Count < 2)
                    continue;

                float half = Mathf.Max(0.1f, road.width * 0.5f);
                for (int s = 0; s < road.centerline.Count - 1; s++)
                {
                    Vector2 a = road.centerline[s];
                    Vector2 b = road.centerline[s + 1];
                    if ((b - a).sqrMagnitude <= 0.000001f ||
                        !Overlaps(a, b, half + RoadEdgeMeters,
                            minX, minZ, maxX, maxZ))
                        continue;

                    roads.Add(new RoadSegment { a = a, b = b,
                        halfWidth = half });
                }
            }

            for (int i = 0; i < plan.Rivers.Count; i++)
            {
                WorldRiverData river = plan.Rivers[i];
                if (river == null || river.centerline == null ||
                    river.centerline.Count < 2)
                    continue;

                for (int s = 0; s < river.centerline.Count - 1; s++)
                {
                    Vector2 a = river.centerline[s];
                    Vector2 b = river.centerline[s + 1];
                    if ((b - a).sqrMagnitude <= 0.000001f)
                        continue;

                    float halfA = Mathf.Max(0.1f,
                        river.GetWidthAtPoint(s) * 0.5f);
                    float halfB = Mathf.Max(0.1f,
                        river.GetWidthAtPoint(s + 1) * 0.5f);
                    if (!Overlaps(a, b,
                            Mathf.Max(halfA, halfB) + RiverBankEdgeMeters,
                            minX, minZ, maxX, maxZ))
                        continue;

                    rivers.Add(new RiverSegment
                    {
                        a = a, b = b,
                        halfStartWidth = halfA,
                        halfEndWidth = halfB
                    });
                }
            }

            // Alpha must be opaque; the shader's B channel is reserved for
            // authored contact-soil shading and stays zero in this pass.
            for (int z = 0; z < samples; z++)
            {
                for (int x = 0; x < samples; x++)
                {
                    var world = new Vector2(
                        minX + cellSize * x,
                        minZ + cellSize * z);
                    float path = 0f;
                    float riverbank = 0f;

                    for (int i = 0; i < roads.Count; i++)
                    {
                        RoadSegment segment = roads[i];
                        float distance = DistanceToSegment(
                            world, segment.a, segment.b, out _);
                        path = Mathf.Max(path,
                            SoftCorridor(distance, segment.halfWidth,
                                RoadEdgeMeters));
                    }

                    for (int i = 0; i < rivers.Count; i++)
                    {
                        RiverSegment segment = rivers[i];
                        float distance = DistanceToSegment(
                            world, segment.a, segment.b, out float t);
                        float halfWidth = Mathf.Lerp(
                            segment.halfStartWidth,
                            segment.halfEndWidth, t);
                        // Outside the water centerline, retain a smooth
                        // naturally darkened damp bank. Under water the
                        // value is harmless, since the surface is covered.
                        riverbank = Mathf.Max(riverbank,
                            SoftCorridor(distance, halfWidth,
                                RiverBankEdgeMeters));
                    }

                    // Wet channel material overrides the dirt footpath at
                    // physical river crossings. The fixed authored deck is
                    // a separate prefab; no ground-level painted bridge.
                    path *= 1f - riverbank;
                    result[z * samples + x] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(path) * 255f),
                        (byte)Mathf.RoundToInt(
                            Mathf.Clamp01(riverbank) * 255f),
                        0, 255);
                }
            }

            return result;
        }

        private static bool Overlaps(
            Vector2 a, Vector2 b, float padding,
            float minX, float minZ, float maxX, float maxZ)
        {
            return Mathf.Max(a.x, b.x) + padding >= minX &&
                   Mathf.Min(a.x, b.x) - padding <= maxX &&
                   Mathf.Max(a.y, b.y) + padding >= minZ &&
                   Mathf.Min(a.y, b.y) - padding <= maxZ;
        }

        private static float DistanceToSegment(
            Vector2 point, Vector2 a, Vector2 b, out float t)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            t = lengthSquared > 0.000001f
                ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared)
                : 0f;
            return (point - (a + ab * t)).magnitude;
        }

        private static float SoftCorridor(
            float distance, float innerRadius, float falloff)
        {
            if (distance <= innerRadius)
                return 1f;
            float fraction = Mathf.Clamp01(
                (distance - innerRadius) / Mathf.Max(0.01f, falloff));
            return 1f - Mathf.SmoothStep(0f, 1f, fraction);
        }
    }
}
