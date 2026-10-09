using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.World
{
    /// <summary>
    /// Builds one read-only water surface for one resident chunk. All sampling
    /// positions come from the absolute terrain lattice, so adjacent chunks
    /// evaluate their shared border at the same world positions.
    /// </summary>
    public static class RiverWaterMeshBuilder
    {
        private const int SamplesPerTerrainCell = 2;
        private const float DryScore = -1000000f;

        private struct Segment
        {
            public Vector2 a;
            public Vector2 b;
            public float widthA;
            public float widthB;
            public float depthA;
            public float depthB;
            public long riverId;
        }

        private struct Sample
        {
            public Vector3 vertex;
            public float score;
        }

        public static Mesh Build(
            WorldChunkData chunk,
            float chunkWorldSize,
            MacroWorldPlan plan)
        {
            if (chunk == null || plan == null || plan.Rivers.Count == 0 ||
                chunk.CellsPerSide <= 0 || !IsFinite(chunkWorldSize) ||
                chunkWorldSize <= 0f)
                return null;

            int cells = chunk.CellsPerSide * SamplesPerTerrainCell;
            float step = chunkWorldSize / cells;
            if (!IsFinite(step) || step <= 0f)
                return null;

            float originX = chunk.Coordinate.x * chunkWorldSize;
            float originZ = chunk.Coordinate.z * chunkWorldSize;
            var segments = CollectSegments(
                plan, originX, originZ, chunkWorldSize);
            if (segments.Count == 0)
                return null;

            var sites = CollectSites(
                plan, originX, originZ, chunkWorldSize, segments);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            // Weld exact XYZ shared by clipped triangles. World UVs derive
            // from that position. Never epsilon-weld chunk boundary water.
            var vertexIndices = new Dictionary<Vector3, int>();
            var lattice = new Sample[cells + 1, cells + 1];

            for (int z = 0; z <= cells; z++)
            {
                for (int x = 0; x <= cells; x++)
                {
                    lattice[x, z] = Evaluate(chunk, cells, step,
                        x, z, originX, originZ, segments, sites);
                }
            }

            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    Sample a = lattice[x, z];
                    Sample b = lattice[x + 1, z];
                    Sample c = lattice[x + 1, z + 1];
                    Sample d = lattice[x, z + 1];
                    Sample middle = Evaluate(chunk, cells, step,
                        x + 0.5f, z + 0.5f,
                        originX, originZ, segments, sites);

                    EmitClipped(a, middle, b, vertices, triangles, uvs,
                        vertexIndices, originX, originZ);
                    EmitClipped(b, middle, c, vertices, triangles, uvs,
                        vertexIndices, originX, originZ);
                    EmitClipped(c, middle, d, vertices, triangles, uvs,
                        vertexIndices, originX, originZ);
                    EmitClipped(d, middle, a, vertices, triangles, uvs,
                        vertexIndices, originX, originZ);
                }
            }

            if (triangles.Count == 0)
                return null;

            var mesh = new Mesh
            {
                name = $"RiverWater_{chunk.Coordinate.x}_{chunk.Coordinate.z}"
            };
            if (vertices.Count > 65535)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);

            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < normals.Length; i++)
                normals[i] = Vector3.up;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<Segment> CollectSegments(
            MacroWorldPlan plan, float originX, float originZ, float size)
        {
            var segments = new List<Segment>();
            for (int r = 0; r < plan.Rivers.Count; r++)
            {
                WorldRiverData river = plan.Rivers[r];
                if (river == null || river.centerline == null ||
                    river.centerline.Count < 2)
                    continue;

                for (int i = 0; i < river.centerline.Count - 1; i++)
                {
                    Vector2 a = river.centerline[i];
                    Vector2 b = river.centerline[i + 1];
                    if (!IsFinite(a.x) || !IsFinite(a.y) ||
                        !IsFinite(b.x) || !IsFinite(b.y))
                        continue;

                    float widthA = river.GetWidthAtPoint(i);
                    float widthB = river.GetWidthAtPoint(i + 1);
                    float depthA = river.GetDepthAtPoint(i);
                    float depthB = river.GetDepthAtPoint(i + 1);
                    if (!IsFinite(widthA) || !IsFinite(widthB) ||
                        !IsFinite(depthA) || !IsFinite(depthB))
                        continue;

                    // Seven metres is the fixed bridge's river-axis half span.
                    float reach = Mathf.Max(widthA, widthB) * 0.5f +
                        FixedBridgeSiteProfile.SupportHalfExtentX;
                    if (Mathf.Max(a.x, b.x) + reach < originX ||
                        Mathf.Min(a.x, b.x) - reach > originX + size ||
                        Mathf.Max(a.y, b.y) + reach < originZ ||
                        Mathf.Min(a.y, b.y) - reach > originZ + size)
                        continue;

                    segments.Add(new Segment
                    {
                        a = a,
                        b = b,
                        widthA = widthA,
                        widthB = widthB,
                        depthA = depthA,
                        depthB = depthB,
                        riverId = river.stableId
                    });
                }
            }
            return segments;
        }

        private static List<WorldBridgeSiteData> CollectSites(
            MacroWorldPlan plan, float originX, float originZ, float size,
            List<Segment> segments)
        {
            var sites = new List<WorldBridgeSiteData>();
            for (int i = 0; i < plan.BridgeSites.Count; i++)
            {
                WorldBridgeSiteData site = plan.BridgeSites[i];
                if (!BridgeSitePresentationUtility.TryGetWorldAnchors(
                        site, out _, out _, out _, out _, out _, out _))
                    continue;

                FixedBridgeSiteProfile.GetWorldAabbHalfExtents(
                    site.yawDegrees, out float halfX, out float halfZ);
                if (site.worldPosition.x + halfX < originX ||
                    site.worldPosition.x - halfX > originX + size ||
                    site.worldPosition.y + halfZ < originZ ||
                    site.worldPosition.y - halfZ > originZ + size)
                    continue;

                // Prevent orphan bridge sites from producing new water.
                // This also lets the linked river control bridge contact
                // when a different river wins the confluence envelope.
                if (!HasMatchingRiverAtSite(site, segments))
                    continue;

                sites.Add(site);
            }
            return sites;
        }

        private static bool HasMatchingRiverAtSite(
            WorldBridgeSiteData site, List<Segment> segments)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                Segment segment = segments[i];
                if (segment.riverId != site.riverId)
                    continue;

                float distance = RiverEnvelopeUtility.DistanceToSegment(
                    site.worldPosition, segment.a, segment.b, out float t);
                float halfWidth = Mathf.Lerp(
                    segment.widthA, segment.widthB, t) * 0.5f;
                if (distance <= Mathf.Max(0.05f, halfWidth))
                    return true;
            }
            return false;
        }

        private static Sample Evaluate(
            WorldChunkData chunk, int cells, float step,
            float gridX, float gridZ, float originX, float originZ,
            List<Segment> segments, List<WorldBridgeSiteData> sites)
        {
            // The absolute integer lattice gives both sides of a negative or
            // positive chunk seam the same world-space sample coordinates.
            float worldX = (chunk.Coordinate.x * cells + gridX) * step;
            float worldZ = (chunk.Coordinate.z * cells + gridZ) * step;
            var world = new Vector2(worldX, worldZ);
            float terrainY = SampleTerrainHeight(
                chunk, gridX / SamplesPerTerrainCell,
                gridZ / SamplesPerTerrainCell);

            float bestScore = DryScore;
            float distance = float.PositiveInfinity;
            float width = 0f;
            float depth = 0f;
            long riverId = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                Segment segment = segments[i];
                Vector2 ab = segment.b - segment.a;
                float lengthSq = ab.sqrMagnitude;
                float t = lengthSq > 0.000001f
                    ? Mathf.Clamp01(Vector2.Dot(world - segment.a, ab) /
                        lengthSq)
                    : 0f;
                float candidateDistance =
                    (world - (segment.a + ab * t)).magnitude;
                float candidateWidth =
                    Mathf.Lerp(segment.widthA, segment.widthB, t);
                float candidateScore =
                    candidateWidth * 0.5f - candidateDistance;
                if (candidateScore <= bestScore)
                    continue;
                bestScore = candidateScore;
                distance = candidateDistance;
                width = candidateWidth;
                depth = Mathf.Lerp(segment.depthA, segment.depthB, t);
                riverId = segment.riverId;
            }

            float halfWidth = width * 0.5f;
            float score = bestScore;

            // Approximate a surface above the carved bed without changing
            // terrain data. Exact hydraulic grade is not present in macro data.
            float radial = halfWidth > 0f
                ? Mathf.Clamp01(distance / halfWidth)
                : 1f;
            float waterY = terrainY +
                Mathf.Max(0.02f, depth * (0.55f - 0.33f * radial));

            for (int i = 0; i < sites.Count; i++)
            {
                WorldBridgeSiteData site = sites[i];
                // A validated fixed site controls its protected contact,
                // regardless of another river's max-score at the crossing.
                if (!BridgeSitePresentationUtility.IsInsideFixedFootprint(
                        site, world))
                    continue;
                if (!BridgeSitePresentationUtility.TryGetWorldAnchors(
                        site, out _, out _, out _, out _, out _,
                        out float fixedWaterY))
                    continue;

                Vector2 local = FixedBridgeSiteProfile.WorldToLocal(
                    world, site.worldPosition, site.yawDegrees);
                float along = Mathf.Abs(local.x);
                if (along > FixedBridgeSiteProfile.SupportHalfExtentX)
                    continue;

                float siteScore =
                    FixedBridgeSiteProfile.ChannelHalfWidth(local.x) -
                    Mathf.Abs(local.y);
                // The utility defines the protected channel; its analytic
                // half width also gives signed shore distance for clipping.
                bool protectedWater =
                    BridgeSitePresentationUtility.IsProtectedWaterSurface(
                        site, world);
                if (along <= FixedBridgeSiteProfile.ProtectedCoreHalfExtentX)
                {
                    score = protectedWater ? Mathf.Max(0f, siteScore)
                        : siteScore;
                    waterY = fixedWaterY;
                }
                else
                {
                    float t = Mathf.Clamp01(
                        (along -
                         FixedBridgeSiteProfile.ProtectedCoreHalfExtentX) /
                        (FixedBridgeSiteProfile.SupportHalfExtentX -
                         FixedBridgeSiteProfile.ProtectedCoreHalfExtentX));
                    score = Mathf.Lerp(siteScore, score, t);
                    waterY = Mathf.Lerp(fixedWaterY, waterY, t);
                }
                break;
            }

            return new Sample
            {
                vertex = new Vector3(gridX * step, waterY,
                    gridZ * step),
                score = IsFinite(waterY) && IsFinite(score)
                    ? score
                    : DryScore
            };
        }

        private static float SampleTerrainHeight(
            WorldChunkData chunk, float gridX, float gridZ)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(gridX),
                0, chunk.CellsPerSide);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(gridZ),
                0, chunk.CellsPerSide);
            int x1 = Mathf.Min(x0 + 1, chunk.CellsPerSide);
            int z1 = Mathf.Min(z0 + 1, chunk.CellsPerSide);
            float tx = Mathf.Clamp01(gridX - x0);
            float tz = Mathf.Clamp01(gridZ - z0);
            return Mathf.Lerp(
                Mathf.Lerp(chunk.GetHeight(x0, z0),
                    chunk.GetHeight(x1, z0), tx),
                Mathf.Lerp(chunk.GetHeight(x0, z1),
                    chunk.GetHeight(x1, z1), tx), tz);
        }

        private static void EmitClipped(
            Sample a, Sample b, Sample c,
            List<Vector3> vertices, List<int> triangles,
            List<Vector2> uvs, Dictionary<Vector3, int> vertexIndices,
            float originX, float originZ)
        {
            bool aw = a.score >= 0f;
            bool bw = b.score >= 0f;
            bool cw = c.score >= 0f;
            int count = (aw ? 1 : 0) + (bw ? 1 : 0) + (cw ? 1 : 0);
            if (count == 0)
                return;
            if (count == 3)
            {
                AddTriangle(a.vertex, b.vertex, c.vertex,
                    vertices, triangles, uvs, vertexIndices,
                    originX, originZ);
                return;
            }

            if (count == 1)
            {
                Sample wet = aw ? a : bw ? b : c;
                Sample dryA = !aw ? a : !bw ? b : c;
                Sample dryB = !cw ? c : !bw ? b : a;
                AddTriangle(wet.vertex, Intersect(wet, dryA),
                    Intersect(wet, dryB), vertices, triangles, uvs,
                    vertexIndices, originX, originZ);
                return;
            }

            Sample dry;
            Sample wetA;
            Sample wetB;
            if (!aw)
            {
                dry = a;
                wetA = b;
                wetB = c;
            }
            else if (!bw)
            {
                dry = b;
                wetA = a;
                wetB = c;
            }
            else
            {
                dry = c;
                wetA = a;
                wetB = b;
            }
            Vector3 edgeA = Intersect(wetA, dry);
            Vector3 edgeB = Intersect(wetB, dry);
            AddTriangle(wetA.vertex, wetB.vertex, edgeA,
                vertices, triangles, uvs, vertexIndices, originX, originZ);
            AddTriangle(wetB.vertex, edgeB, edgeA,
                vertices, triangles, uvs, vertexIndices, originX, originZ);
        }

        private static Vector3 Intersect(Sample wet, Sample dry)
        {
            float denominator = wet.score - dry.score;
            float t = denominator > 0.000001f
                ? Mathf.Clamp01(wet.score / denominator)
                : 0.5f;
            return Vector3.Lerp(wet.vertex, dry.vertex, t);
        }

        private static void AddTriangle(
            Vector3 a, Vector3 b, Vector3 c,
            List<Vector3> vertices, List<int> triangles,
            List<Vector2> uvs, Dictionary<Vector3, int> vertexIndices,
            float originX, float originZ)
        {
            float signedArea = (b.z - a.z) * (c.x - a.x) -
                (b.x - a.x) * (c.z - a.z);
            if (Mathf.Abs(signedArea) < 0.0000001f ||
                !IsFinite(a.y) || !IsFinite(b.y) || !IsFinite(c.y))
                return;
            if (signedArea < 0f)
            {
                Vector3 swap = b;
                b = c;
                c = swap;
            }

            // No epsilon welding: exact local positions share UVs/normals
            // and the fixed bridge's base-0.85 waterline stays unchanged.
            triangles.Add(GetOrAddVertex(
                a, vertices, uvs, vertexIndices, originX, originZ));
            triangles.Add(GetOrAddVertex(
                b, vertices, uvs, vertexIndices, originX, originZ));
            triangles.Add(GetOrAddVertex(
                c, vertices, uvs, vertexIndices, originX, originZ));
        }

        private static int GetOrAddVertex(
            Vector3 vertex, List<Vector3> vertices, List<Vector2> uvs,
            Dictionary<Vector3, int> vertexIndices,
            float originX, float originZ)
        {
            if (vertexIndices.TryGetValue(vertex, out int index))
                return index;

            index = vertices.Count;
            vertexIndices.Add(vertex, index);
            vertices.Add(vertex);
            uvs.Add(new Vector2(
                (originX + vertex.x) * 0.1f,
                (originZ + vertex.z) * 0.1f));
            return index;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
