using UnityEngine;
using T = DestructionSandbox.GameConfig.Timing;

namespace DestructionSandbox
{
    /// <summary>
    /// D-20: T toggles slow motion. timeScale 0.25 and the fixed timestep is scaled by the same factor, so the
    /// physics still runs 60 steps per real second (smooth slow motion instead of 15 Hz stepping).
    /// Audio pitch is lowered. Pause (timeScale 0) is layered on top without losing the slow-motion state.
    /// </summary>
    public sealed class SlowMotion : MonoBehaviour
    {
        public bool Active { get; private set; }

        public float WorldTimeScale => Active ? T.SlowMotionTimeScale : 1f;

        public void Toggle()
        {
            Set(!Active);
        }

        public void Set(bool on)
        {
            Active = on;
            Apply();
            Game.Audio.SetPitchScale(on ? T.SlowMotionAudioPitch : 1f);
        }

        /// <summary>Re-applies the time settings (also used when leaving the pause menu).</summary>
        public void Apply()
        {
            float scale = WorldTimeScale;
            Time.fixedDeltaTime = T.FixedTimestep * scale;
            Time.maximumDeltaTime = T.MaximumDeltaTime * scale;   // still at most 2 physics steps per frame
            if (!Game.IsPaused) Time.timeScale = scale;
        }
    }
}
