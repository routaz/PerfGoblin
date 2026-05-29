#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace PerfGoblin
{
    public interface IMemorySource
    {
        long TotalAllocatedBytes { get; }
        bool ExcludingEditor { get; }
        IReadOnlyList<MemoryCategory> Categories { get; }
        void ResetBaseline();
    }
}
#endif
