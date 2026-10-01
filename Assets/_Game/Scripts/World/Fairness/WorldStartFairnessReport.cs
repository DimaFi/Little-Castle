using System;
using System.Collections.Generic;

namespace LittleCastle.World
{
    /// <summary>
    /// Result of evaluating one world seed for a specific finite session.
    ///
    /// A rejected report means the seed should be rerolled before the match,
    /// not that the generator should secretly inject resources around a player.
    /// </summary>
    [Serializable]
    public sealed class WorldStartFairnessReport
    {
        private readonly List<WorldPlayerStartData> starts =
            new List<WorldPlayerStartData>();

        public bool accepted;
        public string rejectionReason;

        public int requestedPlayerCount;
        public int candidatesGenerated;
        public int viableCandidates;

        public float minimumStartDistance;
        public float minimumSelectedScore;
        public float maximumSelectedScore;
        public float selectedScoreSpread;

        public IReadOnlyList<WorldPlayerStartData> Starts => starts;

        public void AddStart(WorldPlayerStartData start)
        {
            starts.Add(start);
        }
    }
}
