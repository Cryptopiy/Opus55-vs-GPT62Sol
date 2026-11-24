using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WaveSurvival
{
    /// <summary>
    /// Automated acceptance runs (test tooling, only active with "-autotest &lt;scenario&gt; [-testDuration s]").
    /// The bot plays through the real input path; scenarios mirror the spec's acceptance script and print
    /// "[TEST] key=value" lines into the log. Scenarios: smoke, wave1, wave2, wave3, boss, wave4, perf, gallery, death,
    /// weapons, uitoggle (F8 hide-UI check, see UiToggleCheck).
    /// </summary>
    public sealed class AutoTest : MonoBehaviour
    {
        string scenario;
        float duration, t, nextStatus, nextSample;
        BotInput bot;
        int warnings, errors;
        readonly Dictionary<string, int> events = new Dictionary<string, int>();
        readonly HashSet<int> shotTaken = new HashSet<int>();
        bool finished;
        float quitAt = -1f;

        // metrics
        float flankSpreadMax, telegraphAt = -1f, maxNoProgress, maxStall, wave1Clear = -1f;
        int flankSamples, flankDetailSamples, linedUpPairs, chasingPairs, coverHidden, coverTotal, coverInBand;
        readonly Dictionary<EnemyBase, float> stallTimers = new Dictionary<EnemyBase, float>();
        readonly Dictionary<EnemyBase, Vector3> stallAnchor = new Dictionary<EnemyBase, Vector3>();
        string armorPhase = "";
        readonly List<string> armorLog = new List<string>();
        float[] frameTimes = new float[20000];
        int frameCount;
        long gcMax, gcSum, lastHeap;
        int gcStart, aliveSum;
        TankEnemy armorTank;
        float armorTimer;
        int armorStep;
        bool galleryVictory;
        UiToggleCheck uiCheck;

        public static void TryStart(Transform root)
        {
            string[] args = Environment.GetCommandLineArgs();
            string sc = null;
            float dur = 90f;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-autotest") sc = args[i + 1];
                if (args[i] == "-testDuration") float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dur);
            }
            if (sc == null) return;
            var go = new GameObject("AutoTest");
            go.transform.SetParent(root, false);
            AutoTest at = go.AddComponent<AutoTest>();
            at.scenario = sc;
            at.duration = Mathf.Min(dur, 115f);
            at.bot = new BotInput();
            InputRouter.Source = at.bot;
            Application.logMessageReceived += at.OnLog;
            EnemyBase.EventHook += at.OnEvent;
            EnemyBase.DamageHook += at.OnDamage;
            Game.GodMode = sc != "death";
            Log("start scenario=" + sc + " duration=" + at.duration + " gfx=" + SystemInfo.graphicsDeviceType + " res=" + Screen.width + "x" + Screen.height);
        }

        static void Log(string msg) => Debug.Log("[TEST] " + msg);

        void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Warning) { warnings++; Log("console_warning: " + condition); }
            else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; Log("console_error: " + condition + " | " + stack.Replace('\n', ' ')); }
        }

        void Count(string key)
        {
            events.TryGetValue(key, out int n);
            events[key] = n + 1;
        }

        void OnEvent(EnemyBase e, string ev)
        {
            Count(ev);
            if (ev == "telegraph") telegraphAt = Time.time;
            if (ev == "charge_start" && telegraphAt > 0f) Log("tank_telegraph_to_charge_s=" + (Time.time - telegraphAt).ToString("0.00"));
            if ((ev == "phase2" || ev == "phase3") && e is WardenBoss b) Log(ev + "_at_health_pct=" + (b.HealthFraction * 100f).ToString("0.0"));
            if (ev == "cover_chosen" && e is ShooterEnemy s && Game.Player != null)
            {
                coverTotal++;
                if (CoverFinder.IsHidden(s.CoverPoint, Game.Player.EyePosition)) coverHidden++;
                float d = MathUtil.FlatDistance(s.CoverPoint, Game.Player.Position);
                if (d >= GameConfig.Enemy.ShooterMinDist && d <= GameConfig.Enemy.ShooterMaxDist) coverInBand++;
            }
            if (ev.StartsWith("summon") || ev == "slam" || ev == "slam_hit" || ev == "retreat" || ev == "wave_cleared" || ev.StartsWith("wave_start"))
                Log("event " + ev + " t=" + t.ToString("0.0"));
        }

        void OnDamage(EnemyBase e, HitZone zone, float mult, float dmg)
        {
            if (e is TankEnemy && armorPhase.Length > 0) armorLog.Add(armorPhase + ":" + zone + ":x" + mult.ToString("0.##"));
            if (e is WardenBoss && zone == HitZone.Head) Count("boss_head_x" + mult.ToString("0.##"));
            if (e is TankEnemy && zone == HitZone.WeakPoint) Count("tank_weakpoint_hit_x" + mult.ToString("0.##"));
        }

        void Start()
        {
            switch (scenario)
            {
                case "wave1": bot.Weapon = 0; bot.AllowFire = false; bot.AllowMove = false; break;
                case "wave2": Game.Waves.JumpToWave(2); bot.AllowFire = false; bot.AllowMove = false; break;
                case "wave3": Game.Waves.JumpToWave(3); bot.Weapon = 1; break;
                case "boss": Game.Waves.JumpToWave(5); bot.Weapon = 1; break;
                case "wave4": Game.Waves.JumpToWave(4); Game.AiDebug.Toggle(); bot.Weapon = 1; break;
                case "perf": Game.Waves.JumpToWave(6); bot.AllowFire = false; bot.AllowMove = false; break;
                case "gallery": bot.AllowFire = false; bot.AllowMove = false; break;
                case "death": Game.Waves.JumpToWave(3); bot.AllowFire = false; bot.AllowMove = false; break;
                case "weapons": bot.Weapon = 0; bot.AllowMove = false; CheckPlatformPath(); break;
                case "uitoggle": uiCheck = new UiToggleCheck(bot); break;
                default: bot.Weapon = 1; break;
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            if (finished)
            {
                // Unscaled timer: Invoke() would never fire while an end screen has Time.timeScale = 0.
                if (quitAt > 0f && t >= quitAt) { quitAt = -1f; Quit(); }
                return;
            }
            switch (scenario)
            {
                case "wave1": TickWave1(); break;
                case "wave2": TickWave2(); break;
                case "wave3": TickWave3(dt); break;
                case "boss": TickBoss(); break;
                case "wave4": TickWave4(dt); break;
                case "perf": TickPerf(dt); break;
                case "gallery": TickGallery(); break;
                case "death": TickDeath(); break;
                case "weapons": TickWeapons(); break;
                case "uitoggle": if (uiCheck.Tick(t, dt)) duration = Mathf.Min(duration, t + 1f); break;
                default: TickSmoke(); break;
            }
            if (t >= nextStatus)
            {
                nextStatus = t + 5f;
                PlayerRig p = Game.Player;
                Log("status t=" + t.ToString("0") + " wave=" + Game.Waves.Wave + " phase=" + Game.Waves.Phase + " alive=" + Game.Enemies.AliveCount +
                    " remaining=" + Game.Waves.Remaining + " fps=" + Game.Hud.Overlay.LastFps.ToString("0") + " hp=" + (p != null ? p.Health.Hp.ToString("0") : "-") +
                    " state=" + Game.Manager.State + " kills=" + Game.Stats.TotalKills + " shots=" + Game.Stats.ShotsFired + " hits=" + Game.Stats.ShotsHit);
            }
            if (t >= duration) Finish();
        }

        void Shot(int id, float at, string tag)
        {
            if (t < at || shotTaken.Contains(id)) return;
            shotTaken.Add(id);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            Log("screenshot " + Screenshot.Capture(scenario + "_" + tag));
        }

        // ---------------- scenarios ----------------

        void TickSmoke()
        {
            Shot(0, 6f, "start");
            Shot(1, 20f, "combat");
            Shot(2, 40f, "combat2");
        }

        void TickWave1()
        {
            // Hold fire for the first seconds of the wave so the pack can close in and spread (observation window).
            if (t > 19f) { bot.AllowFire = true; bot.AllowMove = true; }
            Shot(0, 16f, "runners");
            if (t >= nextSample)
            {
                nextSample = t + 0.25f;
                SampleRunners();
            }
            if (wave1Clear < 0f && Game.Waves.Wave >= 2)
            {
                wave1Clear = t;
                Log("wave1_cleared_at=" + t.ToString("0.0"));
                duration = Mathf.Min(duration, t + 2f);
            }
        }

        void SampleRunners()
        {
            Vector3 pp = Game.Player.Position;
            var list = Game.Enemies.Active;
            float minA = 999f, maxA = -999f;
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (!(list[i] is RunnerEnemy r) || !r.IsAlive || r.Fsm.CurrentId != EnemyState.Chase) continue;
                if (r.DistanceToPlayer > 16f) continue;
                n++;
                string d = r.StateDetail;
                if (d != null && d.StartsWith("flank")) flankDetailSamples++;
                float a = Mathf.Atan2(r.transform.position.x - pp.x, r.transform.position.z - pp.z) * Mathf.Rad2Deg;
                minA = Mathf.Min(minA, a);
                maxA = Mathf.Max(maxA, a);
                for (int j = i + 1; j < list.Count; j++)
                {
                    if (!(list[j] is RunnerEnemy o) || !o.IsAlive || o.Fsm.CurrentId != EnemyState.Chase || o.DistanceToPlayer > 16f) continue;
                    chasingPairs++;
                    float b = Mathf.Atan2(o.transform.position.x - pp.x, o.transform.position.z - pp.z) * Mathf.Rad2Deg;
                    // "Single file": two runners on (almost) the same bearing within a few metres of each other.
                    if (Mathf.Abs(Mathf.DeltaAngle(a, b)) < 8f && MathUtil.FlatDistance(r.transform.position, o.transform.position) < 3f) linedUpPairs++;
                }
            }
            if (n >= 2)
            {
                flankSamples++;
                flankSpreadMax = Mathf.Max(flankSpreadMax, Mathf.Min(360f, maxA - minA));
            }
        }

        void TickWave2()
        {
            Shot(0, 3f, "start");
            if (t > 14f && !Game.AiDebug.Enabled) Game.AiDebug.Toggle();
            Shot(1, 18f, "cover_debug");
            if (t > 26f)
            {
                // Walk straight at the nearest shooter to provoke the retreat behaviour (< 6 m).
                EnemyBase target = Nearest(EnemyType.Shooter);
                if (target != null)
                {
                    bot.ForcedTarget = target;
                    bot.AllowFire = false;
                    Vector3 local = Game.Player.transform.InverseTransformDirection(MathUtil.FlatDirection(Game.Player.Position, target.transform.position));
                    bot.ForcedMove = new Vector2(local.x, local.z);
                    bot.HasForcedMove = true;
                }
            }
            Shot(2, 34f, "retreat");
        }

        void TickWave3(float dt)
        {
            Shot(0, 4f, "start");
            if (armorStep == 0)
            {
                armorTank = Nearest(EnemyType.Tank) as TankEnemy;
                // Freeze only once it walked away from the walls, so there is room on every side of it.
                if (armorTank != null && armorTank.Fsm.CurrentId == EnemyState.Chase && ArenaInfo.Inside(armorTank.transform.position, 8f))
                {
                    armorStep = 1;
                    armorTimer = 0f;
                }
                return;
            }
            if (armorTank == null || !armorTank.IsAlive)
            {
                if (armorStep < 99) { armorStep = 99; FinishArmor(); }
                return;
            }
            armorTimer += dt;
            Transform tt = armorTank.transform;
            switch (armorStep)
            {
                case 1: // freeze the tank (test only) and stand in front of it
                    armorTank.enabled = false;
                    armorTank.StopMoving();
                    Refill();
                    Teleport(tt.position + tt.forward * 7f);
                    armorPhase = "front";
                    bot.ForcedTarget = armorTank;
                    bot.ForcedAimPoint = armorTank.Body.Spine.position + Vector3.up * 0.35f;
                    armorStep = 2; armorTimer = 0f;
                    break;
                case 2:
                    if (armorTimer > 1.6f)
                    {
                        Refill();
                        if (!Teleport(tt.position + tt.right * 7f, 1.2f)) Teleport(tt.position - tt.right * 7f);
                        armorPhase = "side"; armorStep = 3; armorTimer = 0f;
                    }
                    break;
                case 3:
                    if (armorTimer > 1.6f)
                    {
                        Refill();
                        if (!Teleport(tt.position - tt.forward * 6f, 1.2f)) Teleport(tt.position - tt.forward * 4f);
                        armorPhase = "rear";
                        Transform weak = armorTank.Body.Spine.Find("WeakPoint");
                        bot.ForcedAimPoint = weak != null ? weak.position : armorTank.CenterPosition;
                        armorStep = 4; armorTimer = 0f;
                    }
                    break;
                case 4:
                    if (armorTimer > 1.6f)
                    {
                        FinishArmor();
                        armorTank.enabled = true; // release: observe telegraph -> charge -> stun
                        bot.ForcedAimPoint = null;
                        bot.AllowFire = false;
                        Teleport(tt.position + tt.forward * 10f);
                        armorStep = 5; armorTimer = 0f;
                    }
                    break;
                case 5:
                    Shot(1, 0f, "tank_charge");
                    if (armorTimer > 8f) { bot.AllowFire = true; bot.ForcedTarget = null; armorStep = 6; }
                    break;
            }
        }

        void FinishArmor()
        {
            armorPhase = "";
            var counts = new Dictionary<string, int>();
            foreach (string s in armorLog) { counts.TryGetValue(s, out int n); counts[s] = n + 1; }
            foreach (var kv in counts) Log("armor " + kv.Key + " hits=" + kv.Value);
        }

        /// <summary>Test-only: put the player on the NavMesh near a point; false if it had to move more than maxShift.</summary>
        bool Teleport(Vector3 pos, float maxShift = 99f)
        {
            PlayerRig p = Game.Player;
            Vector3 want = pos;
            if (Game.Arena.SampleHumanoid(pos, 4f, out Vector3 s)) pos = s;
            if (MathUtil.FlatDistance(want, pos) > maxShift) return false;
            p.Controller.enabled = false;
            p.transform.position = pos;
            p.Controller.enabled = true;
            return true;
        }

        static void Refill()
        {
            WeaponSlot w = Game.Player.Weapons.Current;
            w.Mag = w.Def.MagSize; // test-only: a reload must not hide the armour measurement window
        }

        void TickBoss()
        {
            Shot(0, 4f, "boss_intro");
            WardenBoss boss = Game.Enemies.Boss;
            WeaponSlot rifle = Game.Player.Weapons.Slots[1];
            if (rifle.Reserve < 60) rifle.Reserve = 180; // test-only: the bot does not collect ammo crates
            if (boss != null)
            {
                bot.ForcedTarget = boss;
                bot.AimAtHead = (int)(t / 3f) % 2 == 0;
                if (boss.Phase == 2) Shot(1, 0f, "boss_phase2");
                if (boss.Phase == 3) Shot(2, 0f, "boss_phase3");
            }
            if (Game.Manager.State == GameState.Victory && !galleryVictory)
            {
                galleryVictory = true;
                Log("victory_screen=true rank=" + Game.Stats.Rank() + " time=" + Game.Stats.PlayTime.ToString("0.0"));
                duration = Mathf.Min(duration, t + 1.5f);
            }
            if (galleryVictory) Shot(3, 0f, "victory");
        }

        void TickWave4(float dt)
        {
            Shot(0, 10f, "ai_debug");
            Shot(1, 30f, "ai_debug2");
            var list = Game.Enemies.Active;
            for (int i = 0; i < list.Count; i++)
            {
                EnemyBase e = list[i];
                if (!e.IsAlive) continue;
                maxNoProgress = Mathf.Max(maxNoProgress, e.Stuck.NoProgressTime);
                // Independent check: time since the agent last moved 0.5 m while it wanted to move.
                if (!stallAnchor.TryGetValue(e, out Vector3 a) || !e.WantsToMove) { stallAnchor[e] = e.transform.position; stallTimers[e] = 0f; continue; }
                if (MathUtil.FlatDistance(a, e.transform.position) >= GameConfig.Enemy.StuckMinProgress) { stallAnchor[e] = e.transform.position; stallTimers[e] = 0f; continue; }
                float st = stallTimers[e] + dt;
                stallTimers[e] = st;
                maxStall = Mathf.Max(maxStall, st);
                if (!ArenaInfo.Inside(e.transform.position, 0f)) Count("outside_arena_sample");
            }
            if (wave1Clear < 0f && (Game.Waves.Wave >= 5 || (t > 10f && Game.Waves.Phase == WavePhase.Countdown)))
            {
                wave1Clear = t;
                Log("wave4_cleared_at=" + t.ToString("0.0"));
                duration = Mathf.Min(duration, t + 1f);
            }
        }

        void TickPerf(float dt)
        {
            int alive = Game.Enemies.AliveCount;
            if (t > 20f && t < 50f)
            {
                if (frameCount == 0) { gcStart = GC.CollectionCount(0); lastHeap = GC.GetTotalMemory(false); }
                if (frameCount < frameTimes.Length) frameTimes[frameCount++] = dt;
                // Managed-heap growth per frame = bytes allocated by scripts (release players have no GC profiler counter).
                long heap = GC.GetTotalMemory(false);
                long grown = heap - lastHeap;
                if (grown > 0) { gcSum += grown; if (grown > gcMax) gcMax = grown; }
                lastHeap = heap;
                aliveSum += alive;
            }
            Shot(0, 50.2f, "perf25");
            if (t >= 50.5f && frameCount > 0)
            {
                Array.Sort(frameTimes, 0, frameCount);
                float sum = 0f;
                for (int i = 0; i < frameCount; i++) sum += frameTimes[i];
                float avg = sum / frameCount;
                float p99 = frameTimes[Mathf.Min(frameCount - 1, (int)(frameCount * 0.99f))];
                Log("perf frames=" + frameCount + " avg_fps=" + (1f / avg).ToString("0.0") + " avg_ms=" + (avg * 1000f).ToString("0.00") +
                    " p99_ms=" + (p99 * 1000f).ToString("0.00") + " max_ms=" + (frameTimes[frameCount - 1] * 1000f).ToString("0.00") +
                    " alive_avg=" + ((float)aliveSum / frameCount).ToString("0.0") + " gc_alloc_max_bytes=" + gcMax +
                    " gc_alloc_total_bytes=" + gcSum + " gc_collections=" + (GC.CollectionCount(0) - gcStart) + " vsync=" + QualitySettings.vSyncCount);
                frameCount = 0;
                duration = Mathf.Min(duration, t + 0.5f);
            }
        }

        void TickGallery()
        {
            PlayerRig p = Game.Player;
            Shot(0, 2.5f, "arena");
            if (t > 3f && !shotTaken.Contains(100)) { shotTaken.Add(100); Game.Hud.Overlay.Toggle(); }
            Shot(1, 4f, "overlay");
            if (t > 4.5f && !shotTaken.Contains(101))
            {
                shotTaken.Add(101);
                Game.Hud.Overlay.Toggle();
                Vector3 f = p.transform.forward, r = p.transform.right;
                Freeze(Game.Enemies.Spawn(EnemyType.Runner, p.Position + f * 6f - r * 2.5f, 180f));
                Freeze(Game.Enemies.Spawn(EnemyType.Shooter, p.Position + f * 6.5f, 180f));
                Freeze(Game.Enemies.Spawn(EnemyType.Tank, p.Position + f * 7f + r * 2.8f, 180f));
            }
            Shot(2, 6.5f, "enemies");
            if (t > 7f && t < 9.5f) { bot.Weapon = 1; }
            Shot(3, 9f, "rifle");
            if (t > 9.5f && !shotTaken.Contains(102)) { shotTaken.Add(102); Game.Waves.JumpToWave(5); }
            Shot(4, 15f, "boss");
            if (t > 16f && !shotTaken.Contains(103)) { shotTaken.Add(103); Game.AiDebug.Toggle(); }
            Shot(5, 17f, "aidebug");
            if (t > 18f && !shotTaken.Contains(104)) { shotTaken.Add(104); Game.AiDebug.Toggle(); Game.Manager.Pause(); }
            Shot(6, 18.5f, "pause");
            if (t > 19f && !shotTaken.Contains(105))
            {
                shotTaken.Add(105);
                Game.Manager.Resume();
                WardenBoss b = Game.Enemies.Boss;
                if (b != null) b.Die(Vector3.forward, false, true);
            }
            Shot(7, 23f, "victory");
            if (t > 24f) duration = Mathf.Min(duration, t);
        }

        static void Freeze(EnemyBase e)
        {
            if (e == null) return;
            e.enabled = false;
        }

        void TickDeath()
        {
            if (Game.Manager.State == GameState.PlayerDead && !shotTaken.Contains(200)) { shotTaken.Add(200); telegraphAt = t; Log("player_died t=" + t.ToString("0.0")); }
            if (!shotTaken.Contains(200)) return;
            Shot(0, telegraphAt, "dying");
            // The death screen appears after the 1.4 s death camera.
            Shot(1, telegraphAt + 2.5f, "death_screen");
            duration = Mathf.Min(duration, telegraphAt + 4f);
        }

        /// <summary>S-04 check: a NavMesh path from the arena floor to the top of the platform (via a ramp).</summary>
        static void CheckPlatformPath()
        {
            var path = new UnityEngine.AI.NavMeshPath();
            Game.Arena.SampleHumanoid(new Vector3(-15f, 0f, -20f), 3f, out Vector3 a);
            Game.Arena.SampleHumanoid(new Vector3(0f, GameConfig.Arena.PlatformHeight, 0f), 1f, out Vector3 b);
            UnityEngine.AI.NavMesh.CalculatePath(a, b, Game.Arena.HumanoidFilter, path);
            float maxY = 0f;
            foreach (Vector3 c in path.corners) maxY = Mathf.Max(maxY, c.y);
            Log("navmesh_platform_path status=" + path.status + " corners=" + path.corners.Length + " target_y=" + b.y.ToString("0.00") + " max_corner_y=" + maxY.ToString("0.00"));
        }

        float weaponsPhaseT;
        int weaponsPhase;
        float staminaMin = 999f, fovMin = 999f, eyeMin = 999f, jumpMaxY;

        /// <summary>Exercises movement keys, ADS and all three weapons through the bot's input.</summary>
        void TickWeapons()
        {
            PlayerRig p = Game.Player;
            WeaponController w = p.Weapons;
            if (t < 2f) return; // skip start-up frames (the first delta contains the whole bootstrap time)
            weaponsPhaseT += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            staminaMin = Mathf.Min(staminaMin, p.Motor.Stamina);
            fovMin = Mathf.Min(fovMin, p.Camera.fieldOfView);
            eyeMin = Mathf.Min(eyeMin, p.Motor.EyeHeight);
            jumpMaxY = Mathf.Max(jumpMaxY, p.Position.y);
            switch (weaponsPhase)
            {
                case 0: // sprint forward 3 s (stamina must drain at 25/s)
                    bot.ForcedMove = new Vector2(0f, 1f); bot.HasForcedMove = true; bot.Sprint = true; bot.AllowFire = false;
                    if (weaponsPhaseT > 3f) { Log("sprint stamina_after_3s=" + p.Motor.Stamina.ToString("0.0") + " speed=" + new Vector3(p.Velocity.x, 0f, p.Velocity.z).magnitude.ToString("0.00")); Next(); }
                    break;
                case 1: // crouch 1.5 s
                    bot.Sprint = false; bot.Crouch = true;
                    if (weaponsPhaseT > 1.5f) { Log("crouch capsule_h=" + p.Controller.height.ToString("0.00") + " speed=" + new Vector3(p.Velocity.x, 0f, p.Velocity.z).magnitude.ToString("0.00")); bot.Crouch = false; Next(); }
                    break;
                case 2: // jump
                    bot.HasForcedMove = false; bot.Jump = weaponsPhaseT < 0.1f;
                    if (weaponsPhaseT > 1.5f) { Log("jump apex_y=" + jumpMaxY.ToString("0.00") + " stamina_min=" + staminaMin.ToString("0.0")); Next(); }
                    break;
                case 3: // ADS 1 s
                    bot.Aim = true;
                    if (weaponsPhaseT > 1f) { Log("ads fov_min=" + fovMin.ToString("0.0")); bot.Aim = false; Next(); }
                    break;
                case 4: case 5: case 6: // fight with shotgun, rifle, pistol (7 s each)
                    bot.Weapon = 6 - weaponsPhase; bot.AllowFire = true;
                    if (weaponsPhaseT > 7f)
                    {
                        WeaponSlot sl = w.Slots[6 - weaponsPhase];
                        Log("weapon " + sl.Def.Name + " mag=" + sl.Mag + " reserve=" + sl.Reserve + " shots_total=" + Game.Stats.ShotsFired + " kills_total=" + Game.Stats.TotalKills);
                        Next();
                    }
                    break;
            }
        }

        void Next() { weaponsPhase++; weaponsPhaseT = 0f; }

        static EnemyBase Nearest(EnemyType type)
        {
            EnemyBase best = null;
            float bd = float.MaxValue;
            var list = Game.Enemies.Active;
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].IsAlive || list[i].Type != type) continue;
                float d = list[i].DistanceToPlayer;
                if (d < bd) { bd = d; best = list[i]; }
            }
            return best;
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            if (scenario == "wave1")
                Log("runner_flank samples=" + flankSamples + " max_spread_deg=" + flankSpreadMax.ToString("0") + " flank_detail_samples=" + flankDetailSamples +
                    " lined_up_pairs=" + linedUpPairs + " chasing_pairs=" + chasingPairs);
            if (scenario == "wave2")
                Log("shooter_cover chosen=" + coverTotal + " hidden_from_player=" + coverHidden + " in_12_18m=" + coverInBand);
            if (scenario == "wave4")
                Log("stuck_check max_no_progress_s=" + maxNoProgress.ToString("0.00") + " max_stall_s=" + maxStall.ToString("0.00"));
            foreach (var kv in events) Log("count " + kv.Key + "=" + kv.Value);
            Log("summary scenario=" + scenario + " t=" + t.ToString("0.0") + " kills=" + Game.Stats.TotalKills + " accuracy=" + (Game.Stats.Accuracy * 100f).ToString("0") +
                "% headshots=" + Game.Stats.Headshots + " console_warnings=" + warnings + " console_errors=" + errors);
            Application.logMessageReceived -= OnLog;
            EnemyBase.EventHook -= OnEvent;
            EnemyBase.DamageHook -= OnDamage;
            quitAt = t + 0.5f;
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            EnemyBase.EventHook -= OnEvent;
            EnemyBase.DamageHook -= OnDamage;
        }

        void Quit()
        {
            Log("quit");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
