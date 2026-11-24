using System.Diagnostics;
using UnityEngine;

namespace DestructionSandbox
{
    /// <summary>
    /// Cheap per-frame timing counters (Stopwatch ticks, no allocations) shown in the F1 overlay and used by the
    /// self-test to explain frame spikes: physics step time, fracture work, shard spawns and recycles.
    /// </summary>
    public static class PerfProbe
    {
        private static long stepStart;
        private static long sectionStart;

        public static double LastFramePhysicsMs { get; private set; }
        public static double LastFrameFractureMs { get; private set; }
        public static int LastFrameSteps { get; private set; }
        public static int LastFrameBreaks { get; private set; }
        public static int LastFrameSpawns { get; private set; }

        private static long physicsTicks, fractureTicks;
        private static int steps, breaks, spawns;

        private static double ToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        /// <summary>Called once per frame (earliest Update) to publish the previous frame's numbers.</summary>
        public static void EndFrame()
        {
            LastFramePhysicsMs = ToMs(physicsTicks);
            LastFrameFractureMs = ToMs(fractureTicks);
            LastFrameSteps = steps;
            LastFrameBreaks = breaks;
            LastFrameSpawns = spawns;
            physicsTicks = fractureTicks = 0;
            steps = breaks = spawns = 0;
        }

        public static void MarkStepStart() => stepStart = Stopwatch.GetTimestamp();

        public static void MarkStepEnd()
        {
            if (stepStart != 0) physicsTicks += Stopwatch.GetTimestamp() - stepStart;
            stepStart = 0;
            steps++;
        }

        public static void BeginSection() => sectionStart = Stopwatch.GetTimestamp();
        public static void EndFractureSection() => fractureTicks += Stopwatch.GetTimestamp() - sectionStart;
        public static void CountBreak() => breaks++;
        public static void CountSpawn() => spawns++;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            stepStart = sectionStart = physicsTicks = fractureTicks = 0;
            steps = breaks = spawns = 0;
        }
    }
}
