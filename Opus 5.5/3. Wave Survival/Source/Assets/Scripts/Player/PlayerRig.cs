using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

namespace WaveSurvival
{
    /// <summary>Builds and references the player: CharacterController body, camera, weapons (S-05..S-07).</summary>
    public sealed class PlayerRig : MonoBehaviour
    {
        public CharacterController Controller { get; private set; }
        public PlayerMotor Motor { get; private set; }
        public PlayerCamera View { get; private set; }
        public PlayerHealth Health { get; private set; }
        public WeaponController Weapons { get; private set; }
        public Camera Camera { get; private set; }
        public Transform CameraPivot { get; private set; }
        public Transform ViewmodelRoot { get; private set; }

        public Vector3 Position => transform.position;
        public Vector3 EyePosition => CameraPivot.position;
        public Vector3 ChestPosition => transform.position + Vector3.up * (Controller.height * 0.72f);
        public Vector3 Velocity => Motor.Velocity;
        public bool IsAlive => Health.Alive;

        public static PlayerRig Create(Vector3 position, float yaw)
        {
            var go = new GameObject("Player");
            go.layer = GameConfig.Layers.Player;
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            CharacterController cc = go.AddComponent<CharacterController>();
            cc.height = GameConfig.Player.StandHeight;
            cc.radius = GameConfig.Player.Radius;
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
            cc.stepOffset = GameConfig.Player.StepOffset;
            cc.slopeLimit = 45f;
            cc.skinWidth = 0.04f;
            cc.minMoveDistance = 0f;

            // Non-carving obstacle: NavMesh agents steer around the player instead of walking through them.
            NavMeshObstacle obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = GameConfig.Player.Radius + 0.05f;
            obstacle.height = GameConfig.Player.StandHeight;
            obstacle.center = new Vector3(0f, GameConfig.Player.StandHeight * 0.5f, 0f);
            obstacle.carving = false;

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(0f, GameConfig.Player.EyeStand, 0f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pivot, false);
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = GameConfig.View.HipFov;
            cam.nearClipPlane = GameConfig.View.Near;
            cam.farClipPlane = GameConfig.View.Far;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = GameConfig.Lighting.FogColor;
            cam.allowHDR = true;
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            camGo.AddComponent<AudioListener>();

            // Weak fill light above the shoulder: keeps the (near-eye) viewmodel readable in the dim hall.
            var fillGo = new GameObject("ViewmodelFill");
            fillGo.transform.SetParent(camGo.transform, false);
            fillGo.transform.localPosition = GameConfig.View.ViewmodelFillOffset;
            Light fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = GameConfig.View.ViewmodelFillRange;
            fill.intensity = GameConfig.View.ViewmodelFillIntensity;
            fill.color = new Color(1f, 0.9f, 0.8f);
            fill.shadows = LightShadows.None;

            // The viewmodel lives under a 0.1-scaled root right in front of the eye: it looks identical on screen
            // but sits inside the player's capsule, so it can never clip into walls (single camera, no stacking).
            var vmRoot = new GameObject("ViewmodelRoot").transform;
            vmRoot.SetParent(camGo.transform, false);
            vmRoot.localScale = Vector3.one * GameConfig.View.ViewmodelScale;
            vmRoot.gameObject.layer = GameConfig.Layers.Viewmodel;

            PlayerRig rig = go.AddComponent<PlayerRig>();
            rig.Controller = cc;
            rig.Camera = cam;
            rig.CameraPivot = pivot;
            rig.ViewmodelRoot = vmRoot;
            rig.Health = go.AddComponent<PlayerHealth>();
            rig.Motor = go.AddComponent<PlayerMotor>();
            rig.View = go.AddComponent<PlayerCamera>();
            rig.Weapons = go.AddComponent<WeaponController>();
            rig.Motor.Init(rig);
            rig.View.Init(rig, yaw);
            rig.Health.Init(rig);
            rig.Weapons.Init(rig);
            return rig;
        }
    }
}
