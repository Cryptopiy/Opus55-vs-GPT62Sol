using UnityEngine;
using UnityEngine.AI;

namespace WaveSurvival
{
    /// <summary>
    /// S-15 cover search. Samples 16 (>= 12) candidate points on the NavMesh within 20 m of the shooter: points
    /// hugging the arena's cover objects plus random NavMesh samples. Each candidate is scored on
    /// (1) not visible from the player's eyes (hard requirement), (2) distance to the player inside 12..18 m,
    /// (3) short travel from the current position, (4) not already claimed by another shooter.
    /// </summary>
    public static class CoverFinder
    {
        public const int Max = GameConfig.Enemy.ShooterCandidates;
        static readonly int[] anchorIdx = new int[256];
        static readonly NavMeshPath path = new NavMeshPath();

        public struct Candidate
        {
            public Vector3 Pos;
            public bool Hidden;
            public float Score;
        }

        public static bool IsHidden(Vector3 point, Vector3 eye)
        {
            // Tested at standing head height: the shooter must be fully concealed, not just its feet.
            return Physics.Linecast(eye, point + Vector3.up * 1.4f, GameConfig.Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Fills <paramref name="debug"/> with the evaluated candidates and returns the chosen cover point.</summary>
        public static bool Find(ShooterEnemy self, bool retreat, Candidate[] debug, out int debugCount, out Vector3 best)
        {
            Vector3 me = self.transform.position;
            PlayerRig player = Game.Player;
            Vector3 pp = player.Position;
            Vector3 eye = player.EyePosition;
            ArenaInfo arena = Game.Arena;
            float search = GameConfig.Enemy.ShooterSearchRadius;
            int n = 0;

            // Cover-hugging anchors within the search radius (random subset if there are many).
            int anchors = 0;
            for (int i = 0; i < arena.CoverAnchors.Length && anchors < anchorIdx.Length; i++)
                if (MathUtil.FlatDistance(arena.CoverAnchors[i], me) <= search) anchorIdx[anchors++] = i;
            int fromAnchors = Mathf.Min(anchors, Max - 6);
            for (int k = 0; k < fromAnchors; k++)
            {
                int pick = Random.Range(k, anchors);
                (anchorIdx[k], anchorIdx[pick]) = (anchorIdx[pick], anchorIdx[k]);
                debug[n++].Pos = arena.CoverAnchors[anchorIdx[k]];
            }
            // Random NavMesh samples fill the rest (always at least 6 of them).
            int guard = 0;
            while (n < Max && guard++ < Max * 3)
            {
                Vector2 r = Random.insideUnitCircle * search;
                if (arena.SampleHumanoid(me + new Vector3(r.x, 0f, r.y), 2f, out Vector3 s) && MathUtil.FlatDistance(s, me) <= search)
                    debug[n++].Pos = s;
            }
            debugCount = n;

            for (int i = 0; i < n; i++)
            {
                Vector3 c = debug[i].Pos;
                bool hidden = IsHidden(c, eye);
                float dp = MathUtil.FlatDistance(c, pp);
                float score = hidden ? 0f : -100f;
                float min = GameConfig.Enemy.ShooterMinDist, max = GameConfig.Enemy.ShooterMaxDist;
                if (dp >= min && dp <= max) score += 30f;
                else score -= Mathf.Abs(dp - Mathf.Clamp(dp, min, max)) * 6f;
                score -= MathUtil.FlatDistance(c, me) * 0.8f;
                if (retreat)
                {
                    score += dp * 2f;
                    if (dp < GameConfig.Enemy.ShooterRetreatDist + 2f) score -= 60f;
                }
                if (ShooterEnemy.IsReservedByOther(self, c)) score -= 40f;
                debug[i].Hidden = hidden;
                debug[i].Score = score;
            }

            // Pick the best candidate that is hidden (or, when retreating, the best overall) with a complete path.
            for (int attempt = 0; attempt < n; attempt++)
            {
                int bi = -1;
                float bs = float.NegativeInfinity;
                for (int i = 0; i < n; i++)
                {
                    if (debug[i].Score > bs && (debug[i].Hidden || retreat))
                    {
                        bs = debug[i].Score;
                        bi = i;
                    }
                }
                if (bi < 0) break;
                if (NavMesh.CalculatePath(me, debug[bi].Pos, self.Filter, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    best = debug[bi].Pos;
                    return true;
                }
                debug[bi].Score = float.NegativeInfinity; // unreachable: try the next one
            }
            best = me;
            return false;
        }

        /// <summary>
        /// Peek position: step sideways out of cover (perpendicular to the line to the player) until the player's chest
        /// is visible from head height; falls back to a step toward the player.
        /// </summary>
        public static bool FindPeek(ShooterEnemy self, Vector3 cover, out Vector3 peek)
        {
            PlayerRig player = Game.Player;
            Vector3 toPlayer = MathUtil.FlatDirection(cover, player.Position);
            Vector3 side = new Vector3(toPlayer.z, 0f, -toPlayer.x);
            float sign = Random.value < 0.5f ? 1f : -1f;
            float step = GameConfig.Enemy.ShooterPeekStep;
            for (int i = 0; i < 6; i++)
            {
                float dist = step * (1f + (i / 2) * 0.55f);
                Vector3 c = cover + side * (sign * dist);
                sign = -sign;
                if (Game.Arena.SampleHumanoid(c, 1f, out Vector3 s) && !Physics.Linecast(s + Vector3.up * 1.4f, player.ChestPosition, GameConfig.Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                {
                    peek = s;
                    return true;
                }
            }
            Vector3 fwd = cover + toPlayer * 2f;
            if (Game.Arena.SampleHumanoid(fwd, 1.5f, out Vector3 f) && !Physics.Linecast(f + Vector3.up * 1.4f, player.ChestPosition, GameConfig.Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
            {
                peek = f;
                return true;
            }
            peek = cover;
            return false;
        }
    }
}
