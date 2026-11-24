using UnityEngine;
using UnityEngine.InputSystem;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>
    /// Photo mode (I-29): P toggles. Hides the HUD, freezes the player and the day cycle, and gives a free
    /// fly camera (WASD + mouse look while the right button is held, Q/E down/up, Shift faster).
    /// The photo panel sets FOV 20-100, DOF focus/aperture, exposure and time of day; F12 renders a
    /// 3840x2160 PNG through a RenderTexture.
    /// </summary>
    public sealed class PhotoMode : MonoBehaviour
    {
        public bool Active { get; private set; }
        public float Fov { get; set; } = C.DefaultFov;
        public float Exposure { get; set; }
        public string LastCapture { get; private set; } = "";

        float yaw, pitch;
        bool timeWasPaused;

        public void SetActive(bool on)
        {
            if (on == Active || Game.MainCamera == null) return;
            Active = on;
            var cam = Game.MainCamera;
            if (on)
            {
                timeWasPaused = Game.TimeOfDay.Paused;
                Game.TimeOfDay.Paused = true;
                Game.Player.Frozen = true;
                Game.CameraRig.enabled = false;
                Vector3 e = cam.transform.eulerAngles;
                yaw = e.y;
                pitch = e.x > 180f ? e.x - 360f : e.x;
                Fov = cam.fieldOfView;
                Game.Post.DofEnabled = true;
                Game.Post.DofFocusDistance = Mathf.Max(1f, Vector3.Distance(cam.transform.position, Game.Player.transform.position));
            }
            else
            {
                Game.TimeOfDay.Paused = timeWasPaused;
                Game.Player.Frozen = false;
                Game.CameraRig.enabled = true;
                Game.CameraRig.Snap();
                cam.fieldOfView = C.DefaultFov;
                Game.Post.DofEnabled = false;
                Game.Post.PhotoExposure = 0f;
            }
            Game.UI.OnPhotoModeChanged(on);
        }

        /// <summary>Places the free camera (automated showcase screenshots).</summary>
        public void SetView(Vector3 position, float yawDegrees, float pitchDegrees)
        {
            if (!Active) return;
            yaw = yawDegrees;
            pitch = pitchDegrees;
            Game.MainCamera.transform.SetPositionAndRotation(position, Quaternion.Euler(pitch, yaw, 0f));
        }

        public void CaptureHighRes()
        {
            if (!Active) return;
            LastCapture = ScreenshotService.CaptureCamera(Game.MainCamera, C.PhotoCaptureWidth, C.PhotoCaptureHeight, "photo4k");
        }

        void LateUpdate()
        {
            if (!Active || Game.Paused) return;
            var cam = Game.MainCamera;
            float dt = Time.unscaledDeltaTime;
            if (GameInput.RightMouseHeld)
            {
                Vector2 d = GameInput.MouseDelta;
                yaw += d.x * C.MouseSensitivity;
                pitch = Mathf.Clamp(pitch - d.y * C.MouseSensitivity, -89f, 89f);
            }
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 move = Vector3.zero;
            if (GameInput.Held(Key.W)) move += rot * Vector3.forward;
            if (GameInput.Held(Key.S)) move -= rot * Vector3.forward;
            if (GameInput.Held(Key.D)) move += rot * Vector3.right;
            if (GameInput.Held(Key.A)) move -= rot * Vector3.right;
            if (GameInput.Held(Key.E)) move += Vector3.up;
            if (GameInput.Held(Key.Q)) move -= Vector3.up;
            float speed = C.PhotoFlySpeed * (GameInput.Held(Key.LeftShift) ? 3f : 1f);
            Vector3 pos = cam.transform.position + move * (speed * dt);
            float b = C.WorldBoundary;
            pos.x = Mathf.Clamp(pos.x, -b, b);
            pos.z = Mathf.Clamp(pos.z, -b, b);
            if (Game.World != null) pos.y = Mathf.Max(pos.y, Game.World.Data.HeightAt(pos.x, pos.z) + 0.3f);
            pos.y = Mathf.Min(pos.y, 400f);
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = Mathf.Clamp(Fov, C.PhotoFovMin, C.PhotoFovMax);
            Game.Post.PhotoExposure = Exposure;
        }
    }
}
