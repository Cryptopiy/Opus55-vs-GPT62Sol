using System.Collections.Generic;
using UnityEngine;
using ScoreCfg = DestructionSandbox.GameConfig.Score;

namespace DestructionSandbox
{
    /// <summary>
    /// Owns the three layouts, the block pool and the live structure: selection/rebuild (F5-F7, R), destroyed
    /// counting (D-21: fractured OR displaced more than 3 m) and the block safety checks.
    /// </summary>
    public sealed class StructureManager : MonoBehaviour
    {
        private StructureLayout[] layouts;
        private Block[] pool;
        private readonly Stack<Block> free = new Stack<Block>(512);
        private readonly List<Block> active = new List<Block>(512);
        private MaterialLibrary materials;
        private MeshLibrary meshes;

        public int CurrentIndex { get; private set; } = -1;
        public string CurrentName => CurrentIndex >= 0 ? layouts[CurrentIndex].Name : "-";
        public int TotalBlocks { get; private set; }
        public int DestroyedBlocks { get; private set; }
        public int FracturedBlocks { get; private set; }
        public int ActiveBlockCount => active.Count;
        public int PoolSize => pool.Length;
        public int FreeCount => free.Count;
        public int BuildCount { get; private set; }
        public float BuildTime { get; private set; }
        public IReadOnlyList<Block> ActiveBlocks => active;
        public StructureLayout Layout(int index) => layouts[index];

        public void Init(MaterialLibrary mats, MeshLibrary meshLibrary)
        {
            materials = mats;
            meshes = meshLibrary;
            layouts = new[] { BasicLayouts.BuildTower(), WallLayout.Build(), BasicLayouts.BuildPyramid() };

            int max = 0;
            foreach (StructureLayout layout in layouts)
            {
                max = Mathf.Max(max, layout.Pieces.Length);
                // Cook convex collision data once so rebuilding the wall never re-cooks meshes during play.
                foreach (PieceDef piece in layout.Pieces)
                    if (!piece.IsBox) Physics.BakeMesh(piece.PrismMesh.GetEntityId(), true);
            }

            pool = new Block[max];
            for (int i = 0; i < max; i++)
            {
                pool[i] = Block.Create(transform, i);
                free.Push(pool[i]);
            }
        }

        /// <summary>F5/F6/F7: remove the old structure and its debris, then build the selected one instantly.</summary>
        public void Select(int index)
        {
            index = Mathf.Clamp(index, 0, layouts.Length - 1);
            ClearWorld();
            CurrentIndex = index;
            StructureLayout layout = layouts[index];
            for (int i = 0; i < layout.Pieces.Length; i++)
            {
                Block b = free.Pop();
                b.Activate(layout.Pieces[i], materials, meshes);
                b.ActiveIndex = active.Count;
                active.Add(b);
            }
            TotalBlocks = layout.Pieces.Length;
            DestroyedBlocks = 0;
            FracturedBlocks = 0;
            BuildCount++;
            BuildTime = Time.time;
        }

        public void Rebuild()
        {
            Select(CurrentIndex < 0 ? 0 : CurrentIndex);
        }

        private void ClearWorld()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Block b = active[i];
                b.Deactivate();
                b.ActiveIndex = -1;
                free.Push(b);
            }
            active.Clear();
            Game.Fracture.ClearQueue();
            Game.Shards.ClearAll();
            Game.Projectiles.ClearAll();
            Game.Orbs.ClearAll();
            Game.Explosions.ClearPending();
            Game.Effects.ClearAll();
        }

        /// <summary>Called by the fracture system when a block shatters: count it and return it to the pool.</summary>
        public void OnBlockFractured(Block b, Vector3 at)
        {
            if (!b.InUse) return;
            FracturedBlocks++;
            if (!b.Counted) MarkDestroyed(b, at);
            Release(b);
        }

        private void MarkDestroyed(Block b, Vector3 at)
        {
            b.Counted = true;
            DestroyedBlocks++;
            Game.Score.OnBlockDestroyed(b.Def.Material, at);
        }

        private void Release(Block b)
        {
            // Swap-remove keeps the active list compact without shifting.
            int i = b.ActiveIndex;
            int last = active.Count - 1;
            if (i >= 0 && i <= last)
            {
                Block moved = active[last];
                active[i] = moved;
                moved.ActiveIndex = i;
                active.RemoveAt(last);
            }
            b.ActiveIndex = -1;
            b.Deactivate();
            free.Push(b);
        }

        private void FixedUpdate()
        {
            float limit = ScoreCfg.DisplacementForDestroyed * ScoreCfg.DisplacementForDestroyed;
            float killY = GameConfig.World.KillY;
            float debrisSpeed2 = GameConfig.Physics.DebrisSpeed * GameConfig.Physics.DebrisSpeed;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Block b = active[i];
                Rigidbody rb = b.Body;
                if (rb.IsSleeping()) continue;
                RadialImpulse.ReleaseIfSlow(rb);
                if (!b.IsDebris && rb.linearVelocity.sqrMagnitude > debrisSpeed2) b.MakeDebris();
                Vector3 p = rb.position;
                if (!b.Counted && (p - b.StartPosition).sqrMagnitude > limit) MarkDestroyed(b, p);
                if (p.y < killY || float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z))
                {
                    if (!b.Counted) MarkDestroyed(b, b.StartPosition);
                    Release(b);
                }
            }
        }

        /// <summary>D-27: after each step no block may move faster than the clamp (PhysX clamps before its solver only).</summary>
        public void ClampSpeeds(float max)
        {
            float max2 = max * max;
            float fast2 = GameConfig.Physics.FastBodySpeed * GameConfig.Physics.FastBodySpeed;
            for (int i = 0; i < active.Count; i++)
            {
                Rigidbody rb = active[i].Body;
                if (rb.IsSleeping()) continue;
                Vector3 v = rb.linearVelocity;
                float sq = v.sqrMagnitude;
                if (sq > max2) rb.linearVelocity = v * (max / v.magnitude);
                if (sq > fast2) RadialImpulse.MarkFast(rb);
            }
        }

        /// <summary>Awake / sleeping statistics for the debug overlay and the self-test.</summary>
        public void Measure(out int awake, out float maxSpeed, out float maxDisplacement)
        {
            awake = 0;
            maxSpeed = 0f;
            maxDisplacement = 0f;
            for (int i = 0; i < active.Count; i++)
            {
                Rigidbody rb = active[i].Body;
                if (!rb.IsSleeping())
                {
                    awake++;
                    maxSpeed = Mathf.Max(maxSpeed, rb.linearVelocity.magnitude);
                }
                maxDisplacement = Mathf.Max(maxDisplacement, (rb.position - active[i].StartPosition).magnitude);
            }
        }
    }
}
