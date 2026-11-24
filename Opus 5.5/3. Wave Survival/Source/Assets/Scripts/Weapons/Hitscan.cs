using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// Resolves one trigger pull: casts every pellet ray, applies damage through enemy hitboxes and spawns the per
    /// surface impact particles, decals and tracers (S-12).
    /// </summary>
    public static class Hitscan
    {
        public struct Outcome
        {
            public bool HitEnemy;
            public bool Killed;
            public bool Headshot;
        }

        public static Outcome Fire(WeaponDef def, Vector3 origin, Vector3 aim, float spreadDeg, Vector3 tracerStart)
        {
            var outcome = new Outcome();
            // Enemies are moved by NavMesh agents during Update; sync so the rays test this frame's poses.
            Physics.SyncTransforms();
            for (int p = 0; p < def.Pellets; p++)
            {
                Vector3 dir = MathUtil.RandomInCone(aim, spreadDeg);
                Vector3 end;
                if (Physics.Raycast(origin, dir, out RaycastHit hit, GameConfig.Weapons.Range, GameConfig.Layers.PlayerShotMask, QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    if (hit.collider.TryGetComponent(out Hitbox hb) && hb.Owner != null && hb.Owner.IsAlive)
                    {
                        float falloff = def.FalloffEnd > 0f ? Mathf.Lerp(1f, def.FalloffMin, hit.distance / def.FalloffEnd) : 1f;
                        var info = new DamageInfo
                        {
                            Amount = def.Damage * falloff,
                            Point = hit.point,
                            Normal = hit.normal,
                            Direction = dir,
                            SourcePosition = origin,
                        };
                        DamageResult r = hb.Owner.ApplyHit(ref info, hb);
                        outcome.HitEnemy = true;
                        outcome.Killed |= r.Killed;
                        outcome.Headshot |= r.Headshot;
                        Game.Fx.Impact(hb.Surface, hit.point, hit.normal, dir);
                        Game.Fx.Decal(hb.Surface, hit.point, hit.normal, hb.transform);
                    }
                    else
                    {
                        SurfaceKind kind = hit.collider.TryGetComponent(out SurfaceTag tag) ? tag.Kind : SurfaceKind.Concrete;
                        Game.Fx.Impact(kind, hit.point, hit.normal, dir);
                        Game.Fx.Decal(kind, hit.point, hit.normal, null);
                        if (kind == SurfaceKind.Metal && Random.value < 0.35f) Game.Audio.PlayAt(SoundId.Ricochet, hit.point, 0.5f);
                    }
                }
                else
                {
                    end = origin + dir * 60f;
                }
                Game.Fx.Tracer(tracerStart, end);
            }
            return outcome;
        }
    }
}
