using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace WaveSurvival
{
    /// <summary>
    /// Owns the enemy pools (pre-warmed, hard-capped), the list of active enemies and the alive counters used by the
    /// 25-alive cap (S-19), the HUD and the debug overlay.
    /// </summary>
    public sealed class EnemyManager : MonoBehaviour
    {
        ComponentPool<RunnerEnemy> runners;
        ComponentPool<ShooterEnemy> shooters;
        ComponentPool<TankEnemy> tanks;
        WardenBoss warden;
        bool wardenInUse;

        public readonly List<EnemyBase> Active = new List<EnemyBase>(96);
        public readonly int[] AliveByType = new int[4];
        public int AliveCount { get; private set; }
        public WardenBoss Boss => wardenInUse && warden.IsAlive ? warden : null;

        public static EnemyManager Create(Transform parent)
        {
            var go = new GameObject("Enemies");
            go.transform.SetParent(parent, false);
            EnemyManager m = go.AddComponent<EnemyManager>();
            m.runners = new ComponentPool<RunnerEnemy>(() => m.Make<RunnerEnemy>("Runner"), GameConfig.Pools.RunnersWarm, GameConfig.Pools.Runners);
            m.shooters = new ComponentPool<ShooterEnemy>(() => m.Make<ShooterEnemy>("Shooter"), GameConfig.Pools.ShootersWarm, GameConfig.Pools.Shooters);
            m.tanks = new ComponentPool<TankEnemy>(() => m.Make<TankEnemy>("Tank"), GameConfig.Pools.TanksWarm, GameConfig.Pools.Tanks);
            m.warden = m.Make<WardenBoss>("Warden");
            m.warden.gameObject.SetActive(false);
            return m;
        }

        T Make<T>(string name) where T : EnemyBase
        {
            // Built inactive so the NavMeshAgent never tries to bind before it is placed on the NavMesh.
            var go = new GameObject(name);
            go.SetActive(false);
            go.layer = GameConfig.Layers.Enemy;
            go.transform.SetParent(transform, false);
            go.AddComponent<NavMeshAgent>();
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            go.AddComponent<CapsuleCollider>();
            T e = go.AddComponent<T>();
            e.Init();
            return e;
        }

        public bool HasFreeSlot => AliveCount < GameConfig.Waves.MaxAlive;

        /// <summary>Spawns an enemy at (or near) a position; returns null when the cap or the pool limit is reached.</summary>
        public EnemyBase Spawn(EnemyType type, Vector3 position, float yaw)
        {
            if (!HasFreeSlot) return null;
            ArenaInfo arena = Game.Arena;
            Vector3 p;
            bool ok = type == EnemyType.Warden ? arena.SampleBoss(position, 6f, out p) : arena.SampleHumanoid(position, GameConfig.Nav.SampleRadius, out p);
            if (!ok) return null;
            EnemyBase e;
            switch (type)
            {
                case EnemyType.Runner: e = runners.Get(); break;
                case EnemyType.Shooter: e = shooters.Get(); break;
                case EnemyType.Tank: e = tanks.Get(); break;
                default:
                    if (wardenInUse) return null;
                    wardenInUse = true;
                    e = warden;
                    break;
            }
            if (e == null) return null;
            AliveCount++;
            AliveByType[(int)type]++;
            Active.Add(e);
            e.Spawn(p, yaw);
            Game.Fx.SpawnBurst(p, e.GlowColor);
            EnemyBase.EventHook?.Invoke(e, "spawned");
            return e;
        }

        /// <summary>Called exactly once when an enemy stops counting as alive (death or forced despawn).</summary>
        public void OnEnemyDied(EnemyBase e)
        {
            AliveCount = Mathf.Max(0, AliveCount - 1);
            AliveByType[(int)e.Type] = Mathf.Max(0, AliveByType[(int)e.Type] - 1);
            Game.Waves?.OnEnemyDied(e);
        }

        public void Release(EnemyBase e)
        {
            Active.Remove(e);
            switch (e.Type)
            {
                case EnemyType.Runner: runners.Release((RunnerEnemy)e); break;
                case EnemyType.Shooter: shooters.Release((ShooterEnemy)e); break;
                case EnemyType.Tank: tanks.Release((TankEnemy)e); break;
                default:
                    e.gameObject.SetActive(false);
                    wardenInUse = false;
                    break;
            }
        }

        /// <summary>Removes every enemy immediately (F3 cheat).</summary>
        public void DespawnAll()
        {
            for (int i = Active.Count - 1; i >= 0; i--) Active[i].Despawn();
        }

        /// <summary>Kills everything except <paramref name="keep"/> without score or drops (boss defeat).</summary>
        public void KillAllExcept(EnemyBase keep)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                EnemyBase e = Active[i];
                if (e != keep && e.IsAlive) e.Die(Vector3.back, false, false);
            }
        }

        public int CreatedCount => runners.Created + shooters.Created + tanks.Created + 1;
    }
}
