using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    public static class WorldStartLayoutUtility
    {
        public static List<Vector2> BuildTargets(
            Rect playableRect,
            int playerCount,
            int worldSeed,
            WorldSessionStartOptions options)
        {
            int count =
                Mathf.Max(
                    1,
                    playerCount);

            var result =
                new List<Vector2>(
                    count);

            if (options == null ||
                options.placementMode ==
                    WorldStartPlacementMode.RandomScattered)
            {
                return result;
            }

            float rotation =
                options.randomizeFormationRotation
                    ? DeterministicHash.Hash01(
                        worldSeed,
                        count,
                        (int)options.placementMode,
                        0x53544152) *
                      Mathf.PI *
                      2f
                    : 0f;

            Vector2 center =
                playableRect.center;

            float halfX =
                playableRect.width *
                0.5f;

            float halfZ =
                playableRect.height *
                0.5f;

            switch (options.placementMode)
            {
                case WorldStartPlacementMode.MapPerimeter:
                    BuildPerimeterTargets(
                        result,
                        playableRect,
                        count,
                        rotation);

                    break;

                case WorldStartPlacementMode.PolygonRing:
                    BuildRingTargets(
                        result,
                        center,
                        halfX,
                        halfZ,
                        count,
                        Mathf.Clamp(
                            options.ringRadius,
                            0.2f,
                            0.95f),
                        rotation);

                    break;

                case WorldStartPlacementMode.RadialStar:
                    BuildStarTargets(
                        result,
                        center,
                        halfX,
                        halfZ,
                        count,
                        Mathf.Clamp(
                            options.starInnerRadius,
                            0.05f,
                            0.8f),
                        Mathf.Clamp(
                            options.starOuterRadius,
                            0.2f,
                            0.95f),
                        rotation);

                    break;
            }

            return result;
        }

        private static void BuildRingTargets(
            List<Vector2> result,
            Vector2 center,
            float halfX,
            float halfZ,
            int count,
            float radius,
            float rotation)
        {
            for (int i = 0;
                 i < count;
                 i++)
            {
                float angle =
                    rotation +
                    Mathf.PI *
                    2f *
                    i /
                    count;

                result.Add(
                    center +
                    new Vector2(
                        Mathf.Cos(angle) *
                        halfX *
                        radius,
                        Mathf.Sin(angle) *
                        halfZ *
                        radius));
            }
        }

        private static void BuildStarTargets(
            List<Vector2> result,
            Vector2 center,
            float halfX,
            float halfZ,
            int count,
            float innerRadius,
            float outerRadius,
            float rotation)
        {
            float safeInner =
                Mathf.Min(
                    innerRadius,
                    outerRadius);

            float safeOuter =
                Mathf.Max(
                    innerRadius,
                    outerRadius);

            for (int i = 0;
                 i < count;
                 i++)
            {
                float angle =
                    rotation +
                    Mathf.PI *
                    2f *
                    i /
                    count;

                float radius =
                    (i & 1) == 0
                        ? safeOuter
                        : safeInner;

                result.Add(
                    center +
                    new Vector2(
                        Mathf.Cos(angle) *
                        halfX *
                        radius,
                        Mathf.Sin(angle) *
                        halfZ *
                        radius));
            }
        }

        private static void BuildPerimeterTargets(
            List<Vector2> result,
            Rect rect,
            int count,
            float rotation)
        {
            float perimeter =
                2f *
                (rect.width +
                 rect.height);

            float normalizedOffset =
                Mathf.Repeat(
                    rotation /
                    (Mathf.PI * 2f),
                    1f);

            for (int i = 0;
                 i < count;
                 i++)
            {
                float distance =
                    Mathf.Repeat(
                        (i / (float)count +
                         normalizedOffset),
                        1f) *
                    perimeter;

                result.Add(
                    PointOnPerimeter(
                        rect,
                        distance));
            }
        }

        private static Vector2 PointOnPerimeter(
            Rect rect,
            float distance)
        {
            float d =
                distance;

            if (d <= rect.width)
                return new Vector2(
                    rect.xMin + d,
                    rect.yMin);

            d -= rect.width;

            if (d <= rect.height)
                return new Vector2(
                    rect.xMax,
                    rect.yMin + d);

            d -= rect.height;

            if (d <= rect.width)
                return new Vector2(
                    rect.xMax - d,
                    rect.yMax);

            d -= rect.width;

            return new Vector2(
                rect.xMin,
                rect.yMax - d);
        }
    }
}
