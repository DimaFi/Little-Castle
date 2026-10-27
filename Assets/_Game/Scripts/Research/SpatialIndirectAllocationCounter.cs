using System;
using UnityEngine.Profiling;

namespace LittleCastle.Research
{
    /// <summary>Optional allocation-call diagnostics, separate from Release timings.</summary>
    public sealed class SpatialIndirectAllocationCounter
    {
        readonly Recorder recorder;
        public bool Supported {get;}
        public SpatialIndirectAllocationCounter(bool requested)
        {
            if(!requested)return;
            recorder=Recorder.Get("GC.Alloc");recorder.enabled=false;recorder.FilterToCurrentThread();
            recorder.enabled=true;GC.KeepAlive(new byte[4096]);recorder.enabled=false;
            Supported=recorder.sampleBlockCount>0;
        }
        public void Begin(){if(Supported)recorder.enabled=true;}
        public long End(){if(!Supported)return -1;recorder.enabled=false;return recorder.sampleBlockCount;}
    }
}
