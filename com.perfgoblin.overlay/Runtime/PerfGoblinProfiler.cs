using System.Diagnostics;

namespace PerfGoblin
{
    /// <summary>
    /// Public API for manual performance instrumentation.
    /// Available only in the editor and development builds. Wrap calls in
    /// <c>#if UNITY_EDITOR || DEVELOPMENT_BUILD</c> so release builds still compile.
    ///
    /// Usage:
    ///   #if UNITY_EDITOR || DEVELOPMENT_BUILD
    ///   PerfGoblinProfiler.BeginSample("MyMethod");
    ///   #endif
    ///   // ... your code ...
    ///   #if UNITY_EDITOR || DEVELOPMENT_BUILD
    ///   PerfGoblinProfiler.EndSample("MyMethod");
    ///   #endif
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
