using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// Owns the three weapons: selection (1/2/3, wheel), firing (LMB), ADS (RMB), reloading (R), switching (0.35 s),
    /// spread/bloom, recoil and all per-shot feedback (S-08..S-13).
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public sealed class WeaponController : MonoBehaviour
    {
        public WeaponSlot[] Slots { get; private set; }
        public int CurrentIndex { get; private set; }
        public WeaponSlot Current => Slots[CurrentIndex];
        public bool IsAiming { get; private set; }
        public bool IsReloading { get; private set; }
        public bool IsSwitching => switchTimer > 0f;
        public float CurrentSpread { get; private set; }
        public float ReloadProgress => IsReloading ? Mathf.Clamp01(reloadTimer / Mathf.Max(0.01f, reloadDuration)) : 0f;
        /// <summary>0 = weapon fully raised, 1 = fully lowered (switch animation, S-13).</summary>
        public float SwitchLower
        {
            get
            {
                if (!IsSwitching) return 0f;
                float half = GameConfig.Weapons.SwitchTime * 0.5f;
                return swapped ? Mathf.Clamp01(switchTimer / half) : Mathf.Clamp01(1f - (switchTimer - half) / half);
            }
        }
        public int SwitchTarget => IsSwitching ? switchTarget : CurrentIndex;

        PlayerRig rig;
        Viewmodel[] models;
        ViewmodelAnimator anim;
        Light muzzleLight;
        float muzzleTimer;
        float bufferedUntil, lastShotTime = -10f;
        int sprayIndex;
        float reloadTimer, reloadDuration;
        bool magInPlayed;
        float switchTimer;
        int switchTarget;
        bool swapped;
        float pumpSoundAt = -1f;

        public void Init(PlayerRig owner)
        {
            rig = owner;
            WeaponDef[] defs = WeaponDef.CreateAll();
            Slots = new WeaponSlot[defs.Length];
            for (int i = 0; i < defs.Length; i++) Slots[i] = new WeaponSlot(defs[i]);
            models = ViewmodelFactory.BuildAll(owner.ViewmodelRoot);
            anim = owner.ViewmodelRoot.gameObject.AddComponent<ViewmodelAnimator>();
            anim.Init(owner, this, models);

            var lightGo = new GameObject("MuzzleLight");
            lightGo.transform.SetParent(owner.Camera.transform, false);
            muzzleLight = lightGo.AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.range = GameConfig.Fx.MuzzleLightRange;
            muzzleLight.intensity = GameConfig.Fx.MuzzleLightIntensity;
            muzzleLight.color = new Color(1f, 0.72f, 0.38f);
            muzzleLight.shadows = LightShadows.None;
            muzzleLight.enabled = false;
            CurrentIndex = 0;
            anim.SetActive(0);
        }

        /// <summary>Adds reserve ammo (pickups / wave breaks). Returns the amount actually added.</summary>
        public int AddAmmo(WeaponId id, int amount) => Slots[(int)id].AddReserve(amount);

        public bool AmmoFull =>
            Slots[1].Reserve >= Slots[1].Def.ReserveMax && Slots[2].Reserve >= Slots[2].Def.ReserveMax;

        void Update()
        {
            float dt = Time.deltaTime;
            UpdateMuzzle(dt);
            if (!Game.IsPlaying || !rig.IsAlive)
            {
                IsAiming = false;
                rig.View.SetAds(false);
                return;
            }

            InputState input = InputRouter.State;
            int select = input.SelectWeapon;
            int target = SwitchTarget;
            if (input.Scroll > 0.01f) select = (target + Slots.Length - 1) % Slots.Length;
            else if (input.Scroll < -0.01f) select = (target + 1) % Slots.Length;
            if (select >= 0 && select != target) BeginSwitch(select);
            UpdateSwitch(dt);

            IsAiming = input.AimHeld && !IsSwitching && !IsReloading;
            rig.View.SetAds(IsAiming);

            if (input.ReloadPressed) TryStartReload();
            UpdateReload(dt);
            UpdateFire(input);
            UpdateBloom(input, dt);
            CurrentSpread = EffectiveSpread(Current);

            if (pumpSoundAt > 0f && Time.time >= pumpSoundAt)
            {
                pumpSoundAt = -1f;
                Game.Audio.Play2D(SoundId.ShotgunPump, 0.8f);
            }
            // Automatic reload once the magazine is empty and the gun is ready again.
            if (Current.Mag == 0 && Current.CanReload && !IsReloading && !IsSwitching && Time.time >= Current.NextFireTime) TryStartReload();
        }

        // ---------------- firing ----------------

        void UpdateFire(InputState input)
        {
            WeaponSlot s = Current;
            WeaponDef d = s.Def;
            float now = Time.time;
            if (input.FirePressed) bufferedUntil = now + GameConfig.Weapons.FireBufferTime;
            bool wants = d.Automatic ? input.FireHeld : now <= bufferedUntil;
            if (!wants || IsSwitching) return;
            if (IsReloading)
            {
                // S-10: the shell-by-shell reload is interruptible by firing.
                if (d.ShellByShell && s.Mag > 0) IsReloading = false;
                else return;
            }
            if (now < s.NextFireTime) return;
            if (s.Mag <= 0)
            {
                if (input.FirePressed) Game.Audio.Play2D(SoundId.DryFire, 0.7f);
                bufferedUntil = 0f;
                return;
            }
            Fire(s);
            bufferedUntil = 0f;
            // Keep an exact cadence for automatic fire, but never "bank" shots after a pause.
            s.NextFireTime = now - s.NextFireTime < d.Interval ? s.NextFireTime + d.Interval : now + d.Interval;
        }

        float EffectiveSpread(WeaponSlot s)
        {
            WeaponDef d = s.Def;
            float ads = rig.View.AdsBlend;
            if (d.Id == WeaponId.Rifle) return s.Bloom * Mathf.Lerp(1f, GameConfig.Weapons.AdsSpreadMultiplier, ads);
            return Mathf.Lerp(d.SpreadHip, d.SpreadAds, ads);
        }

        void Fire(WeaponSlot s)
        {
            WeaponDef d = s.Def;
            float now = Time.time;
            s.Mag--;
            if (now - lastShotTime > GameConfig.Weapons.RifleResetTime) sprayIndex = 0;
            lastShotTime = now;

            Transform cam = rig.Camera.transform;
            Vector3 origin = cam.position;
            Vector3 aim = cam.forward;
            Viewmodel vm = models[CurrentIndex];
            Vector3 muzzleWorld = vm.Muzzle.position;
            // Tracers start on the ray through the (tiny, near-eye) viewmodel muzzle, so on screen they leave the barrel.
            Vector3 tracerStart = origin + (muzzleWorld - origin).normalized * 0.8f;

            Hitscan.Outcome o = Hitscan.Fire(d, origin, aim, EffectiveSpread(s), tracerStart);
            GameStats stats = Game.Stats;
            stats.ShotsFired++;
            if (o.HitEnemy) stats.ShotsHit++;
            if (o.Headshot) stats.Headshots++;
            if (o.Killed)
            {
                Game.Hud.ShowHitMarker(true);
                Game.Audio.Play2D(SoundId.KillConfirm, 0.9f);
            }
            else if (o.HitEnemy)
            {
                Game.Hud.ShowHitMarker(false);
                Game.Audio.Play2D(SoundId.HitTick, 0.75f);
            }

            if (d.BloomPerShot > 0f) s.Bloom = Mathf.Min(d.SpreadMax, s.Bloom + d.BloomPerShot);
            if (d.Id == WeaponId.Rifle)
            {
                Vector2 r = RecoilPattern.Rifle(sprayIndex);
                rig.View.AddRecoil(r.x, r.y);
            }
            else
            {
                rig.View.AddRecoil(d.ViewRecoil, Random.Range(-0.2f, 0.2f) * d.ViewRecoil);
            }
            sprayIndex++;
            float k = d.CameraKick * 60f;
            rig.View.Kick(new Vector3(-k, Random.Range(-0.25f, 0.25f) * k, Random.Range(-0.35f, 0.35f) * k));

            Game.Audio.PlayShot(d.Shot, origin);
            anim.OnFire();
            vm.SetFlash(true);
            muzzleTimer = GameConfig.Fx.MuzzleFlashTime;
            // Light the world from 0.8 m ahead (where the tracer starts): placed at the near-eye viewmodel muzzle it
            // would blow out the gun itself.
            muzzleLight.transform.position = tracerStart;
            muzzleLight.enabled = true;
            Game.Fx.MuzzleSmoke(tracerStart, aim);
            if (d.EjectsShells)
            {
                Vector3 port = origin + (vm.EjectPort.position - origin).normalized * 0.32f;
                Vector3 vel = cam.right * Random.Range(1.8f, 2.6f) + cam.up * Random.Range(1.2f, 1.8f) + cam.forward * 0.4f + rig.Velocity;
                Game.Fx.EjectShell(port, vel, d.Id == WeaponId.Pistol);
            }
            if (d.Id == WeaponId.Shotgun) pumpSoundAt = now + 0.28f;
        }

        void UpdateBloom(InputState input, float dt)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                WeaponSlot s = Slots[i];
                WeaponDef d = s.Def;
                if (d.BloomPerShot <= 0f) continue;
                bool firing = i == CurrentIndex && input.FireHeld && Time.time - lastShotTime < d.Interval * 1.5f;
                // S-09: recovers from the 4 deg ceiling back to 0.8 deg in 0.5 s once firing stops.
                if (!firing) s.Bloom = Mathf.MoveTowards(s.Bloom, d.SpreadHip, (d.SpreadMax - d.SpreadHip) / d.BloomRecoverTime * dt);
            }
        }

        void UpdateMuzzle(float dt)
        {
            if (muzzleTimer <= 0f) return;
            muzzleTimer -= dt;
            if (muzzleTimer <= 0f)
            {
                muzzleLight.enabled = false;
                for (int i = 0; i < models.Length; i++) models[i].SetFlash(false);
            }
        }

        // ---------------- reload ----------------

        void TryStartReload()
        {
            WeaponSlot s = Current;
            if (IsReloading || IsSwitching || !s.CanReload) return;
            IsReloading = true;
            reloadTimer = 0f;
            magInPlayed = false;
            if (s.Def.ShellByShell)
            {
                reloadDuration = s.Def.ShellTime;
            }
            else
            {
                reloadDuration = s.Def.ReloadTime;
                Game.Audio.Play2D(SoundId.MagOut, 0.8f);
            }
        }

        void UpdateReload(float dt)
        {
            if (!IsReloading) return;
            WeaponSlot s = Current;
            reloadTimer += dt;
            if (s.Def.ShellByShell)
            {
                if (reloadTimer < s.Def.ShellTime) return;
                reloadTimer -= s.Def.ShellTime;
                s.Mag++;
                s.Reserve--;
                anim.OnShellInserted();
                Game.Audio.Play2D(SoundId.ShellInsert, 0.8f, Random.Range(0.95f, 1.05f));
                if (s.Mag >= s.Def.MagSize || s.Reserve <= 0) IsReloading = false;
                return;
            }
            float p = reloadTimer / reloadDuration;
            if (!magInPlayed && p >= 0.62f)
            {
                magInPlayed = true;
                Game.Audio.Play2D(SoundId.MagIn, 0.85f);
            }
            if (p >= 1f)
            {
                int need = s.Def.MagSize - s.Mag;
                int take = s.Def.InfiniteReserve ? need : Mathf.Min(need, s.Reserve);
                s.Mag += take;
                if (!s.Def.InfiniteReserve) s.Reserve -= take;
                IsReloading = false;
                Game.Audio.Play2D(SoundId.BoltRack, 0.8f);
            }
        }

        // ---------------- switching ----------------

        void BeginSwitch(int target)
        {
            IsReloading = false;
            switchTarget = target;
            switchTimer = GameConfig.Weapons.SwitchTime;
            swapped = false;
            Game.Audio.Play2D(SoundId.WeaponSwitch, 0.6f);
        }

        void UpdateSwitch(float dt)
        {
            if (switchTimer <= 0f) return;
            switchTimer -= dt;
            if (!swapped && switchTimer <= GameConfig.Weapons.SwitchTime * 0.5f)
            {
                // Halfway: the old weapon is fully lowered, swap models and raise the new one.
                swapped = true;
                CurrentIndex = switchTarget;
                anim.SetActive(CurrentIndex);
            }
            if (switchTimer <= 0f) switchTimer = 0f;
        }
    }
}
