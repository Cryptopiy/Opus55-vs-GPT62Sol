using UnityEngine;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>
    /// Third-person character movement (I-25, I-26): camera-relative WASD at 3 m/s, Shift runs at 6 m/s,
    /// Space jumps; when the water at the character is deeper than 1.2 m it swims at 2.5 m/s, floating on
    /// the CPU wave model so it bobs with the rendered waves. The world is closed by an invisible square
    /// boundary far out at sea.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public bool Frozen { get; set; }
        public bool IsSwimming { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsRunning { get; private set; }
        public float HorizontalSpeed { get; private set; }
        public Vector3 Velocity => velocity;
        public float WaterDepthHere { get; private set; }
        public float WaterSurfaceHere { get; private set; }
        public Transform CameraTransform { get; set; }

        CharacterController cc;
        IslandData island;
        Vector3 velocity;
        float yawVelocity;
        const float SwimFloatOffset = 1.28f;    // feet below the surface while swimming (head stays out)

        public void Init(IslandData data)
        {
            island = data;
            cc = GetComponent<CharacterController>();
            cc.height = C.PlayerHeight;
            cc.radius = C.PlayerRadius;
            cc.center = new Vector3(0f, C.PlayerHeight * 0.5f, 0f);
            cc.slopeLimit = 52f;
            cc.stepOffset = 0.45f;
            cc.skinWidth = 0.04f;
            cc.minMoveDistance = 0f;
        }

        public void SetIsland(IslandData data) => island = data;

        /// <summary>Moves the character instantly (spawn, restart, regeneration, automated test).</summary>
        public void Teleport(Vector3 position, float yawDegrees)
        {
            cc.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
            cc.enabled = true;
            velocity = Vector3.zero;
            IsSwimming = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || island == null) return;
            Vector3 pos = transform.position;
            float ground = island.HeightAt(pos.x, pos.z);
            WaterSurfaceHere = WaveModel.HeightAt(pos.x, pos.z);
            WaterDepthHere = WaterSurfaceHere - ground;

            // Hysteresis: start swimming above 1.2 m, stop once it is clearly shallower again.
            if (!IsSwimming && WaterDepthHere > C.SwimDepthThreshold && pos.y < WaterSurfaceHere - 0.3f) IsSwimming = true;
            else if (IsSwimming && WaterDepthHere < C.SwimDepthThreshold - 0.15f) IsSwimming = false;

            Vector2 input = Frozen ? Vector2.zero : GameInput.MoveAxis;
            Vector3 camFwd = CameraTransform != null ? CameraTransform.forward : transform.forward;
            camFwd.y = 0f;
            camFwd = camFwd.sqrMagnitude > 1e-4f ? camFwd.normalized : Vector3.forward;
            Vector3 camRight = new Vector3(camFwd.z, 0f, -camFwd.x);
            Vector3 wish = camFwd * input.y + camRight * input.x;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            IsRunning = !IsSwimming && wish.sqrMagnitude > 0.01f && !Frozen && GameInput.Run;
            float speed = IsSwimming ? C.SwimSpeed : (IsRunning ? C.RunSpeed : C.WalkSpeed);
            Vector3 targetHorizontal = wish * speed;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float accel = IsSwimming ? 4f : (cc.isGrounded ? 14f : 3f);
            horizontal = Vector3.MoveTowards(horizontal, targetHorizontal, accel * speed * dt);

            if (IsSwimming)
            {
                // Buoyancy: spring the feet towards a point below the animated surface (slight bobbing).
                float targetY = WaterSurfaceHere - SwimFloatOffset;
                float vy = (targetY - pos.y) * 3.2f;
                velocity = new Vector3(horizontal.x, Mathf.Clamp(vy, -3f, 3f), horizontal.z);
            }
            else
            {
                if (cc.isGrounded && velocity.y < 0f) velocity.y = -2f;
                if (cc.isGrounded && !Frozen && GameInput.Jump)
                    velocity.y = Mathf.Sqrt(2f * -C.Gravity * C.JumpHeight);
                velocity.y += C.Gravity * dt;
                velocity.x = horizontal.x;
                velocity.z = horizontal.z;
            }

            cc.Move(velocity * dt);
            IsGrounded = cc.isGrounded;

            // Invisible boundary of the 500 x 500 m world.
            Vector3 p = transform.position;
            float b = C.WorldBoundary;
            if (Mathf.Abs(p.x) > b || Mathf.Abs(p.z) > b)
            {
                p.x = Mathf.Clamp(p.x, -b, b);
                p.z = Mathf.Clamp(p.z, -b, b);
                cc.enabled = false; transform.position = p; cc.enabled = true;
            }
            // Safety net: never let the character fall through the terrain (e.g. after regeneration).
            float g = island.HeightAt(p.x, p.z);
            if (p.y < g - 0.5f)
            {
                cc.enabled = false; transform.position = new Vector3(p.x, g + 0.1f, p.z); cc.enabled = true;
                velocity.y = 0f;
            }

            HorizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            if (wish.sqrMagnitude > 0.01f)
            {
                float targetYaw = Mathf.Atan2(wish.x, wish.z) * Mathf.Rad2Deg;
                float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref yawVelocity, 0.12f);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }
    }
}
