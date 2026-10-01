using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Authoritative macro data for one generated river.
    ///
    /// nominalWidth/nominalDepth are retained as fallback/reference values.
    /// Per-point profiles allow the channel to grow downstream and widen after
    /// tributary confluences without changing the rendering contract.
    /// </summary>
    [Serializable]
    public sealed class WorldRiverData
    {
        public long stableId;

        public float nominalWidth;
        public float nominalDepth;

        /// <summary>
        /// If non-zero, this river terminates into another generated river.
        /// </summary>
        public long downstreamRiverId;

        /// <summary>
        /// Approximate point index on the downstream river where this river joins.
        /// -1 means there is no confluence.
        /// </summary>
        public int downstreamJoinPointIndex = -1;

        public Vector2 confluencePosition;

        public List<Vector2> centerline =
            new List<Vector2>();

        /// <summary>
        /// Local channel width for each centerline point.
        /// Count should match centerline.Count when a profile has been built.
        /// </summary>
        public List<float> widths =
            new List<float>();

        /// <summary>
        /// Local channel depth for each centerline point.
        /// Count should match centerline.Count when a profile has been built.
        /// </summary>
        public List<float> depths =
            new List<float>();

        /// <summary>
        /// Relative flow units at each centerline point.
        /// These are not physical cubic-meters-per-second values.
        /// They are deterministic gameplay/generation weights that can later
        /// seed water visuals, erosion and resource/ecology systems.
        /// </summary>
        public List<float> flow =
            new List<float>();

        public bool HasConfluence =>
            downstreamRiverId != 0L &&
            downstreamJoinPointIndex >= 0;

        public float GetWidthAtPoint(int pointIndex)
        {
            if (widths != null &&
                widths.Count == centerline.Count &&
                widths.Count > 0)
            {
                int safeIndex =
                    Mathf.Clamp(
                        pointIndex,
                        0,
                        widths.Count - 1);

                return Mathf.Max(
                    0.1f,
                    widths[safeIndex]);
            }

            return Mathf.Max(
                0.1f,
                nominalWidth);
        }

        public float GetDepthAtPoint(int pointIndex)
        {
            if (depths != null &&
                depths.Count == centerline.Count &&
                depths.Count > 0)
            {
                int safeIndex =
                    Mathf.Clamp(
                        pointIndex,
                        0,
                        depths.Count - 1);

                return Mathf.Max(
                    0.01f,
                    depths[safeIndex]);
            }

            return Mathf.Max(
                0.01f,
                nominalDepth);
        }

        public float GetFlowAtPoint(int pointIndex)
        {
            if (flow != null &&
                flow.Count == centerline.Count &&
                flow.Count > 0)
            {
                int safeIndex =
                    Mathf.Clamp(
                        pointIndex,
                        0,
                        flow.Count - 1);

                return Mathf.Max(
                    0f,
                    flow[safeIndex]);
            }

            return 1f;
        }

        public float GetWidthAtSegment(
            int segmentIndex,
            float segmentT)
        {
            if (centerline == null ||
                centerline.Count < 2)
            {
                return Mathf.Max(
                    0.1f,
                    nominalWidth);
            }

            int a =
                Mathf.Clamp(
                    segmentIndex,
                    0,
                    centerline.Count - 2);

            int b = a + 1;

            return Mathf.Lerp(
                GetWidthAtPoint(a),
                GetWidthAtPoint(b),
                Mathf.Clamp01(segmentT));
        }

        public float GetDepthAtSegment(
            int segmentIndex,
            float segmentT)
        {
            if (centerline == null ||
                centerline.Count < 2)
            {
                return Mathf.Max(
                    0.01f,
                    nominalDepth);
            }

            int a =
                Mathf.Clamp(
                    segmentIndex,
                    0,
                    centerline.Count - 2);

            int b = a + 1;

            return Mathf.Lerp(
                GetDepthAtPoint(a),
                GetDepthAtPoint(b),
                Mathf.Clamp01(segmentT));
        }

        public float GetMaxWidth()
        {
            float max =
                Mathf.Max(
                    0.1f,
                    nominalWidth);

            if (widths == null)
                return max;

            for (int i = 0; i < widths.Count; i++)
                max = Mathf.Max(max, widths[i]);

            return max;
        }

        public float GetTerminalFlow()
        {
            if (flow != null &&
                flow.Count > 0)
            {
                return Mathf.Max(
                    0f,
                    flow[flow.Count - 1]);
            }

            return 1f;
        }
    }
}
