using UnityEngine;

namespace DestructionSandbox
{
    /// <summary>Runs after every other FixedUpdate: marks the start of the physics simulation of this step.</summary>
    [DefaultExecutionOrder(32000)]
    public sealed class PhysicsStepTimer : MonoBehaviour
    {
        private void FixedUpdate() => PerfProbe.MarkStepStart();
    }
}
