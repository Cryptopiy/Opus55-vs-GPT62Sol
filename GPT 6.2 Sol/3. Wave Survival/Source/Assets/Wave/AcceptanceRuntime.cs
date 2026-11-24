using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;
using static WaveSurvival.GameConfig;

namespace WaveSurvival
{
    internal sealed partial class Acceptance
    {
        ProfilerRecorder gcCollect, gcIncremental, gcAlloc, gcBytes;
        DrawCounters draws;
        double frameSeconds;
        float slowest, peakGc, nextSnapshot, timer, startingHeight, peakHeight, lastPitch, motionTime;
        bool wasFiring;
        long peakBytes, peakDraws;
        int frames, framesUnder60, stalledFrames, checks;
        Vector3 moveStart;
        bool gcStarted;

        partial void OtherScenario()
        {
            float elapsed = Time.realtimeSinceStartup - began;
            if (scenario == "mechanics")
            {
                Mechanics();
                return;
            }
            if (scenario == "keys")
            {
                Keys(elapsed);
                return;
            }
            if (scenario == "controls" || scenario == "layout")
            {
                Controls(elapsed);
                return;
            }
            if (scenario == "bossframe")
            {
                BossFrame(elapsed);
                return;
            }
            if (scenario == "performance" || scenario == "repeat") Performance(elapsed);
        }

        void Performance(float elapsed)
        {
            if (stage == 0)
            {
                game.ClearEnemies();
                game.Waves.Begin(7);
                game.Player.Place(new Vector3(0, .05f, -11.5f), Vector3.forward);
                gcCollect = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Collect");
                gcIncremental = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.CollectIncremental");
                gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
                gcBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                draws = new DrawCounters();
                gcStarted = true;
                LogCounters();
                Next();
            }
            if (game.Alive == AliveCap && stage == 1)
            {
                Debug.Log($"WAVE_CHECK performance population={game.Alive} remaining={game.Waves.Remaining} at={elapsed:F2}s");
                if (scenario == "performance") Debug.Log("WAVE_CHECK screenshot " + game.Capture("_Wave07"));
                nextSnapshot = elapsed + 1;
                Next();
            }
            if (game.Alive == AliveCap && stage == 2 && elapsed >= nextSnapshot)
            {
                float dt = Time.unscaledDeltaTime;
                frames++;
                frameSeconds += dt;
                slowest = Mathf.Max(slowest, dt);
                if (dt > .0182f) framesUnder60++;
                if (dt > .0333f) stalledFrames++;
                if (gcCollect.Valid) peakGc = Mathf.Max(peakGc, gcCollect.LastValue / 1000000f);
                if (gcIncremental.Valid) peakGc = Mathf.Max(peakGc, gcIncremental.LastValue / 1000000f);
                if (gcAlloc.Valid) peakGc = Mathf.Max(peakGc, gcAlloc.LastValue / 1000000f);
                if (gcBytes.Valid) peakBytes = System.Math.Max(peakBytes, gcBytes.LastValue);
                peakDraws = System.Math.Max(peakDraws, draws.Count);
            }
            bool firing = Mathf.Repeat(elapsed, 9) > 7.3f;
            float turning = Mathf.Cos(elapsed * .9f) * Time.deltaTime * 138;
            PlayerMotor player = game.Player;
            // Roof shots keep the cap occupied and exercise muzzle effects and impact pools.
            if (firing != wasFiring)
            {
                Vector3 facing = player.Root.forward;
                player.Place(player.Position, firing ? (facing * .45f + Vector3.up).normalized : facing);
                wasFiring = firing;
            }
            player.Tick(Time.deltaTime, Vector2.zero, new Vector2(turning, 0), false, false, false, false);
            if (game.Guns.Magazine == 0) game.Guns.Tick(Time.deltaTime, false, false, true);
            else
            {
                bool shot = firing && Time.time >= nextFire;
                if (shot) nextFire = Time.time + .4f;
                Physics.SyncTransforms();
                game.Guns.Tick(Time.deltaTime, shot, firing, false);
            }
            if (elapsed > 4 && game.Guns.Selected == 0) game.Guns.Select(1);
            // Leave time for editor startup and shutdown within the external test deadline.
            if (elapsed < (Application.isEditor ? 98 : 104)) return;
            Debug.Log($"WAVE_CHECK profile version={Application.unityVersion} res={Screen.width}x{Screen.height} alive={game.Alive} " +
                $"frames={frames} fps={frames / frameSeconds:F2} worstMs={slowest * 1000:F2} under55={framesUnder60} over33ms={stalledFrames} " +
                $"gcMarker={gcCollect.Valid}/{gcIncremental.Valid}/{gcAlloc.Valid} gcMaxMs={peakGc:F3} allocCounter={gcBytes.Valid} " +
                $"gcMaxBytes={peakBytes} draws={peakDraws} vsync={QualitySettings.vSyncCount}");
            Dispose();
            Finish();
        }

