using UnityEngine;
using PhysicsCfg = DestructionSandbox.GameConfig.Physics;

namespace DestructionSandbox
{
    /// <summary>
    /// One pooled fracture shard (a box). Shards form an intrusive doubly linked list in spawn order so the
    /// manager can recycle the oldest one in O(1) when the 1500 budget is reached (D-13).
    /// </summary>
    public sealed class Shard : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public BlockMaterialType Material { get; private set; }
        /// <summary>Exact sub-box (cell) of the parent: used for further 2x2x2 splits and the D-12 size rule.</summary>
        public Vector3 CellSize { get; private set; }
        /// <summary>Drawn / collision box = cell x ShardInset.</summary>
        public Vector3 Size { get; private set; }
        public uint UserValue { get; private set; }
        public bool InUse { get; private set; }
        public bool BreakQueued { get; set; }
        public bool CanBreak { get; private set; }
        /// <summary>Incremented on every activation so stale fracture requests for a recycled shard are ignored.</summary>
        public int Generation { get; private set; }

        // Settle / shrink lifecycle (D-13).
        public float SettledTime { get; set; }
        public bool Shrinking { get; private set; }
        public float ShrinkElapsed { get; set; }

        // Active list links (oldest = head).
        public Shard Prev { get; set; }
        public Shard Next { get; set; }

        private BoxCollider box;
        private MeshRenderer meshRenderer;

        public Collider Collider => box;

        public static Shard Create(Transform parent, int index, MeshLibrary meshes)
        {
            var go = new GameObject("Shard" + index);
            go.SetActive(false);
            go.layer = GameConfig.Layers.Shard;
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<Shard>();
            go.AddComponent<MeshFilter>().sharedMesh = meshes.UnitCube;
            s.meshRenderer = go.AddComponent<MeshRenderer>();
            s.box = go.AddComponent<BoxCollider>();
            s.Body = go.AddComponent<Rigidbody>();
            s.Body.interpolation = RigidbodyInterpolation.Interpolate;
            s.Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            s.Body.maxLinearVelocity = PhysicsCfg.MaxLinearVelocity;
            s.Body.linearDamping = PhysicsCfg.ShardLinearDamping;
            s.Body.angularDamping = PhysicsCfg.ShardAngularDamping;
            s.Body.solverIterations = PhysicsCfg.DebrisSolverIterations;
            s.Body.solverVelocityIterations = PhysicsCfg.DebrisSolverVelocityIterations;
            s.Body.sleepThreshold = PhysicsCfg.ShardSleepThreshold;
            s.box.contactOffset = PhysicsCfg.ShardContactOffset;
            return s;
        }

        public void Activate(BlockMaterialType material, Vector3 position, Quaternion rotation, Vector3 size, float mass,
            Vector3 velocity, Vector3 angularVelocity, uint userValue, MaterialLibrary materials)
        {
            Generation++;
            Material = material;
            CellSize = size;
            Size = size * GameConfig.Fracture.ShardInset;
            UserValue = userValue;
            CanBreak = BoxSplit.CanSubdivide(CellSize);   // D-12 judged on the exact cell size
            SettledTime = 0f;
            Shrinking = false;
            ShrinkElapsed = 0f;
            BreakQueued = false;

            Transform t = transform;
            t.SetPositionAndRotation(position, rotation);
            t.localScale = Size;
            meshRenderer.sharedMaterial = materials.ForBlock(material);
            meshRenderer.SetShaderUserValue(userValue);
            meshRenderer.shadowCastingMode = material == BlockMaterialType.Glass
                ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            box.sharedMaterial = materials.PhysicsFor(material);
            box.enabled = true;
            Body.isKinematic = false;
            Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            Body.mass = Mathf.Max(mass, 0.01f);

            InUse = true;
            gameObject.SetActive(true);
            Body.maxLinearVelocity = PhysicsCfg.MaxLinearVelocity;   // D-27 clamp (re-applied on the live body)
            Body.maxAngularVelocity = PhysicsCfg.MaxAngularSpeed;
            Body.solverIterations = PhysicsCfg.DebrisSolverIterations;
            Body.solverVelocityIterations = PhysicsCfg.DebrisSolverVelocityIterations;
            Body.sleepThreshold = PhysicsCfg.ShardSleepThreshold;
            Body.ResetCenterOfMass();
            Body.ResetInertiaTensor();
            Body.linearVelocity = velocity;
            Body.angularVelocity = angularVelocity;
            if (velocity.sqrMagnitude > PhysicsCfg.FastBodySpeed * PhysicsCfg.FastBodySpeed) RadialImpulse.MarkFast(Body);
        }

        /// <summary>Settled long enough: stop colliding and shrink to zero (visual only).</summary>
        public void BeginShrink()
        {
            Shrinking = true;
            ShrinkElapsed = 0f;
            box.enabled = false;
            Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            Body.isKinematic = true;
        }

        public void SetShrink(float t)
        {
            transform.localScale = Size * Mathf.Max(0.0001f, 1f - t);
        }

        public void Deactivate()
        {
            InUse = false;
            BreakQueued = false;
            Shrinking = false;
            gameObject.SetActive(false);
            Body.isKinematic = false;
            box.enabled = true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (InUse && !Shrinking) Game.Fracture.OnShardCollision(this, collision);
        }
    }
}
