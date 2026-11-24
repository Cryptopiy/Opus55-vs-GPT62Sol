using UnityEngine;
using UnityEngine.InputSystem;
using static WaveSurvival.GameConfig;

namespace WaveSurvival
{
    internal sealed class PlayerMotor
    {
        public readonly Transform Root, View;
        public readonly Camera Camera;
        public readonly CharacterController Controller;
        public Vector3 Position => Root.position;
        public Vector3 Eyes => View.position;
        public Vector3 DamageDirection { get; private set; }
        public float DamagePulse { get; private set; }
        public float HP { get; private set; } = Health;
        public float Energy { get; private set; } = Stamina;
        public float Aim { get; private set; }
        public Vector2 LookDelta { get; private set; }
        public float Motion { get; private set; }
        public float Gait { get; private set; }
        public bool Invulnerable { get; set; }
        public bool Moving { get; private set; }
        float yaw, pitch, vertical, regenAt, kick, flinch, roll, nextStep;
        Vector3 impulse;

        public PlayerMotor()
        {
            Root = Geometry.Root("Player");
            Root.position = new(0, .1f, -13);
            Root.gameObject.layer = PlayerLayer;
            Controller = Root.gameObject.AddComponent<CharacterController>();
            Controller.height = StandHeight;
            Controller.radius = .34f;
            Controller.center = Vector3.up * (StandHeight * .5f);
            Controller.stepOffset = .32f;
            Controller.minMoveDistance = 0;
            View = Geometry.Root("First person camera", Root);
            View.localPosition = new(0, 1.62f, 0);
            View.gameObject.tag = "MainCamera";
            Camera = View.gameObject.AddComponent<Camera>();
            Camera.fieldOfView = HipFov;
            Camera.nearClipPlane = .045f;
            Camera.farClipPlane = 100;
            View.gameObject.AddComponent<AudioListener>();
        }

        public bool Tick(float dt, Vector2 move, Vector2 look, bool sprint, bool crouch, bool jump, bool ads)
        {
            LookDelta = look;
            Aim = Mathf.MoveTowards(Aim, ads ? 1 : 0, dt / AimTime);
            Camera.fieldOfView = Mathf.Lerp(HipFov, AimFov, Mathf.SmoothStep(0, 1, Aim));
            float sensitivity = .115f * Mathf.Lerp(1, AimSensitivity, Aim);
            yaw += look.x * sensitivity;
            pitch = Mathf.Clamp(pitch - look.y * sensitivity, -85, 85);
            Root.rotation = Quaternion.Euler(0, yaw, 0);

            if (!crouch && Controller.height < StandHeight)
                crouch = Physics.CheckCapsule(Position + Vector3.up * .77f, Position + Vector3.up * 1.46f,
                    .30f, WorldMask, QueryTriggerInteraction.Ignore);
            float height = crouch ? CrouchHeight : StandHeight;
            if (!Mathf.Approximately(Controller.height, height))
            {
                Controller.height = height;
                Controller.center = Vector3.up * (height * .5f);
            }
            Moving = move.sqrMagnitude > .01f;
            bool run = sprint && Moving && !crouch && !ads && Energy > 0 && Controller.isGrounded;
            if (run)
            {
                Energy = Mathf.Max(0, Energy - StaminaDrain * dt);
                regenAt = Time.time + StaminaDelay;
            }
            else if (Time.time >= regenAt)
                Energy = Mathf.Min(Stamina, Energy + StaminaGain * dt);

            float speed = crouch ? Crouch : run ? Sprint : Walk;
            if (Controller.isGrounded && vertical < 0) vertical = -2;
            if (jump && Controller.isGrounded && !crouch) vertical = Mathf.Sqrt(2 * Gravity * Jump);
            float averageVertical = vertical - Gravity * dt * .5f;
            vertical -= Gravity * dt;
            Vector3 planar = Root.TransformDirection(new(move.x, 0, move.y)) * speed;
            Controller.Move((planar + impulse + Vector3.up * averageVertical) * dt);
            impulse = Vector3.MoveTowards(impulse, Vector3.zero, 18 * dt);

            Motion = Mathf.MoveTowards(Motion, Moving && Controller.isGrounded ? speed / Sprint : 0, dt * 5);
            Gait += dt * (6 + speed) * Motion;
            Vector3 view = View.localPosition;
            view.y = Mathf.Lerp(view.y, height - .18f + Mathf.Sin(Gait * 2) * .035f * Motion, 1 - Mathf.Exp(-16 * dt));
            view.x = Mathf.Cos(Gait) * .028f * Motion;
            View.localPosition = view;
            kick = Mathf.MoveTowards(kick, 0, 10 * dt);
            flinch = Mathf.MoveTowards(flinch, 0, 14 * dt);
            roll = Mathf.MoveTowards(roll, 0, 13 * dt);
            View.localRotation = Quaternion.Euler(pitch - kick + flinch, 0, roll);
            DamagePulse = Mathf.MoveTowards(DamagePulse, 0, dt * 1.65f);
            if (Motion < .15f || !Controller.isGrounded || Time.time < nextStep) return false;
            nextStep = Time.time + 2.2f / speed;
            return true;
        }

        public float Hurt(float amount, Vector3 attacker, float knockback = 0)
        {
            if (Invulnerable || HP <= 0) return 0;
            float applied = Mathf.Min(HP, amount);
            HP -= applied;
            DamagePulse = 1;
            DamageDirection = attacker;
            var direction = Vector3.ProjectOnPlane(Position - attacker, Vector3.up).normalized;
            impulse += direction * knockback;
            flinch = 4;
            roll = Vector3.Dot(Root.right, direction) * 4;
            return applied;
        }

        public void Heal(float amount) => HP = Mathf.Min(Health, HP + amount);
        public void Recoil(float up, float sideways)
        {
            kick += up;
            yaw += sideways;
        }

        public void Place(Vector3 world, Vector3 direction)
        {
            Controller.enabled = false;
            Root.position = world;
            Controller.enabled = true;
            yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            pitch = -Mathf.Asin(direction.normalized.y) * Mathf.Rad2Deg;
            Root.rotation = Quaternion.Euler(0, yaw, 0);
            View.localRotation = Quaternion.Euler(pitch, 0, 0);
            vertical = -2;
            impulse = Vector3.zero;
        }

        public static Vector2 Movement(Keyboard keyboard)
        {
            if (keyboard == null) return Vector2.zero;
            return Vector2.ClampMagnitude(new Vector2(
                (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0)), 1);
        }
    }
}

