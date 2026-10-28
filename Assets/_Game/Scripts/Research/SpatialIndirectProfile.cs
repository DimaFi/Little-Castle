using System;
using System.Diagnostics;

namespace LittleCastle.Research
{
    /// <summary>Opt-in diagnostic counters. Per-ray timers are disabled in performance runs.</summary>
    [Serializable]
    public sealed class SpatialIndirectProfile
    {
        public bool enabled;
        public long primaryRays,shadowRays,skyRays,boxTests,skyHits,skyMisses,geometryBuilds,injections,directReweights;
        public long primaryTicks,shadowTicks,skyTicks,projectionTicks;
        public double primaryMs=>Ms(primaryTicks);
        public double shadowMs=>Ms(shadowTicks);
        public double skyMs=>Ms(skyTicks);
        public double projectionMs=>Ms(projectionTicks);
        public static long Stamp()=>Stopwatch.GetTimestamp();
        public static double Ms(long ticks)=>ticks*1000.0/Stopwatch.Frequency;
        public SpatialIndirectProfile Copy()=> (SpatialIndirectProfile)MemberwiseClone();
    }
}
