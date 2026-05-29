#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;

namespace PerfGoblin
{
    public struct MemoryCategory
    {
        public string Name;
        public long Bytes;
        public Color DisplayColor;
    }

    public class MemoryTracker : IDisposable, IMemorySource
    {
        readonly PerfGoblinConfig _config;
        readonly List<MemoryCategory> _categories = new(8);

        ProfilerRecorder _textureRec;
        ProfilerRecorder _meshRec;
        ProfilerRecorder _audioRec;

        // Editor baseline captured on first sample
        long _baselineTotal;
        long _baselineGfx;
        long _baselineManaged;
        long _baselineTex;
        long _baselineMesh;
        long _baselineAudio;
        bool _baselineCaptured;

        public long TotalAllocatedBytes { get; private set; }
        public long GfxMemoryBytes { get; private set; }
        public long ManagedHeapBytes { get; private set; }
        public long ManagedHeapReservedBytes { get; private set; }
        public long TotalReservedBytes { get; private set; }
        public bool ExcludingEditor => _config.ExcludeEditorOverhead && _baselineCaptured;
        public IReadOnlyList<MemoryCategory> Categories => _categories;

        static readonly Color ColorTextures = new(0.55f, 0.36f, 0.96f);
        static readonly Color ColorMeshes = new(0.13f, 0.77f, 0.37f);
        static readonly Color ColorAudio = new(0.96f, 0.62f, 0.14f);
        static readonly Color ColorManaged = new(0.23f, 0.51f, 0.96f);
        static readonly Color ColorGfx = new(0.94f, 0.27f, 0.27f);
        static readonly Color ColorOther = new(0.5f, 0.5f, 0.5f);

        public MemoryTracker(PerfGoblinConfig config)
        {
            _config = config;

            _textureRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory", 1);
            _meshRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Mesh Memory", 1);
            _audioRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Audio Used Memory", 1);
        }

        public void Update(int frameCount)
        {
            if (frameCount % _config.MemorySampleInterval != 0) return;

            long rawTotal = Profiler.GetTotalAllocatedMemoryLong();
            long rawReserved = Profiler.GetTotalReservedMemoryLong();
            long rawGfx = Profiler.GetAllocatedMemoryForGraphicsDriver();
            long rawManaged = Profiler.GetMonoUsedSizeLong();
            long rawManagedReserved = Profiler.GetMonoHeapSizeLong();

            long rawTex = _textureRec.Valid ? _textureRec.LastValue : 0;
            long rawMesh = _meshRec.Valid ? _meshRec.LastValue : 0;
            long rawAudio = _audioRec.Valid ? _audioRec.LastValue : 0;

            if (!_baselineCaptured && rawTotal > 0)
            {
                _baselineTotal = rawTotal;
                _baselineGfx = rawGfx;
                _baselineManaged = rawManaged;
                _baselineTex = rawTex;
                _baselineMesh = rawMesh;
                _baselineAudio = rawAudio;
                _baselineCaptured = true;
            }

            bool exclude = _config.ExcludeEditorOverhead && _baselineCaptured;
            long offsetTotal = exclude ? _baselineTotal : 0;
            long offsetGfx = exclude ? _baselineGfx : 0;
            long offsetManaged = exclude ? _baselineManaged : 0;
            long offsetTex = exclude ? _baselineTex : 0;
            long offsetMesh = exclude ? _baselineMesh : 0;
            long offsetAudio = exclude ? _baselineAudio : 0;

            TotalAllocatedBytes = Math.Max(0, rawTotal - offsetTotal);
            TotalReservedBytes = Math.Max(0, rawReserved - (exclude ? _baselineTotal : 0));
            GfxMemoryBytes = Math.Max(0, rawGfx - offsetGfx);
            ManagedHeapBytes = Math.Max(0, rawManaged - offsetManaged);
            ManagedHeapReservedBytes = rawManagedReserved;

            long texBytes = Math.Max(0, rawTex - offsetTex);
            long meshBytes = Math.Max(0, rawMesh - offsetMesh);
            long audioBytes = Math.Max(0, rawAudio - offsetAudio);

            long accounted = texBytes + meshBytes + audioBytes + ManagedHeapBytes + GfxMemoryBytes;
            long other = Math.Max(0, TotalAllocatedBytes - accounted);

            _categories.Clear();
            _categories.Add(new MemoryCategory { Name = "Textures", Bytes = texBytes, DisplayColor = ColorTextures });
            _categories.Add(new MemoryCategory { Name = "Meshes", Bytes = meshBytes, DisplayColor = ColorMeshes });
            _categories.Add(new MemoryCategory { Name = "Audio", Bytes = audioBytes, DisplayColor = ColorAudio });
            _categories.Add(new MemoryCategory { Name = "Managed", Bytes = ManagedHeapBytes, DisplayColor = ColorManaged });
            _categories.Add(new MemoryCategory { Name = "GFX", Bytes = GfxMemoryBytes, DisplayColor = ColorGfx });
            _categories.Add(new MemoryCategory { Name = "Other", Bytes = other, DisplayColor = ColorOther });
        }

        public void ResetBaseline()
        {
            _baselineCaptured = false;
        }

        public void Dispose()
        {
            if (_textureRec.Valid) _textureRec.Dispose();
            if (_meshRec.Valid) _meshRec.Dispose();
            if (_audioRec.Valid) _audioRec.Dispose();
        }
    }
}
#endif
