using UnityEngine;
using Wpn = DestructionSandbox.GameConfig.Weapons;
using Layers = DestructionSandbox.GameConfig.Layers;

namespace DestructionSandbox
{
    /// <summary>
    /// D-16..D-19 weapon logic: selection (1/2/3, wheel), cooldowns, shell ammo, aiming and firing.
    /// Projectiles leave the muzzle toward the point under the crosshair (parallax-corrected aim ray).
    /// Cooldowns run on unscaled time so firing rate is the same in slow motion.
    /// </summary>
    public sealed class WeaponController : MonoBehaviour
    {
        private readonly float[] cooldownLeft = new float[Wpn.WeaponCount];
        private CannonView view;

        public WeaponType Current { get; private set; } = WeaponType.Cannonball;
        public int ShellAmmo { get; private set; } = Wpn.ShellAmmo;
        public int ShotsFired { get; private set; }

        public void Init(CannonView cannon)
        {
            view = cannon;
        }

        public static string DisplayName(WeaponType t)
        {
            switch (t)
            {
                case WeaponType.Shell: return "EXPLOSIVE SHELL";
                case WeaponType.Orb: return "GRAVITY ORB";
                default: return "CANNONBALL";
            }
        }

        public static float Cooldown(WeaponType t)
        {
            switch (t)
            {
                case WeaponType.Shell: return Wpn.ShellCooldown;
                case WeaponType.Orb: return Wpn.OrbCooldown;
                default: return Wpn.CannonballCooldown;
            }
        }

        public static float Speed(WeaponType t)
        {
            switch (t)
            {
                case WeaponType.Shell: return Wpn.ShellSpeed;
                case WeaponType.Orb: return Wpn.OrbSpeed;
                default: return Wpn.CannonballSpeed;
            }
        }

        /// <summary>0 = ready, 1 = just fired.</summary>
        public float CooldownFraction(WeaponType t) => Mathf.Clamp01(cooldownLeft[(int)t] / Cooldown(t));

        public bool HasAmmo(WeaponType t) => t != WeaponType.Shell || ShellAmmo > 0;

        public void Select(WeaponType t)
        {
            if (t == Current) return;
            Current = t;
            view.SetAmmoColor(t);
            Game.Audio.PlayUiClick();
        }

        public void Cycle(int direction)
        {
            int next = ((int)Current + direction + Wpn.WeaponCount) % Wpn.WeaponCount;
            Select((WeaponType)next);
        }

        public void Refill()
        {
            ShellAmmo = Wpn.ShellAmmo;
        }

        public void ResetCooldowns()
        {
            for (int i = 0; i < cooldownLeft.Length; i++) cooldownLeft[i] = 0f;
        }

        public bool TryFire()
        {
            WeaponType type = Current;
            if (cooldownLeft[(int)type] > 0f || !HasAmmo(type)) return false;

            Ray aim = Game.Player.AimRay();
            Vector3 target = Physics.Raycast(aim, out RaycastHit hit, Wpn.AimMaxDistance, Layers.AimMask, QueryTriggerInteraction.Ignore)
                ? hit.point : aim.GetPoint(Wpn.AimMaxDistance);
            Vector3 muzzle = view.MuzzlePosition;
            Vector3 dir = target - muzzle;
            // Very close targets would make the shot cross the view; fall back to the camera direction.
            dir = dir.sqrMagnitude < 4f || Vector3.Dot(dir.normalized, aim.direction) < 0.7f ? aim.direction : dir.normalized;

            Game.Projectiles.Spawn(type, muzzle, dir * Speed(type));
            cooldownLeft[(int)type] = Cooldown(type);
            if (type == WeaponType.Shell) ShellAmmo--;
            ShotsFired++;
            view.PlayFire();
            Game.Audio.PlayShot(type, muzzle);
            return true;
        }

        private void Update()
        {
            if (Game.IsPaused) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < cooldownLeft.Length; i++)
                if (cooldownLeft[i] > 0f) cooldownLeft[i] = Mathf.Max(0f, cooldownLeft[i] - dt);
        }
    }
}
