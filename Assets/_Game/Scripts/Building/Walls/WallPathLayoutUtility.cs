using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Building
{
    /// <summary>
    /// Deterministically converts a compact wall control path into repeated
    /// fixed authored modules.
    ///
    /// No mesh deformation is used. Each module stays rigid and is rotated to
    /// the local path tangent.
    /// </summary>
    public static class WallPathLayoutUtility
    {
        public static void BuildSections(
            WallRuntimeState wall,
            float segmentSpacing,
            float curveSampleStep,
            List<WallSectionPose> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            output.Clear();

            if (wall == null ||
                wall.controlPoints == null ||
                wall.controlPoints.Count < 2)
            {
                return;
            }

            float safeSpacing =
                Mathf.Max(
                    0.05f,
                    segmentSpacing);

            float safeSampleStep =
                Mathf.Clamp(
                    curveSampleStep,
                    0.05f,
                    safeSpacing);

            var curve =
                new List<Vector3>(
                    wall.controlPoints.Count *
                    8);

            BuildDenseCurve(
                wall.controlPoints,
                wall.closedLoop,
                safeSampleStep,
                curve);

            if (curve.Count < 2)
                return;

            var cumulative =
                new List<float>(
                    curve.Count);

            cumulative.Add(0f);

            float totalLength = 0f;

            for (int i = 1;
                 i < curve.Count;
                 i++)
            {
                totalLength +=
                    Vector3.Distance(
                        curve[i - 1],
                        curve[i]);

                cumulative.Add(
                    totalLength);
            }

            if (wall.closedLoop)
            {
                totalLength +=
                    Vector3.Distance(
                        curve[curve.Count - 1],
                        curve[0]);
            }

            if (totalLength <= 0.001f)
                return;

            int sectionCount =
                Mathf.Max(
                    1,
                    Mathf.CeilToInt(
                        totalLength /
                        safeSpacing));

            float actualSpacing =
                totalLength /
                sectionCount;

            for (int i = 0;
                 i < sectionCount;
                 i++)
            {
                float distance =
                    wall.closedLoop
                        ? i * actualSpacing
                        : Mathf.Min(
                            totalLength,
                            (i + 0.5f) *
                            actualSpacing);

                Vector3 position =
                    SampleAtDistance(
                        curve,
                        cumulative,
                        totalLength,
                        distance,
                        wall.closedLoop);

                float tangentDistance =
                    Mathf.Max(
                        0.03f,
                        Mathf.Min(
                            actualSpacing * 0.35f,
                            0.5f));

                Vector3 before =
                    SampleAtDistance(
                        curve,
                        cumulative,
                        totalLength,
                        distance -
                        tangentDistance,
                        wall.closedLoop);

                Vector3 after =
                    SampleAtDistance(
                        curve,
                        cumulative,
                        totalLength,
                        distance +
                        tangentDistance,
                        wall.closedLoop);

                Vector3 tangent =
                    after -
                    before;

                tangent.y = 0f;

                if (tangent.sqrMagnitude <=
                    0.000001f)
                {
                    tangent =
                        Vector3.forward;
                }

                float yaw =
                    Mathf.Atan2(
                        tangent.x,
                        tangent.z) *
                    Mathf.Rad2Deg;

                output.Add(
                    new WallSectionPose(
                        CreateSectionId(
                            wall.wallId,
                            i),
                        i,
                        position,
                        yaw));
            }
        }

        public static float GetMaximumNeighborTurnDegrees(
            IReadOnlyList<WallSectionPose> sections,
            bool closedLoop)
        {
            if (sections == null ||
                sections.Count < 2)
            {
                return 0f;
            }

            float maximum = 0f;
            int pairCount =
                closedLoop
                    ? sections.Count
                    : sections.Count - 1;

            for (int i = 0;
                 i < pairCount;
                 i++)
            {
                int next =
                    (i + 1) %
                    sections.Count;

                float turn =
                    Mathf.Abs(
                        Mathf.DeltaAngle(
                            sections[i].yawDegrees,
                            sections[next].yawDegrees));

                maximum =
                    Mathf.Max(
                        maximum,
                        turn);
            }

            return maximum;
        }

        public static long CreateSectionId(
            long wallId,
            int sectionIndex)
        {
            unchecked
            {
                ulong value =
                    (ulong)wallId;

                value ^=
                    (ulong)(sectionIndex + 1) *
                    0x9E3779B185EBCA87UL;

                value ^=
                    value >>
                    30;

                value *=
                    0xBF58476D1CE4E5B9UL;

                value ^=
                    value >>
                    27;

                value *=
                    0x94D049BB133111EBUL;

                value ^=
                    value >>
                    31;

                long result =
                    (long)value;

                if (result == 0)
                    result = 1;

                return result;
            }
        }

        private static void BuildDenseCurve(
            IReadOnlyList<Vector3> points,
            bool closedLoop,
            float sampleStep,
            List<Vector3> output)
        {
            output.Clear();

            if (points.Count == 2 &&
                !closedLoop)
            {
                AppendLinearSegment(
                    points[0],
                    points[1],
                    sampleStep,
                    true,
                    output);

                return;
            }

            int segmentCount =
                closedLoop
                    ? points.Count
                    : points.Count - 1;

            for (int segment = 0;
                 segment < segmentCount;
                 segment++)
            {
                Vector3 p1 =
                    points[segment];

                Vector3 p2 =
                    points[
                        (segment + 1) %
                        points.Count];

                Vector3 p0 =
                    closedLoop
                        ? points[
                            (segment - 1 +
                             points.Count) %
                            points.Count]
                        : points[
                            Mathf.Max(
                                0,
                                segment - 1)];

                Vector3 p3 =
                    closedLoop
                        ? points[
                            (segment + 2) %
                            points.Count]
                        : points[
                            Mathf.Min(
                                points.Count - 1,
                                segment + 2)];

                float chord =
                    Vector3.Distance(
                        p1,
                        p2);

                int steps =
                    Mathf.Max(
                        2,
                        Mathf.CeilToInt(
                            chord /
                            sampleStep));

                for (int step = 0;
                     step < steps;
                     step++)
                {
                    if (segment > 0 &&
                        step == 0)
                    {
                        continue;
                    }

                    float t =
                        (float)step /
                        steps;

                    output.Add(
                        CatmullRom(
                            p0,
                            p1,
                            p2,
                            p3,
                            t));
                }
            }

            if (!closedLoop)
            {
                output.Add(
                    points[
                        points.Count - 1]);
            }
        }

        private static void AppendLinearSegment(
            Vector3 a,
            Vector3 b,
            float sampleStep,
            bool includeEnd,
            List<Vector3> output)
        {
            float length =
                Vector3.Distance(
                    a,
                    b);

            int steps =
                Mathf.Max(
                    1,
                    Mathf.CeilToInt(
                        length /
                        sampleStep));

            for (int i = 0;
                 i < steps;
                 i++)
            {
                float t =
                    (float)i /
                    steps;

                output.Add(
                    Vector3.Lerp(
                        a,
                        b,
                        t));
            }

            if (includeEnd)
                output.Add(b);
        }

        private static Vector3 CatmullRom(
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            Vector3 p3,
            float t)
        {
            float t2 =
                t *
                t;

            float t3 =
                t2 *
                t;

            return
                0.5f *
                ((2f * p1) +
                 (-p0 + p2) * t +
                 (2f * p0 -
                  5f * p1 +
                  4f * p2 -
                  p3) *
                 t2 +
                 (-p0 +
                  3f * p1 -
                  3f * p2 +
                  p3) *
                 t3);
        }

        private static Vector3 SampleAtDistance(
            IReadOnlyList<Vector3> curve,
            IReadOnlyList<float> cumulative,
            float totalLength,
            float distance,
            bool closedLoop)
        {
            if (curve.Count == 0)
                return Vector3.zero;

            if (closedLoop)
            {
                distance =
                    Mathf.Repeat(
                        distance,
                        totalLength);
            }
            else
            {
                distance =
                    Mathf.Clamp(
                        distance,
                        0f,
                        totalLength);
            }

            float openLength =
                cumulative[
                    cumulative.Count - 1];

            if (distance <= openLength ||
                !closedLoop)
            {
                for (int i = 1;
                     i < cumulative.Count;
                     i++)
                {
                    if (distance >
                        cumulative[i])
                    {
                        continue;
                    }

                    float segmentStart =
                        cumulative[i - 1];

                    float segmentLength =
                        cumulative[i] -
                        segmentStart;

                    float t =
                        segmentLength <= 0.000001f
                            ? 0f
                            : (distance -
                               segmentStart) /
                              segmentLength;

                    return
                        Vector3.Lerp(
                            curve[i - 1],
                            curve[i],
                            t);
                }

                return
                    curve[
                        curve.Count - 1];
            }

            float closingLength =
                totalLength -
                openLength;

            float closingDistance =
                distance -
                openLength;

            float closingT =
                closingLength <= 0.000001f
                    ? 0f
                    : closingDistance /
                      closingLength;

            return
                Vector3.Lerp(
                    curve[
                        curve.Count - 1],
                    curve[0],
                    closingT);
        }
    }
}
