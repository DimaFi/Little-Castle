using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.World
{
    /// <summary>
    /// One and only one opt-in river water owner for a WorldStreamer.
    /// Creates a clipped water ribbon under each existing streamed chunk view;
    /// never creates an independent whole-world mesh or a second bridge water.
    /// No changes to authoritative terrain/river data or WorldStreamer.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WorldStreamer))]
    public sealed class RiverWaterPresenter : MonoBehaviour
    {
        [SerializeField] private WorldStreamer streamer;
        [SerializeField] private Material sharedWaterMaterial;
        [SerializeField, Min(0f)] private float surfaceRiseAboveBed = 0.55f;
        [SerializeField, Min(0.1f)] private float bridgeBlendMeters = 20f;
        [SerializeField, Range(1, 8)] private int maxNewChunkMeshesPerFrame = 2;
        [SerializeField, Min(0.05f)] private float scanIntervalSeconds = 0.25f;

        private readonly HashSet<StreamedChunkView> processed =
            new HashSet<StreamedChunkView>();
        private readonly List<StreamedChunkView> scratch =
            new List<StreamedChunkView>();
        private MacroWorldPlan observedPlan;
        private double nextScan;

        public int TrackedChunkCount => processed.Count;

        private void Awake()
        {
            if (streamer == null)
                streamer = GetComponent<WorldStreamer>();
        }

        private void OnDisable()
        {
            // The chunk view owns its water child mesh. Only disable the
            // presentation; do not destroy streamer-owned chunk data.
            foreach (StreamedChunkView view in processed)
            {
                if (view == null) continue;
                Transform child = view.transform.Find(RiverWaterChunkResource.ChildName);
                if (child != null) child.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            // Revisit previously managed chunks so disabled water children
            // are reactivated after the presenter is toggled off and on.
            processed.Clear();
            nextScan = 0;
        }

        private void LateUpdate()
        {
            if (streamer == null || sharedWaterMaterial == null)
                return;

            MacroWorldPlan plan = streamer.MacroPlan;
            if (plan == null || streamer.Definition == null ||
                streamer.Definition.GenerationSettings == null)
            {
                processed.Clear();
                observedPlan = null;
                return;
            }

            if (!ReferenceEquals(observedPlan, plan))
            {
                processed.Clear();
                observedPlan = plan;
            }

            if (Time.unscaledTimeAsDouble < nextScan)
                return;

            nextScan = Time.unscaledTimeAsDouble +
                Mathf.Max(0.05f, scanIntervalSeconds);

            scratch.Clear();
            streamer.GetComponentsInChildren(true, scratch);

            // Reclaim stale managed references after streamer unloads. The
            // associated water GameObjects/meshes are owned by chunk parents.
            processed.RemoveWhere(view => view == null || !scratch.Contains(view));

            int created = 0;
            for (int i = 0; i < scratch.Count; i++)
            {
                StreamedChunkView view = scratch[i];
                if (view == null || processed.Contains(view))
                    continue;

                Transform existing = view.transform.Find(
                    RiverWaterChunkResource.ChildName);
                if (existing != null)
                {
                    existing.gameObject.SetActive(true);
                    processed.Add(view);
                    continue;
                }

                if (created >= Mathf.Clamp(maxNewChunkMeshesPerFrame, 1, 8))
                    break;

                float chunkSize = streamer.Definition.GenerationSettings.ChunkWorldSize;
                MeshFilter terrain = view.GetComponent<MeshFilter>();
                Mesh mesh = RiverWaterMeshBuilder.Build(
                    plan, view.Coordinate, chunkSize,
                    terrain != null ? terrain.sharedMesh : null,
                    surfaceRiseAboveBed, bridgeBlendMeters);

                processed.Add(view);
                if (mesh == null)
                    continue;

                var water = new GameObject(RiverWaterChunkResource.ChildName);
                water.transform.SetParent(view.transform, false);
                var filter = water.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                var renderer = water.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = sharedWaterMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode =
                    MotionVectorGenerationMode.ForceNoMotion;
                water.AddComponent<RiverWaterChunkResource>().Initialize(mesh);
                created++;
            }
        }
    }

    /// <summary>
    /// Holds only runtime river mesh resources. Destroying a streamed chunk
    /// automatically destroys this child and its mesh; the material is shared.
    /// </summary>
    public sealed class RiverWaterChunkResource : MonoBehaviour
    {
        public const string ChildName = "GlobalRiverWater";
        private Mesh owned;

        public void Initialize(Mesh mesh) => owned = mesh;

        private void OnDestroy()
        {
            if (owned == null) return;
            if (Application.isPlaying)
                Destroy(owned);
            else
                DestroyImmediate(owned);
            owned = null;
        }
    }

    /// <summary>
    /// Pure presentation geometry. Each river segment becomes a mitered quad
    /// whose polygon is clipped to the current chunk's half-open X/Z extent.
    /// The same original world-space vertices are used on both sides of a
    /// chunk boundary, including negative chunk coordinates.
    /// </summary>
    public static class RiverWaterMeshBuilder
    {
        public static Mesh Build(
            MacroWorldPlan plan,
            ChunkCoordinate coordinate,
            float chunkSize,
            Mesh terrainMesh,
            float riseAboveBed,
            float bridgeBlendMeters)
        {
            if (plan == null || chunkSize <= 0f || plan.Rivers.Count == 0)
                return null;

            float minX = coordinate.x * chunkSize;
            float minZ = coordinate.z * chunkSize;
            float maxX = minX + chunkSize;
            float maxZ = minZ + chunkSize;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uv = new List<Vector2>();
            var polygon = new List<Vector2>(8);
            var clipped = new List<Vector2>(8);
            Vector3[] heights = terrainMesh != null ? terrainMesh.vertices : null;
            int gridSize = 0;
            if (heights != null)
            {
                int side = Mathf.RoundToInt(Mathf.Sqrt(heights.Length));
                if (side >= 2 && side * side == heights.Length)
                    gridSize = side - 1;
            }

            for (int r = 0; r < plan.Rivers.Count; r++)
            {
                WorldRiverData river = plan.Rivers[r];
                if (river == null || river.centerline == null ||
                    river.centerline.Count < 2)
                    continue;

                for (int s = 0; s < river.centerline.Count - 1; s++)
                {
                    Vector2 a = river.centerline[s];
                    Vector2 b = river.centerline[s + 1];
                    if ((b - a).sqrMagnitude < 0.000001f)
                        continue;

                    Vector2 offsetA = JointOffset(river, s);
                    Vector2 offsetB = JointOffset(river, s + 1);

                    polygon.Clear();
                    polygon.Add(a + offsetA);
                    polygon.Add(b + offsetB);
                    polygon.Add(b - offsetB);
                    polygon.Add(a - offsetA);

                    // Clip the oriented quad against exact chunk limits.
                    Clip(polygon, clipped, 0, minX);
                    Clip(clipped, polygon, 1, maxX);
                    Clip(polygon, clipped, 2, minZ);
                    Clip(clipped, polygon, 3, maxZ);
                    if (polygon.Count < 3)
                        continue;

                    int first = vertices.Count;
                    for (int i = 0; i < polygon.Count; i++)
                    {
                        Vector2 world = polygon[i];
                        float height = SampleSurfaceY(world, river.stableId,
                            plan, minX, minZ, chunkSize, heights, gridSize,
                            riseAboveBed, bridgeBlendMeters);
                        vertices.Add(new Vector3(
                            world.x - minX, height, world.y - minZ));
                        uv.Add(world * 0.05f);
                    }

                    // Vertices are ordered to face +Y. Fan triangulation
                    // retains a common continuous edge at every river joint.
                    for (int i = 1; i < polygon.Count - 1; i++)
                    {
                        triangles.Add(first);
                        triangles.Add(first + i);
                        triangles.Add(first + i + 1);
                    }
                }
            }

            if (triangles.Count == 0)
                return null;

            var mesh = new Mesh { name =
                "GlobalRiverWater_" + coordinate.x + "_" + coordinate.z };
            if (vertices.Count > 65535)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uv);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector2 JointOffset(WorldRiverData river, int i)
        {
            var points = river.centerline;
            Vector2 before = i > 0
                ? points[i] - points[i - 1] : points[1] - points[0];
            Vector2 after = i < points.Count - 1
                ? points[i + 1] - points[i] : points[i] - points[i - 1];

            if (before.sqrMagnitude < 0.000001f) before = after;
            if (after.sqrMagnitude < 0.000001f) after = before;
            Vector2 nA = new Vector2(-before.y, before.x).normalized;
            Vector2 nB = new Vector2(-after.y, after.x).normalized;
            Vector2 bisector = nA + nB;
            if (bisector.sqrMagnitude < 0.000001f)
                bisector = nB;
            bisector.Normalize();

            float miter = Mathf.Max(0.5f, Vector2.Dot(bisector, nB));
            float width = river.GetWidthAtPoint(i);
            return bisector * Mathf.Min(width, width / (2f * miter));
        }

        // Sutherland-Hodgman clipping; outputs consecutive points only.
        private static void Clip(
            List<Vector2> source,
            List<Vector2> target,
            int plane,
            float boundary)
        {
            target.Clear();
            if (source.Count == 0) return;

            Vector2 previous = source[source.Count - 1];
            float prevDistance = Distance(previous, plane, boundary);
            for (int i = 0; i < source.Count; i++)
            {
                Vector2 current = source[i];
                float nextDistance = Distance(current, plane, boundary);
                bool prevInside = prevDistance >= 0f;
                bool nextInside = nextDistance >= 0f;
                if (prevInside != nextInside)
                {
                    float amount = prevDistance / (prevDistance - nextDistance);
                    AppendUnique(target,
                        Vector2.LerpUnclamped(previous, current, amount));
                }

                if (nextInside)
                    AppendUnique(target, current);

                previous = current;
                prevDistance = nextDistance;
            }
            if (target.Count >= 2 &&
                (target[0] - target[target.Count - 1]).sqrMagnitude < 0.00000001f)
                target.RemoveAt(target.Count - 1);
        }

        private static float Distance(Vector2 p, int plane, float edge)
        {
            switch (plane)
            {
                case 0: return p.x - edge;
                case 1: return edge - p.x;
                case 2: return p.y - edge;
                default: return edge - p.y;
            }
        }

        private static void AppendUnique(List<Vector2> target, Vector2 p)
        {
            if (target.Count == 0 ||
                (target[target.Count - 1] - p).sqrMagnitude > 0.00000001f)
                target.Add(p);
        }

        private static float SampleSurfaceY(
            Vector2 world,
            long riverId,
            MacroWorldPlan plan,
            float minX,
            float minZ,
            float chunkSize,
            Vector3[] terrain,
            int cells,
            float rise,
            float blendMeters)
        {
            float y = 0f;
            if (terrain != null && cells > 0)
            {
                float gx = Mathf.Clamp(
                    (world.x - minX) / chunkSize * cells, 0, cells);
                float gz = Mathf.Clamp(
                    (world.y - minZ) / chunkSize * cells, 0, cells);
                int x0 = Mathf.Min(cells - 1, Mathf.FloorToInt(gx));
                int z0 = Mathf.Min(cells - 1, Mathf.FloorToInt(gz));
                float tx = gx - x0;
                float tz = gz - z0;
                int stride = cells + 1;
                float h0 = Mathf.Lerp(
                    terrain[z0 * stride + x0].y,
                    terrain[z0 * stride + x0 + 1].y, tx);
                float h1 = Mathf.Lerp(
                    terrain[(z0 + 1) * stride + x0].y,
                    terrain[(z0 + 1) * stride + x0 + 1].y, tx);
                y = Mathf.Lerp(h0, h1, tz) + Mathf.Max(0f, rise);
            }

            // Bridge v002 is the source of truth for water at its support.
            // Near it, smoothly blend back toward existing carved terrain.
            float weight = 0f;
            float siteY = 0f;
            for (int i = 0; i < plan.BridgeSites.Count; i++)
            {
                WorldBridgeSiteData site = plan.BridgeSites[i];
                if (site.riverId != riverId ||
                    !BridgeSitePresentationUtility.TryGetWorldAnchors(
                        site, out _, out _, out _, out _,
                        out _, out float waterY))
                    continue;
                float distance = Vector2.Distance(world, site.worldPosition);
                float inner = FixedBridgeSiteProfile.SupportHalfExtentX;
                float outer = inner + Mathf.Max(0.1f, blendMeters);
                float t = Mathf.Clamp01((distance - inner) / (outer - inner));
                float w = 1f - t * t * (3f - 2f * t);
                if (w <= weight) continue;
                weight = w;
                siteY = waterY;
            }
            return Mathf.Lerp(y, siteY, weight);
        }
    }
}
