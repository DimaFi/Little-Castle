using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Experimental.MountainBorder
{
    /// <summary>
    /// Stateless world-space sampler for the decorative prototype. Validate once
    /// after creating/loading/editing a recipe, before a sampling batch; do not mutate
    /// that recipe during the batch. Sampling does not repeat structural validation.
    /// </summary>
    public static class MountainRidgeSampler
    {
        public const int MaximumNodes = 256;
        public const float MaximumCoordinate = 100000f;
        public const float MinimumBaseHeight = -1000f;
        public const float MaximumHeight = 10000f;
        public const float MinimumHalfWidth = 0.25f;
        public const float MaximumHalfWidth = 2048f;
        public const float MaximumRoughnessAmplitude = 2048f;
        public const float MinimumRoughnessFrequency = 0.00001f;
        public const float MaximumRoughnessFrequency = 1f;
        const float MinimumSegmentLengthSquared = 0.0001f;

        /// <summary>Rejects unsupported versions, malformed inputs and excessive prototype bounds.</summary>
        public static void Validate(MountainRidgeRecipe recipe)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));
            if (recipe.version != 1)
                throw new ArgumentException("Unsupported mountain ridge recipe version.", nameof(recipe));
            ValidateId(recipe.id, "Ridge");
            RequireRange(recipe.baseHeight, MinimumBaseHeight, MaximumHeight, "Base height");
            RequireRange(recipe.roughnessAmplitude, 0f, MaximumRoughnessAmplitude, "Roughness amplitude");
            RequireRange(recipe.roughnessFrequency, MinimumRoughnessFrequency,
                MaximumRoughnessFrequency, "Roughness frequency");
            if (recipe.nodes == null || recipe.nodes.Count < 2 || recipe.nodes.Count > MaximumNodes)
                throw new ArgumentException("A ridge requires 2–256 nodes.", nameof(recipe));

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < recipe.nodes.Count; i++)
            {
                var node = recipe.nodes[i];
                if (node == null)
                    throw new ArgumentException("A ridge node is missing.", nameof(recipe));
                ValidateId(node.id, "Node");
                if (!ids.Add(node.id))
                    throw new ArgumentException("Duplicate mountain feature ID: " + node.id, nameof(recipe));
                ValidatePosition(node.position, "Node position");
                RequireRange(node.height, recipe.baseHeight, MaximumHeight, "Node height");
                RequireRange(node.halfWidth, MinimumHalfWidth, MaximumHalfWidth, "Node half width");
                if (i > 0)
                    ValidateSegment(recipe.nodes[i - 1].position, node.position);
            }

            if (recipe.pass == null)
                return;
            var pass = recipe.pass;
            ValidateId(pass.id, "Pass");
            if (!ids.Add(pass.id))
                throw new ArgumentException("Duplicate mountain feature ID: " + pass.id, nameof(recipe));
            ValidatePosition(pass.start, "Pass start");
            ValidatePosition(pass.end, "Pass end");
            ValidateSegment(pass.start, pass.end);
            RequireRange(pass.halfWidth, MinimumHalfWidth, MaximumHalfWidth, "Pass half width");
            RequireRange(pass.floorHeight, recipe.baseHeight, MaximumHeight, "Pass floor height");
            RequireRange(pass.shoulderBlend, 0f, MaximumHalfWidth, "Pass shoulder blend");
        }

        /// <summary>
        /// Returns absolute decorative height. Each finite segment has a smooth
        /// radial falloff; overlapping segments use their maximum, not a sum.
        /// Outside all segment envelopes the result is exactly baseHeight.
        /// </summary>
        public static float SampleHeight(MountainRidgeRecipe recipe, Vector2 worldPosition)
        {
            float height = recipe.baseHeight;
            float noise = recipe.roughnessAmplitude == 0f ? 0f :
                Noise(recipe.seed, worldPosition * recipe.roughnessFrequency) * recipe.roughnessAmplitude;
            for (int i = 1; i < recipe.nodes.Count; i++)
            {
                var a = recipe.nodes[i - 1];
                var b = recipe.nodes[i];
                float distance = SegmentDistance(worldPosition, a.position, b.position, out float t);
                float width = Mathf.LerpUnclamped(a.halfWidth, b.halfWidth, t);
                if (distance >= width)
                    continue;
                float peak = Mathf.LerpUnclamped(a.height, b.height, t);
                float weight = 1f - Smooth(distance / width);
                float candidate = recipe.baseHeight + Mathf.Max(0f, peak - recipe.baseHeight + noise) * weight;
                height = Mathf.Max(height, candidate);
            }

            var pass = recipe.pass;
            if (pass != null && height > recipe.baseHeight)
            {
                float distance = SegmentDistance(worldPosition, pass.start, pass.end, out _);
                if (distance < pass.halfWidth + pass.shoulderBlend)
                {
                    float blend = pass.shoulderBlend > 0f
                        ? Smooth(Mathf.Clamp01((distance - pass.halfWidth) / pass.shoulderBlend))
                        : 0f;
                    // A high pass floor never raises the underlying surface.
                    height = Mathf.LerpUnclamped(Mathf.Min(height, pass.floorHeight), height, blend);
                }
            }
            return height;
        }

        /// <summary>World-space normal from the same height function; step is world metres.</summary>
        public static Vector3 SampleNormal(MountainRidgeRecipe recipe, Vector2 worldPosition, float step)
        {
            if (!Finite(step) || step < 0.001f || step > MaximumHalfWidth)
                throw new ArgumentOutOfRangeException(nameof(step), "Normal step must be finite and 0.001–2048 metres.");
            float left = SampleHeight(recipe, worldPosition - new Vector2(step, 0f));
            float right = SampleHeight(recipe, worldPosition + new Vector2(step, 0f));
            float back = SampleHeight(recipe, worldPosition - new Vector2(0f, step));
            float front = SampleHeight(recipe, worldPosition + new Vector2(0f, step));
            return new Vector3(left - right, 2f * step, back - front).normalized;
        }

        static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b, out float t)
        {
            var direction = b - a;
            t = Mathf.Clamp01(Vector2.Dot(point - a, direction) / direction.sqrMagnitude);
            return Vector2.Distance(point, a + direction * t);
        }

        static float Smooth(float value) => value * value * (3f - 2f * value);

        // Stable world lattice noise; negative coordinates use floor, never truncation.
        static float Noise(int seed, Vector2 position)
        {
            int x = Mathf.FloorToInt(position.x);
            int z = Mathf.FloorToInt(position.y);
            float u = Smooth(position.x - x);
            float v = Smooth(position.y - z);
            return Mathf.LerpUnclamped(
                Mathf.LerpUnclamped(HashSigned(seed, x, z), HashSigned(seed, x + 1, z), u),
                Mathf.LerpUnclamped(HashSigned(seed, x, z + 1), HashSigned(seed, x + 1, z + 1), u), v);
        }

        static float HashSigned(int seed, int x, int z)
        {
            unchecked
            {
                uint value = 2166136261u;
                value = (value ^ (uint)seed) * 16777619u;
                value = (value ^ (uint)x) * 16777619u;
                value = (value ^ (uint)z) * 16777619u;
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                value ^= value >> 16;
                return (value & 0x00ffffffu) / 8388607.5f - 1f;
            }
        }

        static void ValidateId(string id, string label)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128)
                throw new ArgumentException(label + " requires a stable ID of 1–128 characters.");
        }

        static void ValidatePosition(Vector2 position, string label)
        {
            RequireRange(position.x, -MaximumCoordinate, MaximumCoordinate, label + " X");
            RequireRange(position.y, -MaximumCoordinate, MaximumCoordinate, label + " Z");
        }

        static void ValidateSegment(Vector2 start, Vector2 end)
        {
            if ((end - start).sqrMagnitude < MinimumSegmentLengthSquared)
                throw new ArgumentException("Mountain segments must be at least 0.01 metres long.");
        }

        static void RequireRange(float value, float minimum, float maximum, string label)
        {
            if (!Finite(value) || value < minimum || value > maximum)
                throw new ArgumentException(label + " is non-finite or outside the supported range.");
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
