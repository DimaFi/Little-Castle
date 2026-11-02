using System;
using System.Collections.Generic;
using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Research
{
    /// <summary>Shared endpoint brush for Editor/Play. Preview never enters session history.</summary>
    public sealed class MapWaterfallToolState
    {
        public int mode;
        public bool GuiTreeChanged {get;private set;}
        public int ModeRevision { get; private set; }
        public long UpperRiverId { get; private set; }
        public long SelectedWaterfallId { get; private set; }
        public float radius = 3;
        public string status;
        public MapWaterfallRouteData PreviewRoute { get; private set; }
        public IReadOnlyList<MapWaterfallCandidate> Candidates => candidates;
        readonly List<MapWaterfallCandidate> candidates = new List<MapWaterfallCandidate>();
        MapRiverNetwork previewGraph;
        MapWaterfallRecord previewRecord;
        long previewHeight;
        int choice;
        Rect previewArea;
        public bool Active => mode != 0;
        public void Begin() { Stop(); mode = 1; ModeRevision++; status = "Выберите выход верхней реки у уступа."; }
        public void BeginPick() { Stop(); mode = 2; ModeRevision++; status = "Щёлкните по объёму водопада."; }
        public void Stop() { mode = 0; UpperRiverId = 0; PreviewRoute = null; previewRecord = null; previewGraph = null; candidates.Clear(); ModeRevision++; }
        public void Click(MapEditSession session, MapToolPointer pointer)
        {
            try
            {
                if (mode == 2) { Pick(session, pointer.ray); return; }
                if (mode != 1 || !pointer.hasSurface) return;
                PreviewRoute = null; previewRecord = null;
                if (UpperRiverId == 0)
                {
                    var upper = session.RiverNetwork.FindUpperEndpoints(pointer.position, radius);
                    if (upper.Count == 0) { status = "Нет свободного выхода реки рядом. Подведите курсор к её концу."; return; }
                    UpperRiverId = upper[0].riverId; status = "Теперь выберите вход нижней реки."; return;
                }
                candidates.Clear(); candidates.AddRange(session.RiverNetwork.FindLowerCandidates(UpperRiverId, pointer.position, radius)); choice = 0;
                if (candidates.Count == 0) { status = "Рядом нет подходящего нижнего конца. Проверьте высоту, направление и занятость."; return; }
                BuildPreview(session);
            }
            catch (Exception e) { status = e.Message; PreviewRoute = null; previewRecord = null; }
        }
        void BuildPreview(MapEditSession session)
        {
            PreviewRoute = null; previewRecord = null;
            var candidate = candidates[choice]; float pad = Mathf.Max(candidate.upper.width, candidate.lower.width) * .6f + 2;
            previewArea = Rect.MinMaxRect(Mathf.Min(candidate.upper.position.x, candidate.lower.position.x) - pad,
                Mathf.Min(candidate.upper.position.y, candidate.lower.position.y) - pad, Mathf.Max(candidate.upper.position.x, candidate.lower.position.x) + pad,
                Mathf.Max(candidate.upper.position.y, candidate.lower.position.y) + pad);
            var snapshot = MapWaterfallSupportCapture.Capture(session, previewArea, true, candidate.upper.riverId, candidate.lower.riverId);
            var route = MapWaterfallRouteBuilder.Build(candidate.upper, candidate.lower, snapshot);
            // Mesh budgets are validated before offering Apply, not after the session changed.
            Mesh validation = MapWaterfallMeshBuilder.Build(route);
            if (Application.isPlaying) UnityEngine.Object.Destroy(validation); else UnityEngine.Object.DestroyImmediate(validation);
            previewRecord = candidate.Record(MapObjectIds.New()); previewRecord.route = route;
            PreviewRoute = route; previewGraph = session.RiverNetwork; previewHeight = session.HeightRevision;
            status = "Маршрут готов. Проверьте кромку, контакт со скалой и чашу, затем примените.";
        }
        public void Apply(MapEditSession session)
        {
            if (previewRecord == null) return;
            try
            {
                if (!ReferenceEquals(previewGraph, session.RiverNetwork) || previewHeight != session.HeightRevision
                    || MapWaterfallSupportCapture.Capture(session, previewArea, true, previewRecord.upstreamRiverId, previewRecord.downstreamRiverId).Revision != PreviewRoute.supportRevision)
                    throw new InvalidOperationException("Рельеф или реки изменились. Повторно выберите нижний конец.");
                session.CreateWaterfall(previewRecord, previewGraph); SelectedWaterfallId = previewRecord.id;
                Stop(); status = "Водопад создан и входит в общую историю и сохранение карты.";
            }
            catch (Exception e) { status = e.Message; PreviewRoute = null; previewRecord = null; }
        }
        void Pick(MapEditSession session, Ray ray)
        {
            float nearest = float.PositiveInfinity; SelectedWaterfallId = 0;
            foreach (var w in session.ExportRiverNetwork().waterfalls)
            {
                if (w.route == null || w.route.IsEmpty || !w.route.Bounds.IntersectRay(ray, out float entry)) continue;
                for (float d = Mathf.Max(0, entry); d <= entry + w.route.Bounds.size.magnitude; d += .08f)
                    if (w.route.Contains(ray.GetPoint(d), .1f))
                    { if (d < nearest) { nearest = d; SelectedWaterfallId = w.id; } break; }
            }
            status = SelectedWaterfallId == 0 ? "Водопад под курсором не найден." : "Выбран водопад. Его можно удалить отдельно от рек.";
        }
        public void Draw(MapEditSession session)
        {
            GuiTreeChanged=false;Action queued=null;void Later(Action action){queued=action;}
            if ((session.Access & MapEditAccess.Rivers) == 0) return;
            GUILayout.Space(8); GUILayout.Label("ВОДОПАДЫ");
            GUILayout.BeginHorizontal(); if (GUILayout.Button("Новый водопад")) Later(Begin); if (GUILayout.Button("Выбрать")) Later(BeginPick); GUILayout.EndHorizontal();
            if (mode == 1)
            {
                GUILayout.Label("Радиус привязки: " + radius.ToString("0.0") + " м"); radius = GUILayout.HorizontalSlider(radius, 0, 8);
                if (UpperRiverId != 0 && GUILayout.Button("Выбрать другой верхний конец")) Later(Begin);
                if (candidates.Count > 1)
                {
                    GUILayout.Label("Подходящий нижний конец: " + (choice + 1) + " из " + candidates.Count);
                    if (GUILayout.Button("Следующий вариант"))Later(()=>
                    {
                        choice = (choice + 1) % candidates.Count;
                        try { BuildPreview(session); } catch (Exception e) { status = e.Message; }
                    });
                }
                if (PreviewRoute != null && GUILayout.Button("Применить водопад")) Later(()=>Apply(session));
            }
            if (Active && GUILayout.Button("Отменить выбор водопада"))Later(()=>{ Stop(); status = "Выбор отменён."; });
            if (SelectedWaterfallId != 0 && session.HasWaterfall(SelectedWaterfallId))
            {
                GUILayout.Label("Выбранный водопад · связан с двумя реками");
                if (GUILayout.Button("Удалить выбранный водопад"))Later(()=>
                { session.DeleteWaterfall(SelectedWaterfallId); SelectedWaterfallId = 0; status = "Связь удалена; реки сохранены. Доступна отмена."; });
            }
            if (!string.IsNullOrEmpty(status)) GUILayout.Label(status);
            if(queued!=null){queued();GuiTreeChanged=true;}
        }
    }
}