        void Controls(float elapsed)
        {
            float dt = Time.deltaTime;
            PlayerMotor p = game.Player;
            if (stage == 0)
            {
                p.Place(new Vector3(-20, .02f, -18), Vector3.forward);
                NavMeshPath route = new();
                bool ramp = NavMesh.CalculatePath(new Vector3(0, 0, -11), new Vector3(0, PlatformHeight, 0),
                    NavMesh.AllAreas, route) && route.status == NavMeshPathStatus.PathComplete;
                Debug.Log($"WAVE_CHECK arena covers={game.Arena.CoverAnchors.Length} gates={game.Arena.Gates.Length} " +
                    $"navTriangles={NavMesh.CalculateTriangulation().indices.Length / 3} rampRoute={ramp} " +
                    $"agentHeight={NavMesh.GetSettingsByID(0).agentHeight} agentRadius={NavMesh.GetSettingsByID(0).agentRadius}");
                moveStart = p.Position;
                Next();
                return;
            }

            timer = Time.realtimeSinceStartup - stageAt;
            switch (stage)
            {
                case 1:
                    if (timer < .65f)
                    {
                        p.Tick(dt, Vector2.zero, Vector2.zero, false, false, false, false);
                        moveStart = p.Position;
                        return;
                    }
                    p.Tick(dt, Vector2.up, Vector2.zero, false, false, false, false);
                    motionTime += dt;
                    if (motionTime < 1) return;
                    Debug.Log($"WAVE_CHECK walk metres={(p.Position - moveStart).magnitude:F2} mps={(p.Position - moveStart).magnitude / motionTime:F2}");
                    moveStart = p.Position;
                    Next();
                    return;
                case 2:
                    p.Tick(dt, Vector2.up, Vector2.zero, true, false, false, false);
                    if (timer < 1) return;
                    Debug.Log($"WAVE_CHECK sprint metres={(p.Position - moveStart).magnitude:F2} stamina={p.Energy:F2}");
                    moveStart = p.Position;
                    Next();
                    return;
                case 3:
                    p.Tick(dt, Vector2.up, Vector2.zero, false, true, false, true);
                    if (timer < 1) return;
                    Debug.Log($"WAVE_CHECK crouch metres={(p.Position - moveStart).magnitude:F2} capsule={p.Controller.height:F2} fov={p.Camera.fieldOfView:F2}");
                    startingHeight = p.Position.y;
                    peakHeight = startingHeight;
                    Next();
                    return;
                case 4:
                    p.Tick(dt, Vector2.zero, Vector2.zero, false, false, timer < .08f && checks++ == 0, false);
                    peakHeight = Mathf.Max(peakHeight, p.Position.y);
                    if (timer < .85f) return;
                    Debug.Log($"WAVE_CHECK jump height={peakHeight - startingHeight:F3} hipFov={p.Camera.fieldOfView:F2} stamina={p.Energy:F2}");
                    p.Place(new Vector3(0, .05f, -12), Vector3.forward);
                    game.Guns.Select(1);
                    Next();
                    return;
                case 5:
                    game.Guns.Tick(dt, false, timer > .4f && timer < 2.4f, timer > 2.5f);
                    if (timer < 2.49f)
                    {
                        lastPitch = Mathf.Max(lastPitch, game.Guns.Spread);
                        return;
                    }
                    if (timer < 4.65f) return;
                    Debug.Log($"WAVE_CHECK rifle shots={game.Stats.Shots} bloom={lastPitch:F2} mag={game.Guns.Magazine} reserve={game.Guns.Reserve}");
                    game.Guns.Select(2);
                    checks = 0;
                    Next();
                    return;
                case 6:
                    bool trigger = timer > .52f && checks == 0 || timer > 2.11f && checks == 1;
                    if (trigger) checks++;
                    game.Guns.Tick(dt, trigger, trigger, timer > 1.47f && timer < 2.11f);
                    if (timer < 2.42f) return;
                    Debug.Log($"WAVE_CHECK shotgun mag={game.Guns.Magazine} reserve={game.Guns.Reserve} totalShots={game.Stats.Shots} reload={game.Guns.Reloading}");
                    game.Player.Invulnerable = false;
                    game.DamagePlayer(15, p.Position - p.Root.right);
                    game.Player.Invulnerable = true;
                    Debug.Log($"WAVE_CHECK damage hp={p.HP} pulse={p.DamagePulse} direction={p.DamageDirection}");
                    Next();
                    return;
                case 7:
                    p.Tick(dt, Vector2.zero, Vector2.zero, false, false, false, false);
                    if (timer < 1f) return;
                    Debug.Log($"WAVE_CHECK noRegen hp={p.HP} damageTaken={game.Stats.Damage}");
                    game.Pause();
                    Debug.Log("WAVE_CHECK pause " + game.State);
                    Debug.Log("WAVE_CHECK screenshot " + game.Capture("_Pause"));
                    Next();
                    return;
                case 8:
                    if (timer < .6f) return;
                    game.Resume();
                    Debug.Log("WAVE_CHECK resume " + game.State);
                    game.DebugAI = true;
                    game.DebugStats = true;
                    Next();
                    return;
                case 9:
                    if (timer < .7f) return;
                    Debug.Log($"WAVE_CHECK screenshot res={Screen.width}x{Screen.height} " + game.Capture("_Arena"));
                    if (scenario == "layout")
                    {
                        Next();
                        return;
                    }
                    game.DebugAI = false;
                    game.DebugStats = false;
                    game.Guns.Select(0);
                    Next();
                    return;
                case 10:
                    if (scenario == "layout")
                    {
                        if (timer > .6f) Finish();
                        return;
                    }
                    if (elapsed > 101)
                    {
                        game.Player.Invulnerable = false;
                        game.DamagePlayer(Health, p.Position + p.Root.forward);
                        Debug.Log($"WAVE_CHECK death={game.State} HP={p.HP}");
                        Debug.Log("WAVE_CHECK screenshot " + game.Capture("_Death"));
                        Next();
                        return;
                    }
                    p.Tick(dt, PlayerMotor.Movement(null), new Vector2(Mathf.Sin(elapsed * .41f) * dt * 270, 0),
                        false, false, false, false);
                    ShootFirst(null);
                    return;
                case 11:
                    if (elapsed >= 105) Finish();
                    return;
            }
        }

