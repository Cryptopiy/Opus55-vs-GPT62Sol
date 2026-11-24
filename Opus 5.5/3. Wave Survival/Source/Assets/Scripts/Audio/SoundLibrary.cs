using System;
using System.Threading.Tasks;
using UnityEngine;

namespace WaveSurvival
{
    public enum SoundId
    {
        PistolShot, RifleShot, ShotgunShot, MagOut, MagIn, BoltRack, ShellInsert, ShotgunPump, DryFire, WeaponSwitch,
        Footstep, HitTick, KillConfirm, PlayerHurt, ShellClink, Ricochet,
        RunnerGrowl, ShooterGrowl, TankGrowl, TankRoar, BossRoar, RunnerDeath, ShooterDeath, TankDeath,
        EnemyShot, BossShot, Slam, Whoosh, Summon, PickupHealth, PickupAmmo, WaveHorn, GateHum, Victory, UiClick,
        AmbientDrone, Count
    }

    /// <summary>
    /// Generates every AudioClip once per application run (S-26: all audio synthesized). The heavy sample math runs
    /// in parallel on worker threads; only AudioClip creation happens on the main thread.
    /// </summary>
    public static class SoundLibrary
    {
        const int FootstepVariants = 4;
        static AudioClip[][] clips;

        public static bool Ready => clips != null;

        public static void Ensure()
        {
            if (clips != null) return;
            int count = (int)SoundId.Count;
            var data = new float[count][][];
            Parallel.For(0, count, i => data[i] = Generate((SoundId)i));
            clips = new AudioClip[count][];
            for (int i = 0; i < count; i++)
            {
                clips[i] = new AudioClip[data[i].Length];
                for (int v = 0; v < data[i].Length; v++)
                {
                    float[] samples = data[i][v];
                    AudioClip clip = AudioClip.Create(((SoundId)i).ToString() + v, samples.Length, 1, Dsp.Rate, false);
                    clip.SetData(samples, 0);
                    clips[i][v] = AssetTracker.Track(clip);
                }
            }
        }

        public static void Clear() => clips = null;

        public static AudioClip Get(SoundId id)
        {
            AudioClip[] set = clips[(int)id];
            return set.Length == 1 ? set[0] : set[UnityEngine.Random.Range(0, set.Length)];
        }

        static float[][] One(float[] d) => new[] { d };

        static float[][] Generate(SoundId id)
        {
            switch (id)
            {
                case SoundId.PistolShot: return One(WeaponSounds.Pistol());
                case SoundId.RifleShot: return One(WeaponSounds.Rifle());
                case SoundId.ShotgunShot: return One(WeaponSounds.Shotgun());
                case SoundId.MagOut: return One(WeaponSounds.MagOut());
                case SoundId.MagIn: return One(WeaponSounds.MagIn());
                case SoundId.BoltRack: return One(WeaponSounds.BoltRack());
                case SoundId.ShellInsert: return One(WeaponSounds.ShellInsert());
                case SoundId.ShotgunPump: return One(WeaponSounds.Pump());
                case SoundId.DryFire: return One(WeaponSounds.DryFire());
                case SoundId.WeaponSwitch: return One(WeaponSounds.Switch());
                case SoundId.Footstep:
                    var steps = new float[FootstepVariants][];
                    for (int i = 0; i < FootstepVariants; i++) steps[i] = WeaponSounds.Footstep((uint)(401 + i * 17));
                    return steps;
                case SoundId.HitTick: return One(WeaponSounds.HitTick());
                case SoundId.KillConfirm: return One(WeaponSounds.KillConfirm());
                case SoundId.PlayerHurt: return One(WeaponSounds.PlayerHurt());
                case SoundId.ShellClink: return One(WeaponSounds.ShellClink());
                case SoundId.Ricochet: return One(WeaponSounds.Ricochet());
                case SoundId.RunnerGrowl: return One(WorldSounds.RunnerGrowl());
                case SoundId.ShooterGrowl: return One(WorldSounds.ShooterGrowl());
                case SoundId.TankGrowl: return One(WorldSounds.TankGrowl());
                case SoundId.TankRoar: return One(WorldSounds.TankRoar());
                case SoundId.BossRoar: return One(WorldSounds.BossRoar());
                case SoundId.RunnerDeath: return One(WorldSounds.RunnerDeath());
                case SoundId.ShooterDeath: return One(WorldSounds.ShooterDeath());
                case SoundId.TankDeath: return One(WorldSounds.TankDeath());
                case SoundId.EnemyShot: return One(WorldSounds.EnemyShot());
                case SoundId.BossShot: return One(WorldSounds.BossShot());
                case SoundId.Slam: return One(WorldSounds.Slam());
                case SoundId.Whoosh: return One(WorldSounds.Whoosh());
                case SoundId.Summon: return One(WorldSounds.Summon());
                case SoundId.PickupHealth: return One(WorldSounds.PickupHealth());
                case SoundId.PickupAmmo: return One(WorldSounds.PickupAmmo());
                case SoundId.WaveHorn: return One(WorldSounds.WaveHorn());
                case SoundId.GateHum: return One(WorldSounds.GateHum());
                case SoundId.Victory: return One(WorldSounds.Victory());
                case SoundId.UiClick: return One(WorldSounds.UiClick());
                case SoundId.AmbientDrone: return One(WorldSounds.AmbientDrone());
                default: throw new ArgumentOutOfRangeException(nameof(id), id, null);
            }
        }
    }
}
