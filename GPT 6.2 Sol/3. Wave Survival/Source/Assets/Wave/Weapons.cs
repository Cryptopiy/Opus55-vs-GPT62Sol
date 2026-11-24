using UnityEngine;
using static WaveSurvival.GameConfig;

namespace WaveSurvival
{
    internal sealed class Weapons
    {
        readonly CoreGame game;
        readonly int[] magazine = new int[GameConfig.Weapons.Length];
        readonly int[] reserve = new int[GameConfig.Weapons.Length];
        readonly float[] shotAt = new float[GameConfig.Weapons.Length];
        float reloadAt, reloadStarted, switchStarted = -1, bloom, resetChainAt;
        int selected, queued, chain;
        bool reload;
        public int Selected => selected;
        public int Magazine => magazine[selected];
        public int Reserve => reserve[selected];
        public int ReserveFor(int slot) => reserve[slot];
        public bool Reloading => reload;
        public float LastShot { get; private set; } = -100;
        public float Spread => CurrentSpread(game.Player.Aim);
        public bool CanAim => !reload && switchStarted < 0;
        public float Stage => switchStarted >= 0 ? Mathf.Sin(Mathf.Clamp01((Time.time - switchStarted) / ModelSwitch) * Mathf.PI)
            : reload ? .72f + .28f * Mathf.Sin(Mathf.Clamp01((Time.time - reloadStarted) / Spec.Reload) * Mathf.PI) : 0;
        GameConfig.Weapon Spec => GameConfig.Weapons[selected];

        public Weapons(CoreGame game)
        {
            this.game = game;
            for (int i = 0; i < magazine.Length; i++)
            {
                magazine[i] = GameConfig.Weapons[i].Magazine;
                reserve[i] = GameConfig.Weapons[i].Reserve;
            }
        }

        public void AddAmmo(int rifle, int shotgun)
        {
            reserve[(int)WeaponKind.Rifle] = Mathf.Min(RifleReserveMax, reserve[(int)WeaponKind.Rifle] + rifle);
            reserve[(int)WeaponKind.Shotgun] = (int)System.Math.Min(int.MaxValue, (long)reserve[(int)WeaponKind.Shotgun] + shotgun);
        }

        public void Select(int slot)
        {
            int from = switchStarted >= 0 ? queued : selected;
            if (slot < 0 || slot == from) return;
            queued = slot;
            switchStarted = Time.time;
            reload = false;
            game.ReloadSound(false);
        }

        public void Cycle(int step) => Select(((switchStarted >= 0 ? queued : selected) + step + magazine.Length) % magazine.Length);

        public void Tick(float dt, bool pressed, bool held, bool reloadPressed)
        {
            float now = Time.time;
            if (now > resetChainAt) chain = 0;
            bloom = Mathf.MoveTowards(bloom, 0, dt / RifleBloomRecovery);
            if (switchStarted >= 0)
            {
                float elapsed = now - switchStarted;
                if (elapsed >= ModelSwitch * .5f) selected = queued;
                if (elapsed < ModelSwitch) return;
                switchStarted = -1;
                chain = 0;
                bloom = 0;
            }
            bool shotgun = selected == (int)WeaponKind.Shotgun;
            if (reload && now >= reloadAt) FinishReload();
            if (shotgun && reload && pressed && Magazine > 0) reload = false;
            if (reloadPressed && !reload) StartReload();
            bool wantsShot = selected == (int)WeaponKind.Rifle ? held : pressed;
            if (reload || !wantsShot || now < shotAt[selected]) return;
            if (Magazine <= 0)
            {
                if (pressed) game.ReloadSound(false);
                return;
            }
            // Keep automatic cadence stable across frame boundaries without stockpiling missed shots.
            bool recoverCadence = selected == (int)WeaponKind.Rifle && now - shotAt[selected] <= dt * 1.5f;
            shotAt[selected] = (recoverCadence ? shotAt[selected] : now) + Spec.Interval;
            Fire();
        }

        void StartReload()
        {
            if (Magazine >= Spec.Magazine || Reserve == 0) return;
            reload = true;
            reloadStarted = Time.time;
            reloadAt = reloadStarted + Spec.Reload;
            game.ReloadSound(false);
        }

        void FinishReload()
        {
            bool single = selected == (int)WeaponKind.Shotgun;
            int fill = single ? 1 : Spec.Magazine - Magazine;
            if (Reserve >= 0)
            {
                fill = Mathf.Min(fill, Reserve);
                reserve[selected] -= fill;
            }
            magazine[selected] += fill;
            game.ReloadSound(true);
            reload = single && Magazine < Spec.Magazine && Reserve > 0;
            if (!reload) return;
            reloadStarted = reloadAt;
            reloadAt += Spec.Reload;
        }

        float CurrentSpread(float aim)
        {
            if (selected == (int)WeaponKind.Shotgun) return Spec.Spread;
            float hip = selected == (int)WeaponKind.Rifle ? Mathf.Lerp(Spec.Spread, RifleBloomMax, bloom) : Spec.Spread;
            float ads = selected == (int)WeaponKind.Pistol ? PistolAimSpread : hip * AimSpread;
            return Mathf.Lerp(hip, ads, aim);
        }

        void Fire()
        {
            magazine[selected]--;
            LastShot = Time.time;
            bool shotgun = selected == (int)WeaponKind.Shotgun;
            bool rifle = selected == (int)WeaponKind.Rifle;
            int bullets = shotgun ? Pellets : 1;
            float spread = Mathf.Tan(Spread * Mathf.Deg2Rad * (shotgun ? .5f : 1));
            game.Stats.Shots += bullets;
            Vector3 origin = game.Player.Eyes;
            Transform view = game.Player.View;
            game.ShotEffect((WeaponKind)selected);
            for (int i = 0; i < bullets; i++)
            {
                Vector2 offset = Random.insideUnitCircle * spread;
                Vector3 direction = view.TransformDirection(new Vector3(offset.x, offset.y, 1).normalized);
                Vector3 end = origin + direction * 75;
                if (Physics.Raycast(origin, direction, out RaycastHit hit, 75, WorldMask | HurtMask, QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    var zone = hit.collider.GetComponent<HitZone>();
                    if (zone != null && zone.Enemy.Alive)
                    {
                        game.Stats.Hits++;
                        if (zone.Head) game.Stats.Headshots++;
                        float damage = shotgun ? Spec.Damage * Mathf.Lerp(1, FalloffFloor, hit.distance / FalloffDistance) : Spec.Damage;
                        bool dead = zone.Enemy.Hurt(damage, origin, zone.Head);
                        game.RegisterHit(dead);
                        game.Impact(hit.point, hit.normal, SurfaceKind.Flesh);
                    }
                    else game.Impact(hit.point, hit.normal, game.Arena.IsMetal(hit.collider.transform) ? SurfaceKind.Metal : SurfaceKind.Concrete);
                }
                game.Tracer(end, shotgun);
            }
            if (rifle)
            {
                chain++;
                resetChainAt = Time.time + .24f;
                // Recovery runs between shots, so the increment must outrun that recovery while held.
                bloom = Mathf.Clamp01(bloom + .31f);
                float upward = chain <= RifleClimbShots ? .65f + chain * .079f : 1.20f;
                game.Player.Recoil(upward, chain > RifleClimbShots ? Mathf.Sin(chain * 1.8f) * .23f : 0);
            }
            else game.Player.Recoil(shotgun ? 4.5f : 1.45f, 0);
        }
    }
}

