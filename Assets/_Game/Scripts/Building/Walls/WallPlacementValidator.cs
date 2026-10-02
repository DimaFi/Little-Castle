using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Building
{
    public static class WallPlacementValidator
    {
        public const int MaximumSectionsPerWall = 2048;

        public static bool Validate(
            WallRuntimeState wall,
            WallPlacementDefinition definition,
            IReadOnlyList<WallSectionPose> sections,
            out string reason)
        {
            if (wall == null)
            {
                reason = "Wall state is missing.";
                return false;
            }

            if (definition == null)
            {
                reason = "Wall definition is missing.";
                return false;
            }

            int minimumPoints =
                wall.closedLoop
                    ? 3
                    : 2;

            if (wall.controlPoints == null ||
                wall.controlPoints.Count <
                minimumPoints)
            {
                reason =
                    wall.closedLoop
                        ? "Closed wall needs at least three control points."
                        : "Wall needs at least two control points.";

                return false;
            }

            if (HasSelfIntersection(
                    wall.controlPoints,
                    wall.closedLoop))
            {
                reason =
                    "Wall path intersects itself.";

                return false;
            }

            if (sections == null ||
                sections.Count == 0)
            {
                reason =
                    "Wall path does not produce any modules.";

                return false;
            }

            if (sections.Count >
                MaximumSectionsPerWall)
            {
                reason =
                    "Wall is too long. Split it into multiple wall paths.";

                return false;
            }

            float maximumTurn =
                WallPathLayoutUtility.GetMaximumNeighborTurnDegrees(
                    sections,
                    wall.closedLoop);

            if (maximumTurn >
                definition.MaximumTurnDegrees +
                0.01f)
            {
                reason =
                    "Wall curve is too sharp for this module (" +
                    maximumTurn.ToString("0.0") +
                    "° > " +
                    definition.MaximumTurnDegrees.ToString("0.0") +
                    "°).";

                return false;
            }

            for (int i = 1;
                 i < sections.Count;
                 i++)
            {
                float heightStep =
                    Mathf.Abs(
                        sections[i].position.y -
                        sections[i - 1].position.y);

                if (heightStep >
                    definition.MaximumHeightStep)
                {
                    reason =
                        "Ground height changes too much between neighboring " +
                        "wall modules.";

                    return false;
                }
            }

            if (wall.closedLoop &&
                sections.Count > 1)
            {
                float seamHeightStep =
                    Mathf.Abs(
                        sections[0].position.y -
                        sections[
                            sections.Count - 1]
                            .position.y);

                if (seamHeightStep >
                    definition.MaximumHeightStep)
                {
                    reason =
                        "Closed wall seam has too large a height step.";

                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        private static bool HasSelfIntersection(
            IReadOnlyList<Vector3> points,
            bool closedLoop)
        {
            int segmentCount =
                closedLoop
                    ? points.Count
                    : points.Count - 1;

            for (int a = 0;
                 a < segmentCount;
                 a++)
            {
                int aNext =
                    (a + 1) %
                    points.Count;

                Vector2 a0 =
                    ToXZ(
                        points[a]);

                Vector2 a1 =
                    ToXZ(
                        points[aNext]);

                for (int b = a + 1;
                     b < segmentCount;
                     b++)
                {
                    int bNext =
                        (b + 1) %
                        points.Count;

                    if (SegmentsAreAdjacent(
                            a,
                            aNext,
                            b,
                            bNext,
                            points.Count,
                            closedLoop))
                    {
                        continue;
                    }

                    Vector2 b0 =
                        ToXZ(
                            points[b]);

                    Vector2 b1 =
                        ToXZ(
                            points[bNext]);

                    if (SegmentsIntersect(
                            a0,
                            a1,
                            b0,
                            b1))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool SegmentsAreAdjacent(
            int a,
            int aNext,
            int b,
            int bNext,
            int pointCount,
            bool closedLoop)
        {
            if (a == b ||
                a == bNext ||
                aNext == b ||
                aNext == bNext)
            {
                return true;
            }

            if (!closedLoop)
                return false;

            return
                (a == 0 &&
                 bNext == 0) ||
                (b == 0 &&
                 aNext == 0) ||
                (aNext == pointCount - 1 &&
                 b == 0) ||
                (bNext == pointCount - 1 &&
                 a == 0);
        }

        private static bool SegmentsIntersect(
            Vector2 a0,
            Vector2 a1,
            Vector2 b0,
            Vector2 b1)
        {
            float d1 =
                Cross(
                    a1 - a0,
                    b0 - a0);

            float d2 =
                Cross(
                    a1 - a0,
                    b1 - a0);

            float d3 =
                Cross(
                    b1 - b0,
                    a0 - b0);

            float d4 =
                Cross(
                    b1 - b0,
                    a1 - b0);

            return
                ((d1 > 0f &&
                  d2 < 0f) ||
                 (d1 < 0f &&
                  d2 > 0f)) &&
                ((d3 > 0f &&
                  d4 < 0f) ||
                 (d3 < 0f &&
                  d4 > 0f));
        }

        private static float Cross(
            Vector2 a,
            Vector2 b)
        {
            return
                a.x * b.y -
                a.y * b.x;
        }

        private static Vector2 ToXZ(
            Vector3 value)
        {
            return
                new Vector2(
                    value.x,
                    value.z);
        }
    }
}
