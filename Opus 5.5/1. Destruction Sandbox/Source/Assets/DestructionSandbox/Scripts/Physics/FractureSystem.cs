using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using F = DestructionSandbox.GameConfig.Fracture;
using AudioCfg = DestructionSandbox.GameConfig.Audio;

namespace DestructionSandbox
{
    /// <summary>
    /// D-10..D-12: decides which collisions break blocks/shards and performs the break right after the physics
    /// step that produced the collision (WaitForFixedUpdate hook), before the next step and before rendering.
    ///
    /// Break rule: collision impulse magnitude &gt; material threshold (6 / 40 / 180 N*s) AND the contact closed
    /// with at least ImpactSpeedGate. The speed gate is required because a resting contact inside a stack
    /// reports the static support impulse every step (e.g. m*g*dt = 204 N*s for one glass block), which is
    /// already above every threshold; without the gate the structures would shatter under their own weight.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class FractureSystem : MonoBehaviour
    {
        private struct Request
        {
            public Block Block;
            public Shard Shard;
            public int ShardGeneration;
            public Vector3 Point;
            public float Impulse;
            public Projectile Projectile;
            public Vector3 ProjectilePreVelocity;
            public float Time;
            public bool Done;
        }

        private readonly List<Request> queue = new List<Request>(512);
        private readonly ShardBox[] octants = new ShardBox[8];
        private readonly WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();
        private int stepIndex;

        public int BlocksBroken { get; private set; }
        public int ShardsBroken { get; private set; }
        /// <summary>Blocks fractured per material (Stone, Wood, Glass) since the last ResetStats.</summary>
        public int[] BrokenByMaterial { get; } = new int[BlockMaterials.Count];
        /// <summary>Smallest impulse/threshold ratio of any break since the last ResetStats (must stay above 1).</summary>
        public float MinBreakImpulseRatio { get; private set; } = float.MaxValue;
        public int PendingBreaks => queue.Count;
        public int DroppedShardBreaks { get; private set; }
        public float LastBreakTime { get; private set; } = -1f;

        private IEnumerator Start()
        {
            while (true)
            {
                yield return waitForFixedUpdate;
                PerfProbe.MarkStepEnd();
                stepIndex++;
                PerfProbe.BeginSection();
                ProcessQueue();
                Game.Explosions.ProcessPending();
                PerfProbe.EndFractureSection();
                Game.Structures.ClampSpeeds(GameConfig.Physics.MaxLinearVelocity);
                Game.Shards.ClampSpeeds(GameConfig.Physics.MaxLinearVelocity);
                Game.Audio.FlushImpacts();
            }
        }

        public void ClearQueue()
        {
            queue.Clear();
        }

        public void ResetStats()
        {
            BlocksBroken = 0;
            ShardsBroken = 0;
            MinBreakImpulseRatio = float.MaxValue;
            for (int i = 0; i < BrokenByMaterial.Length; i++) BrokenByMaterial[i] = 0;
        }

        public void OnBlockCollision(Block b, Collision c)
        {
            BlockMaterialType mat = b.Def.Material;
            if (!Evaluate(c, mat, out Vector3 point, out float impulse, out bool breaks)) return;
            if (!breaks || b.BreakQueued) return;
            b.BreakQueued = true;
            Enqueue(new Request { Block = b, Point = point, Impulse = impulse }, c);
        }

        public void OnShardCollision(Shard s, Collision c)
        {
            if (!Evaluate(c, s.Material, out Vector3 point, out float impulse, out bool breaks)) return;
            // D-12: shards smaller than the limit in every dimension never subdivide again.
            if (!breaks || !s.CanBreak || s.BreakQueued) return;
            s.BreakQueued = true;
            Enqueue(new Request { Shard = s, ShardGeneration = s.Generation, Point = point, Impulse = impulse }, c);
        }

        /// <summary>Returns false for contacts that are not impacts; also feeds the impact-sound requests.</summary>
        private static bool Evaluate(Collision c, BlockMaterialType mat, out Vector3 point, out float impulse, out bool breaks)
        {
            ContactPoint contact = c.GetContact(0);
            point = contact.point;
            impulse = c.impulse.magnitude;
            float impactSpeed = Mathf.Abs(Vector3.Dot(c.relativeVelocity, contact.normal));
            breaks = false;
            if (impactSpeed < AudioCfg.ImpactSoundMinSpeed && impactSpeed < F.CrushSpeedGate) return false;
            if (impactSpeed >= AudioCfg.ImpactSoundMinSpeed) Game.Audio.RequestImpact(mat, point, impulse, false);
            float threshold = BlockMaterials.BreakImpulse(mat);
            bool impact = impactSpeed >= F.ImpactSpeedGate;
            bool crush = impactSpeed >= F.CrushSpeedGate && impulse >= threshold * F.CrushImpulseFactor;
            breaks = (impact || crush) && impulse > threshold;
            return true;
        }

        private void Enqueue(Request r, Collision c)
        {
            Rigidbody other = c.rigidbody;
            if (other != null && other.gameObject.layer == GameConfig.Layers.Projectile && other.TryGetComponent(out Projectile p))
            {
                r.Projectile = p;
                r.ProjectilePreVelocity = p.PreStepVelocity;
            }
            r.Time = Time.time;
            queue.Add(r);
        }

        /// <summary>
        /// Immediate fracture used by the explosion blast: an exposed piece whose received blast impulse exceeds
        /// its D-10 threshold breaks at once, so the explosion force that follows also throws its shards.
        /// </summary>
        public bool BlastBreak(Block b, Vector3 point, float impulse)
        {
            if (!b.InUse || b.BreakQueued) return false;
            b.BreakQueued = true;
            BreakBlock(new Request { Block = b, Point = point, Impulse = impulse });
            return true;
        }

        public bool BlastBreak(Shard s, Vector3 point, float impulse)
        {
            if (!s.InUse || s.Shrinking || !s.CanBreak || s.BreakQueued) return false;
            s.BreakQueued = true;
            BreakShard(new Request { Shard = s, ShardGeneration = s.Generation, Point = point, Impulse = impulse });
            return true;
        }

        /// <summary>
        /// Processes queued breaks in arrival order within the per-step shard-spawn budget (at least one break
        /// always goes through). Whatever does not fit waits for the next step.
        /// </summary>
        private void ProcessQueue()
        {
            if (queue.Count == 0) return;
            int budget = F.MaxShardSpawnsPerStep;
            float now = Time.time;

            // Pass 1: structure blocks (never dropped, they only wait when the budget is exhausted).
            for (int i = 0; i < queue.Count; i++)
            {
                Request r = queue[i];
                if (r.Block == null) continue;
                int cost = r.Block.InUse && !r.Block.Def.IsBox ? r.Block.Def.PrismShards.Length : 8;
                if (cost > budget && budget < F.MaxShardSpawnsPerStep) break;
                budget -= cost;
                BreakBlock(r);
                r.Block = null;
                r.Done = true;
                queue[i] = r;
            }

            // Pass 2: shard sub-fractures with the remaining budget; stale ones are dropped.
            for (int i = 0; i < queue.Count; i++)
            {
                Request r = queue[i];
                if (r.Done || r.Shard == null) continue;
                if (now - r.Time > F.MaxShardBreakDelay)
                {
                    if (r.Shard.Generation == r.ShardGeneration) r.Shard.BreakQueued = false;
                    DroppedShardBreaks++;
                }
                else
                {
                    if (budget < 8) continue;
                    budget -= 8;
                    BreakShard(r);
                }
                r.Done = true;
                queue[i] = r;
            }

            queue.RemoveAll(IsDone);
            LastBreakTime = now;
        }

        private static readonly System.Predicate<Request> IsDone = r => r.Done;

        private void BreakBlock(Request r)
        {
            Block b = r.Block;
            if (!b.InUse) return;
            PieceDef def = b.Def;
            Rigidbody rb = b.Body;
            Transform t = b.transform;
            Vector3 pos = t.position;
            Quaternion rot = t.rotation;
            Vector3 v = rb.linearVelocity;
            Vector3 w = rb.angularVelocity;
            Vector3 com = rb.worldCenterOfMass;
            float threshold = BlockMaterials.BreakImpulse(def.Material);
            float scatter = ScatterSpeed(r.Impulse, threshold, def.Mass);
            float density = BlockMaterials.Density(def.Material);

            Game.Structures.OnBlockFractured(b, pos);   // counts + scores + returns the block to the pool

            ShardBox[] boxes;
            int count;
            if (def.IsBox) { BoxSplit.Octants(def.Size, octants); boxes = octants; count = 8; }
            else { boxes = def.PrismShards; count = boxes.Length; }
            Collider ignore = PassThroughCollider(r);
            for (int i = 0; i < count; i++)
                SpawnShard(def.Material, pos, rot, boxes[i], density, v, w, com, scatter, def.UserValue, ignore);

            BlocksBroken++;
            PerfProbe.CountBreak();
            BrokenByMaterial[(int)def.Material]++;
            MinBreakImpulseRatio = Mathf.Min(MinBreakImpulseRatio, r.Impulse / threshold);
            float size = Mathf.Max(def.Size.x, Mathf.Max(def.Size.y, def.Size.z));
            Game.Effects.FractureBurst(def.Material, pos, size, 1f);
            Game.Audio.RequestImpact(def.Material, pos, r.Impulse, true);
            PassThrough(r, threshold);
        }

        private void BreakShard(Request r)
        {
            Shard s = r.Shard;
            // The shard may have been recycled (budget) and reused since the request was made.
            if (!s.InUse || s.Shrinking || s.Generation != r.ShardGeneration) return;
            Rigidbody rb = s.Body;
            Transform t = s.transform;
            Vector3 pos = t.position;
            Quaternion rot = t.rotation;
            Vector3 v = rb.linearVelocity;
            Vector3 w = rb.angularVelocity;
            Vector3 com = rb.worldCenterOfMass;
            Vector3 size = s.CellSize;
            BlockMaterialType mat = s.Material;
            uint userValue = s.UserValue;
            float threshold = BlockMaterials.BreakImpulse(mat);
            float scatter = ScatterSpeed(r.Impulse, threshold, rb.mass);
            float density = BlockMaterials.Density(mat);

            Game.Shards.Recycle(s);   // free the slot first so one of the children can reuse it
            BoxSplit.Octants(size, octants);
            Collider ignore = PassThroughCollider(r);
            for (int i = 0; i < 8; i++)
                SpawnShard(mat, pos, rot, octants[i], density, v, w, com, scatter, userValue, ignore);

            ShardsBroken++;
            PerfProbe.CountBreak();
            MinBreakImpulseRatio = Mathf.Min(MinBreakImpulseRatio, r.Impulse / threshold);
            float maxSize = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            Game.Effects.FractureBurst(mat, pos, maxSize, 0.5f);
            Game.Audio.RequestImpact(mat, pos, r.Impulse, true);
            PassThrough(r, threshold);
        }

        /// <summary>
        /// D-11: each shard gets its mass share (volume x density) and the parent's rigid-body velocity at its
        /// own centre (v + w x r) plus a small outward scatter that grows with how far the impulse exceeded
        /// the threshold, so a shattered block visibly falls apart instead of staying a perfect 2x2x2 stack.
        /// </summary>
        /// <summary>
        /// The cannonball that shattered a piece flies on through that piece's own debris (Physics.IgnoreCollision
        /// between the ball and the new shards; Unity drops the pair when either object is deactivated/pooled).
        /// Otherwise the ball would spend its remaining momentum re-hitting the fragments it just made.
        /// </summary>
        private static Collider PassThroughCollider(Request r)
        {
            Projectile p = r.Projectile;
            return p != null && p.InUse && p.Type == WeaponType.Cannonball ? p.Collider : null;
        }

        private static void SpawnShard(BlockMaterialType mat, Vector3 pos, Quaternion rot, ShardBox box, float density,
            Vector3 v, Vector3 w, Vector3 com, float scatter, uint userValue, Collider ignore)
        {
            Vector3 offset = rot * box.LocalCenter;
            Vector3 center = pos + offset;
            Vector3 dir = offset.sqrMagnitude > 1e-8f ? offset.normalized : Random.onUnitSphere;
            Vector3 velocity = v + Vector3.Cross(w, center - com) + dir * scatter;
            velocity = Vector3.ClampMagnitude(velocity, GameConfig.Physics.MaxLinearVelocity);   // D-27 holds from the first step
            Vector3 angular = w + Random.insideUnitSphere * (F.ScatterAngularSpeed * scatter / F.MaxScatterSpeed);
            angular = Vector3.ClampMagnitude(angular, GameConfig.Physics.MaxAngularSpeed);
            // box.Size is the exact 2x2x2 cell; the shard insets only its drawn/collision box (mass = full cell share).
            Shard shard = Game.Shards.Spawn(mat, center, rot, box.Size, box.Volume * density, velocity, angular, userValue);
            if (ignore != null) Physics.IgnoreCollision(ignore, shard.Collider, true);
        }

        private static float ScatterSpeed(float impulse, float threshold, float mass)
        {
            float excess = Mathf.Max(0f, impulse - threshold);
            return Mathf.Clamp(excess / Mathf.Max(mass, 0.01f) * F.ScatterImpulseFraction, F.MinScatterSpeed, F.MaxScatterSpeed);
        }

        /// <summary>
        /// A piece that fails can only resist with its threshold impulse, so a cannonball keeps the rest of its
        /// momentum and ploughs on (instead of bouncing off a block that no longer exists).
        /// </summary>
        private void PassThrough(Request r, float threshold)
        {
            Projectile p = r.Projectile;
            if (p == null || !p.InUse || p.Type != WeaponType.Cannonball) return;
            if (p.PassStamp != stepIndex)
            {
                p.PassStamp = stepIndex;
                p.PassVelocity = r.ProjectilePreVelocity;
            }
            float speed = p.PassVelocity.magnitude;
            if (speed < 0.01f) return;
            float keep = Mathf.Max(0f, 1f - threshold / (p.Body.mass * speed));
            p.PassVelocity *= keep;
            if (p.PassVelocity.sqrMagnitude > p.Body.linearVelocity.sqrMagnitude) p.Body.linearVelocity = p.PassVelocity;
        }
    }
}
