using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static DestructionRange.SandboxConfig;

namespace DestructionRange
{
    public sealed partial class Sandbox : MonoBehaviour
    {
        readonly Mesh[] primitives = new Mesh[6];
        Shader lit;
        internal PlayerRig Player { get; private set; }
        internal PieceField Pieces { get; private set; }
        internal ProjectileField Guns { get; private set; }
        internal readonly ScoreRun Run = new();
        internal Layout Current { get; private set; }
        internal bool Paused { get; private set; }
        internal bool Slow { get; private set; }
        internal bool DebugVisible { get; private set; }
        internal Keyboard ProbeKeyboard { get; set; }
        internal Mouse ProbeMouse { get; set; }
        internal string ScreenshotRoot { get; set; }

        void Awake()
        {
            QualitySettings.vSyncCount = VSync;
            if (Application.isBatchMode) Application.targetFrameRate = 120;
            ApplyClock();
            Application.runInBackground = true;
            Physics.defaultSolverIterations = Solver;
            Physics.defaultSolverVelocityIterations = VelocitySolver;
            Physics.defaultContactOffset = ContactOffset;
            Physics.reuseCollisionCallbacks = true;
            Physics.IgnoreLayerCollision(PlayerLayer, ShotLayer);
            // Co-located shards can contact the world and intact blocks without forming a dense contact island together.
            Physics.IgnoreLayerCollision(ShardLayer, ShardLayer);
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.shadowDistance = ShadowDistance;
            lit = Resources.Load<Material>("LitReference").shader;
            BuildArena();
            var surfaces = BuildSurfaces();
            Player = new PlayerRig(this);
            Guns = new ProjectileField(this);
            Pieces = new PieceField(this, surfaces);
            FinishSetup();
            Rebuild(Layout.Tower);
            LockCursor(true);
            SandboxProbe.TryStart(this);
        }

        void BuildArena()
        {
            var deck = Paint("Deck", new Color(.29f, .36f, .38f), .23f);
            var concrete = Paint("Perimeter", new Color(.43f, .49f, .46f), .16f);
            GroundStyle(deck, concrete);
            // Keep the upper ground face at world zero.
            Solid("Ground", new Vector3(0, -.1f, 0), new Vector3(ArenaSize, .2f, ArenaSize), deck);
            for (int side = 0; side < 4; side++)
            {
                Quaternion orientation = Quaternion.Euler(0, 90 * side, 0);
                float edge = ArenaSize * .5f;
                Transform border = Solid("Concrete border", orientation * new Vector3(0, BorderHeight * .5f, edge),
                    new Vector3(ArenaSize, BorderHeight, .65f), concrete);
                border.rotation = orientation;
                var wall = Node("Invisible perimeter");
                wall.transform.SetPositionAndRotation(orientation * new Vector3(0, InvisibleWallHeight * .5f, edge), orientation);
                wall.AddComponent<BoxCollider>().size = new Vector3(ArenaSize, InvisibleWallHeight, .4f);
            }
            var sun = Node("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1, .845f, .68f);
            sun.intensity = 2.15f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(37, -31, 0);
            RenderSettings.sun = sun;
            SkyStyle();
        }

        Transform Solid(string name, Vector3 position, Vector3 size, Material material)
        {
            var go = Shape(name, transform, position, size, material);
            go.AddComponent<BoxCollider>().contactOffset = ContactOffset;
            return go.transform;
        }

        Material[,] BuildSurfaces()
        {
            Color[] colors = { new(.54f, .56f, .52f), new(.56f, .29f, .12f), new(.34f, .82f, .88f) };
            const int shades = 13;
            var surfaces = new Material[colors.Length, shades];
            for (int i = 0; i < colors.Length; i++)
            for (int j = 0; j < shades; j++)
            {
                float brightness = Mathf.Lerp(1f - BrightnessVariation, 1f + BrightnessVariation, (float)j / (shades - 1));
                Color tint = colors[i] * brightness;
                tint.a = 1;
                surfaces[i, j] = Paint((Matter)i + " " + j, tint, i == (int)Matter.Glass ? .8f : .22f);
            }
            SurfaceStyle(surfaces);
            return surfaces;
        }

        internal GameObject Node(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent == null ? transform : parent, false);
            return go;
        }

        internal Mesh MeshFor(PrimitiveType kind)
        {
            if (primitives[(int)kind] != null) return primitives[(int)kind];
            GameObject sample = GameObject.CreatePrimitive(kind);
            var mesh = sample.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(sample);
            primitives[(int)kind] = mesh;
            return mesh;
        }

        internal GameObject Shape(string name, Transform parent, Vector3 p, Vector3 scale, Material material, PrimitiveType kind = PrimitiveType.Cube)
        {
            var go = Node(name, parent);
            go.transform.localPosition = p;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = MeshFor(kind);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return go;
        }

