using System.Collections.Generic;
using UnityEngine;
using F = DestructionSandbox.GameConfig.Fracture;

namespace DestructionSandbox
{
    /// <summary>
    /// D-13 / D-14: a fixed pool of 1500 shards created at start-up. Spawning beyond the budget recycles the
    /// oldest active shard. Shards settled for 12 s shrink to zero over 1.5 s and return to the pool.
    /// </summary>
    public sealed class ShardManager : MonoBehaviour
    {
        private Shard[] all;
        private readonly Stack<Shard> free = new Stack<Shard>(F.ShardBudget);
        private readonly List<Shard> shrinking = new List<Shard>(256);
        private Shard head;   // oldest
        private Shard tail;   // newest
        private float settleTimer;
        private MaterialLibrary materials;

        public int ActiveCount { get; private set; }
        public int PoolSize => all.Length;
        public int FreeCount => free.Count;
        public int RecycledOldest { get; private set; }
        public int PeakActive { get; private set; }
        public Shard Head => head;

        public void Init(MaterialLibrary mats, MeshLibrary meshes)
        {
            materials = mats;
            all = new Shard[F.ShardBudget];
            for (int i = 0; i < all.Length; i++)
            {
                all[i] = Shard.Create(transform, i, meshes);
                free.Push(all[i]);
            }
        }

        public Shard Spawn(BlockMaterialType material, Vector3 position, Quaternion rotation, Vector3 size, float mass,
            Vector3 velocity, Vector3 angularVelocity, uint userValue)
        {
            Shard s;
            if (free.Count > 0) s = free.Pop();
            else
            {
                s = head;                      // budget reached: recycle the oldest shard
                RemoveActive(s);
                s.Deactivate();
                RecycledOldest++;
            }
            s.Activate(material, position, rotation, size, mass, velocity, angularVelocity, userValue, materials);
            PerfProbe.CountSpawn();
            Append(s);
            if (ActiveCount > PeakActive) PeakActive = ActiveCount;
            return s;
        }

        public void Recycle(Shard s)
        {
            if (!s.InUse) return;
            RemoveActive(s);
            s.Deactivate();
            free.Push(s);
        }

        public void ClearAll()
        {
            while (head != null) Recycle(head);
            shrinking.Clear();
        }

        public void ResetStats()
        {
            RecycledOldest = 0;
            PeakActive = ActiveCount;
        }

        /// <summary>D-27: after each step no shard may move faster than the clamp (PhysX clamps before its solver only).</summary>
        public void ClampSpeeds(float max)
        {
            float max2 = max * max;
            float fast2 = GameConfig.Physics.FastBodySpeed * GameConfig.Physics.FastBodySpeed;
            float floor = -GameConfig.World.BelowGroundLimit;
            Shard s = head;
            while (s != null)
            {
                Shard next = s.Next;
                Rigidbody rb = s.Body;
                if (!s.Shrinking && !rb.IsSleeping())
                {
                    if (rb.position.y < floor) { Recycle(s); s = next; continue; }   // squeezed into the ground
                    Vector3 v = rb.linearVelocity;
                    float sq = v.sqrMagnitude;
                    if (sq > max2) rb.linearVelocity = v * (max / v.magnitude);
                    if (sq > fast2) RadialImpulse.MarkFast(rb);
                }
                s = next;
            }
        }

        /// <summary>Number of active shards whose rigidbody is awake (debug overlay).</summary>
        public int CountAwake()
        {
            int awake = 0;
            for (Shard s = head; s != null; s = s.Next)
                if (!s.Shrinking && !s.Body.IsSleeping()) awake++;
            return awake;
        }

        private void Append(Shard s)
        {
            s.Prev = tail;
            s.Next = null;
            if (tail != null) tail.Next = s; else head = s;
            tail = s;
            ActiveCount++;
        }

        private void RemoveActive(Shard s)
        {
            if (s.Prev != null) s.Prev.Next = s.Next; else head = s.Next;
            if (s.Next != null) s.Next.Prev = s.Prev; else tail = s.Prev;
            s.Prev = s.Next = null;
            ActiveCount--;
            if (s.Shrinking) shrinking.Remove(s);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Shrinking shards animate every frame.
            for (int i = shrinking.Count - 1; i >= 0; i--)
            {
                Shard s = shrinking[i];
                s.ShrinkElapsed += dt;
                float t = s.ShrinkElapsed / F.ShardShrinkTime;
                if (t >= 1f)
                {
                    shrinking.RemoveAt(i);
                    Recycle(s);
                }
                else s.SetShrink(t);
            }

            // Settle checks are cheap but there can be 1500 shards: run them a few times per second.
            settleTimer += dt;
            if (settleTimer < F.SettleCheckInterval) return;
            float interval = settleTimer;
            settleTimer = 0f;
            float vLimit = F.ShardSettledSpeed * F.ShardSettledSpeed;
            float wLimit = F.ShardSettledAngularSpeed * F.ShardSettledAngularSpeed;
            float killY = GameConfig.World.KillY;

            Shard cur = head;
            while (cur != null)
            {
                Shard next = cur.Next;
                if (!cur.Shrinking)
                {
                    Rigidbody rb = cur.Body;
                    Vector3 p = rb.position;
                    if (p.y < killY || float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z))
                    {
                        Recycle(cur);       // safety net (D-27): never keep a lost or invalid body
                    }
                    else
                    {
                        RadialImpulse.ReleaseIfSlow(rb);
                        bool settled = rb.IsSleeping() ||
                                       (rb.linearVelocity.sqrMagnitude < vLimit && rb.angularVelocity.sqrMagnitude < wLimit);
                        cur.SettledTime = settled ? cur.SettledTime + interval : 0f;
                        if (cur.SettledTime >= F.ShardSettleTime)
                        {
                            cur.BeginShrink();
                            shrinking.Add(cur);
                        }
                    }
                }
                cur = next;
            }
        }
    }
}
