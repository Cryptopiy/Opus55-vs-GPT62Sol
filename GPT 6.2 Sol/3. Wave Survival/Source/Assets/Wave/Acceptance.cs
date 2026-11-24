using System;
using UnityEngine;
using UnityEngine.AI;
using static WaveSurvival.GameConfig;

namespace WaveSurvival
{
    // The command-line checks drive the same systems as the player and log observed outcomes.
    internal sealed partial class Acceptance
    {
        readonly CoreGame game;
        readonly string scenario;
        float began, stageAt, nextFire, lastReport;
        int stage, shooterShots, bossShots, seenCover, slams, previousAlive;
        bool flankSeen, tankSignal, tankCharge, tankStun, phase2, phase3, summonedRunners, summonedShooters, complete;
        int invalidNav, recoveries, prolongedStalls;
        float retreatFrom, retreatFarthest, firstWarningLead;
        bool coverOccluded;
        public bool ControlsPlayer => scenario != "keys" && scenario != "ui";

        Acceptance(CoreGame game, string scenario)
        {
            this.game = game;
            this.scenario = scenario;
            began = stageAt = Time.realtimeSinceStartup;
            if (scenario == "systems") game.SetTestTime(2);
            if (!ControlsPlayer) PrepareTestInput();
            game.Player.Invulnerable = true;
            Debug.Log("WAVE_CHECK start " + scenario);
        }

        public static Acceptance TryStart(CoreGame game)
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-waveCheck");
            return at >= 0 && at + 1 < args.Length ? new Acceptance(game, args[at + 1]) : null;
        }

        public void BeforeTick()
        {
            if (complete) return;
            if (scenario == "systems") Systems();
            else if (scenario == "boss") Boss();
            else if (scenario == "ui") UiVisibility();
            else OtherScenario();
        }


        public void Shot(EnemyKind kind)
        {
            if (kind == EnemyKind.Shooter) shooterShots++;
            if (kind == EnemyKind.Warden) bossShots++;
        }

        public void Cover(Enemy enemy)
        {
            seenCover++;
            coverOccluded |= !Arena.Clear(game.Player.Eyes, enemy.CoverPoint + Vector3.up * 1.02f);
        }
        public void Slam() => slams++;

