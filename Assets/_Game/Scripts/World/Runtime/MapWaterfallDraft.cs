using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Input-independent two-endpoint draft for the future authoring tool.
    /// Does not mutate a MapEditSession or claim permission to edit terrain/water.
    /// The session adapter must commit the returned snapshot in its own atomic command.
    /// </summary>
    public sealed class MapWaterfallDraft
    {
        MapRiverNetwork previewSource;
        List<MapWaterfallCandidate> candidates = new List<MapWaterfallCandidate>();
        public long UpperRiverId { get; private set; }
        public bool HasUpper => UpperRiverId != 0;
        public bool IsAmbiguous => candidates.Count > 1;
        public IReadOnlyList<MapWaterfallCandidate> Candidates => candidates.AsReadOnly();

        public void Begin(MapRiverNetwork source, long upperRiverId)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var upper = source.Endpoint(upperRiverId, false);
            bool open = source.FindUpperEndpoints(upper.position).Exists(e => e.Equals(upper));
            if (!open) throw new InvalidOperationException("Верхний конец уже занят связью.");
            UpperRiverId = upperRiverId; previewSource = null; candidates.Clear();
        }

        public void Preview(MapRiverNetwork source, Vector2 pointer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!HasUpper) throw new InvalidOperationException("Сначала выберите верхний конец реки.");
            var next = source.FindLowerCandidates(UpperRiverId, pointer);
            candidates = next; previewSource = source;
        }

        public MapRiverNetwork PrepareCommit(MapRiverNetwork current, int candidateIndex, long waterfallId)
        {
            if (!ReferenceEquals(current, previewSource) || current == null)
                throw new InvalidOperationException("Речная сеть изменилась. Обновите предпросмотр водопада.");
            if (candidateIndex < 0 || candidateIndex >= candidates.Count) throw new ArgumentOutOfRangeException(nameof(candidateIndex));
            // Keep the draft until the caller has committed successfully; failures,
            // permission rejections and undo are the session adapter's responsibility.
            return current.WithWaterfall(candidates[candidateIndex].Record(waterfallId));
        }

        public void Cancel() { UpperRiverId = 0; previewSource = null; candidates.Clear(); }
    }
}
