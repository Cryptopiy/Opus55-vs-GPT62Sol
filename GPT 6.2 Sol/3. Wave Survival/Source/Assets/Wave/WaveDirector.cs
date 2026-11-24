using UnityEngine;
using static WaveSurvival.GameConfig;

namespace WaveSurvival
{
    internal sealed class WaveDirector
    {
        CoreGame game;
        readonly int[] queue = new int[4];
        public int Number { get; private set; }
        public int Gate { get; private set; } = -1;
        public float TimeUntilSpawn => Gate < 0 ? 0 : Mathf.Max(0, spawnAt - Time.time);
        public bool IsBreak { get; private set; }
        public float Countdown => Mathf.Max(0, breakEnd - Time.time);
        public Enemy Boss { get; private set; }
        public long Remaining => game.Alive + Queued;
        long Queued => (long)queue[0] + queue[1] + queue[2] + queue[3];
        float breakEnd, spawnAt;
        int group;

        public WaveDirector(CoreGame game)
        {
            this.game = game;
            Begin(StartWave);
        }

        public void Begin(int wave)
        {
            Number = wave;
            Boss = null;
            Gate = -1;
            group = 0;
            IsBreak = false;
            System.Array.Clear(queue, 0, queue.Length);
            if (wave == BossWave) queue[(int)EnemyKind.Warden] = 1;
            else
            {
                queue[(int)EnemyKind.Runner] = (int)System.Math.Min(int.MaxValue, RunnerBase + (long)RunnerGrowth * wave);
                queue[(int)EnemyKind.Shooter] = (int)System.Math.Min(int.MaxValue, System.Math.Floor(ShooterGrowth * (double)(wave - StartWave)));
                queue[(int)EnemyKind.Tank] = wave / TankWaveStep;
            }
            spawnAt = Time.time + GateWarning;
            game.WaveHorn();
        }

        public void Break(int next)
        {
            Number = next;
            Gate = -1;
            group = 0;
            Boss = null;
            System.Array.Clear(queue, 0, queue.Length);
            game.Guns.AddAmmo(WaveRifle, WaveShells);
            IsBreak = true;
            breakEnd = Time.time + WaveBreak;
        }

        public void Summon(EnemyKind kind, int count) => queue[(int)kind] = Mathf.Min(AliveCap, queue[(int)kind] + count);

        public void Tick()
        {
            if (IsBreak)
            {
                if (Time.time >= breakEnd) Begin(Number);
                return;
            }
            if (Queued == 0)
            {
                if (game.Alive != 0 || Number == BossWave) return;
                game.Stats.WavesCleared++;
                Break(Number < int.MaxValue ? Number + 1 : Number);
                return;
            }
            int slots = AliveCap - game.Alive;
            if (Gate < 0 && Time.time >= spawnAt - GateWarning && slots > 0)
            {
                int available = (int)System.Math.Min(Queued, slots);
                int max = Mathf.Min(available, GroupMax);
                if (max < GroupMin && Queued > 1) return;
                int min = Mathf.Min(GroupMin, max);
                group = Random.Range(min, max + 1);
                // Keep the next group viable at both the wave boundary and the living-enemy cap.
                if (available - group == 1 && available > GroupMin)
                {
                    if (group < max) group++;
                    else if (group > min) group--;
                }
                Gate = ChooseGate();
                spawnAt = Mathf.Max(spawnAt, Time.time + GateWarning);
            }
            if (Gate < 0 || Time.time < spawnAt) return;
            for (int i = 0; i < group && i < slots; i++)
            {
                int kind = NextKind();
                if (kind < 0) break;
                Enemy enemy = game.SpawnAtGate((EnemyKind)kind, Gate, i);
                if (enemy == null) continue;
                if (enemy.Kind == EnemyKind.Warden) Boss = enemy;
                queue[kind]--;
            }
            Gate = -1;
            spawnAt += GroupInterval;
        }

        int NextKind()
        {
            if (queue[(int)EnemyKind.Warden] > 0) return (int)EnemyKind.Warden;
            long roll = System.Math.Min(Queued - 1, (long)(Random.value * Queued));
            for (int i = 0; i < queue.Length; i++)
            {
                if (roll < queue[i]) return i;
                roll -= queue[i];
            }
            return -1;
        }

        int ChooseGate()
        {
            int bestTier = -1, matching = 0, chosen = 0;
            var camera = game.Player.Camera;
            for (int i = 0; i < game.Arena.Gates.Length; i++)
            {
                Vector3 point = game.Arena.Gates[i];
                Vector3 projected = camera.WorldToViewportPoint(point + Vector3.up * 1.5f);
                bool visible = projected.z > 0 && projected.x >= 0 && projected.x <= 1 && projected.y >= 0 && projected.y <= 1
                    && Arena.Clear(game.Player.Eyes, point + Vector3.up * 1.5f);
                bool far = (point - game.Player.Position).sqrMagnitude >= GateDistance * GateDistance;
                int tier = (far ? 2 : 0) + (visible ? 0 : 1);
                if (tier < bestTier) continue;
                if (tier > bestTier) { bestTier = tier; matching = 0; }
                if (Random.Range(0, ++matching) == 0) chosen = i;
            }
            return chosen;
        }
    }
}

