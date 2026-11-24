using System.Collections;
using UnityEngine;

namespace DestructionSandbox
{
    /// <summary>Performance (D-26), soak (5 minutes of play, console must stay clean) and screenshot scenarios.</summary>
    public sealed partial class AutoTest
    {
        private IEnumerator Collapse(float measureSeconds)
        {
            Game.Controller.SelectStructure(0);
            yield return WaitGame(2f);
            Game.Shards.ResetStats();
            // Bring the whole tower down: shells into the base and the middle, cannonballs into the rest.
            yield return FireAt(WeaponType.Shell, new Vector3(0f, 1.0f, -2f));
            yield return FireAt(WeaponType.Cannonball, new Vector3(-1f, 2.5f, -2f));
            yield return FireAt(WeaponType.Cannonball, new Vector3(1f, 4f, -2f));
            yield return FireAt(WeaponType.Shell, new Vector3(0f, 6f, -2f));
            yield return FireAt(WeaponType.Cannonball, new Vector3(0f, 8f, -2f));
            yield return FireAt(WeaponType.Shell, new Vector3(0f, 3f, -2f));

            int frames = 0;
            float sum = 0f, worst = 0f;
            int over20 = 0;
            int spikesLogged = 0;
            var frameTimes = new System.Collections.Generic.List<float>(4096);
            float end = Time.realtimeSinceStartup + measureSeconds;
            float endGame = Time.time + measureSeconds;
            bool shotTaken = false;
            float summaryAt = Time.time + 0.5f;
            double physSum = 0; int stepSum = 0;
            while (headless ? Time.time < endGame : Time.realtimeSinceStartup < end)
            {
                yield return null;
                SampleBodies();
                float ms = Time.unscaledDeltaTime * 1000f;
                physSum += PerfProbe.LastFramePhysicsMs;
                stepSum += PerfProbe.LastFrameSteps;
                if (Time.time >= summaryAt)
                {
                    summaryAt += 0.5f;
                    Line("  t=" + Time.time.ToString("0.0") + " physics " + (physSum / Mathf.Max(1, stepSum)).ToString("0.0") + " ms/step, shards " +
                         Game.Shards.ActiveCount + " (" + Game.Shards.CountAwake() + " awake), blocks " + Game.Structures.ActiveBlockCount);
                    physSum = 0; stepSum = 0;
                }
                frames++;
                frameTimes.Add(ms);
                sum += ms;
                worst = Mathf.Max(worst, ms);
                if (ms > 20f) over20++;
                if (ms > 40f && spikesLogged < 25)
                {
                    spikesLogged++;
                    Line("  spike " + ms.ToString("0.0") + " ms at t=" + Time.time.ToString("0.00") + ": physics " + PerfProbe.LastFramePhysicsMs.ToString("0.0") +
                         " ms in " + PerfProbe.LastFrameSteps + " steps, fracture " + PerfProbe.LastFrameFractureMs.ToString("0.0") + " ms, breaks " +
                         PerfProbe.LastFrameBreaks + ", spawns " + PerfProbe.LastFrameSpawns + ", shards " + Game.Shards.ActiveCount + " (" + Game.Shards.CountAwake() + " awake)");
                }
                if (!shotTaken && Game.Shards.ActiveCount > 1000) { shotTaken = true; Screenshot("collapse_peak"); }
            }
            float avgMs = sum / Mathf.Max(1, frames);
            frameTimes.Sort();
            float p99 = frameTimes.Count > 0 ? frameTimes[Mathf.Min(frameTimes.Count - 1, (int)(frameTimes.Count * 0.99f))] : 0f;
            float fps = 1000f / Mathf.Max(avgMs, 0.001f);
            Line("Collapse: destroyed " + Game.Structures.DestroyedBlocks + "/" + Game.Structures.TotalBlocks + ", shards peak " + Game.Shards.PeakActive +
                 ", oldest-recycled " + Game.Shards.RecycledOldest + ", blocks fractured " + Game.Fracture.BlocksBroken + ", shards fractured " + Game.Fracture.ShardsBroken);
            Check("D-13 shard budget", Game.Shards.PeakActive <= GameConfig.Fracture.ShardBudget, "peak active shards " + Game.Shards.PeakActive + " (budget 1500)");
            if (!headless)
            {
                Check("D-26 60 FPS during collapse", fps >= 58f,
                    "avg " + fps.ToString("0.0") + " FPS (" + avgMs.ToString("0.00") + " ms) over " + frames + " frames, worst " + worst.ToString("0.0") +
                    " ms, 99th percentile " + p99.ToString("0.0") + " ms, frames > 20 ms: " + over20 + ", resolution " + Screen.width + "x" + Screen.height + ", vSync " + QualitySettings.vSyncCount);
            }
            yield return Shot("collapse_end");
            Line("Overlay snapshot:\n" + Game.Overlay.BuildText());
        }

