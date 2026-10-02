using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Building
{
    /// <summary>
    /// Deterministically converts one compact wall path into repeated rigid
    /// modules and optional structural wall towers.
    ///
    /// Smooth control points use Catmull-Rom interpolation.
    /// Sharp control points force straight segments into/out of that point.
    /// </summary>
    public static class WallPathLayoutUtility
    {
        public static void BuildSections(
            WallRuntimeState wall,
            float segmentSpacing,
            float curveSampleStep,
            List<WallSectionPose> output)
        {
            BuildSectionsInternal(
                wall,
                segmentSpacing,
                curveSampleStep,
                null,
                output);
        }

        public static void BuildSections(
            WallRuntimeState wall,
            WallPlacementDefinition definition,
            List<WallSectionPose> output)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            BuildSectionsInternal(
                wall,
                definition.SegmentSpacing,
                definition.CurveSampleStep,
                definition,
                output);
        }

        public static void BuildTowerPoses(
            WallRuntimeState wall,
            WallPlacementDefinition definition,
            List<WallTowerPose> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            output.Clear();

            if (wall == null ||
                definition == null ||
                wall.controlPoints == null ||
                wall.controlPoints.Count == 0)
            {
                return;
            }

            bool hasStartTower =
                definition.StartTowerPrefab != null;

            bool hasRepeatTower =
                definition.RepeatTowerPrefab != null &&
                definition.AutomaticTowerSpacing >
                0.01f;

            if (!hasStartTower &&
                !hasRepeatTower)
            {
                return;
            }

            wall.EnsureControlPointModes();

            if (wall.controlPoints.Count == 1)
            {
                if (hasStartTower)
                {
                    output.Add(
                        new WallTowerPose(
                            CreateTowerId(
                                wall.wallId,
                                0),
                            0,
                            wall.controlPoints[0],
                            0f,
                            true));
                }

                return;
            }

            var curve =
                new List<Vector3>();

            BuildDenseCurve(
                wall,
                definition.CurveSampleStep,
                curve);

            if (curve.Count < 2)
                return;

            BuildCumulativeLengths(
                curve,
                wall.closedLoop,
                out List<float> cumulative,
                out float totalLength);

            if (totalLength <= 0.001f)
                return;

            int towerIndex = 0;

            if (hasStartTower)
            {
                output.Add(
                    new WallTowerPose(
                        CreateTowerId(
                            wall.wallId,
                            towerIndex),
                        towerIndex,
                        wall.controlPoints[0],
                        SampleYawAtDistance(
                            curve,
                            cumulative,
                            totalLength,
                            0f,
                            wall.closedLoop,
                            definition.SegmentSpacing),
                        true));

                towerIndex++;
            }

            if (!hasRepeatTower)
                return;

            float spacing =
                definition.AutomaticTowerSpacing;

            float distance =
                spacing;

            float minimumDistanceFromStart =
                Mathf.Max(
                    definition.TowerSectionClearanceRadius *
                    2f,
                    spacing * 0.35f);

            while (distance <
                   totalLength -
                   minimumDistanceFromStart)
            {
                Vector3 position =
                    SampleAtDistance(
                        curve,
                        cumulative,
                        totalLength,
                        distance,
                        wall.closedLoop);

                float yaw =
                    SampleYawAtDistance(
                        curve,
                        cumulative,
                        totalLength,
                        distance,
                        wall.closedLoop,
                        definition.SegmentSpacing);

                output.Add(
                    new WallTowerPose(
                        CreateTowerId(
                            wall.wallId,
                            towerIndex),
                        towerIndex,
                        position,
                        yaw,
                        false));

                towerIndex++;
                distance += spacing;
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
            return
                MixStableId(
                    wallId,
                    sectionIndex + 1,
                    0x9E3779B185EBCA87UL);
        }

        public static long CreateTowerId(
            long wallId,
            int towerIndex)
        {
            return
                MixStableId(
                    wallId,
                    towerIndex + 1,
                    0xD6E8FEB86659FD93UL);
        }

        private static void BuildSectionsInternal(
            WallRuntimeState wall,
            float segmentSpacing,
            float curveSampleStep,
            WallPlacementDefinition definition,
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

            wall.EnsureControlPointModes();

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
                wall,
                safeSampleStep,
                curve);

            if (curve.Count < 2)
                return;

            BuildCumulativeLengths(
                curve,
                wall.closedLoop,
                out List<float> cumulative,
                out float totalLength);

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

            var towers =
                definition != null
                    ? new List<WallTowerPose>()
                    : null;

            if (definition != null)
            {
                BuildTowerPoses(
                    wall,
                    definition,
                    towers);
            }

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

                if (definition != null &&
                    ShouldSuppressSection(
                        wall,
                        definition,
                        towers,
                        position))
                {
                    continue;
                }

                float yaw =
                    SampleYawAtDistance(
                        curve,
                        cumulative,
                        totalLength,
                        distance,
                        wall.closedLoop,
                        actualSpacing);

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

        private static bool ShouldSuppressSection(
            WallRuntimeState wall,
            WallPlacementDefinition definition,
            IReadOnlyList<WallTowerPose> towers,
            Vector3 position)
        {
            float towerRadius =
                definition.TowerSectionClearanceRadius;

            if (towerRadius > 0f &&
                towers != null)
            {
                float towerRadiusSqr =
                    towerRadius *
                    towerRadius;

                for (int i = 0;
                     i < towers.Count;
                     i++)
                {
                    Vector3 delta =
                        position -
                        towers[i].position;

                    delta.y = 0f;

                    if (delta.sqrMagnitude <=
                        towerRadiusSqr)
                    {
                        return true;
                    }
                }
            }

            if (wall.socketAttachments == null ||
                wall.socketAttachments.Count == 0)
            {
                return false;
            }

            for (int i = 0;
                 i < wall.socketAttachments.Count;
                 i++)
            {
                WallSocketAttachmentState attachment =
                    wall.socketAttachments[i];

                if (attachment.controlPointIndex < 0 ||
                    attachment.controlPointIndex >=
                    wall.controlPoints.Count ||
                    attachment.clearanceRadius <= 0f)
                {
                    continue;
                }

                Vector3 center =
                    wall.controlPoints[
                        attachment.controlPointIndex];

                Vector3 delta =
                    position -
                    center;

                delta.y = 0f;

                if (delta.sqrMagnitude <=
                    attachment.clearanceRadius *
                    attachment.clearanceRadius)
                {
                    return true;
                }
            }

            return false;
        }

        private static void BuildDenseCurve(
            WallRuntimeState wall,
            float sampleStep,
            List<Vector3> output)
        {
            output.Clear();

            IReadOnlyList<Vector3> points =
                wall.controlPoints;

            if (points.Count == 2 &&
                !wall.closedLoop)
            {
                AppendLinearSegmentSamples(
                    points[0],
                    points[1],
                    sampleStep,
                    true,
                    output);

                return;
            }

            int segmentCount =
                wall.closedLoop
                    ? points.Count
                    : points.Count - 1;

            for (int segment = 0;
                 segment < segmentCount;
                 segment++)
            {
                int p1Index =
                    segment;

                int p2Index =
                    (segment + 1) %
                    points.Count;

                Vector3 p1 =
                    points[p1Index];

                Vector3 p2 =
                    points[p2Index];

                Vector3 p0 =
                    wall.closedLoop
                        ? points[
                            (segment - 1 +
                             points.Count) %
                            points.Count]
                        : points[
                            Mathf.Max(
                                0,
                                segment - 1)];

                Vector3 p3 =
                    wall.closedLoop
                        ? points[
                            (segment + 2) %
                            points.Count]
                        : points[
                            Mathf.Min(
                                points.Count - 1,
                                segment + 2)];

                bool forceStraightSegment =
                    wall.GetControlPointMode(
                        p1Index) ==
                    WallControlPointMode.Sharp ||
                    wall.GetControlPointMode(
                        p2Index) ==
                    WallControlPointMode.Sharp;

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
                    float t =
                        (float)step /
                        steps;

                    Vector3 point =
                        forceStraightSegment
                            ? Vector3.Lerp(
                                p1,
                                p2,
                                t)
                            : CatmullRom(
                                p0,
                                p1,
                                p2,
                                p3,
                                t);

                    output.Add(
                        point);
                }
            }

            if (!wall.closedLoop)
            {
                output.Add(
                    points[
                        points.Count - 1]);
            }
        }

        private static void AppendLinearSegmentSamples(
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

        private static void BuildCumulativeLengths(
            IReadOnlyList<Vector3> curve,
            bool closedLoop,
            out List<float> cumulative,
            out float totalLength)
        {
            cumulative =
                new List<float>(
                    curve.Count);

            cumulative.Add(0f);

            totalLength = 0f;

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

            if (closedLoop)
            {
                totalLength +=
                    Vector3.Distance(
                        curve[
                            curve.Count - 1],
                        curve[0]);
            }
        }

        private static float SampleYawAtDistance(
            IReadOnlyList<Vector3> curve,
            IReadOnlyList<float> cumulative,
            float totalLength,
            float distance,
            bool closedLoop,
            float spacingReference)
        {
            float tangentDistance =
                Mathf.Max(
                    0.03f,
                    Mathf.Min(
                        Mathf.Max(
                            0.1f,
                            spacingReference) *
                        0.35f,
                        0.5f));

            Vector3 before =
                SampleAtDistance(
                    curve,
                    cumulative,
                    totalLength,
                    distance -
                    tangentDistance,
                    closedLoop);

            Vector3 after =
                SampleAtDistance(
                    curve,
                    cumulative,
                    totalLength,
                    distance +
                    tangentDistance,
                    closedLoop);

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

            return
                Mathf.Atan2(
                    tangent.x,
                    tangent.z) *
                Mathf.Rad2Deg;
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
                        segmentLength <=
                        0.000001f
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

        private static long MixStableId(
            long wallId,
            int index,
            ulong salt)
        {
            unchecked
            {
                ulong value =
                    (ulong)wallId;

                value ^=
                    (ulong)index *
                    salt;

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

                return
                    result == 0
                        ? 1
                        : result;
            }
        }
    }
}
