using System.Collections.Generic;

namespace PerfGoblin.Tests
{
    public class MockFrameTimeSource : IFrameTimeSource
    {
        public float CurrentFPS { get; set; } = 60f;
        public float CurrentFrameTimeMs { get; set; } = 16.67f;
        public float AverageFPS { get; set; } = 60f;
        public float MinFPS { get; set; } = 55f;
        public float MaxFPS { get; set; } = 65f;
        public int TargetFPS { get; set; } = 60;
        public RingBuffer<float> FrameTimeHistory { get; set; } = new RingBuffer<float>(60);
    }

    public class MockScriptSource : IScriptSource
    {
        readonly List<ScriptTimingData> _scripts = new();
        public IReadOnlyList<ScriptTimingData> SortedScripts => _scripts;
        public float TotalScriptTimeMs { get; set; }

        public void AddScript(string name, float ms, PerformanceStatus status)
        {
            _scripts.Add(new ScriptTimingData
            {
                ScriptName = name,
                LastFrameMs = ms,
                Status = status,
                HasPreciseTiming = true
            });
        }

        public void Clear() => _scripts.Clear();
    }

    public class MockSpikeSource : ISpikeSource
    {
        readonly List<SpikeEvent> _spikes = new();
        public IEnumerable<SpikeEvent> RecentSpikes => _spikes;
        public int TotalSpikeCount { get; set; }

        public void AddSpike(SpikeEvent spike)
        {
            _spikes.Add(spike);
            TotalSpikeCount = _spikes.Count;
        }
    }

    public class MockMemorySource : IMemorySource
    {
        readonly List<MemoryCategory> _categories = new();
        public long TotalAllocatedBytes { get; set; }
        public bool ExcludingEditor { get; set; }
        public IReadOnlyList<MemoryCategory> Categories => _categories;
        public bool BaselineReset { get; private set; }

        public void ResetBaseline() => BaselineReset = true;

        public void SetCategories(params MemoryCategory[] cats)
        {
            _categories.Clear();
            _categories.AddRange(cats);
        }
    }
}
