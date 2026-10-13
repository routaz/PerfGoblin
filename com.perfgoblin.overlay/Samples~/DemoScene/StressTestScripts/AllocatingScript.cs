#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

namespace PerfGoblin.Samples
{
    public class AllocatingScript : MonoBehaviour
    {
        [Range(10, 1000)]
        public int allocationsPerFrame = 100;

        void Update()
        {
            for (int i = 0; i < allocationsPerFrame; i++)
            {
                var list = new List<string>(10);
                list.Add("frame_" + Time.frameCount + "_alloc_" + i);
                string joined = string.Join(",", list);
            }
        }
    }
}
#endif
