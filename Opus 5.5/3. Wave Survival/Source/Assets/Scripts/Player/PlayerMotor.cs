using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// S-05 movement on a CharacterController: walk 5, sprint 8 with stamina (100, -25/s, +15/s after 1 s),
    /// crouch (hold C) 2.5 m/s with a 1.0 m capsule, jump 1.0 m. Also knockback impulses and footstep timing.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public sealed class PlayerMotor : MonoBehaviour
    {
        public Vector3 Velocity;            // controller velocity (without knockback)
        public float Stamina = GameConfig.Player.StaminaMax;
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsGrounded { get; private set; }
        public float EyeHeight { get; private set; } = GameConfig.Player.EyeStand;
        public float StepPhase { get; private set; }   // grows by 1 per footstep (drives the head bob)
        public float BobWeight { get; private set; }   // 0..~1.6 depending on speed and ground contact
        public float LandImpact { get; private set; }  // set on the frame the player lands (impact speed)

        PlayerRig rig;
        CharacterController cc;
        Vector3 external;            // knockback velocity, decays over time
        float lastDrainTime = -10f;
        bool exhausted;              // stamina hit zero: sprint needs Shift to be released first
        float airborneMaxFall;

        public void Init(PlayerRig owner)
        {
            rig = owner;
            cc = owner.Controller;
        }

        public void AddImpulse(Vector3 impulse)
        {
            Vector3 flat = new Vector3(impulse.x, 0f, impulse.z);
            external += flat;
            if (impulse.y > 0f) Velocity.y = Mathf.Max(Velocity.y, impulse.y);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            InputState input = InputRouter.State;
            bool alive = rig.Health.Alive;
            if (!alive) input = default;

            UpdateCrouch(input.CrouchHeld, dt);

            // Sprint + stamina (S-05).
            bool aiming = rig.Weapons != null && rig.Weapons.IsAiming;
            if (!input.SprintHeld) exhausted = false;
            bool wantSprint = input.SprintHeld && input.Move.y > 0.1f && !IsCrouching && !aiming && !exhausted && Stamina > 0f;
            IsSprinting = wantSprint && IsGrounded;
            if (IsSprinting)
            {
                Stamina -= GameConfig.Player.StaminaDrain * dt;
                lastDrainTime = Time.time;
                if (Stamina <= 0f) { Stamina = 0f; exhausted = true; IsSprinting = false; }
            }
            else if (Time.time - lastDrainTime >= GameConfig.Player.StaminaRegenDelay)
            {
                Stamina = Mathf.Min(GameConfig.Player.StaminaMax, Stamina + GameConfig.Player.StaminaRegen * dt);
            }

            float speed = IsCrouching ? GameConfig.Player.CrouchSpeed : IsSprinting ? GameConfig.Player.SprintSpeed : GameConfig.Player.WalkSpeed;
            Vector3 wish = transform.right * input.Move.x + transform.forward * input.Move.y;
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            Vector3 targetVel = wish * speed;
            Vector3 horiz = new Vector3(Velocity.x, 0f, Velocity.z);
            float accel = IsGrounded ? GameConfig.Player.GroundAccel : GameConfig.Player.AirAccel;
            horiz = Vector3.MoveTowards(horiz, targetVel, accel * dt);
            Velocity.x = horiz.x;
            Velocity.z = horiz.z;

            if (IsGrounded && input.JumpPressed && !IsCrouching)
                Velocity.y = Mathf.Sqrt(2f * GameConfig.Player.Gravity * GameConfig.Player.JumpHeight); // reaches exactly 1.0 m

            Velocity.y -= GameConfig.Player.Gravity * dt;
            external *= Mathf.Exp(-GameConfig.Player.KnockbackDamping * dt);

            bool wasGrounded = IsGrounded;
            CollisionFlags flags = cc.Move((Velocity + external) * dt);
            IsGrounded = (flags & CollisionFlags.Below) != 0 || cc.isGrounded;
            if ((flags & CollisionFlags.Above) != 0 && Velocity.y > 0f) Velocity.y = 0f;

            LandImpact = 0f;
            if (!IsGrounded) airborneMaxFall = Mathf.Max(airborneMaxFall, -Velocity.y);
            if (IsGrounded)
            {
                if (!wasGrounded && airborneMaxFall > 3f)
                {
                    LandImpact = airborneMaxFall;
                    Game.Audio?.PlayFootstep(transform.position, 1.2f);
                }
                airborneMaxFall = 0f;
                if (Velocity.y < -2f) Velocity.y = -2f; // keep the controller pressed onto slopes and ramps
            }

            UpdateSteps(dt);
        }

        void UpdateCrouch(bool wantCrouch, float dt)
        {
            if (wantCrouch) IsCrouching = true;
            else if (IsCrouching && CanStand()) IsCrouching = false;
            float h = IsCrouching ? GameConfig.Player.CrouchHeight : GameConfig.Player.StandHeight;
            if (!Mathf.Approximately(cc.height, h))
            {
                cc.height = h;
                cc.center = new Vector3(0f, h * 0.5f, 0f);
            }
            float eye = IsCrouching ? GameConfig.Player.EyeCrouch : GameConfig.Player.EyeStand;
            EyeHeight = MathUtil.Damp(EyeHeight, eye, GameConfig.Player.CrouchLerpSpeed, dt);
        }

        bool CanStand()
        {
            float r = cc.radius - 0.02f;
            Vector3 bottom = transform.position + Vector3.up * (r + 0.05f);
            Vector3 top = transform.position + Vector3.up * (GameConfig.Player.StandHeight - r);
            return !Physics.CheckCapsule(bottom, top, r, GameConfig.Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);
        }

        void UpdateSteps(float dt)
        {
            float hs = new Vector3(Velocity.x, 0f, Velocity.z).magnitude;
            BobWeight = MathUtil.Damp(BobWeight, IsGrounded ? hs / GameConfig.Player.WalkSpeed : 0f, 10f, dt);
            if (!IsGrounded || hs < 0.4f) return;
            float stride = IsCrouching ? 1.1f : IsSprinting ? 2.5f : 1.9f; // metres per footstep
            float before = StepPhase;
            StepPhase += hs * dt / stride;
            if (Mathf.FloorToInt(before) != Mathf.FloorToInt(StepPhase))
            {
                float vol = IsCrouching ? 0.45f : IsSprinting ? 1.2f : 0.85f;
                Game.Audio?.PlayFootstep(transform.position, vol);
            }
        }
    }
}
