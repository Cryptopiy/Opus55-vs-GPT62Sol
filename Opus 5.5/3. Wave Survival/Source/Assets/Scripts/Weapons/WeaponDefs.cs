using UnityEngine;

namespace WaveSurvival
{
    public enum WeaponId { Pistol = 0, Rifle = 1, Shotgun = 2 }

    /// <summary>Static weapon data built from <see cref="GameConfig.Weapons"/> (S-08, S-09, S-10).</summary>
    public sealed class WeaponDef
    {
        public WeaponId Id;
        public string Name;
        public bool Automatic;
        public float Damage;
        public float Interval;
        public int MagSize;
        public int ReserveStart;
        public int ReserveMax;
        public bool InfiniteReserve;
        public float ReloadTime;
        public bool ShellByShell;
        public float ShellTime;
        public float SpreadHip;
        public float SpreadAds;
        public float SpreadMax;        // rifle bloom ceiling
        public float BloomPerShot;
        public float BloomRecoverTime; // time to recover from SpreadMax back to SpreadHip
        public int Pellets = 1;
        public float FalloffEnd;       // shotgun: damage falls linearly to FalloffMin at this distance
        public float FalloffMin = 1f;
        public float CameraKick;       // visual punch (degrees)
        public float ViewRecoil;       // aim recoil per shot for non-pattern weapons (degrees)
        public bool EjectsShells;
        public SoundId Shot;

        public static WeaponDef[] CreateAll()
        {
            var w = new WeaponDef[3];
            w[0] = new WeaponDef
            {
                Id = WeaponId.Pistol, Name = "PISTOL", Automatic = false,
                Damage = GameConfig.Weapons.PistolDamage, Interval = GameConfig.Weapons.PistolInterval,
                MagSize = GameConfig.Weapons.PistolMag, InfiniteReserve = true,
                ReloadTime = GameConfig.Weapons.PistolReload,
                SpreadHip = GameConfig.Weapons.PistolSpread, SpreadAds = GameConfig.Weapons.PistolSpreadAds,
                CameraKick = GameConfig.Weapons.PistolKick, ViewRecoil = GameConfig.Weapons.PistolViewRecoil,
                EjectsShells = true, Shot = SoundId.PistolShot,
            };
            w[1] = new WeaponDef
            {
                Id = WeaponId.Rifle, Name = "ASSAULT RIFLE", Automatic = true,
                Damage = GameConfig.Weapons.RifleDamage, Interval = GameConfig.Weapons.RifleInterval,
                MagSize = GameConfig.Weapons.RifleMag, ReserveStart = GameConfig.Weapons.RifleReserve, ReserveMax = GameConfig.Weapons.RifleReserveMax,
                ReloadTime = GameConfig.Weapons.RifleReload,
                SpreadHip = GameConfig.Weapons.RifleSpreadMin, SpreadAds = GameConfig.Weapons.RifleSpreadMin * GameConfig.Weapons.AdsSpreadMultiplier,
                SpreadMax = GameConfig.Weapons.RifleSpreadMax, BloomPerShot = GameConfig.Weapons.RifleBloomPerShot,
                BloomRecoverTime = GameConfig.Weapons.RifleBloomRecover,
                CameraKick = GameConfig.Weapons.RifleKick, EjectsShells = true, Shot = SoundId.RifleShot,
            };
            w[2] = new WeaponDef
            {
                Id = WeaponId.Shotgun, Name = "SHOTGUN", Automatic = false,
                Damage = GameConfig.Weapons.ShotgunDamage, Interval = GameConfig.Weapons.ShotgunPump,
                MagSize = GameConfig.Weapons.ShotgunMag, ReserveStart = GameConfig.Weapons.ShotgunReserve, ReserveMax = GameConfig.Weapons.ShotgunReserveMax,
                ShellByShell = true, ShellTime = GameConfig.Weapons.ShotgunShellTime,
                SpreadHip = GameConfig.Weapons.ShotgunCone, SpreadAds = GameConfig.Weapons.ShotgunCone, // S-11: ADS does not tighten the shotgun
                Pellets = GameConfig.Weapons.ShotgunPellets,
                FalloffEnd = GameConfig.Weapons.ShotgunFalloffEnd, FalloffMin = GameConfig.Weapons.ShotgunFalloffMin,
                CameraKick = GameConfig.Weapons.ShotgunKick, ViewRecoil = GameConfig.Weapons.ShotgunViewRecoil,
                EjectsShells = false, Shot = SoundId.ShotgunShot,
            };
            return w;
        }
    }

    /// <summary>
    /// S-09 rifle recoil pattern: the first 10 shots climb vertically (with a tiny deterministic drift), afterwards the
    /// pattern settles into a slight horizontal wobble. Deterministic, so it can be learned and compensated.
    /// </summary>
    public static class RecoilPattern
    {
        /// <summary>Returns (pitch up, yaw right) in degrees for the given shot index of a spray.</summary>
        public static Vector2 Rifle(int shot)
        {
            if (shot < GameConfig.Weapons.RifleClimbShots)
            {
                float climb = GameConfig.Weapons.RifleClimbPerShot * (1.1f - shot * 0.02f);
                float drift = (shot % 3 - 1) * 0.04f;
                return new Vector2(climb, drift);
            }
            int k = shot - GameConfig.Weapons.RifleClimbShots;
            return new Vector2(GameConfig.Weapons.RifleWobblePitch, Mathf.Sin(k * 1.35f) * GameConfig.Weapons.RifleWobbleYaw);
        }
    }

    /// <summary>Runtime ammo/spread state of one carried weapon.</summary>
    public sealed class WeaponSlot
    {
        public readonly WeaponDef Def;
        public int Mag;
        public int Reserve;
        public float Bloom;
        public float NextFireTime;   // per weapon, so a shotgun pump never blocks the pistol after a switch

        public WeaponSlot(WeaponDef def)
        {
            Def = def;
            Mag = def.MagSize;
            Reserve = def.ReserveStart;
            Bloom = def.SpreadHip;
        }

        public bool CanReload => Mag < Def.MagSize && (Def.InfiniteReserve || Reserve > 0);

        /// <summary>Adds reserve ammo up to the cap; returns how much was actually added.</summary>
        public int AddReserve(int amount)
        {
            if (Def.InfiniteReserve) return 0;
            int before = Reserve;
            Reserve = Mathf.Min(Def.ReserveMax, Reserve + amount);
            return Reserve - before;
        }
    }
}
