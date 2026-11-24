using UnityEngine;
using UnityEngine.AI;
using static WaveSurvival.GameConfig;

namespace WaveSurvival
{
    internal sealed partial class Enemy
    {
        void RunBrain(float dt)
        {
            Vector3 delta = game.Player.Position - Position;
            float distance = delta.magnitude;
            Face(delta, dt);
            if (State == EnemyState.Attack)
            {
                if (Time.time < until) return;
                if (distance <= RunnerRange && CanSeePlayer()) game.DamagePlayer(RunnerDamage, Position);
                Change(EnemyState.Chase);
            }
            if (distance <= RunnerRange && Time.time >= nextAttack && CanSeePlayer())
            {
                Stop();
                Change(EnemyState.Attack, RunnerWindup);
                nextAttack = Time.time + RunnerInterval;
                game.EnemyVoice(this);
                return;
            }
            Change(EnemyState.Chase);
            if (distance < RunnerRange * .79f && CanSeePlayer())
            {
                Stop();
                return;
            }
            if (Time.time < moveAt) return;
            Vector3 separation = Vector3.zero;
            int neighbors = 0;
            foreach (Enemy other in game.Enemies)
            {
                if (other == this || other == null || !other.Alive || other.Kind != EnemyKind.Runner) continue;
                Vector3 apart = Position - other.Position;
                float sqr = apart.sqrMagnitude;
                if (sqr >= 18 || sqr <= .002f) continue;
                neighbors++;
                separation += apart.normalized * Mathf.Lerp(0, 3, 1 - sqr / 18);
            }
            Vector3 side = Vector3.Cross(Vector3.up, delta.normalized);
            float shift = neighbors > 0 && distance > RunnerRange ? flank * Mathf.Min(distance * .42f, 4) : 0;
            Vector3 goal = game.Player.Position + side * shift + Vector3.ClampMagnitude(separation, 3);
            if (distance < 2.3f) goal = game.Player.Position;
            Follow(goal);
        }

        void ShooterBrain(float dt)
        {
            Vector3 delta = game.Player.Position - Position;
            float distance = delta.magnitude;
            Face(delta, dt);
            if (distance < RetreatRange)
            {
                if (State != EnemyState.Chase) moveAt = 0;
                Change(EnemyState.Chase);
                coverValid = false;
                Vector3 retreat = Position - delta.normalized * 7;
                if (Time.time >= moveAt)
                {
                    moveAt = Time.time + .23f;
                    if (!TryRetreat(retreat)) TryRetreat(Position + Root.right * flank * 6);
                }
                return;
            }
            switch (State)
            {
                case EnemyState.TakeCover:
                    if (!Arrived() && Time.time < until) return;
                    Stop();
                    if (attackStep == 1)
                    {
                        Change(EnemyState.Idle, Random.Range(CoverWaitMin, CoverWaitMax));
                        return;
                    }
                    BeginPeek();
                    return;
                case EnemyState.Peek:
                    PeekBurst();
                    return;
                case EnemyState.Idle:
                    if (Time.time < until) return;
                    ChooseCover();
                    return;
                default:
                    if (Time.time >= nextAttack) ChooseCover();
                    return;
            }
        }

        bool TryRetreat(Vector3 target)
        {
            if (Arena.Sample(target, out Vector3 sample, 3) && Reachable(sample)) return Go(sample);
            return false;
        }

        bool Reachable(Vector3 candidate)
            => NavMesh.CalculatePath(Position, candidate, filter, path) && path.status == NavMeshPathStatus.PathComplete;

