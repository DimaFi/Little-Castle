using System;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Data-only foundation/entrance check for generated villages and buildings.
    /// No Physics, scene objects, UnityEngine.Random, terrain mutation or
    /// per-sample allocations. The adapter must provide world-space samples
    /// with consistent masks and heights on neighboring chunk boundaries.
    /// </summary>
    public static class BuildingFootprintValidator
    {
        private const int EntranceColumns = 3;
        private const int EntranceRows = 3;

        public static BuildingFootprintResult Validate(
            Vector2 worldCenter,
            float yawDegrees,
            BuildingFootprintDefinition definition,
            Func<Vector2, BuildingSiteSample> sampleWorld)
        {
            if (!definition.IsValid ||
                !IsFinite(worldCenter.x) ||
                !IsFinite(worldCenter.y) ||
                !IsFinite(yawDegrees) ||
                sampleWorld == null)
            {
                return new BuildingFootprintResult(
                    BuildingFootprintRejection.InvalidRequest,
                    worldCenter, 0, float.NaN, float.NaN);
            }

            float radians = yawDegrees * Mathf.Deg2Rad;
            float sine = Mathf.Sin(radians);
            float cosine = Mathf.Cos(radians);
            int count = 0;
            int grid = definition.samplesPerAxis;
            int middle = grid / 2;
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            float frontHeight = float.NaN;

            // All boundary and interior support samples use the same local
            // space and winding; an odd grid always includes the center.
            for (int z = 0; z < grid; z++)
            {
                float localZ = -definition.halfExtents.y +
                    (2f * definition.halfExtents.y * z) / (grid - 1);
                for (int x = 0; x < grid; x++)
                {
                    float localX = -definition.halfExtents.x +
                        (2f * definition.halfExtents.x * x) / (grid - 1);
                    Vector2 point = RotateTranslate(
                        worldCenter, localX, localZ, sine, cosine);

                    count++;
                    BuildingSiteSample sample = sampleWorld(point);
                    BuildingFootprintRejection rejected = CheckSample(
                        sample, false, definition.maximumSlopeDegrees);
                    if (rejected != BuildingFootprintRejection.None)
                        return Failed(rejected, point, count, minimum, maximum);

                    minimum = Mathf.Min(minimum, sample.height);
                    maximum = Mathf.Max(maximum, sample.height);
                    if (x == middle && z == grid - 1)
                        frontHeight = sample.height;
                }
            }

            if (maximum - minimum > definition.maximumHeightSpread)
            {
                return Failed(
                    BuildingFootprintRejection.HeightSpreadExceeded,
                    worldCenter, count, minimum, maximum);
            }

            // The door is at local +Z. Require an oriented 3-by-3 passable
            // corridor ahead of that edge. Roads are allowed on this corridor
            // (indeed often desired), but water and occupied cells are not.
            // Row order follows each lane outward to check consecutive steps.
            for (int x = 0; x < EntranceColumns; x++)
            {
                float localX = (x - 1) * definition.entranceWidth * 0.5f;
                float previousHeight = frontHeight;
                for (int z = 0; z < EntranceRows; z++)
                {
                    float localZ = definition.halfExtents.y +
                        definition.entranceDepth * (z + 1) / EntranceRows;
                    Vector2 point = RotateTranslate(
                        worldCenter, localX, localZ, sine, cosine);
                    count++;
                    BuildingSiteSample sample = sampleWorld(point);
                    BuildingFootprintRejection rejected = CheckSample(
                        sample, true, definition.maximumSlopeDegrees);
                    if (rejected != BuildingFootprintRejection.None)
                        return Failed(rejected, point, count, minimum, maximum);

                    if (Mathf.Abs(sample.height - previousHeight) >
                        definition.maximumEntranceStep)
                    {
                        return Failed(
                            BuildingFootprintRejection.EntranceStepExceeded,
                            point, count, minimum, maximum);
                    }

                    previousHeight = sample.height;
                }
            }

            return new BuildingFootprintResult(
                BuildingFootprintRejection.None,
                worldCenter, count, minimum, maximum);
        }

        private static BuildingFootprintRejection CheckSample(
            BuildingSiteSample sample,
            bool entrance,
            float maximumSlope)
        {
            if (!sample.Has(BuildingSiteFlags.Ready) ||
                !IsFinite(sample.height) ||
                !IsFinite(sample.slopeDegrees) ||
                sample.slopeDegrees < 0f || sample.slopeDegrees > 90f)
            {
                return BuildingFootprintRejection.SampleUnavailable;
            }

            if (!sample.Has(BuildingSiteFlags.Playable))
                return BuildingFootprintRejection.OutsidePlayableBounds;

            if (sample.Has(BuildingSiteFlags.Water))
                return BuildingFootprintRejection.Water;

            if (sample.Has(BuildingSiteFlags.Occupied))
                return BuildingFootprintRejection.Occupied;

            if (!entrance && sample.Has(BuildingSiteFlags.Road))
                return BuildingFootprintRejection.RoadUnderFoundation;

            if (entrance && !sample.Has(BuildingSiteFlags.Walkable))
                return BuildingFootprintRejection.EntranceNotWalkable;

            if (!entrance && !sample.Has(BuildingSiteFlags.Buildable))
                return BuildingFootprintRejection.UnbuildableTerrain;

            if (sample.slopeDegrees > maximumSlope)
                return BuildingFootprintRejection.SlopeTooSteep;

            return BuildingFootprintRejection.None;
        }

        private static Vector2 RotateTranslate(
            Vector2 center,
            float localX,
            float localZ,
            float sine,
            float cosine)
        {
            return new Vector2(
                center.x + localX * cosine + localZ * sine,
                center.y - localX * sine + localZ * cosine);
        }

        private static BuildingFootprintResult Failed(
            BuildingFootprintRejection reason,
            Vector2 point,
            int count,
            float minimum,
            float maximum)
        {
            if (float.IsInfinity(minimum) || float.IsInfinity(maximum))
            {
                minimum = float.NaN;
                maximum = float.NaN;
            }

            return new BuildingFootprintResult(
                reason, point, count, minimum, maximum);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
