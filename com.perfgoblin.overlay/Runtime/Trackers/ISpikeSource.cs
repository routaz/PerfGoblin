#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace PerfGoblin
{
    public interface ISpikeSource
    {
        IEnumerable<SpikeEvent> RecentSpikes { get; }
        int TotalSpikeCount { get; }
    }
}
#endif
