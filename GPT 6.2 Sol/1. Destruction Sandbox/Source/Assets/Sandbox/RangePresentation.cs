using UnityEngine;

namespace DestructionRange
{
    public sealed partial class Sandbox
    {
        VfxField vfx;
        SynthAudio audioBus;
        readonly float[] lastImpact = new float[SandboxConfig.MatterSpecs.Length];

        internal int ActiveParticles => vfx?.ActiveParticles ?? 0;
        internal int ActiveWaves => vfx?.ActiveWaves ?? 0;
        internal int StartedSounds => audioBus.StartedMask;
        internal int PlayingVoices => audioBus.Playing;

        partial void FinishSetup()
        {
            vfx = new VfxField(this);
            audioBus = new SynthAudio(this);
            SetUpInterface();
        }

        partial void ResetPresentation()
        {
            vfx.Reset();
            audioBus.Reset();
            ResetInterface();
        }

        partial void PresentationTick()
        {
            vfx.Tick(Time.deltaTime);
            TickInterface();
        }

        partial void PauseChanged(bool value)
        {
            audioBus.SetClock(value, Slow);
            PauseInterface(value);
        }

        partial void DebrisCleared() => vfx.ClearDebris();

        partial void ShotPresentation(Weapon weapon)
        {
            vfx.Muzzle(weapon);
            var sound = weapon == Weapon.Cannon ? Sound.Cannon : weapon == Weapon.Shell ? Sound.Shell : Sound.OrbShot;
            audioBus.Play(sound, Player.Muzzle.position, .90f);
        }

        partial void SelectionPresentation() => Click();

        partial void CannonPresentation(Vector3 point, float impulse)
        {
            if (impulse < 26f) return;
            vfx.HeavyImpact(point, impulse);
            float energy = Mathf.InverseLerp(26, 800, impulse);
            audioBus.Play(Sound.Heavy, point, Mathf.Lerp(.32f, .88f, energy), Mathf.Lerp(1.12f, .74f, energy));
        }

        partial void ImpactPresentation(Matter kind, Vector3 point, float impulse)
        {
            int type = (int)kind;
            if (Time.time < lastImpact[type]) return;
            lastImpact[type] = Time.time + .048f;
            float ratio = Mathf.Clamp01(impulse / (SandboxConfig.MatterSpecs[type].Threshold * 1.6f));
            Sound cue = kind == Matter.Stone ? Sound.Stone : kind == Matter.Wood ? Sound.Wood : Sound.Glass;
            audioBus.Play(cue, point, Mathf.Lerp(.11f, .76f, ratio), Mathf.Lerp(1.22f, .77f, ratio));
        }

        partial void FracturePresentation(Matter kind, Vector3 point, float impulse)
        {
            vfx.Break(kind, point);
            ImpactPresentation(kind, point, impulse);
        }

        partial void BlastPresentation(Vector3 point)
        {
            vfx.Blast(point);
            audioBus.Play(Sound.Blast, point, 1f);
        }

        partial void OrbPresentation(Vector3 point)
        {
            vfx.Anchor(point);
            audioBus.Play(Sound.Hum, point, .7f);
        }

        partial void OrbReleasePresentation(Vector3 point)
        {
            vfx.Release(point);
            audioBus.StopHum(point);
            audioBus.Play(Sound.Release, point, 1f);
        }

        internal void Click() => audioBus.Play(Sound.Click, Vector3.zero, .55f);
        internal float MasterGain { get => audioBus.Master; set => audioBus.Master = value; }

        partial void SetUpInterface();
        partial void ResetInterface();
        partial void TickInterface();
        partial void PauseInterface(bool value);
    }
}
