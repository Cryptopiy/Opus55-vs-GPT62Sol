using UnityEngine;
using PhysicsCfg = DestructionSandbox.GameConfig.Physics;

namespace DestructionSandbox
{
    /// <summary>
    /// One pooled structure piece (D-14). A box uses the shared unit cube scaled to its size and a BoxCollider;
    /// an arch/cut piece uses its convex prism mesh and a convex MeshCollider. Mass = volume x density (D-07).
    /// </summary>
    public sealed class Block : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public PieceDef Def { get; private set; }
        public bool InUse { get; private set; }
        public bool Counted { get; set; }          // already counted as destroyed (D-21)
        public bool BreakQueued { get; set; }
        public Vector3 StartPosition { get; private set; }
        public int ActiveIndex { get; set; } = -1;
        public bool IsDebris { get; private set; }

        private BoxCollider box;
        private MeshCollider hull;
        private MeshFilter filter;
        private MeshRenderer meshRenderer;

        public static Block Create(Transform parent, int index)
        {
            var go = new GameObject("Block" + index);
            go.SetActive(false);
            go.layer = GameConfig.Layers.Block;
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<Block>();
            b.filter = go.AddComponent<MeshFilter>();
            b.meshRenderer = go.AddComponent<MeshRenderer>();
            b.box = go.AddComponent<BoxCollider>();
            b.hull = go.AddComponent<MeshCollider>();
            b.hull.convex = true;
            b.hull.enabled = false;
            b.Body = go.AddComponent<Rigidbody>();
            b.Body.interpolation = RigidbodyInterpolation.Interpolate;
            b.Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            b.Body.maxLinearVelocity = PhysicsCfg.MaxLinearVelocity;
            b.Body.angularDamping = PhysicsCfg.BlockAngularDamping;
            b.Body.linearDamping = 0f;
            return b;
        }

        public void Activate(PieceDef def, MaterialLibrary materials, MeshLibrary meshes)
        {
            Def = def;
            Transform t = transform;
            t.SetPositionAndRotation(def.Position, def.Rotation);
            PhysicsMaterial physics = materials.PhysicsFor(def.Material);
            if (def.IsBox)
            {
                t.localScale = def.Size;
                filter.sharedMesh = meshes.UnitCube;
                box.enabled = true;
                box.sharedMaterial = physics;
                hull.enabled = false;
            }
            else
            {
                t.localScale = Vector3.one;
                filter.sharedMesh = def.PrismMesh;
                hull.sharedMesh = def.PrismMesh;
                hull.sharedMaterial = physics;
                hull.enabled = true;
                box.enabled = false;
            }
            meshRenderer.sharedMaterial = materials.ForBlock(def.Material);
            meshRenderer.SetShaderUserValue(def.UserValue);
            // Glass is see-through: it does not cast a hard opaque shadow.
            meshRenderer.shadowCastingMode = def.Material == BlockMaterialType.Glass
                ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;

            Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            Body.mass = def.Mass;
            StartPosition = def.Position;
            Counted = false;
            BreakQueued = false;
            InUse = true;
            gameObject.SetActive(true);
            Body.maxLinearVelocity = PhysicsCfg.MaxLinearVelocity;   // D-27 clamp (re-applied on the live body)
            Body.maxAngularVelocity = PhysicsCfg.MaxAngularSpeed;
            Body.solverIterations = PhysicsCfg.SolverIterations;     // D-08: standing structure uses 12 / 4
            Body.solverVelocityIterations = PhysicsCfg.SolverVelocityIterations;
            IsDebris = false;
            Body.ResetCenterOfMass();
            Body.ResetInertiaTensor();
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }

        /// <summary>A block that started to collapse no longer needs the stacking-grade solver.</summary>
        public void MakeDebris()
        {
            if (IsDebris) return;
            IsDebris = true;
            Body.solverIterations = PhysicsCfg.DebrisSolverIterations;
            Body.solverVelocityIterations = PhysicsCfg.DebrisSolverVelocityIterations;
        }

        public void Deactivate()
        {
            InUse = false;
            BreakQueued = false;
            Def = null;
            gameObject.SetActive(false);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (InUse) Game.Fracture.OnBlockCollision(this, collision);
        }
    }
}