        void Systems()
        {
            float elapsed = Time.realtimeSinceStartup - stageAt;
            switch (stage)
            {
                case 0:
                    firstWarningLead = Mathf.Max(firstWarningLead, game.Waves.TimeUntilSpawn);
                    if (elapsed < 4)
                    {
                        int left = 0, right = 0;
                        foreach (Enemy e in game.Enemies)
                        {
                            if (e == null || !e.Alive || !e.HasGoal) continue;
                            if (e.Goal.x < game.Player.Position.x - .5f) left++;
                            if (e.Goal.x > game.Player.Position.x + .5f) right++;
                        }
                        flankSeen |= left > 0 && right > 0;
                        return;
                    }
                    if (game.Stats.Kills[0] == RunnerBase + RunnerGrowth)
                    {
                        Debug.Log($"WAVE_CHECK wave1 pistolKills={game.Stats.Kills[0]} spread={flankSeen} headshots={game.Stats.Headshots} break={game.Waves.IsBreak} countdown={game.Waves.Countdown:F2} rifleReserve={game.Guns.ReserveFor(1)} shells={game.Guns.ReserveFor(2)} gateLead={firstWarningLead:F2}");
                        Next();
                    }
                    else ShootFirst(EnemyKind.Runner);
                    return;
                case 1:
                    if (game.Waves.Number != 2 || game.Waves.IsBreak) return;
                    game.Player.Place(new Vector3(7, .02f, 8), Vector3.forward);
                    Next();
                    return;
                case 2:
                    if (elapsed < 13 && shooterShots < ShooterBurst) return;
                    Enemy shooter = Find(EnemyKind.Shooter);
                    bool retreat = false;
                    if (shooter != null && Arena.Sample(shooter.Position + shooter.Root.right * 3.4f, out Vector3 close, 2))
                    {
                        game.Player.Place(close + Vector3.up * .03f, shooter.Position - close);
                        retreatFrom = retreatFarthest = Vector3.Distance(game.Player.Position, shooter.Position);
                        retreat = retreatFrom < RetreatRange;
                    }
                    Debug.Log($"WAVE_CHECK wave2 coverChoices={seenCover} occluded={coverOccluded} shooterProjectiles={shooterShots} nearForRetreat={retreat}");
                    Next();
                    return;
                case 3:
                    Enemy retreating = Find(EnemyKind.Shooter);
                    if (retreating != null) retreatFarthest = Mathf.Max(retreatFarthest, Vector3.Distance(game.Player.Position, retreating.Position));
                    if (elapsed < .8f) return;
                    Debug.Log($"WAVE_CHECK retreat state={retreating?.State} goal={retreating?.HasGoal} distanceFrom={retreatFrom:F2} distanceTo={retreatFarthest:F2}");
                    game.ClearEnemies();
                    game.Player.Place(new Vector3(7, .02f, 8), Vector3.forward);
                    game.Waves.Begin(3);
                    Next();
                    return;
                case 4:
                    Enemy tank = Find(EnemyKind.Tank);
                    if (tank == null) return;
                    Vector3 towardCenter = Vector3.ProjectOnPlane(-tank.Position, Vector3.up).normalized;
                    if (Arena.Sample(tank.Position + towardCenter * 8, out Vector3 approach, 2))
                        game.Player.Place(approach + Vector3.up * .03f, tank.Position - approach);
                    Debug.Log($"WAVE_CHECK tankArmor front={tank.Armor(tank.Root.forward)} side={tank.Armor(tank.Root.right)} rear={tank.Armor(-tank.Root.forward)}");
                    Next();
                    return;
                case 5:
                    Enemy liveTank = Find(EnemyKind.Tank);
                    if (liveTank != null)
                    {
                        tankSignal |= liveTank.State == EnemyState.Attack;
                        tankCharge |= liveTank.State == EnemyState.Charge;
                        tankStun |= liveTank.State == EnemyState.Stunned;
                    }
                    if (elapsed < 6 && !tankStun) return;
                    Debug.Log($"WAVE_CHECK tank warning={tankSignal} charge={tankCharge} stunned={tankStun}");
                    game.ClearEnemies();
                    game.Player.Place(new Vector3(7, .02f, 8), Vector3.forward);
                    game.Waves.Begin(4);
                    game.DebugAI = true;
                    Next();
                    return;
                case 6:
                    TrackAgents();
                    if (game.Waves.Number == 4 && (game.Waves.Remaining > game.Alive || elapsed < 8)) return;
                    if (game.Waves.Number == 4)
                    {
                        ShootFirst(null);
                        return;
                    }
                    Debug.Log($"WAVE_CHECK wave4 debug={game.DebugAI} invalidNav={invalidNav} recoveries={recoveries} prolongedStalls={prolongedStalls} remaining={game.Waves.Remaining}");
                    game.ClearEnemies();
                    game.DebugAI = false;
                    game.Waves.Begin(7);
                    Next();
                    return;
                case 7:
                    if (game.Alive != AliveCap) return;
                    Debug.Log($"WAVE_CHECK wave7 population={game.Alive} queue={game.Waves.Remaining - game.Alive}");
                    int removed = 0;
                    foreach (Enemy enemy in game.Enemies)
                    {
                        if (enemy == null || !enemy.Alive) continue;
                        enemy.Hurt(enemy.HP * 4, game.Player.Position, false);
                        if (++removed == GroupMin) break;
                    }
                    Next();
                    return;
                case 8:
                    if (game.Alive != AliveCap && elapsed < 4) return;
                    Debug.Log($"WAVE_CHECK wave7 refill={game.Alive} remainingQueue={game.Waves.Remaining - game.Alive}");
                    Finish();
                    return;
            }
        }

