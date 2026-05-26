using System.Diagnostics;

namespace PerfGoblin
{
    /// <summary>
    /// Public API for manual performance instrumentation.
    /// Calls are automatically stripped in release builds — no #if wrappers needed.
    ///
    /// Usage:
    ///   PerfGoblinProfiler.BeginSample("MyMethod");
    ///   // ... your code ...
    ///   PerfGoblinProfiler.EndSample("MyMethod");
    /// </summary>
    public static class PerfGoblinProfiler
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void BeginSample(string name)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PerfGoblinManager.BeginSample(name);
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void EndSample(string name)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PerfGoblinManager.EndSample(name);
#endif
        }
    }
}