        void ChooseCover()
        {
            Vector3 eye = game.Player.Eyes;
            Vector3 player = game.Player.Position;
            float best = float.PositiveInfinity;
            coverValid = false;
            // Sample purposeful positions behind cover first, then distributed NavMesh candidates.
            int sampled = 0;
            for (int i = 0; i < CoverSearchAttempts && sampled < CoverSamples; i++)
            {
                Vector3 guess;
                if (i < game.Arena.CoverAnchors.Length * 3)
                {
                    Vector3 anchor = game.Arena.CoverAnchors[i % game.Arena.CoverAnchors.Length];
                    Vector3 away = Vector3.ProjectOnPlane(anchor - player, Vector3.up).normalized;
                    int variant = i / game.Arena.CoverAnchors.Length;
                    guess = anchor + away * (variant == 0 ? 1.45f : 2.2f) +
                        Vector3.Cross(Vector3.up, away) * (variant == 0 ? 0 : variant == 1 ? .8f : -.8f);
                }
                else
                {
                    float radius = i < game.Arena.CoverAnchors.Length * 3 + CoverSamples / 2 ? CoverRadius * .45f : CoverRadius;
                    Vector2 random = Random.insideUnitCircle * radius;
                    guess = Position + new Vector3(random.x, 0, random.y);
                }
                if (!Arena.Sample(guess, out Vector3 candidate, 2)) continue;
                float travel = Vector3.Distance(candidate, Position);
                float range = Vector3.Distance(candidate, player);
                if (travel > CoverRadius) continue;
                sampled++;
                if (range < CoverNear || range > CoverFar) continue;
                float score = travel + Mathf.Abs((CoverNear + CoverFar) * .5f - range) * 1.35f;
                if (score >= best || Arena.Clear(eye, candidate + Vector3.up * 1.02f) ||
                    Arena.Clear(eye, candidate + Vector3.up * .48f) || !Reachable(candidate)) continue;
                best = score;
                cover = candidate;
                coverValid = true;
            }
            attackStep = 0;
            if (coverValid && Go(cover))
            {
                game.CoverChosen(this);
                Change(EnemyState.TakeCover, 9);
                return;
            }
            float distance = Vector3.Distance(Position, player);
            if (distance >= CoverNear && distance <= CoverFar && CanSeePlayer())
            {
                BeginPeek();
                return;
            }
            Vector3 offset = Vector3.ProjectOnPlane(Position - player, Vector3.up).normalized;
            Vector3 desired = player + offset * ((CoverNear + CoverFar) * .5f) + Root.right * flank * 2;
            if (!TryRetreat(desired)) Go(player);
            Change(EnemyState.Chase);
            nextAttack = Time.time + .7f;
        }

        void BeginPeek()
        {
            hasPeek = false;
            if (coverValid)
            {
                Vector3 tangent = Vector3.Cross(Vector3.up, (game.Player.Position - cover).normalized);
                for (int i = 0; i < 8; i++)
                {
                    float step = 1.05f + .78f * (i / 2);
                    Vector3 guess = cover + tangent * (i % 2 == 0 ? step : -step);
                    if (!Arena.Sample(guess, out peek, .8f) ||
                        !Arena.Clear(peek + Vector3.up * 1.57f, game.Player.Eyes) || !Reachable(peek)) continue;
                    hasPeek = Go(peek);
                    if (hasPeek) break;
                }
                if (!hasPeek)
                {
                    coverValid = false;
                    Change(EnemyState.Chase);
                    nextAttack = Time.time + .35f;
                    return;
                }
            }
            shotAt = Time.time + .24f;
            burstLeft = ShooterBurst;
            Change(EnemyState.Peek, 3);
        }

        void PeekBurst()
        {
            if (hasPeek && !Arrived() && Time.time < until) return;
            if (Time.time < shotAt) return;
            if (burstLeft > 0 && CanSeePlayer())
            {
                Stop();
                hasPeek = false;
                game.FireEnemy(this, ShooterProjectileSpeed, ShooterDamage, Random.value < MissChance ? MissAngle : 0);
                burstLeft--;
                shotAt = Time.time + .13f;
                return;
            }
            if (coverValid && Go(cover))
            {
                attackStep = 1;
                Change(EnemyState.TakeCover, 6);
            }
            else
            {
                Stop();
                Change(EnemyState.Idle, Random.Range(CoverWaitMin, CoverWaitMax));
            }
        }

        void TankBrain(float dt)
        {
            Vector3 delta = game.Player.Position - Position;
            if (State == EnemyState.Stunned)
            {
                if (Time.time >= until) Change(EnemyState.Chase);
                return;
            }
            if (State == EnemyState.Attack)
            {
                Face(delta, dt);
                if (Time.time < until) return;
                chargeDirection = Root.forward;
                chargingHit = false;
                Agent.isStopped = false;
                Change(EnemyState.Charge, ChargeMax);
                return;
            }
            if (State == EnemyState.Charge)
            {
                Vector3 step = chargeDirection * (ChargeSpeed * dt);
                if (Time.time >= until || Agent.Raycast(Position + step + chargeDirection * .35f, out _))
                {
                    Change(EnemyState.Stunned, Stun);
                    return;
                }
                Agent.Move(step);
                if (!chargingHit && delta.magnitude < 1.55f && CanSeePlayer())
                {
                    chargingHit = true;
                    game.DamagePlayer(ChargeDamage, Position - chargeDirection, ChargeKnockback);
                    Change(EnemyState.Stunned, Stun);
                }
                return;
            }
            Face(delta, dt);
            if (delta.magnitude <= ChargeRange && CanSeePlayer())
            {
                Stop();
                Change(EnemyState.Attack, ChargeWarning);
                game.EnemyVoice(this);
                return;
            }
            Change(EnemyState.Chase);
            Follow(game.Player.Position);
        }

