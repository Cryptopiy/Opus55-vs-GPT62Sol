using System.Collections;
using UnityEngine;
using Wpn = DestructionSandbox.GameConfig.Weapons;
using Ph = DestructionSandbox.GameConfig.Physics;

namespace DestructionSandbox
{
    /// <summary>The scripted scenarios of the self-test (acceptance script of the specification, section 8).</summary>
    public sealed partial class AutoTest
    {
        private static readonly string[] StructureNames = { "Tower", "Wall", "Pyramid" };

        // ------------------------------------------------------------------ D-08 stability

        private IEnumerator Stability(float watch)
        {
            for (int s = 0; s < 3; s++) yield return StabilityOf(s, watch);
        }

        private IEnumerator StabilityOf(int index, float watch)
        {
            Game.Controller.SelectStructure(index);
            float t0 = Time.time;
            int total = Game.Structures.TotalBlocks;
            yield return WaitGame(Ph.SettleCheckTime);
            Game.Structures.Measure(out int awake3, out float speed3, out float disp3);
            Check("D-08 settle " + StructureNames[index], speed3 < Ph.SettleSpeedLimit,
                "after 3 s: max block speed " + speed3.ToString("0.0000") + " m/s, awake " + awake3 + "/" + total + ", max drift " + (disp3 * 1000f).ToString("0.0") + " mm");

            float maxDisp = disp3;
            float maxSpeedAfter = 0f;
            while (Time.time - t0 < watch)
            {
                yield return WaitGame(1f);
                Game.Structures.Measure(out _, out float sp, out float d);
                maxDisp = Mathf.Max(maxDisp, d);
                maxSpeedAfter = Mathf.Max(maxSpeedAfter, sp);
            }
            Game.Structures.Measure(out int awakeEnd, out _, out float dispEnd);
            bool stands = Game.Structures.FracturedBlocks == 0 && Game.Structures.DestroyedBlocks == 0 && maxDisp < 0.02f && awakeEnd == 0;
            Check("D-08 stand " + StructureNames[index], stands,
                (Time.time - t0).ToString("0") + " s untouched: blocks " + Game.Structures.ActiveBlockCount + "/" + total + ", broken " + Game.Structures.FracturedBlocks +
                ", max drift " + (maxDisp * 1000f).ToString("0.0") + " mm, max speed after 3 s " + maxSpeedAfter.ToString("0.0000") + " m/s, awake at end " + awakeEnd);
        }

        // ------------------------------------------------------------------ acceptance script (spec section 8)

