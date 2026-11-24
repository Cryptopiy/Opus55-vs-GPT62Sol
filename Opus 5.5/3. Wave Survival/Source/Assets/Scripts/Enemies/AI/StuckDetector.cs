using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// S-18: if an agent that wants to move has made less than 0.5 m of progress within 3 s it is "stuck": the first
    /// time its path is recomputed and it is snapped to the nearest valid NavMesh point, repeated failures teleport it
    /// a few metres along its route.
    /// </summary>
    public sealed class StuckDetector
    {
        Vector3 windowStart;
        float windowTime;
        int strikes;

        public float NoProgressTime { get; private set; }
        public int Fixes { get; private set; }

        public void Reset(Vector3 position)
        {
            windowStart = position;
            windowTime = 0f;
            strikes = 0;
            NoProgressTime = 0f;
        }

        public void Tick(EnemyBase e, float dt)
        {
            Vector3 pos = e.transform.position;
            if (!e.WantsToMove)
            {
                windowStart = pos;
                windowTime = 0f;
                NoProgressTime = 0f;
                strikes = 0;
                return;
            }
            windowTime += dt;
            if (MathUtil.FlatDistance(pos, windowStart) >= GameConfig.Enemy.StuckMinProgress)
            {
                // Enough progress: restart the 3 s observation window from here.
                windowStart = pos;
                windowTime = 0f;
                NoProgressTime = 0f;
                strikes = 0;
                return;
            }
            NoProgressTime = windowTime;
            if (windowTime >= GameConfig.Enemy.StuckWindow)
            {
                strikes++;
                Fixes++;
                e.Unstick(strikes);
                windowStart = e.transform.position;
                windowTime = 0f;
                NoProgressTime = 0f;
            }
        }
    }
}
