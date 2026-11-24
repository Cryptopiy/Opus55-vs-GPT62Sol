using System.Collections.Generic;
using UnityEngine;

namespace WaveSurvival
{
    public enum WavePhase { Countdown, Active, Victory }

    /// <summary>
    /// S-19..S-21, S-23, S-24 wave flow. Wave N = 4+2N runners, floor(1.5(N-1)) shooters, floor(N/3) tanks, at most
    /// 25 alive (the rest queue). Groups of 2-4 every 1.5 s from a gate >= 20 m away and preferably out of sight, with
    /// the gate pulsing 1 s first. 8 s breaks with "WAVE N INCOMING" and an ammo top-up. Wave 5 = boss only.
    /// Endless mode continues at N = 6 with the same formulas and no more bosses.
    /// </summary>
    public sealed class WaveManager : MonoBehaviour
    {
        struct PendingGroup
        {
            public SpawnGate Gate;
            public int Count;
            public EnemyType T0, T1, T2, T3;
            public float Timer;
            public bool Active;

            public EnemyType Get(int i) => i == 0 ? T0 : i == 1 ? T1 : i == 2 ? T2 : T3;
            public void Set(int i, EnemyType t) { if (i == 0) T0 = t; else if (i == 1) T1 = t; else if (i == 2) T2 = t; else T3 = t; }
        }

        readonly List<EnemyType> queue = new List<EnemyType>(256);
        readonly PendingGroup[] pending = new PendingGroup[8];
        readonly List<SpawnGate> gateBuffer = new List<SpawnGate>(8);
        int queueIndex;
        float groupTimer;