        void Boss()
        {
            Enemy warden = game.Waves.Boss;
            float elapsed = Time.realtimeSinceStartup - stageAt;
            if (stage == 0)
            {
                game.SkipToBoss();
                game.SetTestTime(2);
                Next();
                return;
            }
            if (warden == null) return;
            if (stage == 1)
            {
                if (elapsed < 1.5f)
                {
                    Vector3 inward = Vector3.ProjectOnPlane(-warden.Position, Vector3.up).normalized;
                    if (Arena.Sample(warden.Position + inward * 11, out Vector3 near, 3))
                        game.Player.Place(near + Vector3.up * .03f, warden.Position - near);
                }
                summonedRunners |= Find(EnemyKind.Runner) != null;
                if (elapsed < 11 && !summonedRunners) return;
                Debug.Log($"WAVE_CHECK warden p1 HP={warden.HP} speed={warden.Agent.speed} shots={bossShots} runners={summonedRunners}");
                warden.Hurt(warden.Maximum * (1 - BossPhase2) + 1, game.Player.Position, false);
                Next();
                return;
            }
            if (stage == 2)
            {
                phase2 |= warden.Phase == 2;
                Vector3 direction = warden.Position - game.Player.Eyes;
                if (slams == 0 && elapsed < 8)
                {
                    if (!Arena.Clear(game.Player.Eyes, warden.Eyes))
                    {
                        Vector3 away = Vector3.ProjectOnPlane(-warden.Position, Vector3.up).normalized;
                        if (Arena.Sample(warden.Position + away * 8, out Vector3 near, 2))
                            game.Player.Place(near + Vector3.up * .03f, direction);
                    }
                    return;
                }
                Debug.Log($"WAVE_CHECK warden p2 HP={warden.HP} speed={warden.Agent.speed} phase={phase2} slams={slams}");
                warden.Hurt(warden.HP - warden.Maximum * BossPhase3 + 1, game.Player.Position, false);
                previousAlive = bossShots;
                Next();
                return;
            }
            if (stage == 3)
            {
                phase3 |= warden.Phase == 3;
                summonedShooters |= Find(EnemyKind.Shooter) != null;
                if (elapsed < 15 && !summonedShooters) return;
                Debug.Log($"WAVE_CHECK warden p3 HP={warden.HP} speed={warden.Agent.speed} phase={phase3} shots={bossShots - previousAlive} shooters={summonedShooters}");
                warden.Hurt(warden.HP + 1, game.Player.Position, false);
                Debug.Log($"WAVE_CHECK victory={game.State} wardenKills={game.Stats.Kills[(int)EnemyKind.Warden]}");
                Next();
                return;
            }
            if (stage == 4 && elapsed >= 1)
            {
                game.StartEndless();
                Debug.Log($"WAVE_CHECK endless={game.Endless} wave={game.Waves.Number} countdown={game.Waves.Countdown:F1}");
                Finish();
            }
        }

        void TrackAgents()
        {
            if (Time.realtimeSinceStartup < lastReport + .25f) return;
            lastReport = Time.realtimeSinceStartup;
            int recovered = 0;
            foreach (Enemy enemy in game.Enemies)
            {
                if (enemy == null || !enemy.Alive) continue;
                if (!enemy.Agent.isOnNavMesh) invalidNav++;
                if (enemy.StationaryAge > StuckTime + .1f) prolongedStalls++;
                recovered += enemy.RecoveryCount;
            }
            recoveries = Mathf.Max(recoveries, recovered);
        }

        Enemy Find(EnemyKind kind)
        {
            foreach (Enemy enemy in game.Enemies)
                if (enemy != null && enemy.Alive && enemy.Kind == kind) return enemy;
            return null;
        }

        void ShootFirst(EnemyKind? kind)
        {
            if (Time.time < nextFire) return;
            Enemy selected = null;
            foreach (Enemy enemy in game.Enemies)
                if (enemy != null && enemy.Alive && (!kind.HasValue || kind.Value == enemy.Kind))
                { selected = enemy; break; }
            if (selected == null) return;
            Vector3 head = selected.Eyes;
            for (int bearing = 0; bearing < 12; bearing++)
            {
                Vector3 offset = Quaternion.Euler(0, bearing * 30, 0) * -selected.Root.forward * 4;
                if (!Arena.Sample(selected.Position + offset, out Vector3 point, 1) ||
                    !Arena.Clear(point + Vector3.up * 1.64f, head)) continue;
                game.Player.Place(point + Vector3.up * .02f, head - (point + Vector3.up * 1.64f));
                break;
            }
            Physics.SyncTransforms();
            bool reload = game.Guns.Magazine == 0;
            game.Guns.Tick(Time.deltaTime, !reload, !reload, reload);
            nextFire = Time.time + (reload ? GameConfig.Weapons[0].Reload : GameConfig.Weapons[0].Interval) + .06f;
        }

        void Next()
        {
            stage++;
            stageAt = Time.realtimeSinceStartup;
        }

        void Finish()
        {
            complete = true;
            Debug.Log($"WAVE_CHECK finished {scenario} seconds={Time.realtimeSinceStartup - began:F1}");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }

        partial void OtherScenario();
    }
}




