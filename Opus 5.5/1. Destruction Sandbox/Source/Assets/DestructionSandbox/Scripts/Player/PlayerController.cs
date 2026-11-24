using UnityEngine;
using UnityEngine.Rendering.Universal;
using Cfg = DestructionSandbox.GameConfig.Player;

namespace DestructionSandbox
{
    /// <summary>
    /// D-15 first-person controller on a CharacterController (blocked by every collider, so it cannot walk
    /// through the structures). Movement and look use unscaled time so the player keeps full control while the
    /// world runs in slow motion; the camera sits exactly EyeHeight above the feet.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public Camera Camera { get; private set; }
        public Transform Pivot { get; private set; }
        public float Sensitivity { get; set; } = Cfg.DefaultSensitivity;
        /// <summary>When set, Update ignores the devices and the self-test drives <see cref="Step"/> directly.</summary>
        public bool ExternallyDriven { get; set; }

        private CharacterController controller;
        private float yaw;
        private float pitch;
        private float verticalVelocity;

        public static PlayerController Create(Transform parent)
        {
            var go = new GameObject("Player");
            go.layer = GameConfig.Layers.Player;
            go.transform.SetParent(parent, false);
            var cc = go.AddComponent<CharacterController>();
            cc.height = Cfg.ControllerHeight;
            cc.radius = Cfg.ControllerRadius;
            cc.skinWidth = Cfg.ControllerSkinWidth;
            cc.stepOffset = Cfg.ControllerStepOffset;
            cc.slopeLimit = Cfg.ControllerSlopeLimit;
            // Capsule bottom rests one skin width above the feet pivot, so the pivot stays exactly on the ground.
            cc.center = new Vector3(0f, Cfg.ControllerHeight * 0.5f + Cfg.ControllerSkinWidth, 0f);

            var pivot = new GameObject("EyePivot").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(0f, Cfg.EyeHeight, 0f);

            var rig = new GameObject("ShakeRig").transform;
            rig.SetParent(pivot, false);
            var shake = rig.gameObject.AddComponent<CameraShake>();

            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = Cfg.CameraNearClip;
            cam.farClipPlane = Cfg.CameraFarClip;
            cam.fieldOfView = Cfg.CameraFieldOfView;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowMSAA = true;
            cam.allowHDR = true;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;      // MSAA from the URP asset
            data.renderShadows = true;
            camGo.AddComponent<AudioListener>();

            var player = go.AddComponent<PlayerController>();
            player.controller = cc;
            player.Pivot = pivot;
            player.Camera = cam;
            Game.Shake = shake;
            Game.MainCamera = cam;
            return player;
        }

        /// <summary>D-03: eye at (0, 1.8, -28) looking at (0, 6, 0).</summary>
        public void Spawn()
        {
            Vector3 eye = Cfg.SpawnEyePosition;
            Teleport(new Vector3(eye.x, eye.y - Cfg.EyeHeight, eye.z));
            LookAt(Cfg.SpawnLookAt);
            verticalVelocity = 0f;
        }

        public void Teleport(Vector3 feetPosition)
        {
            controller.enabled = false;
            transform.position = feetPosition;
            controller.enabled = true;
        }

        public void LookAt(Vector3 point)
        {
            Vector3 d = point - Pivot.position;
            yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float flat = new Vector2(d.x, d.z).magnitude;
            pitch = Mathf.Clamp(-Mathf.Atan2(d.y, flat) * Mathf.Rad2Deg, -Cfg.MaxPitch, Cfg.MaxPitch);
            ApplyRotation();
        }

        private void ApplyRotation()
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Pivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void Update()
        {
            if (Game.IsPaused || Game.Input == null || ExternallyDriven) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, Cfg.MaxFrameDelta);
            GameInput input = Game.Input;

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = input.LookDelta * Sensitivity;
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, -Cfg.MaxPitch, Cfg.MaxPitch);
                ApplyRotation();
            }

            Step(input.Move, input.Sprint, input.JumpPressed, dt);
        }

        /// <summary>One movement step: walk/sprint on the yaw plane, jump of JumpHeight, gravity.</summary>
        public void Step(Vector2 move, bool sprint, bool jump, float dt)
        {
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            float speed = sprint ? Cfg.SprintSpeed : Cfg.WalkSpeed;
            Vector3 horizontal = (forward * move.y + right * move.x) * speed;

            float gravity = Physics.gravity.y;
            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = Cfg.GroundedStickVelocity;
                if (jump) verticalVelocity = Mathf.Sqrt(2f * -gravity * Cfg.JumpHeight);
            }
            // Average of the start and end velocity = exact parabola under constant gravity (apex is exactly JumpHeight).
            float previous = verticalVelocity;
            verticalVelocity += gravity * dt;
            float vertical = (previous + verticalVelocity) * 0.5f;
            controller.Move((horizontal + Vector3.up * vertical) * dt);
        }

        public Ray AimRay()
        {
            Transform t = Camera.transform;
            return new Ray(t.position, t.forward);
        }
    }
}