        private IEnumerator Acceptance(float watch)
        {
            Line("Block counts: Tower " + Game.Structures.Layout(0).Pieces.Length + ", Wall " + Game.Structures.Layout(1).Pieces.Length +
                 ", Pyramid " + Game.Structures.Layout(2).Pieces.Length);
            int objectsAtStart = GameObjectCount();

            // 1. Tower stands untouched.
            yield return StabilityOf(0, watch);
            Screenshot("1_tower_standing");

            // 2. Five cannonballs at the base of the tower.
            Game.Fracture.ResetStats();
            for (int i = 0; i < 5; i++) yield return FireAt(WeaponType.Cannonball, new Vector3(-1.2f + i * 0.6f, 0.6f, -2f));
            yield return WaitGame(1.0f);
            Screenshot("2_cannon_impact");
            yield return WaitGame(7f);
            int[] byMat = Game.Fracture.BrokenByMaterial;
            float pct = 100f * Game.Structures.DestroyedBlocks / Game.Structures.TotalBlocks;
            Check("ACC-2 cannonballs collapse tower", pct >= 40f && byMat[(int)BlockMaterialType.Glass] > 0 && byMat[(int)BlockMaterialType.Wood] > 0,
                "destroyed " + Game.Structures.DestroyedBlocks + "/" + Game.Structures.TotalBlocks + " (" + pct.ToString("0") + "%), fractured stone " + byMat[0] +
                ", wood " + byMat[1] + ", glass " + byMat[2] + ", shards active " + Game.Shards.ActiveCount + " (peak " + Game.Shards.PeakActive + ")");
            Check("D-10 thresholds", Game.Fracture.MinBreakImpulseRatio >= 1f,
                "lowest impulse/threshold ratio among breaks = " + Game.Fracture.MinBreakImpulseRatio.ToString("0.00") + " (must be > 1)");
            yield return Shot("2_tower_collapsed");

            // 3. Reset, one explosive shell at the middle floors.
            Game.Controller.ResetStructure();
            yield return WaitGame(1f);
            int explosionsBefore = Game.Explosions.ExplosionCount;
            yield return FireAt(WeaponType.Shell, new Vector3(0f, 6.2f, -2f));
            float worstMs = 0f;
            float endReal = Time.realtimeSinceStartup + 3f;
            bool shot = false;
            float endGame = Time.time + 3f;
            while (headless ? Time.time < endGame : Time.realtimeSinceStartup < endReal)
            {
                yield return null;
                SampleBodies();
                worstMs = Mathf.Max(worstMs, Time.unscaledDeltaTime * 1000f);
                if (!shot && Game.Explosions.ExplosionCount > explosionsBefore)
                {
                    shot = true;
                    Screenshot("3_explosion_shockwave");
                }
            }
            int displaced = 0;
            foreach (Block b in Game.Structures.ActiveBlocks) if ((b.Body.position - b.StartPosition).magnitude > 3f) displaced++;
            Check("ACC-3 explosive shell", Game.Explosions.ExplosionCount == explosionsBefore + 1 && Game.Explosions.LastBodiesHit > 20 && (Game.Structures.DestroyedBlocks > 10),
                "explosions " + (Game.Explosions.ExplosionCount - explosionsBefore) + ", bodies pushed " + Game.Explosions.LastBodiesHit + ", pieces shattered by the blast " +
                Game.Explosions.LastBlastBreaks + ", destroyed " + Game.Structures.DestroyedBlocks +
                ", blocks displaced >3 m " + displaced + ", scorch decals " + Game.Effects.ActiveScorchDecals());
            if (!headless) Check("ACC-3 no stutter", worstMs < 100f, "worst frame in the 3 s after the shot: " + worstMs.ToString("0.0") + " ms");
            yield return Shot("3_after_explosion");

            // 4. Reset, Wall (F6), gravity orb at the wall: pulled in, then pushed out.
            Game.Controller.SelectStructure(1);
            yield return WaitGame(1.5f);
            int pulsesBefore = Game.Orbs.PulsesFired;
            yield return FireAt(WeaponType.Orb, new Vector3(-5.5f, 5.2f, -0.25f));
            float waitStart = Time.time;
            while (Game.Orbs.ActiveCount == 0 && Time.time - waitStart < 4f) yield return null;
            Vector3 center = Game.Orbs.LastCenter;
            TrackStart(center);
            yield return WaitGame(3.5f);
            int pulledIn = TrackPulled(center, 0.5f, out int brokenByPull);
            int fieldBodies = Game.Orbs.LastPulledBodies;
            Screenshot("4_orb_pulling");
            while (Game.Orbs.PulsesFired == pulsesBefore && Time.time - waitStart < 8f) yield return null;
            yield return WaitGame(0.15f);
            float outward = RadialVelocity(center, out int nearOut);
            yield return WaitGame(1.0f);
            int pushedOut = TrackPushed(center, 1.0f, out int shatteredPulled);
            Check("ACC-4 gravity orb", pulledIn + brokenByPull >= 5 && pushedOut + shatteredPulled >= pulledIn / 2 && outward > 0.5f,
                "orb at " + center.ToString("F1") + ": " + fieldBodies + " bodies in the 8 m field, " + pulledIn + " blocks pulled >= 0.5 m toward it and " +
                brokenByPull + " torn out and shattered during 3.5 s, " +
                pushedOut + " of them thrown >= 1 m back out after the pulse (" + shatteredPulled + " others shattered); mean radial velocity right after the pulse " + outward.ToString("0.0") + " m/s over " + nearOut + " bodies");
            Screenshot("4_orb_pulse");
            yield return WaitGame(2f);

            // 5. Pyramid (F7), slow motion (T), one cannonball.
            Game.Controller.SelectStructure(2);
            yield return WaitGame(1f);
            Game.SlowMo.Toggle();
            bool slowOk = Mathf.Approximately(Time.timeScale, GameConfig.Timing.SlowMotionTimeScale) &&
                          Mathf.Abs(Time.fixedDeltaTime - GameConfig.Timing.FixedTimestep * GameConfig.Timing.SlowMotionTimeScale) < 1e-6f;
            Game.Fracture.ResetStats();
            yield return FireAt(WeaponType.Cannonball, new Vector3(0f, 2.5f, -4f));
            yield return WaitGame(0.6f);
            Screenshot("5_pyramid_slowmo");
            yield return WaitGame(2.5f);
            Check("ACC-5 pyramid slow motion", slowOk && Game.Fracture.BlocksBroken > 0,
                "timeScale " + Time.timeScale + ", fixedDeltaTime " + (Time.fixedDeltaTime * 1000f).ToString("0.000") + " ms, blocks fractured " + Game.Fracture.BlocksBroken +
                ", destroyed " + Game.Structures.DestroyedBlocks);
            Game.SlowMo.Toggle();
            yield return WaitGame(1f);

            // 6. R twenty times: counts stay identical (D-28).
            yield return Resets(objectsAtStart);
            yield return Movement();
            yield return UiToggle();                                        // U-01..U-06 (F8 hide-all-UI switch)
        }

