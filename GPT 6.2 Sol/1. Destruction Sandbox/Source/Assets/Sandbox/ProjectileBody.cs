using UnityEngine;

namespace DestructionRange
{
    internal sealed class ProjectileBody : MonoBehaviour
    {
        public Rigidbody Body;
        public SphereCollider Collider;
        public MeshRenderer Mesh;
        public TrailRenderer Trail;
        public Weapon Kind;
        public float Life;
        public bool Anchored, Spent;
        public ProjectileField Field { get; set; }

        void OnCollisionEnter(Collision collision)
        {
            if (!Anchored && isActiveAndEnabled && collision.contactCount > 0)
                Field.Contact(this, collision.GetContact(0).point, collision.impulse.magnitude);
        }
    }
}
