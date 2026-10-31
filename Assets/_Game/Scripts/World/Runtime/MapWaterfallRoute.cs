using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    public enum MapWaterfallSectionKind { Contact, FreeFall, Impact }

    [Serializable] public sealed class MapWaterfallContactPoint
    {
        // Back surface of the water; front = position + normal * section.thickness.
        public Vector3 position, normal;
        public float thickness;
        public float across = -1;
        public bool detached;
        public string surfaceKey;
        public int triangle = -1;
        public Vector3 barycentric;
        public Vector3 Front(float fallbackThickness) => position + normal * (thickness > 0 ? thickness : fallbackThickness);
        public MapWaterfallContactPoint Copy() => (MapWaterfallContactPoint)MemberwiseClone();
    }

    [Serializable] public sealed class MapWaterfallSection
    {
        public MapWaterfallSectionKind kind;
        public float thickness = .2f, speed = 2;
        public List<MapWaterfallContactPoint> points = new List<MapWaterfallContactPoint>();
        public MapWaterfallSection Copy()
        {
            var copy = (MapWaterfallSection)MemberwiseClone(); copy.points = new List<MapWaterfallContactPoint>();
            foreach (var p in points) copy.points.Add(p?.Copy()); return copy;
        }
        public Vector3 FrontCenter
        {
            get
            {
                int middle = points.Count / 2;
                return points[middle].Front(thickness);
            }
        }
    }

    /// <summary>Saved contact lattice; independent of scene loading, colliders and render LOD.</summary>
    [Serializable] public sealed class MapWaterfallRouteData
    {
        public int version = 1;
        public string supportRevision;
        public List<MapWaterfallSection> sections = new List<MapWaterfallSection>();
        public bool IsEmpty => sections != null && sections.Count == 0;
        public MapWaterfallRouteData Copy()
        {
            var copy = (MapWaterfallRouteData)MemberwiseClone(); copy.sections = new List<MapWaterfallSection>();
            foreach (var s in sections) copy.sections.Add(s?.Copy()); return copy;
        }
        public void Validate(MapRiverEndpoint upper, MapRiverEndpoint lower)
        {
            if (version != 1 || sections == null || sections.Count < 2 || sections.Count > 2048
                || string.IsNullOrEmpty(supportRevision) || supportRevision.Length > 128)
                throw new InvalidDataException("Неверный маршрут водопада.");
            int columns = sections[0]?.points?.Count ?? 0;
            if (columns < 3 || columns > 257 || columns % 2 == 0 || columns * sections.Count > 65536)
                throw new InvalidDataException("Превышен размер контактного маршрута.");
            int triangles = (sections.Count - 1) * (columns - 1) * 4 + (sections.Count - 1) * 4 + (columns - 1) * 4;
            if (triangles > 11200) throw new InvalidDataException("Контактный маршрут превышает бюджет M1. Уменьшите его ширину или длину.");
            Vector3 previous = default;
            for (int row = 0; row < sections.Count; row++)
            {
                var section = sections[row];
                if (section == null || section.points == null || section.points.Count != columns
                    || !Range(section.thickness, .02f, 1) || !Range(section.speed, .02f, 12)
                    || (int)section.kind < 0 || (int)section.kind > (int)MapWaterfallSectionKind.Impact)
                    throw new InvalidDataException("Неверное сечение водопада.");
                foreach (var p in section.points)
                    if (p == null || !Finite(p.position) || !Finite(p.normal) || !Finite(p.barycentric)
                        || Mathf.Abs(p.normal.sqrMagnitude - 1) > .02f || (p.surfaceKey?.Length ?? 0) > 256
                        || p.thickness != 0 && !Range(p.thickness, .004f, 10.05f) || p.across != -1 && !Range(p.across, 0, 1))
                        throw new InvalidDataException("Неверная опора водопада.");
                Vector3 center = section.FrontCenter;
                if (row > 0 && (center.y > previous.y + .02f || (center - previous).sqrMagnitude < 1e-10f))
                {
                    var error=new InvalidDataException("Маршрут водопада идёт вверх или содержит повторные сечения.");
                    error.Data["row"]=row;error.Data["center"]=center;error.Data["previous"]=previous;error.Data["distance"]=(center-previous).magnitude;throw error;
                }
                float width = Vector3.Distance(section.points[0].position, section.points[columns - 1].position);
                if (!Range(width, 1, 120)) throw new InvalidDataException("Неверная ширина маршрута.");
                previous = center;
            }
            CheckEndpoint(sections[0], upper); CheckEndpoint(sections[sections.Count - 1], lower);
        }
        static void CheckEndpoint(MapWaterfallSection section, MapRiverEndpoint endpoint)
        {
            var target = new Vector3(endpoint.position.x, endpoint.waterHeight + .018f, endpoint.position.y);
            var left = section.points[0].position; var right = section.points[section.points.Count - 1].position;
            if (Vector3.Distance(section.FrontCenter, target) > .025f
                || Mathf.Abs(Vector2.Distance(new Vector2(left.x,left.z),new Vector2(right.x,right.z)) - endpoint.width) > .025f)
                throw new InvalidDataException("Маршрут больше не совпадает с концом реки. Обновите предпросмотр.");
        }
        public Bounds Bounds
        {
            get
            {
                var result = new Bounds(sections[0].points[0].position, Vector3.zero);
                foreach (var s in sections) foreach (var p in s.points)
                { result.Encapsulate(p.position); result.Encapsulate(p.Front(s.thickness)); }
                result.Expand(.1f); return result;
            }
        }
        public bool Contains(Vector3 point, float padding = 0)
        {
            var bounds = Bounds; bounds.Expand(padding * 2);
            if (!bounds.Contains(point)) return false;
            for (int row = 1; row < sections.Count; row++)
            {
                var a = sections[row - 1]; var b = sections[row];
                for (int col = 0; col < a.points.Count - 1; col++)
                {
                    if (InPrism(point, a.points[col], a.points[col + 1], b.points[col], a.thickness, a.thickness, b.thickness, padding)
                        || InPrism(point, a.points[col + 1], b.points[col + 1], b.points[col], a.thickness, b.thickness, b.thickness, padding)) return true;
                }
            }
            return false;
        }
        static bool InPrism(Vector3 point, MapWaterfallContactPoint a, MapWaterfallContactPoint b, MapWaterfallContactPoint c,
            float fallbackA, float fallbackB, float fallbackC, float padding)
        {
            Vector3 pa = a.Front(fallbackA), pb = b.Front(fallbackB), pc = c.Front(fallbackC);
            Vector3 ab = pb - pa, ac = pc - pa, normal = Vector3.Cross(ab, ac);
            if (normal.sqrMagnitude < 1e-12f) return false;
            normal.Normalize(); if (Vector3.Dot(normal, a.normal + b.normal + c.normal) < 0) normal = -normal;
            float signed = Vector3.Dot(point - pa, normal);
            float ta = a.thickness > 0 ? a.thickness : fallbackA, tb = b.thickness > 0 ? b.thickness : fallbackB,
                tc = c.thickness > 0 ? c.thickness : fallbackC;
            if (signed > padding || signed < -Mathf.Max(ta, Mathf.Max(tb, tc)) - padding) return false;
            Vector3 projected = point - normal * signed - pa;
            float aa = Vector3.Dot(ab, ab), bb = Vector3.Dot(ac, ac), cross = Vector3.Dot(ab, ac), det = aa * bb - cross * cross;
            if (det < 1e-12f) return false;
            float da = Vector3.Dot(projected, ab), db = Vector3.Dot(projected, ac);
            float u = (bb * da - cross * db) / det, v = (aa * db - cross * da) / det;
            float tolerance = padding / Mathf.Max(.02f, Mathf.Min(ab.magnitude, ac.magnitude));
            return u >= -tolerance && v >= -tolerance && u + v <= 1 + tolerance
                && signed >= -(ta * (1 - u - v) + tb * u + tc * v) - padding;
        }
        static bool Range(float v, float min, float max) => float.IsFinite(v) && v >= min && v <= max;
        internal static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
    }
}
