using System;
using UnityEngine;

namespace LittleCastle.Experimental.MountainBorder
{
    /// <summary>
    /// Explicit draft connection between two river levels. Coordinates are in
    /// world space; the referenced rivers and their lifecycle remain external.
    /// This record neither discovers nor modifies river data.
    /// </summary>
    [Serializable]
    public sealed class WaterfallLinkDraft
    {
        public const int CurrentVersion = 1;
        public const float MaximumWidth = 1000f;

        public int version = CurrentVersion;
        public string id;
        public string upstreamRiverId;
        public string downstreamRiverId;
        public Vector3 top;
        public Vector3 bottom;
        public float width;

        /// <summary>Positive when the lip is above the landing.</summary>
        public double DropHeight => (double)top.y - bottom.y;

        public static bool Validate(WaterfallLinkDraft link)
        {
            return Validate(link, out _);
        }

        /// <summary>
        /// Checks the draft without normalizing or changing it. The generous
        /// displacement limit rejects remote, accidentally connected points;
        /// it allows vertical and sloped falls and is not a hydraulic model.
        /// </summary>
        public static bool Validate(WaterfallLinkDraft link, out string error)
        {
            if (link == null)
                return Fail("Link is required.", out error);
            if (link.version != CurrentVersion)
                return Fail("Unsupported waterfall link version.", out error);
            if (string.IsNullOrWhiteSpace(link.id))
                return Fail("Link ID is required.", out error);
            if (string.IsNullOrWhiteSpace(link.upstreamRiverId) ||
                string.IsNullOrWhiteSpace(link.downstreamRiverId))
                return Fail("Both river IDs are required.", out error);
            if (string.Equals(link.upstreamRiverId.Trim(), link.downstreamRiverId.Trim(), StringComparison.Ordinal))
                return Fail("Upstream and downstream river IDs must differ.", out error);
            if (!IsFinite(link.top) || !IsFinite(link.bottom) || !IsFinite(link.width))
                return Fail("Endpoints and width must be finite.", out error);
            if (Mathf.Max(Mathf.Abs(link.top.x), Mathf.Abs(link.top.y), Mathf.Abs(link.top.z),
                Mathf.Abs(link.bottom.x), Mathf.Abs(link.bottom.y), Mathf.Abs(link.bottom.z)) > 100000f)
                return Fail("Endpoints exceed the bounded preview coordinate limit.", out error);
            if (link.width <= 0f || link.width > MaximumWidth)
                return Fail("Width must be positive and within the draft limit.", out error);
            if (link.DropHeight <= 0d)
                return Fail("The lip must be above the landing.", out error);

            double dx = (double)link.top.x - link.bottom.x;
            double dz = (double)link.top.z - link.bottom.z;
            double horizontalDistance = Math.Sqrt(dx * dx + dz * dz);
            double maximumDistance = 100d + 20d * link.width + 20d * link.DropHeight;
            if (horizontalDistance > maximumDistance)
                return Fail("Endpoints are too far apart horizontally for this draft link.", out error);

            error = null;
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
