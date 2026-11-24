using UnityEngine;

namespace DestructionRange
{
    public sealed partial class Sandbox
    {
        RangeHud hud;
        internal RangeHud Hud => hud;

        partial void SetUpInterface() => hud = new RangeHud(this);
        partial void ResetInterface() => hud.Reset();
        partial void TickInterface() => hud.Tick();
        partial void PauseInterface(bool value) => hud.Pause(value);
        partial void ScorePop(Vector3 point, int score) => hud.Pop(point, score);

        void OnDestroy()
        {
            hud?.Dispose();
            Time.timeScale = 1;
            Time.fixedDeltaTime = SandboxConfig.FixedStep;
        }
    }
}