        void BossFrame(float elapsed)
        {
            if (stage == 0)
            {
                game.SkipToBoss();
                Next();
                return;
            }
            Enemy enemy = game.Waves.Boss;
            if (enemy == null) return;
            if (stage == 1 && elapsed > 2)
            {
                Vector3 inside = Vector3.ProjectOnPlane(-enemy.Position, Vector3.up).normalized;
                if (Arena.Sample(enemy.Position + inside * 9, out Vector3 point, 2))
                    game.Player.Place(point + Vector3.up * .04f, enemy.Eyes - point - Vector3.up * 1.62f);
                Next();
                return;
            }
            if (stage == 2 && elapsed > 3.2f)
            {
                Debug.Log($"WAVE_CHECK screenshot res={Screen.width}x{Screen.height} {game.Capture("_Warden")}");
                Next();
                return;
            }
            if (stage == 3 && elapsed > 4.3f)
            {
                enemy.Hurt(enemy.Maximum, game.Player.Position, false);
                Next();
                return;
            }
            if (stage == 4 && elapsed > 5)
            {
                Debug.Log("WAVE_CHECK screenshot " + game.Capture("_Victory"));
                Next();
                return;
            }
            if (stage == 5 && elapsed > 6) Finish();
        }

        public void Dispose()
        {
            ReleaseDevices();
            if (!gcStarted) return;
            gcStarted = false;
            gcCollect.Dispose();
            gcIncremental.Dispose();
            gcAlloc.Dispose();
            gcBytes.Dispose();
            draws.Dispose();
        }
    }
}







