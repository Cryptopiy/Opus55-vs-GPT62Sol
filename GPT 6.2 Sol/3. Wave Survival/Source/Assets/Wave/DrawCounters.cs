using System;
using Unity.Profiling;

namespace WaveSurvival
{
    internal sealed class DrawCounters : IDisposable
    {
        readonly ProfilerRecorder[] buckets =
        {
            ProfilerRecorder.StartNew(ProfilerCategory.Render, "Standard Draw Calls Count"),
            ProfilerRecorder.StartNew(ProfilerCategory.Render, "SRP Batcher Draw Calls Count"),
            ProfilerRecorder.StartNew(ProfilerCategory.Render, "BRG Draw Calls Count"),
            ProfilerRecorder.StartNew(ProfilerCategory.Render, "Standard Instanced Draw Calls Count"),
            ProfilerRecorder.StartNew(ProfilerCategory.Render, "Standard Indirect Draw Calls Count"),
            ProfilerRecorder.StartNew(ProfilerCategory.Render, "BRG Indirect Draw Calls Count")
        };

        public long Count
        {
            get
            {
                long total = 0;
                foreach (ProfilerRecorder bucket in buckets)
                    if (bucket.Valid) total += bucket.LastValue;
                return total;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < buckets.Length; i++) buckets[i].Dispose();
        }
    }
}
