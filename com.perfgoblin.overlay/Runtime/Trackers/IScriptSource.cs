#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace PerfGoblin
{
    public interface IScriptSource
    {
        IReadOnlyList<ScriptTimingData> SortedScripts { get; }
        float TotalScriptTimeMs { get; }
    }
}
#endif
