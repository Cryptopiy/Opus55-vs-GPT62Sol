using UnityEngine;

namespace DestructionSandbox
{
    /// <summary>
    /// Radial blast impulse with the semantics of Rigidbody.AddExplosionForce (peak impulse at the centre,
    /// linear falloff to zero at the radius, measured to the body's centre of mass; the push direction starts
    /// 'upwardsModifier' metres below the centre so bodies are lifted) - but the impulse is limited so the body
    /// never leaves with more than the D-27 speed clamp. PhysX clamps velocity before its solver only, so a raw
    /// 1500 N*s kick on a 2 kg shard would otherwise fly at ~600 m/s for one step and could tunnel.
    /// </summary>
    public static class RadialImpulse
    {
        public static bool Apply(Rigidbody rb, Vector3 center, float peakImpulse, float radius, float upwardsModifier, float maxSpeed)
        {
            Vector3 com = rb.worldCenterOfMass;
            float distance = Vector3.Distance(com, center);
            if (distance > radius) return false;
            float magnitude = peakImpulse * (1f - distance / radius);
            if (magnitude <= 0f) return false;

            Vector3 origin = center;
            origin.y -= upwardsModifier;
            Vector3 dir = com - origin;
            dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector3.up;

            Vector3 impulse = dir * magnitude;
            Vector3 v = rb.linearVelocity;
            float mass = rb.mass;
            Vector3 result = v + impulse / mass;
            float speed = result.magnitude;
            if (speed > maxSpeed)
            {
                result *= maxSpeed / speed;
                impulse = (result - v) * mass;
            }
            rb.AddForce(impulse, ForceMode.Impulse);
            if (result.sqrMagnitude > GameConfig.Physics.FastBodySpeed * GameConfig.Physics.FastBodySpeed) MarkFast(rb);
            return true;
        }

        /// <summary>Speculative CCD for a body that is about to move fast (never used for slow bodies).</summary>
        public static void MarkFast(Rigidbody rb)
        {
            if (!rb.isKinematic && rb.collisionDetectionMode == CollisionDetectionMode.Discrete)
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        /// <summary>Back to discrete detection once the body has slowed down again.</summary>
        public static void ReleaseIfSlow(Rigidbody rb)
        {
            if (rb.collisionDetectionMode != CollisionDetectionMode.ContinuousSpeculative) return;
            float release = GameConfig.Physics.FastBodyReleaseSpeed;
            if (rb.linearVelocity.sqrMagnitude < release * release) rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
    }
}
