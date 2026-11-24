using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace DestructionRange
{
    internal sealed class PlayerRig
    {
        readonly CharacterController controller;
        readonly Transform root, view, cannon;
        public readonly Camera Camera;
        public readonly Transform Muzzle;
        float yaw, pitch, vertical, recoil, shake, shakeTime;
        public float Sensitivity { get; set; } = 1f;
        public Vector3 Position => root.position;

        public PlayerRig(Sandbox game)
        {
            root = game.Node("Player").transform;
            root.gameObject.layer = SandboxConfig.PlayerLayer;
            controller = root.gameObject.AddComponent<CharacterController>();
            controller.height = SandboxConfig.CharacterHeight;
            controller.center = Vector3.down * (SandboxConfig.CharacterHeight * .5f);
            controller.radius = .34f;
            controller.stepOffset = .24f;
            controller.skinWidth = .035f;

            view = game.Node("View", root).transform;
            Camera = view.gameObject.AddComponent<Camera>();
            Camera.tag = "MainCamera";
            Camera.fieldOfView = 66;
            Camera.nearClipPlane = .045f;
            Camera.farClipPlane = 155f;
            Camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            view.gameObject.AddComponent<AudioListener>();

            cannon = game.Node("Cannon", view).transform;
            cannon.localPosition = GunHome;
            var steel = game.Paint("Gunmetal", new Color(.24f, .32f, .37f), .59f);
            var grip = game.Paint("Grip", new Color(.09f, .121f, .14f), .44f);
            var amber = game.Paint("Safety orange", new Color(.95f, .29f, .065f), .42f);
            var pale = game.Paint("Sighting dot", new Color(.69f, .94f, .87f), .43f);
            game.Shape("Receiver", cannon, new Vector3(0, -.012f, .066f), new Vector3(.20f, .17f, .34f), steel);
            game.Shape("Underbody", cannon, new Vector3(0, -.093f, .02f), new Vector3(.15f, .07f, .23f), grip);
            game.Shape("Barrel", cannon, new Vector3(0, .028f, .28f), new Vector3(.135f, .25f, .135f), steel, PrimitiveType.Cylinder)
                .transform.localRotation = Quaternion.Euler(90, 0, 0);
            game.Shape("Collar", cannon, new Vector3(0, .028f, .433f), new Vector3(.195f, .032f, .195f), amber, PrimitiveType.Cylinder)
                .transform.localRotation = Quaternion.Euler(90, 0, 0);
            game.Shape("Muzzle bore", cannon, new Vector3(0, .028f, .467f), new Vector3(.129f, .004f, .129f), grip, PrimitiveType.Cylinder)
                .transform.localRotation = Quaternion.Euler(90, 0, 0);
            game.Shape("Front sight", cannon, new Vector3(0, .126f, .36f), new Vector3(.024f, .04f, .05f), grip);
            game.Shape("Sight insert", cannon, new Vector3(0, .15f, .354f), new Vector3(.014f, .009f, .028f), pale);
            for (int i = 0; i < 3; i++)
                game.Shape("Vent", cannon, new Vector3(-.104f, .012f, i * .039f + .008f), new Vector3(.011f, .052f, .018f), amber);

            Muzzle = game.Node("Muzzle FX", cannon).transform;
            Muzzle.localPosition = new Vector3(0, .028f, .505f);
            Respawn();
        }

        static readonly Vector3 GunHome = new(.26f, -.29f, .43f);

        public void Respawn()
        {
            controller.enabled = false;
            root.position = SandboxConfig.Spawn;
            controller.enabled = true;
            vertical = recoil = shakeTime = 0;
            Aim(SandboxConfig.Focus);
            UpdatePose(0);
        }

        public void Aim(Vector3 target)
        {
            Vector3 direction = (target - view.position).normalized;
            yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            pitch = -Mathf.Asin(direction.y) * Mathf.Rad2Deg;
            UpdatePose(0);
        }

        public void Kick()
        {
            recoil = SandboxConfig.RecoilTime;
            cannon.localPosition = GunHome - Vector3.forward * SandboxConfig.Recoil;
        }

        public void Shake(Vector3 position)
        {
            float distance = Vector3.Distance(view.position, position);
            float scale = 1 - Mathf.InverseLerp(SandboxConfig.ShakeNear, SandboxConfig.ShakeFar, distance);
            if (scale > shake || shakeTime <= 0) shake = scale;
            if (scale > 0) shakeTime = .52f;
        }

        public void Tick(Keyboard keys, Mouse mouse, bool jumping, float dt, float realDt)
        {
            if (mouse != null)
            {
                Vector2 look = mouse.delta.ReadValue() * (Sensitivity * SandboxConfig.BaseSensitivity);
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, -88f, 88f);
            }

            Vector2 direction = Vector2.zero;
            bool sprint = false;
            if (keys != null)
            {
                direction.x = (keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0);
                direction.y = (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0);
                sprint = keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed;
            }

            if (controller.isGrounded && vertical <= 0)
                vertical = jumping ? Mathf.Sqrt(-2 * Physics.gravity.y * SandboxConfig.Jump) : -2f;
            float descent = (vertical + Physics.gravity.y * dt * .5f) * dt;
            vertical += Physics.gravity.y * dt;
            UpdatePose(realDt);
            Vector2 move = Vector2.ClampMagnitude(direction, 1) * (sprint ? SandboxConfig.Sprint : SandboxConfig.Move);
            controller.Move((root.right * move.x + root.forward * move.y) * dt + Vector3.up * descent);
        }

        void UpdatePose(float dt)
        {
            root.rotation = Quaternion.Euler(0, yaw, 0);
            shakeTime = Mathf.Max(0, shakeTime - dt);
            float jitter = shake * shakeTime;
            float clock = Time.unscaledTime;
            view.localRotation = Quaternion.Euler(pitch + Mathf.Sin(clock * 113f) * jitter * 3, 
                Mathf.Cos(clock * 83f) * jitter, Mathf.Sin(clock * 97f) * jitter * 2);
            recoil = Mathf.Max(0, recoil - dt);
            cannon.localPosition = GunHome - Vector3.forward * (SandboxConfig.Recoil * recoil / SandboxConfig.RecoilTime);
        }
    }
}
