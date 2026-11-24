using UnityEngine;
using PhysicsCfg = DestructionSandbox.GameConfig.Physics;
using LayersCfg = DestructionSandbox.GameConfig.Layers;
using TimingCfg = DestructionSandbox.GameConfig.Timing;

namespace DestructionSandbox
{
    /// <summary>
    /// Applies the global engine settings the game depends on at start-up, so the result does not depend on
    /// whatever was left in the editor. (The TGS solver type is a project setting: DynamicsManager.asset.)
    /// </summary>
    public static class RuntimeSettings
    {
        public static void Apply()
        {
            // GPU safety: VSync on, no frame-rate override.
            QualitySettings.vSyncCount = TimingCfg.VSyncCount;
            Application.targetFrameRate = -1;

            Time.timeScale = 1f;
            Time.fixedDeltaTime = TimingCfg.FixedTimestep;
            Time.maximumDeltaTime = TimingCfg.MaximumDeltaTime;

            Physics.simulationMode = SimulationMode.FixedUpdate;
            Physics.defaultSolverIterations = PhysicsCfg.SolverIterations;
            Physics.defaultSolverVelocityIterations = PhysicsCfg.SolverVelocityIterations;
            Physics.sleepThreshold = PhysicsCfg.SleepThreshold;
            Physics.defaultContactOffset = PhysicsCfg.ContactOffset;
            Physics.defaultMaxDepenetrationVelocity = PhysicsCfg.MaxDepenetrationVelocity;
            Physics.bounceThreshold = PhysicsCfg.BounceThreshold;
            Physics.defaultMaxAngularSpeed = PhysicsCfg.MaxAngularSpeed;
            Physics.improvedPatchFriction = true;   // much better static friction for stacked boxes
            Physics.reuseCollisionCallbacks = true; // no Collision allocations per contact callback
            Physics.queriesHitTriggers = false;

            // The player never collides with its own projectiles.
            Physics.IgnoreLayerCollision(LayersCfg.Player, LayersCfg.Projectile, true);
        }
    }
}
