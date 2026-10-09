using System;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Pure, deterministic data-only neutral village layout. No physics,
    /// prefab spawning, flattening, runtime Random or streamed collider order.
    /// Returns no partial village if any required house/approach fails.
    /// </summary>
    public static class VillageLayoutPlanner
    {
        public const int MaximumAlternativesPerHouse = 8;
        private const int CourtyardRadialSamples = 12;
        private const int MaximumPathSteps = 64;
        private const int VillageSalt = 0x314765;
        private const int HouseSalt = 0x417D11;
        private const int PathSalt = 0x417D12;
        private const int WellSalt = 0x417D13;

        private static readonly float[] AngleAlternatives =
            { 0f, -8f, 8f, -16f, 16f, 0f, -12f, 12f };

        private static readonly float[] DistanceAlternatives =
            { 0f, 0f, 0f, 0f, 0f, 2.5f, 4f, 6f };

        public static bool TryPlan(
            int worldSeed,
            long villageStableId,
            Vector2 worldCenter,
            VillageLayoutSettings settings,
            Func<Vector2, BuildingSiteSample> sampleWorld,
            out VillageLayoutData result,
            out VillageLayoutFailure failure)
        {
            result = null;
            failure = default;
            if (villageStableId == 0L ||
                !settings.IsValid || sampleWorld == null ||
                !Finite(worldCenter.x) || !Finite(worldCenter.y))
            {
                failure = Fail(VillageLayoutRejection.InvalidRequest,
                    BuildingFootprintRejection.InvalidRequest, -1, 0,
                    worldCenter);
                return false;
            }

            if (!ValidateCourtyard(
                    worldCenter, settings, sampleWorld,
                    out float wellHeight, out Vector2 blocked))
            {
                failure = Fail(VillageLayoutRejection.WellOrCourtyardBlocked,
                    BuildingFootprintRejection.None, -1, 0, blocked);
                return false;
            }

            long wellId = DeterministicHash.StablePairId(
                worldSeed, villageStableId, -1L, WellSalt);
            var candidate = new VillageLayoutData(
                worldSeed, villageStableId, worldCenter, wellId,
                wellHeight, settings.courtyardRadius);

            int lowId = unchecked((int)villageStableId);
            int highId = unchecked((int)(villageStableId >> 32));
            float globalAngle = DeterministicHash.Hash01(
                worldSeed, lowId, highId, VillageSalt) * 360f;
            int attempts = 0;

            for (int slot = 0; slot < settings.houseCount; slot++)
            {
                bool placed = false;
                VillageLayoutRejection lastReason =
                    VillageLayoutRejection.ExhaustedAlternatives;
                BuildingFootprintRejection lastFootprint =
                    BuildingFootprintRejection.None;
                Vector2 lastPosition = worldCenter;

                for (int option = 0;
                     option < MaximumAlternativesPerHouse; option++)
                {
                    attempts++;
                    float degrees =
                        globalAngle + 360f * slot / settings.houseCount +
                        AngleAlternatives[option];
                    float radians = degrees * Mathf.Deg2Rad;
                    Vector2 outward = new Vector2(
                        Mathf.Sin(radians), Mathf.Cos(radians));
                    Vector2 inward = -outward;

                    float radius = settings.courtyardRadius +
                        settings.houseFootprint.halfExtents.y +
                        settings.houseFootprint.entranceDepth +
                        2.5f + settings.minimumBuildingClearance +
                        DistanceAlternatives[option];
                    Vector2 houseCenter =
                        worldCenter + outward * radius;
                    lastPosition = houseCenter;
                    if (!Finite(houseCenter.x) || !Finite(houseCenter.y))
                    {
                        lastReason = VillageLayoutRejection.InvalidRequest;
                        break;
                    }

                    float yaw = Mathf.Atan2(
                        inward.x, inward.y) * Mathf.Rad2Deg;
                    Vector2 door = houseCenter + inward *
                        settings.houseFootprint.halfExtents.y;
                    Vector2 square = worldCenter + outward *
                        settings.courtyardRadius;

                    long houseId = DeterministicHash.StablePairId(
                        worldSeed, villageStableId, slot + 1L, HouseSalt);
                    long pathId = DeterministicHash.StablePairId(
                        worldSeed, villageStableId, slot + 1L, PathSalt);

                    var approach = new VillageApproachPath(
                        pathId, houseId, door, square,
                        settings.approachHalfWidth);

                    VillageLayoutRejection overlap = CheckOverlap(
                        candidate, houseCenter, yaw, approach, settings);
                    if (overlap != VillageLayoutRejection.None)
                    {
                        lastReason = overlap;
                        continue;
                    }

                    BuildingFootprintResult footprint =
                        BuildingFootprintValidator.Validate(
                            houseCenter, yaw, settings.houseFootprint,
                            sampleWorld);
                    if (!footprint.IsValid)
                    {
                        lastReason =
                            VillageLayoutRejection.UnsupportedHouseFootprint;
                        lastFootprint = footprint.rejection;
                        lastPosition = footprint.rejectedAt;
                        continue;
                    }

                    if (!ValidateApproach(
                            approach, settings, sampleWorld,
                            out Vector2 badApproach))
                    {
                        lastReason = VillageLayoutRejection.ApproachBlocked;
                        lastPosition = badApproach;
                        continue;
                    }

                    candidate.Add(
                        new VillageHousePlacement(
                            houseId, slot, houseCenter, yaw,
                            footprint.minimumHeight),
                        approach);
                    placed = true;
                    break;
                }

                if (!placed)
                {
                    failure = Fail(VillageLayoutRejection.ExhaustedAlternatives,
                        lastFootprint, slot, attempts, lastPosition,
                        lastReason);
                    return false;
                }
            }

            result = candidate;
            return true;
        }

        private static bool ValidateCourtyard(
            Vector2 center,
            VillageLayoutSettings settings,
            Func<Vector2, BuildingSiteSample> sample,
            out float wellHeight,
            out Vector2 badPoint)
        {
            wellHeight = 0f;
            badPoint = center;
            BuildingSiteSample core = sample(center);
            if (!WellGround(core, settings.maximumCourtyardSlope))
                return false;

            float minHeight = core.height;
            float maxHeight = core.height;

            for (int i = 0; i < CourtyardRadialSamples; i++)
            {
                float radians = (i * 360f / CourtyardRadialSamples) *
                    Mathf.Deg2Rad;
                Vector2 radial = new Vector2(
                    Mathf.Sin(radians), Mathf.Cos(radians));
                Vector2 well = center + radial * settings.wellRadius;
                if (!Finite(well.x) || !Finite(well.y))
                    return false;

                BuildingSiteSample inner = sample(well);
                if (!WellGround(inner, settings.maximumCourtyardSlope))
                {
                    badPoint = well;
                    return false;
                }

                minHeight = Mathf.Min(minHeight, inner.height);
                maxHeight = Mathf.Max(maxHeight, inner.height);

                Vector2 ring = center + radial * settings.courtyardRadius;
                if (!Finite(ring.x) || !Finite(ring.y) ||
                    !Walkable(sample(ring), settings.maximumCourtyardSlope))
                {
                    badPoint = ring;
                    return false;
                }
            }

            if (maxHeight - minHeight >
                settings.maximumWellHeightSpread)
                return false;

            wellHeight = minHeight;
            return true;
        }

        private static bool ValidateApproach(
            VillageApproachPath path,
            VillageLayoutSettings settings,
            Func<Vector2, BuildingSiteSample> sample,
            out Vector2 badPoint)
        {
            badPoint = path.doorEdgeWorld;
            Vector2 span =
                path.courtyardEdgeWorld - path.doorEdgeWorld;
            float length = span.magnitude;
            if (!Finite(length) ||
                length > MaximumPathSteps * settings.pathSampleInterval)
                return false;

            int segments = Mathf.Max(1,
                Mathf.CeilToInt(length / settings.pathSampleInterval));
            Vector2 lateral = length > 0.000001f
                ? new Vector2(-span.y, span.x) / length
                : Vector2.right;

            float[] previous = { float.NaN, float.NaN, float.NaN };
            for (int i = 0; i <= segments; i++)
            {
                Vector2 center =
                    path.doorEdgeWorld + span * (i / (float)segments);

                for (int lane = 0; lane < 3; lane++)
                {
                    Vector2 point = center +
                        lateral * ((lane - 1) * path.halfWidth);
                    badPoint = point;
                    if (!Finite(point.x) || !Finite(point.y))
                        return false;
                    BuildingSiteSample at = sample(point);
                    if (!Walkable(at, settings.maximumCourtyardSlope))
                        return false;
                    if (i > 0 && Mathf.Abs(at.height - previous[lane]) >
                        settings.maximumPathStep)
                        return false;

                    previous[lane] = at.height;
                }
            }

            return true;
        }

        private static VillageLayoutRejection CheckOverlap(
            VillageLayoutData existing,
            Vector2 house,
            float yaw,
            VillageApproachPath path,
            VillageLayoutSettings settings)
        {
            float clearance = settings.minimumBuildingClearance;
            Vector2 half = settings.houseFootprint.halfExtents;
            float combinedPathRadius =
                settings.approachHalfWidth * 2f + clearance;
            for (int i = 0; i < existing.Houses.Count; i++)
            {
                VillageHousePlacement prev = existing.Houses[i];
                VillageApproachPath prevPath = existing.Approaches[i];
                if (OrientedBoxesOverlap(
                        house, yaw, prev.worldCenter, prev.yawDegrees,
                        half, clearance))
                    return VillageLayoutRejection.HouseOverlapsBuilding;

                if (SegmentIntersectsBox(
                        prevPath.doorEdgeWorld, prevPath.courtyardEdgeWorld,
                        house, yaw,
                        half + Vector2.one *
                        (prevPath.halfWidth + clearance)) ||
                    SegmentIntersectsBox(
                        path.doorEdgeWorld, path.courtyardEdgeWorld,
                        prev.worldCenter, prev.yawDegrees,
                        half + Vector2.one *
                        (path.halfWidth + clearance)) ||
                    SegmentsTooClose(
                        path.doorEdgeWorld, path.courtyardEdgeWorld,
                        prevPath.doorEdgeWorld, prevPath.courtyardEdgeWorld,
                        combinedPathRadius))
                    return VillageLayoutRejection.HouseOverlapsApproach;
            }

            return VillageLayoutRejection.None;
        }

        private static bool OrientedBoxesOverlap(
            Vector2 a, float yawA,
            Vector2 b, float yawB,
            Vector2 half, float clearance)
        {
            Basis(yawA, out Vector2 ax, out Vector2 az);
            Basis(yawB, out Vector2 bx, out Vector2 bz);
            Vector2 delta = b - a;
            float margin = clearance * 0.5f;
            Vector2 size = half + Vector2.one * margin;
            return OverlapsOnAxis(delta, ax, az, bx, bz, size, ax) &&
                   OverlapsOnAxis(delta, ax, az, bx, bz, size, az) &&
                   OverlapsOnAxis(delta, ax, az, bx, bz, size, bx) &&
                   OverlapsOnAxis(delta, ax, az, bx, bz, size, bz);
        }

        private static bool OverlapsOnAxis(
            Vector2 delta,
            Vector2 ax, Vector2 az,
            Vector2 bx, Vector2 bz,
            Vector2 size, Vector2 axis)
        {
            float aExtent =
                size.x * Mathf.Abs(Vector2.Dot(ax, axis)) +
                size.y * Mathf.Abs(Vector2.Dot(az, axis));
            float bExtent =
                size.x * Mathf.Abs(Vector2.Dot(bx, axis)) +
                size.y * Mathf.Abs(Vector2.Dot(bz, axis));
            return Mathf.Abs(Vector2.Dot(delta, axis)) <=
                aExtent + bExtent;
        }

        private static bool SegmentIntersectsBox(
            Vector2 a,
            Vector2 b,
            Vector2 center,
            float yaw,
            Vector2 half)
        {
            Basis(yaw, out Vector2 lateral, out Vector2 forward);
            Vector2 ra = a - center;
            Vector2 rb = b - center;
            Vector2 localA = new Vector2(
                Vector2.Dot(ra, lateral), Vector2.Dot(ra, forward));
            Vector2 localB = new Vector2(
                Vector2.Dot(rb, lateral), Vector2.Dot(rb, forward));
            Vector2 d = localB - localA;
            float lo = 0f, hi = 1f;
            return Clip(-d.x, localA.x + half.x, ref lo, ref hi) &&
                   Clip(d.x, half.x - localA.x, ref lo, ref hi) &&
                   Clip(-d.y, localA.y + half.y, ref lo, ref hi) &&
                   Clip(d.y, half.y - localA.y, ref lo, ref hi);
        }

        private static bool Clip(
            float p, float q, ref float lo, ref float hi)
        {
            if (Mathf.Abs(p) <= 0.0000001f)
                return q >= 0f;

            float t = q / p;
            if (p < 0f)
                lo = Mathf.Max(lo, t);
            else
                hi = Mathf.Min(hi, t);
            return lo <= hi;
        }

        private static bool SegmentsTooClose(
            Vector2 a, Vector2 b,
            Vector2 c, Vector2 d,
            float threshold)
        {
            float t2 = threshold * threshold;
            if (PointSegmentSqr(a, c, d) <= t2 ||
                PointSegmentSqr(b, c, d) <= t2 ||
                PointSegmentSqr(c, a, b) <= t2 ||
                PointSegmentSqr(d, a, b) <= t2)
                return true;

            Vector2 r = b - a;
            Vector2 s = d - c;
            float denominator = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(denominator) < 0.0000001f)
                return false;

            Vector2 q = c - a;
            float u = (q.x * r.y - q.y * r.x) / denominator;
            float t = (q.x * s.y - q.y * s.x) / denominator;
            return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
        }

        private static float PointSegmentSqr(
            Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float denominator = ab.sqrMagnitude;
            float t = denominator > 0.000001f
                ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / denominator)
                : 0f;
            return (point - (a + ab * t)).sqrMagnitude;
        }

        private static void Basis(
            float yaw, out Vector2 lateral, out Vector2 forward)
        {
            float radians = (yaw % 360f) * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            lateral = new Vector2(cosine, -sine);
            forward = new Vector2(sine, cosine);
        }

        private static bool WellGround(
            BuildingSiteSample site, float slope)
        {
            return Walkable(site, slope) &&
                site.Has(BuildingSiteFlags.Buildable) &&
                !site.Has(BuildingSiteFlags.Road);
        }

        private static bool Walkable(
            BuildingSiteSample site, float slope)
        {
            return site.Has(BuildingSiteFlags.Ready |
                            BuildingSiteFlags.Playable |
                            BuildingSiteFlags.Walkable) &&
                !site.Has(BuildingSiteFlags.Water |
                          BuildingSiteFlags.Occupied) &&
                Finite(site.height) && Finite(site.slopeDegrees) &&
                site.slopeDegrees >= 0f &&
                site.slopeDegrees <= slope;
        }

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static VillageLayoutFailure Fail(
            VillageLayoutRejection reason,
            BuildingFootprintRejection footprint,
            int slot,
            int attempts,
            Vector2 position,
            VillageLayoutRejection last = VillageLayoutRejection.None)
        {
            return new VillageLayoutFailure(
                reason, footprint, slot, attempts, last, position);
        }
    }
}
