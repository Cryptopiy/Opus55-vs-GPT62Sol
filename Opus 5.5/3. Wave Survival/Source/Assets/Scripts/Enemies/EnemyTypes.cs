using UnityEngine;

namespace WaveSurvival
{
    public enum EnemyType { Runner = 0, Shooter = 1, Tank = 2, Warden = 3 }

    /// <summary>Hit zones on an enemy body. Head = x2 (S-12, boss x1.5 S-23); WeakPoint = tank back panel (S-16).</summary>
    public enum HitZone { Body, Head, Limb, WeakPoint }

    /// <summary>Finite-state-machine states shared by all enemies (section 4 of the spec).</summary>
    public enum EnemyState { Idle, Chase, Attack, TakeCover, Peek, Charge, Stunned, Dead }

    public struct DamageInfo
    {
        public float Amount;            // base damage before zone / armour multipliers
        public Vector3 Point;
        public Vector3 Normal;
        public Vector3 Direction;       // travel direction of the bullet
        public Vector3 SourcePosition;  // where the shot came from
    }

    public struct DamageResult
    {
        public bool Killed;
        public bool Headshot;
        public float Dealt;
        public float Multiplier;
    }

    /// <summary>Damage collider on an enemy body part; links back to its owner and zone.</summary>
    public sealed class Hitbox : MonoBehaviour
    {
        public EnemyBase Owner;
        public HitZone Zone;
        public SurfaceKind Surface = SurfaceKind.Flesh;
    }
}
