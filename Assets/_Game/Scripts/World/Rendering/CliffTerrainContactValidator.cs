using System;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Additive intake contract for Q08. Reads the FINAL gameplay heightfield;
    /// never stamps terrain, places objects, changes resources, or grants navigation.
    /// All footprint samples (including across chunks) must already be available.
    /// </summary>
    public static class CliffTerrainContactValidator
    {
        public delegate bool SampleGround(Vector2 world, out float height);
        public delegate bool IsReserved(Vector2 world);

        public readonly struct Result
        {
            public readonly bool Accepted;
            public readonly string Reason;
            public readonly float MaximumContactError, MaximumGradeDegrees;
            public Result(bool accepted, string reason, float error, float grade)
            { Accepted = accepted; Reason = reason; MaximumContactError = error; MaximumGradeDegrees = grade; }
        }

        /// <summary>
        /// Checks the entire rectangular footprint on a <=0.5m sample grid.
        /// This cannot prove absence of sub-grid holes or obstacles: the caller
        /// additionally owns exact corridor/physics/nav clearance and macro masks.
        /// </summary>
        public static Result ValidateRamp(Vector3 toe, float yaw, float width,
            float length, float rise, float clearanceWidth, float tolerance,
            float agentMaxGrade, SampleGround ground, IsReserved reserved)
        {
            if (!Valid(toe, yaw, width, length, rise, tolerance) ||
                !Finite(clearanceWidth) || clearanceWidth <= 0 || clearanceWidth > width ||
                !Finite(agentMaxGrade) || agentMaxGrade < 0 || agentMaxGrade >= 90 ||
                ground == null || reserved == null)
                return new Result(false, "invalid-or-missing-contract", 0, 0);
            // v001 shoulders bow by <=0.34m beyond nominal width. Reserve their full bounds.
            float footprintWidth = width + .7f;
            int nx = Mathf.CeilToInt(footprintWidth / .5f), nz = Mathf.CeilToInt(length / .5f);
            float maxError = 0, maxGrade = 0;
            // Full shoulder footprint is reserved before accepting a walk strip.
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= nx; x++)
                    if (reserved(World(toe, yaw, -footprintWidth / 2 + footprintWidth * x / nx, length * z / nz)))
                        return new Result(false, "reserved-footprint", maxError, maxGrade);
            int walkNx = Mathf.Max(1, Mathf.CeilToInt(clearanceWidth / .5f));
            float[,] heights = new float[nz + 1, walkNx + 1];
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= walkNx; x++)
                {
                    float localZ = length * z / nz;
                    var world = World(toe, yaw, -clearanceWidth / 2 + clearanceWidth * x / walkNx, localZ);
                    if (!ground(world, out float h) || !Finite(h))
                        return new Result(false, "ground-not-ready", maxError, maxGrade);
                    heights[z, x] = h;
                    maxError = Mathf.Max(maxError, Mathf.Abs(h - (toe.y + rise * localZ / length)));
                    if (maxError > tolerance)
                        return new Result(false, "ramp-does-not-match-ground", maxError, maxGrade);
                }
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < walkNx; x++)
                {
                    float dz = (heights[z + 1, x] - heights[z, x]) / (length / nz);
                    float dzFar = (heights[z + 1, x + 1] - heights[z, x + 1]) / (length / nz);
                    float dx = (heights[z, x + 1] - heights[z, x]) / (clearanceWidth / walkNx);
                    float dxFar = (heights[z + 1, x + 1] - heights[z + 1, x]) / (clearanceWidth / walkNx);
                    float grade = Mathf.Atan(Mathf.Sqrt(Mathf.Max(dx * dx, dxFar * dxFar) + Mathf.Max(dz * dz, dzFar * dzFar))) * Mathf.Rad2Deg;
                    maxGrade = Mathf.Max(maxGrade, grade);
                    if (maxGrade > agentMaxGrade)
                        return new Result(false, "agent-grade-exceeded", maxError, maxGrade);
                }
            return new Result(true, "contact-only-nav-clearance-still-required", maxError, maxGrade);
        }

        /// <summary>Only straight/terrace full-height modules. Curved/end caps need their exact authored contact curves.</summary>
        public static Result ValidateStraightCliff(Vector3 toe, float yaw, float width,
            float depth, float rise, float tolerance, SampleGround ground, IsReserved reserved)
        {
            if (!Valid(toe, yaw, width, depth, rise, tolerance) || ground == null || reserved == null)
                return new Result(false, "invalid-or-missing-contract", 0, 0);
            // The v001 front buttresses extend beyond the toe line by less than 2m.
            int nx = Mathf.CeilToInt(width / .5f), nz = Mathf.CeilToInt((depth + 2f) / .5f);
            float maxError = 0;
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= nx; x++)
                {
                    float localX = -width / 2 + width * x / nx;
                    float localZ = -2f + (depth + 2f) * z / nz;
                    var world = World(toe, yaw, localX, localZ);
                    if (reserved(world)) return new Result(false, "reserved-footprint", maxError, 0);
                    if (!ground(world, out float h) || !Finite(h))
                        return new Result(false, "ground-not-ready", maxError, 0);
                    // Toe and upper shelf: front riser interior is decoration, never walkable.
                    if (z == 0 || localZ >= 1f)
                    {
                        float target = toe.y + (z == 0 ? 0 : rise);
                        maxError = Mathf.Max(maxError, Mathf.Abs(h - target));
                        if (maxError > tolerance)
                            return new Result(false, "cliff-does-not-match-ground", maxError, 0);
                    }
                }
            return new Result(true, "contact-only-authored-cap-review-required", maxError, 0);
        }

        private static Vector2 World(Vector3 toe, float yaw, float x, float z)
        {
            float r = yaw * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(toe.x + c * x + s * z, toe.z - s * x + c * z);
        }
        private static bool Valid(Vector3 toe, float yaw, float width, float length, float rise, float tolerance) =>
            Finite(toe.x) && Finite(toe.y) && Finite(toe.z) && Finite(yaw) &&
            Finite(width) && width > 0 && width <= 32 && Finite(length) && length > 0 && length <= 64 &&
            Finite(rise) && rise > 0 && rise <= 32 && Finite(tolerance) && tolerance >= 0 && tolerance <= 1;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
