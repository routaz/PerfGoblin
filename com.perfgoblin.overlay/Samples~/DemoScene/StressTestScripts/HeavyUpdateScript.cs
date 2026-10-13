#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace PerfGoblin.Samples
{
    public class HeavyUpdateScript : MonoBehaviour
    {
        [Range(100, 10000)]
        public int iterations = 5000;

        void Update()
        {
            float result = 0f;
            for (int i = 0; i < iterations; i++)
            {
                result += Mathf.Sin(i * 0.01f) * Mathf.Cos(i * 0.02f);
                result += Mathf.Sqrt(Mathf.Abs(result) + 1f);
            }
        }
    }
}
#endif
