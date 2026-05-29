#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace PerfGoblin
{
    public interface IFrameTimeSource
    {
        float CurrentFPS { get; }
        float CurrentFrameTimeMs { get; }
        float AverageFPS { get; }
        float MinFPS { get; }
        float MaxFPS { get; }
        int TargetFPS { get; }
        RingBuffer<float> FrameTimeHistory { get; }
    }
}
#endif