        /// <summary>D-15: walk and sprint speed, jump height, and the structure blocks the player.</summary>
        private IEnumerator Movement()
        {
            PlayerController p = Game.Player;
            p.ExternallyDriven = true;
            Game.Controller.SelectStructure(0);
            float dt = GameConfig.Timing.FixedTimestep;

            p.Teleport(new Vector3(-20f, 0f, -30f));
            p.LookAt(new Vector3(-20f, 1.8f, 10f));
            for (int i = 0; i < 10; i++) { p.Step(Vector2.zero, false, false, dt); yield return null; }   // settle on the ground
            float walk = 0f, sprint = 0f, apex = 0f;
            Vector3 a = p.transform.position;
            for (int i = 0; i < 60; i++) { p.Step(new Vector2(0f, 1f), false, false, dt); yield return null; }
            walk = (p.transform.position - a).magnitude;
            a = p.transform.position;
            for (int i = 0; i < 60; i++) { p.Step(new Vector2(0f, 1f), true, false, dt); yield return null; }
            sprint = (p.transform.position - a).magnitude;
            float groundY = p.transform.position.y;
            p.Step(Vector2.zero, false, true, dt);
            for (int i = 0; i < 90; i++) { apex = Mathf.Max(apex, p.transform.position.y - groundY); p.Step(Vector2.zero, false, false, dt); yield return null; }

            // Walk straight into the tower face (z = -2) for 2 s: the capsule must stop in front of it.
            p.Teleport(new Vector3(0f, 0f, -5f));
            p.LookAt(new Vector3(0f, 1.8f, 0f));
            for (int i = 0; i < 120; i++) { p.Step(new Vector2(0f, 1f), false, false, dt); yield return null; }
            float frontZ = p.transform.position.z;
            p.ExternallyDriven = false;
            p.Spawn();

            Check("D-15 movement", Mathf.Abs(walk - GameConfig.Player.WalkSpeed) < 0.3f && Mathf.Abs(sprint - GameConfig.Player.SprintSpeed) < 0.5f &&
                                   Mathf.Abs(apex - GameConfig.Player.JumpHeight) < 0.12f && frontZ < -2f - GameConfig.Player.ControllerRadius + 0.1f,
                "walked " + walk.ToString("0.00") + " m in 1 s, sprinted " + sprint.ToString("0.00") + " m in 1 s, jump apex " + apex.ToString("0.00") +
                " m, walking into the tower stopped at z = " + frontZ.ToString("0.00") + " (tower face at z = -2)");
        }

        // Per-body distance tracking for the gravity orb check (test only, allocations are fine here).
        private readonly System.Collections.Generic.List<Rigidbody> tracked = new System.Collections.Generic.List<Rigidbody>();
        private readonly System.Collections.Generic.List<float> trackedStart = new System.Collections.Generic.List<float>();
        private readonly System.Collections.Generic.List<float> trackedPull = new System.Collections.Generic.List<float>();

        private void TrackStart(Vector3 center)
        {
            tracked.Clear(); trackedStart.Clear(); trackedPull.Clear();
            float r2 = Wpn.OrbFieldRadius * Wpn.OrbFieldRadius;
            foreach (Block b in Game.Structures.ActiveBlocks)
            {
                float d2 = (b.Body.position - center).sqrMagnitude;
                if (d2 < r2) { tracked.Add(b.Body); trackedStart.Add(Mathf.Sqrt(d2)); trackedPull.Add(float.NaN); }
            }
        }

        private int TrackPulled(Vector3 center, float minimum, out int brokenByPull)
        {
            int n = 0;
            brokenByPull = 0;
            for (int i = 0; i < tracked.Count; i++)
            {
                if (!tracked[i].gameObject.activeInHierarchy) { brokenByPull++; continue; }
                float d = (tracked[i].position - center).magnitude;
                if (trackedStart[i] - d >= minimum) { trackedPull[i] = d; n++; }
            }
            return n;
        }