        /// <summary>
        /// Five minutes of scripted "normal play": every structure, every weapon, slow motion, resets and
        /// clears; afterwards an idle period proves that settled shards shrink back into the pool (D-13) and
        /// the GameObject count proves nothing was instantiated or destroyed during play (D-14).
        /// </summary>
        private IEnumerator Soak(float minutes)
        {
            int objectsAtStart = GameObjectCount();
            float end = Time.time + minutes * 60f;
            int round = 0;
            var rng = new System.Random((int)ArgFloat("-seed", 7f));
            Line("Soak: " + minutes + " min of play, seed " + ArgFloat("-seed", 7f));
            while (Time.time < end)
            {
                int structure = round % 3;
                Game.Controller.SelectStructure(structure);
                yield return WaitGame(1f);
                for (int shot = 0; shot < 6 && Time.time < end; shot++)
                {
                    var type = (WeaponType)(rng.Next(0, 10) < 6 ? 0 : rng.Next(1, 3));
                    var target = new Vector3((float)rng.NextDouble() * 6f - 3f, (float)rng.NextDouble() * 7f + 0.3f, -1f);
                    yield return FireAt(type, target);
                    if (rng.Next(0, 8) == 0) Game.SlowMo.Toggle();
                    yield return WaitGame(0.4f);
                }
                if (Game.SlowMo.Active) Game.SlowMo.Toggle();
                yield return WaitGame(4f);
                if (round % 4 == 3) Game.Controller.ClearDebris();
                if (round % 5 == 4) Game.Controller.ResetStructure();
                if (Game.Weapons.ShellAmmo == 0) Game.Weapons.Refill();
                round++;
            }
            Line("Soak: " + round + " rounds, shots fired " + Game.Weapons.ShotsFired + ", explosions " + Game.Explosions.ExplosionCount +
                 ", orb pulses " + Game.Orbs.PulsesFired + ", shards peak " + Game.Shards.PeakActive + ", voices stolen " + Game.Audio.StolenVoices + ", sounds dropped " + Game.Audio.DroppedSounds);

            int shardsBeforeIdle = Game.Shards.ActiveCount;
            yield return WaitGame(GameConfig.Fracture.ShardSettleTime + GameConfig.Fracture.ShardShrinkTime + 8f);
            Check("D-13 settle and shrink", Game.Shards.ActiveCount < shardsBeforeIdle || shardsBeforeIdle == 0,
                "active shards " + shardsBeforeIdle + " -> " + Game.Shards.ActiveCount + " after " + (GameConfig.Fracture.ShardSettleTime + GameConfig.Fracture.ShardShrinkTime + 8f) + " s idle");
            int objectsEnd = GameObjectCount();
            Check("D-14 no instantiate/destroy", objectsEnd == objectsAtStart, "GameObjects at start " + objectsAtStart + ", at end " + objectsEnd);
            Line("Overlay snapshot:\n" + Game.Overlay.BuildText());
        }

        /// <summary>Screenshots of every structure and effect for visual review.</summary>
        private IEnumerator Showcase()
        {
            Game.Overlay.Toggle();
            for (int s = 0; s < 3; s++)
            {
                Game.Controller.SelectStructure(s);
                Game.Player.LookAt(GameConfig.Player.SpawnLookAt);
                yield return WaitGame(1.5f);
                yield return Shot("structure_" + StructureNames[s]);
            }
            Game.Overlay.Toggle();
            Game.Controller.SelectStructure(0);
            yield return WaitGame(1f);
            yield return FireAt(WeaponType.Cannonball, new Vector3(0f, 10.5f, -2f));
            yield return WaitGame(0.35f);
            Screenshot("glass_hit");
            yield return WaitGame(1.5f);
            yield return FireAt(WeaponType.Shell, new Vector3(0.5f, 5f, -2f));
            float t = Time.time;
            while (Game.Explosions.ExplosionCount == 0 && Time.time - t < 3f) yield return null;
            yield return WaitGame(0.08f);
            Screenshot("explosion");
            yield return WaitGame(0.6f);
            Screenshot("explosion_fire_smoke");
            yield return WaitGame(4f);
            yield return Shot("debris");
            Game.Controller.SelectStructure(1);
            yield return WaitGame(1f);
            yield return FireAt(WeaponType.Orb, new Vector3(-6f, 3.8f, -0.25f));
            yield return WaitGame(2.5f);
            Screenshot("orb_active");
            yield return WaitGame(2.2f);
            yield return Shot("orb_after_pulse");
            Game.Pause.Open();
            yield return null;
            yield return Shot("pause_menu");
            Game.Pause.Close();
        }
    }
}
