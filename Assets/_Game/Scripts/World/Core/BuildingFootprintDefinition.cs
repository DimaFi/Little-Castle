using System;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Authoritative, prefab-independent footprint parameters in world metres.
    /// World-space X/Z is represented as Vector2(X, Z).
    /// Model's local +Z points out of the entrance, and yaw rotates around +Y.
    /// An external adapter may author/serialize these values, but the validator
    /// never needs a GameObject, a collider or a scene.
    /// </summary>
    [Serializable]
    public struct BuildingFootprintDefinition
    {
        public Vector2 halfExtents;
        public int samplesPerAxis;
        public float maximumHeightSpread;
        public float maximumSlopeDegrees;
        public float entranceWidth;
        public float entranceDepth;
        public float maximumEntranceStep;

        public BuildingFootprintDefinition(
            Vector2 halfExtents,
            int samplesPerAxis,
            float maximumHeightSpread,
            float maximumSlopeDegrees,
            float entranceWidth,
            float entranceDepth,
            float maximumEntranceStep)
        {
            this.halfExtents = halfExtents;
            this.samplesPerAxis = samplesPerAxis;
            this.maximumHeightSpread = maximumHeightSpread;
            this.maximumSlopeDegrees = maximumSlopeDegrees;
            this.entranceWidth = entranceWidth;
            this.entranceDepth = entranceDepth;
            this.maximumEntranceStep = maximumEntranceStep;
        }

        /// <summary>
        /// A sample grid is deliberately odd (includes center and edges).
        /// The maximum 9x9 foundation plus 3x3 entrance limits query cost.
        /// These bounds apply equally to imported metadata and test data.
        /// </summary>
        public bool IsValid =>
            IsFinite(halfExtents.x) && halfExtents.x > 0f &&
            IsFinite(halfExtents.y) && halfExtents.y > 0f &&
            samplesPerAxis >= 3 && samplesPerAxis <= 9 &&
            (samplesPerAxis & 1) == 1 &&
            IsFinite(maximumHeightSpread) && maximumHeightSpread >= 0f &&
            IsFinite(maximumSlopeDegrees) &&
            maximumSlopeDegrees >= 0f && maximumSlopeDegrees <= 90f &&
            IsFinite(entranceWidth) && entranceWidth > 0f &&
            entranceWidth <= halfExtents.x * 2f &&
            IsFinite(entranceDepth) && entranceDepth > 0f &&
            IsFinite(maximumEntranceStep) && maximumEntranceStep >= 0f;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Explicit deterministic masks from the world-data sampling adapter.
    /// An unready/missing chunk is NEVER interpreted as buildable terrain.
    /// Buildable is required under the structure; Walkable is required at the
    /// entrance. Road is forbidden under a structure but allowed on the path.
    /// </summary>
    [Flags]
    public enum BuildingSiteFlags : byte
    {
        None = 0,
        Ready = 1 << 0,
        Playable = 1 << 1,
        Buildable = 1 << 2,
        Walkable = 1 << 3,
        Water = 1 << 4,
        Road = 1 << 5,
        Occupied = 1 << 6
    }

    public readonly struct BuildingSiteSample
    {
        public readonly float height;
        public readonly float slopeDegrees;
        public readonly BuildingSiteFlags flags;

        public BuildingSiteSample(
            float height,
            float slopeDegrees,
            BuildingSiteFlags flags)
        {
            this.height = height;
            this.slopeDegrees = slopeDegrees;
            this.flags = flags;
        }

        public bool Has(BuildingSiteFlags required) =>
            (flags & required) == required;
    }

    public enum BuildingFootprintRejection : byte
    {
        None = 0,
        InvalidRequest,
        SampleUnavailable,
        OutsidePlayableBounds,
        Water,
        RoadUnderFoundation,
        Occupied,
        UnbuildableTerrain,
        EntranceNotWalkable,
        SlopeTooSteep,
        HeightSpreadExceeded,
        EntranceStepExceeded
    }

    public readonly struct BuildingFootprintResult
    {
        public readonly BuildingFootprintRejection rejection;
        public readonly Vector2 rejectedAt;
        public readonly int samplesChecked;
        public readonly float minimumHeight;
        public readonly float maximumHeight;

        public bool IsValid =>
            rejection == BuildingFootprintRejection.None;

        public BuildingFootprintResult(
            BuildingFootprintRejection rejection,
            Vector2 rejectedAt,
            int samplesChecked,
            float minimumHeight,
            float maximumHeight)
        {
            this.rejection = rejection;
            this.rejectedAt = rejectedAt;
            this.samplesChecked = samplesChecked;
            this.minimumHeight = minimumHeight;
            this.maximumHeight = maximumHeight;
        }
    }
}