        private int TrackPushed(Vector3 center, float minimum, out int shattered)
        {
            int n = 0;
            shattered = 0;
            for (int i = 0; i < tracked.Count; i++)
            {
                if (float.IsNaN(trackedPull[i])) continue;
                if (!tracked[i].gameObject.activeInHierarchy) { shattered++; continue; }   // broke apart: its shards flew instead
                if ((tracked[i].position - center).magnitude - trackedPull[i] >= minimum) n++;
            }
            return n;
        }

        private IEnumerator MeasureFrames(int frames, System.Action<float> result)
        {
            float sum = 0f;
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                sum += Time.unscaledDeltaTime;
            }
            result(sum / frames * 1000f);
        }

        private int CountNear(Vector3 center, float radius)
        {
            int n = 0;
            float r2 = radius * radius;
            var blocks = Game.Structures.ActiveBlocks;
            for (int i = 0; i < blocks.Count; i++) if ((blocks[i].Body.position - center).sqrMagnitude < r2) n++;
            for (Shard s = Game.Shards.Head; s != null; s = s.Next) if (!s.Shrinking && (s.Body.position - center).sqrMagnitude < r2) n++;
            return n;
        }

        private float RadialVelocity(Vector3 center, out int count)
        {
            float sum = 0f;
            count = 0;
            var blocks = Game.Structures.ActiveBlocks;
            for (int i = 0; i < blocks.Count; i++) Accumulate(blocks[i].Body, center, ref sum, ref count);
            for (Shard s = Game.Shards.Head; s != null; s = s.Next) if (!s.Shrinking) Accumulate(s.Body, center, ref sum, ref count);
            return count > 0 ? sum / count : 0f;
        }

        private static void Accumulate(Rigidbody rb, Vector3 center, ref float sum, ref int count)
        {
            if (rb.IsSleeping()) return;
            Vector3 d = rb.position - center;
            float dist = d.magnitude;
            if (dist > Wpn.OrbFieldRadius || dist < 0.2f) return;
            sum += Vector3.Dot(rb.linearVelocity, d / dist);
            count++;
        }

        private IEnumerator Resets(int objectsAtStart)
        {
            Game.Controller.ResetStructure();
            yield return WaitGame(2f);
            float msBefore = 0f;
            yield return MeasureFrames(120, v => msBefore = v);
            float fpsBefore = Game.Overlay.LastFps;
            int blocksBefore = Game.Structures.ActiveBlockCount, freeBefore = Game.Structures.FreeCount, shardsFreeBefore = Game.Shards.FreeCount;
            int objectsBefore = GameObjectCount();
            long heapBefore = System.GC.GetTotalMemory(true);
            for (int i = 0; i < 20; i++)
            {
                Game.Controller.ResetStructure();
                yield return null;
                yield return null;
            }
            yield return WaitGame(2f);
            float msAfter = 0f;
            yield return MeasureFrames(120, v => msAfter = v);
            float fpsAfter = Game.Overlay.LastFps;
            int objectsAfter = GameObjectCount();
            long heapAfter = System.GC.GetTotalMemory(true);
            bool same = Game.Structures.ActiveBlockCount == blocksBefore && Game.Structures.FreeCount == freeBefore &&
                        Game.Shards.FreeCount == shardsFreeBefore && objectsAfter == objectsBefore && objectsBefore == objectsAtStart;
            Check("D-28 / ACC-6 twenty resets", same && msAfter <= msBefore * 1.15f + 0.3f,
                "blocks " + blocksBefore + "->" + Game.Structures.ActiveBlockCount + ", block pool free " + freeBefore + "->" + Game.Structures.FreeCount +
                ", shard pool free " + shardsFreeBefore + "->" + Game.Shards.FreeCount + ", GameObjects " + objectsAtStart + "/" + objectsBefore + "->" + objectsAfter +
                ", managed heap " + (heapBefore / 1048576f).ToString("0.0") + "->" + (heapAfter / 1048576f).ToString("0.0") + " MB, FPS " +
                fpsBefore.ToString("0") + "->" + fpsAfter.ToString("0") + ", avg frame " + msBefore.ToString("0.00") + "->" + msAfter.ToString("0.00") +
                " ms, rebuilds " + Game.Structures.BuildCount);
            Line("Overlay snapshot:\n" + Game.Overlay.BuildText());
        }
    }
}
