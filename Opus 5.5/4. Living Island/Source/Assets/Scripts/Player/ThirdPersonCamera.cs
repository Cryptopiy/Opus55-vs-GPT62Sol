using UnityEngine;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>
    /// Mouse-orbit follow camera (I-25): smooth follow of a pivot at head height, sphere-cast collision
    /// against terrain/props and a hard clamp above the terrain heightmap so it never clips into the
    /// ground. It may go below the water surface (underwater view) but never hovers inside the surface.
    /// </summary>
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        public bool InputEnabled { get; set; } = true;
        public float Yaw { get; set; }
        public float Pitch { get; set; } = 12f;

        Transform target;
        IslandData island;
        int collisionMask;
        Vector3 pivot, pivotVelocity;
        float currentDistance = C.CameraDistance;

        public void Init(Transform followTarget, IslandData data, int mask)
        {
            target = followTarget;
            island = data;
            collisionMask = mask;
            Snap();
        }

        public void SetIsland(IslandData data) => island = data;

        /// <summary>Jumps straight to the follow position (after teleports).</summary>
        public void Snap()
        {
            if (target == null) return;
            pivot = target.position + Vector3.up * C.CameraPivotHeight;
            pivotVelocity = Vector3.zero;
            currentDistance = C.CameraDistance;
            Apply(0f);
        }

        void LateUpdate()
        {
            if (target == null) return;
            if (InputEnabled)
            {
                Vector2 d = GameInput.MouseDelta;
                Yaw += d.x * C.MouseSensitivity;
                Pitch = Mathf.Clamp(Pitch - d.y * C.MouseSensitivity, C.CameraMinPitch, C.CameraMaxPitch);
            }
            Vector3 desiredPivot = target.position + Vector3.up * C.CameraPivotHeight;
            pivot = Vector3.SmoothDamp(pivot, desiredPivot, ref pivotVelocity, C.CameraFollowSmoothing);
            Apply(Time.deltaTime);
        }

        void Apply(float dt)
        {
            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 back = rot * Vector3.back;
            float wanted = C.CameraDistance;
            if (Physics.SphereCast(pivot, C.CameraCollisionRadius, back, out RaycastHit hit, wanted, collisionMask, QueryTriggerInteraction.Ignore))
                wanted = Mathf.Max(0.35f, hit.distance - 0.05f);
            // Pull in fast on collision, ease back out slowly (no popping).
            currentDistance = dt <= 0f || wanted < currentDistance ? wanted : Mathf.Lerp(currentDistance, wanted, 1f - Mathf.Exp(-dt * 4f));
            Vector3 pos = pivot + back * currentDistance;

            if (island != null)
            {
                float ground = island.HeightAt(pos.x, pos.z) + 0.35f;
                if (pos.y < ground) pos.y = ground;
                // Keep the near plane out of the animated surface: either clearly above or clearly below.
                float water = WaveModel.HeightAt(pos.x, pos.z);
                float delta = pos.y - water;
                if (Mathf.Abs(delta) < 0.18f) pos.y = water + (delta >= 0f ? 0.18f : -0.18f);
                if (pos.y < ground) pos.y = ground;
            }
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation((pivot - pos).sqrMagnitude > 1e-4f ? pivot - pos : rot * Vector3.forward));
        }
    }
}