        void BossBrain(float dt)
        {
            int phase = HP / Maximum < BossPhase3 ? 3 : HP / Maximum <= BossPhase2 ? 2 : 1;
            if (phase != Phase)
            {
                Phase = phase;
                Agent.speed = phase == 2 ? BossPhase2Speed : BossPhase3Speed;
                if (phase == 2) nextSlam = Time.time + BossSlamInterval;
                if (phase == 3)
                {
                    nextSummon = Time.time + BossShooterInterval;
                    nextAttack = Time.time;
                    burstLeft = 0;
                }
                game.EnemyVoice(this);
            }
            Vector3 player = game.Player.Position;
            Face(player - Position, dt);
            if (State == EnemyState.Charge)
            {
                if (Time.time < until) return;
                if (attackStep == 1)
                {
                    attackStep = 2;
                    Change(EnemyState.Charge, BossAirTime);
                    return;
                }
                if (Arena.Sample(slamTarget, out Vector3 landing, 3)) Agent.Warp(landing);
                if (Vector3.Distance(Position, player) <= BossSlamRadius)
                    game.DamagePlayer(BossSlamDamage, Position - Vector3.ProjectOnPlane(Position - leapOrigin, Vector3.up).normalized * .22f, BossSlamImpulse);
                game.SlamImpact(Position);
                Change(EnemyState.Chase);
            }

            if (Time.time >= nextSummon)
            {
                nextSummon += Phase == 3 ? BossShooterInterval : BossRunnerInterval;
                game.Summon(Phase == 3 ? EnemyKind.Shooter : EnemyKind.Runner, Phase == 3 ? BossShooters : BossRunners);
            }
            if (Phase >= 2 && Time.time >= nextSlam)
            {
                nextSlam = Time.time + BossSlamInterval;
                if (!PlanLeap(player)) return;
                Stop();
                attackStep = 1;
                Change(EnemyState.Charge, BossSlamWarning);
                game.SlamWarning(slamTarget);
                return;
            }
            if (CanSeePlayer() && Time.time >= nextAttack)
            {
                nextAttack = Time.time + (Phase == 3 ? BossVolleyInterval : BossBurstInterval);
                game.EnemyVoice(this);
                if (Phase == 3)
                {
                    for (int i = 0; i < BossVolley; i++)
                        game.FireEnemy(this, BossProjectileSpeed, BossDamage, (i - (BossVolley - 1) * .5f) * 5);
                }
                else
                {
                    burstLeft = BossBurst;
                    shotAt = Time.time;
                }
            }
            if (burstLeft > 0 && Time.time >= shotAt)
            {
                burstLeft--;
                shotAt = Time.time + .11f;
                game.FireEnemy(this, BossProjectileSpeed, BossDamage, 0);
            }
            Change(burstLeft > 0 ? EnemyState.Attack : EnemyState.Chase);
            if (Vector3.Distance(Position, player) > 4) Follow(player, .27f);
            else Stop();
        }
        bool PlanLeap(Vector3 player)
        {
            if (!Arena.Sample(player, out slamTarget, 3) || !Reachable(slamTarget)) return false;
            leapOrigin = Position;
            leapCornerCount = path.GetCornersNonAlloc(leapCorners);
            leapLength = 0;
            for (int i = 1; i < leapCornerCount; i++)
                leapLength += Vector3.Distance(leapCorners[i - 1], leapCorners[i]);
            return true;
        }

        Vector3 LeapPosition(float progress)
        {
            // The airborne silhouette follows the walkable route so it clears columns and walls.
            float left = leapLength * progress;
            for (int i = 1; i < leapCornerCount; i++)
            {
                float segment = Vector3.Distance(leapCorners[i - 1], leapCorners[i]);
                if (left <= segment)
                    return Vector3.MoveTowards(leapCorners[i - 1], leapCorners[i], left);
                left -= segment;
            }
            return slamTarget;
        }

    }
}




