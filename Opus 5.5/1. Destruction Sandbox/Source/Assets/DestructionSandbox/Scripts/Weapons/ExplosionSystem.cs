using System.Collections.Generic;
using UnityEngine;
using Wpn = DestructionSandbox.GameConfig.Weapons;
using Layers = DestructionSandbox.GameConfig.Layers;

namespace DestructionSandbox
{
    /// <summary>
    /// D-18 explosion, applied right after the physics step in which the shell touched something (after the
    /// pending collision fractures):
    ///  1. Blast fracture: every block/shard with a clear line of sight to the centre receives the blast impulse
    ///     1500 N*s * (1 - d/6) (d = distance to its closest point). If that impulse exceeds the piece's D-10
    ///     threshold it shatters immediately. Pieces shielded by other pieces are only pushed.
    ///  2. Push: every rigidbody within 6 m (including the fresh shards) gets the radial impulse
    ///     1500 N*s * (1 - d/6) with upward modifier 0.5 (AddExplosionForce semantics, see RadialImpulse),
    ///     limited so nothing leaves faster than the 80 m/s clamp.
    /// </summary>
    public sealed class ExplosionSystem : MonoBehaviour
    {
        private struct BlastTarget
        {
            public Block Block;
            public Shard Shard;
            public Vector3 Point;
            public float Impulse;
        }

        private readonly List<Vector3> pending = new List<Vector3>(8);
        private readonly List<BlastTarget> targets = new List<BlastTarget>(256);
        private readonly Collider[] hits = new Collider[2048];

        public int ExplosionCount { get; private set; }
        public int LastBodiesHit { get; private set; }
        public int LastBlastBreaks { get; private set; }

        public void Queue(Vector3 position)
        {
            pending.Add(position);
        }

        public void ClearPending()
        {
            pending.Clear();
        }

        public void ProcessPending()
        {
            for (int i = 0; i < pending.Count; i++) Explode(pending[i]);
            pending.Clear();
        }

        private void Explode(Vector3 center)
        {
            const int occluders = Layers.BlockMask | Layers.ShardMask | Layers.EnvironmentMask;
            int n = Physics.OverlapSphereNonAlloc(center, Wpn.ExplosionRadius, hits, Layers.BlockMask | Layers.ShardMask, QueryTriggerInteraction.Ignore);
            targets.Clear();
            for (int i = 0; i < n; i++)
            {
                Collider col = hits[i];
                Vector3 closest = col.ClosestPoint(center);
                Vector3 toward = closest - center;
                float d = toward.magnitude;
                float impulse = Wpn.ExplosionPeakImpulse * Mathf.Clamp01(1f - d / Wpn.ExplosionRadius);
                if (impulse <= 0f) continue;

                GameObject go = col.gameObject;
                BlockMaterialType material;
                Block block = null;
                Shard shard = null;
                if (go.layer == Layers.Block && go.TryGetComponent(out block) && block.InUse) material = block.Def.Material;
                else if (go.layer == Layers.Shard && go.TryGetComponent(out shard) && shard.InUse && shard.CanBreak) material = shard.Material;
                else continue;
                if (impulse <= BlockMaterials.BreakImpulse(material)) continue;

                // Exposure: the first thing a ray from the centre meets on the way must be this very piece.
                if (d > 0.02f && Physics.Raycast(center, toward / d, out RaycastHit hit, d + 0.01f, occluders, QueryTriggerInteraction.Ignore) && hit.collider != col)
                    continue;
                targets.Add(new BlastTarget { Block = block, Shard = shard, Point = closest, Impulse = impulse });
            }

            // Strongest first, capped, so a blast inside a huge pile cannot stall a frame.
            targets.Sort((a, b) => b.Impulse.CompareTo(a.Impulse));
            int breaks = 0;
            for (int i = 0; i < targets.Count && breaks < Wpn.MaxBlastBreaks; i++)
            {
                BlastTarget t = targets[i];
                bool broke = t.Block != null ? Game.Fracture.BlastBreak(t.Block, t.Point, t.Impulse) : Game.Fracture.BlastBreak(t.Shard, t.Point, t.Impulse);
                if (broke) breaks++;
            }
            LastBlastBreaks = breaks;

            n = Physics.OverlapSphereNonAlloc(center, Wpn.ExplosionRadius, hits, Layers.DynamicMask, QueryTriggerInteraction.Ignore);
            int bodies = 0;
            for (int i = 0; i < n; i++)
            {
                Rigidbody rb = hits[i].attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                if (RadialImpulse.Apply(rb, center, Wpn.ExplosionPeakImpulse, Wpn.ExplosionRadius, Wpn.ExplosionUpwardModifier,
                        GameConfig.Physics.MaxLinearVelocity)) bodies++;
            }
            LastBodiesHit = bodies;
            ExplosionCount++;

            Game.Effects.Explosion(center);
            Game.Audio.PlayExplosion(center);
            Game.Shake.ShakeFromExplosion(center, Wpn.ExplosionShakeTrauma);
        }
    }
}
