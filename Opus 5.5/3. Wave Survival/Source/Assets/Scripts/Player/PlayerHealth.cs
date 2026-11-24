using UnityEngine;

namespace WaveSurvival
{
    /// <summary>S-06 / S-07: 100 HP, no regeneration, damage feedback and death.</summary>
    public sealed class PlayerHealth : MonoBehaviour
    {
        public float Hp { get; private set; } = GameConfig.Player.MaxHealth;
        public bool Alive => Hp > 0f;

        PlayerRig rig;

        public void Init(PlayerRig owner) => rig = owner;

        /// <summary>Applies damage from an attacker at <paramref name="sourcePos"/> (used for the HUD direction arcs).</summary>
        public void Damage(float amount, Vector3 sourcePos)
        {
            if (!Alive || !Game.IsPlaying || amount <= 0f) return;
            // Feedback first (also in god mode so the cheat still shows what hit you).
            Game.Hud?.OnPlayerDamaged(sourcePos, amount);
            rig.View.Flinch(sourcePos);
            Game.Audio?.Play2D(SoundId.PlayerHurt, 0.8f, Random.Range(0.92f, 1.08f));
            if (Game.GodMode) return;

            Hp = Mathf.Max(0f, Hp - amount);
            Game.Stats.DamageTaken += amount;
            if (Hp <= 0f) Game.Manager.OnPlayerDied();
        }

        /// <summary>Returns false when already at full health (the pickup then stays on the floor).</summary>
        public bool Heal(float amount)
        {
            if (!Alive || Hp >= GameConfig.Player.MaxHealth) return false;
            Hp = Mathf.Min(GameConfig.Player.MaxHealth, Hp + amount);
            return true;
        }
    }
}
