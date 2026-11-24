using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace WaveSurvival
{
    internal sealed partial class CoreGame
    {
        SynthAudio sound;
        Viewmodel viewmodel;
        Effects effects;
        Light viewLight;
        public int ParticleCount => effects.Particles;

        partial void SetupEffects()
        {
            var cameraData = Player.Camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            Player.Camera.allowHDR = true;
            Player.Camera.clearFlags = CameraClearFlags.SolidColor;
            Player.Camera.backgroundColor = Geometry.Hex(0x1d2c31);
            sound = new SynthAudio(transform);
            effects = new Effects(transform);
            viewmodel = new Viewmodel(this);
            var key = Geometry.Root("Weapon fill", Player.View);
            key.localPosition = new(-.18f, .13f, .39f);
            viewLight = key.gameObject.AddComponent<Light>();
            viewLight.type = LightType.Point;
            viewLight.color = Geometry.Hex(0xa0d2e3);
            viewLight.range = .82f;
            viewLight.intensity = .79f;
        }

        partial void UpdateEffects(float dt)
        {
            Arena.UpdateLighting(Waves.Gate, Waves.TimeUntilSpawn);
            viewmodel.Update(dt);
            effects.Update(dt);
        }

        partial void DeathEffect(Enemy enemy)
        {
            sound.Play(Sound.Death, enemy.Position + Vector3.up, .62f, pitch: enemy.Kind == EnemyKind.Warden ? .62f : 1);
            effects.Burst(enemy.Position + Vector3.up, Vector3.up, Geometry.Hex(0xff6b4f), 17);
        }

        partial void EnemyShotEffect(EnemyKind kind, Vector3 point)
        {
            sound.Play(Sound.Rifle, point, .56f, pitch: kind == EnemyKind.Warden ? .72f : 1.17f);
            effects.Burst(point, (Player.Eyes - point).normalized, Geometry.Hex(0xff954b), 3);
        }

        partial void DisplaySlam(Vector3 point)
        {
            effects.WarnSlam(point);
            sound.Play(Sound.Warden, point, .69f);
        }

        partial void SlamFx(Vector3 point)
        {
            effects.Burst(point + Vector3.up * .22f, Vector3.up, Geometry.Hex(0xffa540), 29);
            sound.Play(Sound.Slam, point, .93f);
        }

        partial void StepAudio() => sound.Play(Sound.Step, Player.Position, .53f, pitch: Random.Range(.88f, 1.12f));
        partial void ReloadAudio(bool done) => sound.Play(done ? Sound.Seat : Sound.Open, Player.Eyes, .52f, false);
        partial void VoiceAudio(Enemy enemy) => sound.Play((Sound)((int)Sound.Runner + (int)enemy.Kind), enemy.Eyes, .74f);
        partial void HornAudio() => sound.Play(Sound.Horn, Player.Eyes, .57f, false);
        partial void PickupAudio() => sound.Play(Sound.Pickup, Player.Eyes, .66f, false);
        partial void HitSound(bool dead) => sound.Play(dead ? Sound.Kill : Sound.Hit, Player.Eyes, dead ? .59f : .35f, false);

        partial void WeaponFx(WeaponKind kind)
        {
            viewmodel.Update(0);
            viewmodel.Flash();
            sound.Play((Sound)kind, viewmodel.Muzzle, .81f, pitch: Random.Range(.96f, 1.04f));
            if (kind != WeaponKind.Shotgun) effects.Shell(viewmodel.Eject, Player.View);
        }

        partial void TracerFx(Vector3 end, bool pellet) => effects.Tracer(viewmodel.Muzzle, end, pellet);
        partial void ImpactFx(Vector3 point, Vector3 normal, SurfaceKind type) => effects.Impact(point, normal, type);
    }
}
