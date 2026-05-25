#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace PerfGoblin.Samples
{
    public class SpikyScript : MonoBehaviour
    {
        [Range(1f, 10f)]
        public float spikeIntervalSeconds = 3f;

        [Range(10, 100)]
        public int spikeMs = 50;

        float _timer;

        void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer >= spikeIntervalSeconds)
            {
                _timer = 0f;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < spikeMs)
                {
                    float x = 0f;
                    for (int i = 0; i < 1000; i++)
                        x += Mathf.Sin(i * 0.001f);
                }
            }
        }
    }
}
#endif
