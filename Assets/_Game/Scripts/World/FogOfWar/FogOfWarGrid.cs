using System;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Runtime visibility grid for one player/team.
    ///
    /// Explored state persists during the match. Current visibility is rebuilt
    /// from vision sources each visibility update.
    ///
    /// Rendering is intentionally separate; this class contains no shader,
    /// texture or camera dependencies.
    /// </summary>
    [Serializable]
    public sealed class FogOfWarGrid
    {
        private readonly Rect worldBounds;
        private readonly float cellWorldSize;
        private readonly int width;
        private readonly int height;

        private readonly bool[] explored;
        private readonly bool[] visible;

        public Rect WorldBounds => worldBounds;
        public float CellWorldSize => cellWorldSize;
        public int Width => width;
        public int Height => height;

        public FogOfWarGrid(
            Rect worldBounds,
            float cellWorldSize)
        {
            this.worldBounds =
                worldBounds;

            this.cellWorldSize =
                Mathf.Max(
                    0.25f,
                    cellWorldSize);

            width =
                Mathf.Max(
                    1,
                    Mathf.CeilToInt(
                        worldBounds.width /
                        this.cellWorldSize));

            height =
                Mathf.Max(
                    1,
                    Mathf.CeilToInt(
                        worldBounds.height /
                        this.cellWorldSize));

            explored =
                new bool[
                    width *
                    height];

            visible =
                new bool[
                    width *
                    height];
        }

        /// <summary>
        /// Clears current visibility while preserving explored history.
        /// Call before applying the current set of vision sources.
        /// </summary>
        public void BeginVisibilityUpdate()
        {
            Array.Clear(
                visible,
                0,
                visible.Length);
        }

        public void RevealCircle(
            Vector2 worldPosition,
            float radius)
        {
            float safeRadius =
                Mathf.Max(
                    0f,
                    radius);

            int minX =
                WorldToCellX(
                    worldPosition.x -
                    safeRadius);

            int maxX =
                WorldToCellX(
                    worldPosition.x +
                    safeRadius);

            int minZ =
                WorldToCellZ(
                    worldPosition.y -
                    safeRadius);

            int maxZ =
                WorldToCellZ(
                    worldPosition.y +
                    safeRadius);

            float radiusSqr =
                safeRadius *
                safeRadius;

            for (int z = minZ;
                 z <= maxZ;
                 z++)
            {
                for (int x = minX;
                     x <= maxX;
                     x++)
                {
                    Vector2 center =
                        GetCellCenter(
                            x,
                            z);

                    if ((center -
                         worldPosition).sqrMagnitude >
                        radiusSqr)
                    {
                        continue;
                    }

                    int index =
                        ToIndex(
                            x,
                            z);

                    visible[index] = true;
                    explored[index] = true;
                }
            }
        }

        public FogOfWarVisibility GetVisibility(
            Vector2 worldPosition)
        {
            if (!worldBounds.Contains(
                    worldPosition))
            {
                return
                    FogOfWarVisibility.Hidden;
            }

            int x =
                WorldToCellX(
                    worldPosition.x);

            int z =
                WorldToCellZ(
                    worldPosition.y);

            int index =
                ToIndex(
                    x,
                    z);

            if (visible[index])
                return FogOfWarVisibility.Visible;

            if (explored[index])
                return FogOfWarVisibility.Explored;

            return FogOfWarVisibility.Hidden;
        }

        public bool IsExplored(
            Vector2 worldPosition)
        {
            return
                GetVisibility(
                    worldPosition) !=
                FogOfWarVisibility.Hidden;
        }

        private int WorldToCellX(float worldX)
        {
            return
                Mathf.Clamp(
                    Mathf.FloorToInt(
                        (worldX -
                         worldBounds.xMin) /
                        cellWorldSize),
                    0,
                    width - 1);
        }

        private int WorldToCellZ(float worldZ)
        {
            return
                Mathf.Clamp(
                    Mathf.FloorToInt(
                        (worldZ -
                         worldBounds.yMin) /
                        cellWorldSize),
                    0,
                    height - 1);
        }

        private Vector2 GetCellCenter(
            int x,
            int z)
        {
            return new Vector2(
                worldBounds.xMin +
                (x + 0.5f) *
                cellWorldSize,
                worldBounds.yMin +
                (z + 0.5f) *
                cellWorldSize);
        }

        private int ToIndex(
            int x,
            int z)
        {
            return
                z *
                width +
                x;
        }
    }
}
