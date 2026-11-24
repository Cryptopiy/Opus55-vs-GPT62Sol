using UnityEngine;

namespace DestructionRange
{
    internal sealed class PhysicsPiece : MonoBehaviour
    {
        public PieceField Field { get; set; }
        public Rigidbody Body;
        public BoxCollider Box;
        public MeshCollider Wedge;
        public MeshRenderer Renderer;
        public MeshFilter Filter;
        public Matter Kind;
        public Vector3 Start, Size, BaseScale;
        public float Rest, PendingImpulse, Mass;
        public int Older, Newer, Slot, Tint;
        public bool Shard, Counted;

        void OnCollisionEnter(Collision hit)
        {
            if (!gameObject.activeSelf || hit.contactCount == 0) return;
            Field.Impact(this, hit.impulse.magnitude, hit.relativeVelocity.sqrMagnitude, hit.GetContact(0).point);
        }
    }
}