        internal Material Paint(string name, Color color, float smoothness)
        {
            var material = new Material(lit) { name = name, color = color, enableInstancing = true };
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        void Update()
        {
            if (ProbeKeyboard != null) InputSystem.Update();
            Keyboard keys = ProbeKeyboard ?? Keyboard.current;
            Mouse mouse = ProbeMouse ?? Mouse.current;
            if (keys != null)
            {
                if (Pressed(keys.f1Key)) DebugVisible = !DebugVisible;
                if (Pressed(keys.f8Key)) Hud.ToggleGameplayVisibility();
                if (Pressed(keys.f12Key)) SaveScreenshot();
                if (Pressed(keys.escapeKey)) SetPaused(!Paused);
            }

            if (!Paused)
            {
                if (keys != null)
                {
                    if (Pressed(keys.f5Key)) Switch(Layout.Tower);
                    if (Pressed(keys.f6Key)) Switch(Layout.Arch);
                    if (Pressed(keys.f7Key)) Switch(Layout.Pyramid);
                    if (Pressed(keys.rKey)) Restart();
                    if (Pressed(keys.xKey)) { Pieces.ClearShards(); Guns.ClearSpent(); DebrisCleared(); }
                    if (Pressed(keys.tKey)) SetSlow(!Slow);
                    if (Pressed(keys.digit1Key)) Guns.Select(Weapon.Cannon);
                    if (Pressed(keys.digit2Key)) Guns.Select(Weapon.Shell);
                    if (Pressed(keys.digit3Key)) Guns.Select(Weapon.Orb);
                }

                if (mouse != null)
                {
                    float wheel = mouse.scroll.ReadValue().y;
                    if (wheel != 0) Guns.Select((Weapon)(((int)Guns.Selected + (wheel > 0 ? 1 : 2)) % Weapons.Length));
                }

                Player.Tick(keys, mouse, keys != null && Pressed(keys.spaceKey), Time.deltaTime, Time.unscaledDeltaTime);
                if (mouse != null && mouse.leftButton.isPressed) Guns.Fire();
                Run.Tick(Time.unscaledDeltaTime);
            }
            PresentationTick();
        }

        bool Pressed(UnityEngine.InputSystem.Controls.KeyControl key) =>
            ProbeKeyboard == null ? key.wasPressedThisFrame : key.isPressed;

        void FixedUpdate()
        {
            Guns.Step(Time.fixedDeltaTime);
            Pieces.Step(Time.fixedDeltaTime);
        }

        internal void Restart()
        {
            Switch(Current);
            SetPaused(false);
        }

        void Switch(Layout layout)
        {
            Player.Respawn();
            Rebuild(layout);
        }

        internal void Rebuild(Layout layout)
        {
            Current = layout;
            Guns.Reset();
            Run.Reset();
            ResetPresentation();
            Pieces.Reset(layout);
        }

        internal void SetSlow(bool value)
        {
            Slow = value;
            ApplyClock();
            audioBus.SetClock(Paused, Slow);
        }

        internal void SetPaused(bool value)
        {
            Paused = value;
            ApplyClock();
            LockCursor(!value);
            PauseChanged(value);
        }

        void ApplyClock()
        {
            float scale = Slow ? SlowScale : 1f;
            Time.timeScale = Paused ? 0 : scale;
            Time.fixedDeltaTime = FixedStep * scale;
        }

        static void LockCursor(bool locked)
        {
            if (Application.isBatchMode) return;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        internal void BlockCounted(Matter kind, Vector3 point)
        {
            int points = Run.Count(kind);
            ScorePop(point, points);
        }

        internal void ShotFired(Weapon weapon) { Player.Kick(); ShotPresentation(weapon); }
        internal void SelectionChanged() => SelectionPresentation();
        internal void CannonImpact(Vector3 point, float impulse) => CannonPresentation(point, impulse);
        internal void PieceImpact(Matter kind, Vector3 point, float impulse) => ImpactPresentation(kind, point, impulse);
        internal void PieceBroke(Matter kind, Vector3 point, float impulse) => FracturePresentation(kind, point, impulse);

        internal void Detonate(Vector3 point)
        {
            Pieces.Radial(point, BlastRadius, BlastImpulse, BlastLift, false);
            Guns.AffectShots(point, BlastRadius, BlastImpulse, BlastLift, false);
            Player.Shake(point);
            BlastPresentation(point);
        }

        internal void OrbStarted(Vector3 point) => OrbPresentation(point);
        internal void OrbPull(Vector3 point)
        {
            Pieces.Radial(point, OrbRadius, SandboxConfig.OrbPull, 0, true);
            Guns.AffectShots(point, OrbRadius, SandboxConfig.OrbPull, 0, true);
        }

        internal void OrbReleased(Vector3 point)
        {
            Pieces.Radial(point, OrbRadius, OrbPulse, 0, false);
            Guns.AffectShots(point, OrbRadius, OrbPulse, 0, false);
            OrbReleasePresentation(point);
        }

        void SaveScreenshot()
        {
            string root = ScreenshotRoot ?? System.IO.Path.GetDirectoryName(Application.dataPath);
            string folder = System.IO.Path.Combine(root, "Screenshots");
            System.IO.Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder, $"Sandbox-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png"));
        }

        partial void GroundStyle(Material ground, Material border);
        partial void SkyStyle();
        partial void SurfaceStyle(Material[,] materials);
        partial void FinishSetup();
        partial void ResetPresentation();
        partial void PresentationTick();
        partial void PauseChanged(bool value);
        partial void DebrisCleared();
        partial void ScorePop(Vector3 point, int score);
        partial void ShotPresentation(Weapon weapon);
        partial void SelectionPresentation();
        partial void CannonPresentation(Vector3 point, float impulse);
        partial void ImpactPresentation(Matter kind, Vector3 point, float impulse);
        partial void FracturePresentation(Matter kind, Vector3 point, float impulse);
        partial void BlastPresentation(Vector3 point);
        partial void OrbPresentation(Vector3 point);
        partial void OrbReleasePresentation(Vector3 point);
    }
}
