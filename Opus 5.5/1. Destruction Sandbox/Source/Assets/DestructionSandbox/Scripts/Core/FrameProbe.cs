using UnityEngine;

namespace DestructionSandbox
{
    /// <summary>Runs before every other Update: closes the frame's counters.</summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class FrameProbe : MonoBehaviour
    {
        private void Update() => PerfProbe.EndFrame();
    }
}
