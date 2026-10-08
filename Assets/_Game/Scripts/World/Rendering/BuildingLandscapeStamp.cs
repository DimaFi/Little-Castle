using System;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Presentation footprint derived from a placed building. The same local
    /// profile can be evaluated at any world position/yaw and on either side
    /// of a chunk border; no scene coordinates or GameObjects are stored here.
    /// Local +Z is the entrance direction.
    /// </summary>
    [Serializable]
    public struct BuildingLandscapeStamp
    {
        public long stableId;
        public Vector2 center;
        public Vector2 halfExtents;
        public float yawDegrees;
        public float entranceHalfWidth;
        public float entranceLength;

        public BuildingLandscapeStamp(long stableId, Vector2 center,
            Vector2 halfExtents, float yawDegrees, float entranceHalfWidth,
            float entranceLength)
        {
            this.stableId = stableId;
            this.center = center;
            this.halfExtents = new Vector2(Mathf.Max(.1f,halfExtents.x),
                Mathf.Max(.1f,halfExtents.y));
            this.yawDegrees = yawDegrees;
            this.entranceHalfWidth = Mathf.Max(0,entranceHalfWidth);
            this.entranceLength = Mathf.Max(0,entranceLength);
        }

        public Vector2 WorldToLocal(Vector2 point)
        {
            Vector2 delta = point-center;
            float angle = -yawDegrees*Mathf.Deg2Rad;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            return new Vector2(c*delta.x+s*delta.y,-s*delta.x+c*delta.y);
        }

        public Vector2 LocalToWorld(Vector2 point)
        {
            float angle = yawDegrees*Mathf.Deg2Rad;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            return center+new Vector2(c*point.x+s*point.y,-s*point.x+c*point.y);
        }

        // Negative inside the footprint, zero on its edge, positive outside.
        public float SignedDistance(Vector2 point)
        {
            Vector2 local = WorldToLocal(point);
            Vector2 d = new Vector2(Mathf.Abs(local.x)-halfExtents.x,
                Mathf.Abs(local.y)-halfExtents.y);
            return new Vector2(Mathf.Max(d.x,0),Mathf.Max(d.y,0)).magnitude+
                Mathf.Min(Mathf.Max(d.x,d.y),0);
        }

        public bool IsEntrance(Vector2 point)
        {
            Vector2 local = WorldToLocal(point);
            return Mathf.Abs(local.x) <= entranceHalfWidth &&
                local.y >= halfExtents.y &&
                local.y <= halfExtents.y+entranceLength;
        }

        /// <summary>Deterministic exterior point for border planting, u in [0,1).</summary>
        public Vector2 PerimeterPoint(float u, float outwardOffset)
        {
            float x = halfExtents.x, z = halfExtents.y;
            float lengthX = 2*x, lengthZ = 2*z;
            float t = Mathf.Repeat(u,1)*2*(lengthX+lengthZ);
            float o = Mathf.Max(0,outwardOffset);
            Vector2 local;
            if(t < lengthX) local = new Vector2(-x+t,-z-o);
            else if((t-=lengthX) < lengthZ) local = new Vector2(x+o,-z+t);
            else if((t-=lengthZ) < lengthX) local = new Vector2(x-t,z+o);
            else { t-=lengthX; local = new Vector2(-x-o,z-t); }
            return LocalToWorld(local);
        }
    }
}
