using UnityEngine;
using S = DestructionSandbox.GameConfig.Score;

namespace DestructionSandbox
{
    /// <summary>
    /// D-22: points per destroyed block (stone 10, wood 6, glass 3) times a combo multiplier x1..x5.
    /// The multiplier is 1 + (blocks destroyed in the last 2 s) / 10 (it only ever rises while destruction
    /// continues) and falls back to x1 after 2 s without any destruction. Uses game time (slow motion aware).
    /// </summary>
    public sealed class ScoreSystem : MonoBehaviour
    {
        private readonly float[] history = new float[S.DestructionHistory];   // ring buffer of destruction times
        private int historyStart;
        private int historyCount;
        private float lastDestruction = -100f;

        public int Score { get; private set; }
        public int Combo { get; private set; } = 1;
        public int MaxComboReached { get; private set; } = 1;
        public int DestroyedThisSession { get; private set; }

        public void ResetCombo()
        {
            Combo = 1;
            historyStart = 0;
            historyCount = 0;
        }

        public void OnBlockDestroyed(BlockMaterialType material, Vector3 position)
        {
            float now = Time.time;
            Prune(now);
            if (historyCount == history.Length) { historyStart = (historyStart + 1) % history.Length; historyCount--; }
            history[(historyStart + historyCount) % history.Length] = now;
            historyCount++;
            lastDestruction = now;

            int fromWindow = Mathf.Min(S.MaxCombo, 1 + historyCount / S.BlocksPerComboStep);
            if (fromWindow > Combo) Combo = fromWindow;
            if (Combo > MaxComboReached) MaxComboReached = Combo;

            int points = BlockMaterials.Points(material) * Combo;
            Score += points;
            DestroyedThisSession++;
            Game.Popups.Show(position, points, Combo);
        }

        private void Prune(float now)
        {
            while (historyCount > 0 && now - history[historyStart] > S.ComboWindow)
            {
                historyStart = (historyStart + 1) % history.Length;
                historyCount--;
            }
        }

        private void Update()
        {
            float now = Time.time;
            Prune(now);
            if (Combo > 1 && now - lastDestruction > S.ComboDecayTime) Combo = 1;
        }
    }
}