        public int Wave { get; private set; }
        public WavePhase Phase { get; private set; }
        public float Countdown { get; private set; }
        public bool Endless { get; private set; }
        public bool IsBossWave => !Endless && Wave == GameConfig.Waves.BossWave;
        public int PendingCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < pending.Length; i++) if (pending[i].Active) n += pending[i].Count;
                return n;
            }
        }
        public int QueueRemaining => queue.Count - queueIndex;
        /// <summary>Enemies of this wave still to be dealt with (queued + spawning + alive).</summary>
        public int Remaining => QueueRemaining + PendingCount + Game.Enemies.AliveCount;

        public static WaveManager Create(Transform parent)
        {
            var go = new GameObject("Waves");
            go.transform.SetParent(parent, false);
            return go.AddComponent<WaveManager>();
        }

        public void Begin()
        {
            Wave = 1;
            StartCountdown(GameConfig.Waves.FirstWaveDelay);
        }

        /// <summary>S-19 composition for wave N.</summary>
        public static void Composition(int n, out int runners, out int shooters, out int tanks)
        {
            runners = GameConfig.Waves.RunnerBase + GameConfig.Waves.RunnerPerWave * n;
            shooters = Mathf.FloorToInt(GameConfig.Waves.ShooterFactor * (n - 1));
            tanks = n / GameConfig.Waves.TankDivisor;
        }

        void StartCountdown(float seconds)
        {
            Phase = WavePhase.Countdown;
            Countdown = seconds;
        }

        void StartWave()
        {
            Phase = WavePhase.Active;
            queue.Clear();
            queueIndex = 0;
            groupTimer = 0f;
            Game.Audio.Play2D(SoundId.WaveHorn, 0.9f);
            if (IsBossWave)
            {
                // S-23: no normal enemies; the Warden walks out of the gate farthest from the player.
                queue.Add(EnemyType.Warden);
                SpawnGate gate = FarthestGate();
                QueueGroup(gate, 1);
                EnemyBase.EventHook?.Invoke(null, "wave_start_" + Wave);
                return;
            }
            Composition(Wave, out int r, out int s, out int t);
            for (int i = 0; i < r; i++) queue.Add(EnemyType.Runner);
            for (int i = 0; i < s; i++) queue.Add(EnemyType.Shooter);
            for (int i = 0; i < t; i++) queue.Add(EnemyType.Tank);
            // Shuffle so specials are mixed in, but keep the first group runners for a readable opening.
            for (int i = queue.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (queue[i], queue[j]) = (queue[j], queue[i]);
            }
            int lead = Mathf.Min(GameConfig.Waves.GroupMin, r);
            for (int i = 0; i < lead; i++)
            {
                int k = queue.IndexOf(EnemyType.Runner, i);
                if (k > i) (queue[i], queue[k]) = (queue[k], queue[i]);
            }
            EnemyBase.EventHook?.Invoke(null, "wave_start_" + Wave);
        }

        void Update()
        {
            if (!Game.IsPlaying) return;
            float dt = Time.deltaTime;
            if (Phase == WavePhase.Countdown)
            {
                Countdown -= dt;
                if (Countdown <= 0f) StartWave();
                return;
            }
            if (Phase != WavePhase.Active) return;

            UpdatePending(dt);
            groupTimer -= dt;
            if (groupTimer <= 0f && QueueRemaining > 0)
            {
                groupTimer = GameConfig.Waves.GroupInterval;
                int free = GameConfig.Waves.MaxAlive - Game.Enemies.AliveCount - PendingCount;
                int size = Mathf.Min(Random.Range(GameConfig.Waves.GroupMin, GameConfig.Waves.GroupMax + 1), QueueRemaining, free);
                if (size > 0) QueueGroup(ChooseGate(), size);
            }
            if (QueueRemaining == 0 && PendingCount == 0 && Game.Enemies.AliveCount == 0 && !IsBossWave) WaveCleared();
        }

        void QueueGroup(SpawnGate gate, int size)
        {
            for (int i = 0; i < pending.Length; i++)
            {
                if (pending[i].Active) continue;
                ref PendingGroup g = ref pending[i];
                g.Active = true;
                g.Gate = gate;
                g.Count = size;
                g.Timer = GameConfig.Waves.GatePulse;
                for (int k = 0; k < size; k++) g.Set(k, queue[queueIndex++]);
                gate.Pulse(GameConfig.Waves.GatePulse); // S-20: the gate light pulses 1 s before the group appears
                return;
            }
        }

        void UpdatePending(float dt)
        {
            for (int i = 0; i < pending.Length; i++)
            {
                ref PendingGroup g = ref pending[i];
                if (!g.Active) continue;
                g.Timer -= dt;
                if (g.Timer > 0f) continue;
                g.Active = false;
                float yaw = g.Gate.transform.eulerAngles.y;
                for (int k = 0; k < g.Count; k++)
                {
                    EnemyType type = g.Get(k);
                    Vector3 p = type == EnemyType.Warden ? g.Gate.SpawnCenter + g.Gate.transform.forward * 1.5f : g.Gate.Slots[k % g.Gate.Slots.Length];
                    if (Game.Enemies.Spawn(type, p, yaw) == null)
                    {
                        // Could not spawn (cap/pool): put it back in the queue so the wave can still finish.
                        queue.Insert(queueIndex, type);
                    }
                }
            }
        }

        /// <summary>S-20: random gate >= 20 m from the player, preferring gates the player cannot currently see.</summary>
        SpawnGate ChooseGate()
        {
            List<SpawnGate> gates = Game.Arena.Gates;
            PlayerRig player = Game.Player;
            gateBuffer.Clear();
            for (int i = 0; i < gates.Count; i++)
                if (!gates[i].IsPulsing && MathUtil.FlatDistance(gates[i].SpawnCenter, player.Position) >= GameConfig.Waves.GateMinDistance && !IsVisible(gates[i], player))
                    gateBuffer.Add(gates[i]);
            if (gateBuffer.Count == 0)
                for (int i = 0; i < gates.Count; i++)
                    if (!gates[i].IsPulsing && MathUtil.FlatDistance(gates[i].SpawnCenter, player.Position) >= GameConfig.Waves.GateMinDistance)
                        gateBuffer.Add(gates[i]);
            if (gateBuffer.Count > 0) return gateBuffer[Random.Range(0, gateBuffer.Count)];
            return FarthestGate();
        }

        SpawnGate FarthestGate()
        {
            List<SpawnGate> gates = Game.Arena.Gates;
            SpawnGate best = gates[0];
            float bd = -1f;
            for (int i = 0; i < gates.Count; i++)
            {
                float d = MathUtil.FlatDistance(gates[i].SpawnCenter, Game.Player.Position);
                if (d > bd) { bd = d; best = gates[i]; }
            }
            return best;
        }

        public static bool IsVisible(SpawnGate gate, PlayerRig player)
        {
            Transform cam = player.Camera.transform;
            Vector3 target = gate.SpawnCenter + Vector3.up * 1.2f;
            Vector3 to = target - cam.position;
            float halfH = Mathf.Atan(Mathf.Tan(player.Camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * player.Camera.aspect) * Mathf.Rad2Deg;
            if (Vector3.Angle(cam.forward, to) > halfH + 10f) return false;
            return !Physics.Linecast(cam.position, target, GameConfig.Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);
        }

        void WaveCleared()
        {
            Game.Stats.WavesCleared++;
            Game.Stats.Score += GameConfig.Score.WaveClearPerWave * Wave;
            Wave++;
            GiveBreakAmmo();
            StartCountdown(GameConfig.Waves.BreakTime);
            EnemyBase.EventHook?.Invoke(null, "wave_cleared");
        }

        void GiveBreakAmmo()
        {
            // S-21: +25 rifle reserve and +6 shotgun shells at every break.
            WeaponController w = Game.Player.Weapons;
            w.AddAmmo(WeaponId.Rifle, GameConfig.Waves.BreakRifleAmmo);
            w.AddAmmo(WeaponId.Shotgun, GameConfig.Waves.BreakShotgunShells);
            Game.Hud.Notify("AMMO RESUPPLY", new Color(0.4f, 0.65f, 1f));
        }

        public void OnEnemyDied(EnemyBase e)
        {
            if (e.Type == EnemyType.Warden && IsBossWave) Phase = WavePhase.Victory;
        }

        /// <summary>S-24 Endless: continue at wave 6 with the normal formulas and no more bosses.</summary>
        public void ContinueEndless()
        {
            Endless = true;
            Wave = GameConfig.Waves.BossWave + 1;
            GiveBreakAmmo();
            StartCountdown(GameConfig.Waves.BreakTime);
        }

        /// <summary>F3 cheat: clear the arena and start wave <paramref name="n"/> immediately.</summary>
        public void JumpToWave(int n)
        {
            Game.Enemies.DespawnAll();
            Game.Projectiles.ClearAll();
            for (int i = 0; i < pending.Length; i++) pending[i].Active = false;
            queue.Clear();
            queueIndex = 0;
            Endless = false;
            Wave = n;
            Game.Stats.WavesCleared = Mathf.Max(Game.Stats.WavesCleared, n - 1);
            StartWave();
        }
    }
}
