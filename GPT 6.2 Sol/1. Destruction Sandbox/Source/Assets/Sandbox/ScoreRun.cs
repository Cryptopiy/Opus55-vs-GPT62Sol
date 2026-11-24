using UnityEngine;
using static DestructionRange.SandboxConfig;

namespace DestructionRange
{
    internal sealed class ScoreRun
    {
        // A block can enter the window at most once per rebuild.
        readonly float[] destructionTimes = new float[BlockCapacity];
        int first, last;
        public int Destroyed { get; private set; }
        public int Score { get; private set; }
        public int Combo { get; private set; } = 1;
        public float Elapsed { get; private set; }

        public void Tick(float dt)
        {
            Elapsed += dt;
            UpdateCombo();
        }

        void UpdateCombo()
        {
            while (first < last && Elapsed - destructionTimes[first] > ComboWindow) first++;
            int earned = Mathf.Min(ComboMax, 1 + (last - first) / ComboStep);
            // The rolling window earns tiers; the earned tier expires when the window has no hits.
            Combo = first == last ? 1 : Mathf.Max(Combo, earned);
        }

        public int Count(Matter kind)
        {
            destructionTimes[last++] = Elapsed;
            UpdateCombo();
            Destroyed++;
            int points = MatterSpecs[(int)kind].Points * Combo;
            Score += points;
            return points;
        }

        public void Reset()
        {
            first = last = Destroyed = Score = 0;
            Combo = 1;
            Elapsed = 0;
        }
    }
}
