using UnityEngine;
using UnityEngine.InputSystem;

namespace DestructionSandbox
{
    /// <summary>
    /// Polls keyboard and mouse once per frame (Input System package; the legacy Input Manager is disabled in
    /// this project). Other systems read the resulting state instead of touching devices directly.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class GameInput : MonoBehaviour
    {
        public Vector2 Move { get; private set; }
        public bool Sprint { get; private set; }
        public bool JumpPressed { get; private set; }
        public Vector2 LookDelta { get; private set; }
        public bool FireHeld { get; private set; }
        public int WeaponSelect { get; private set; } = -1;
        public int WeaponScroll { get; private set; }
        public bool ToggleOverlay { get; private set; }
        public bool ToggleUi { get; private set; }
        public bool Screenshot { get; private set; }
        public bool TogglePause { get; private set; }
        public int SelectStructure { get; private set; } = -1;
        public bool ToggleSlowMotion { get; private set; }
        public bool ResetStructure { get; private set; }
        public bool ClearDebris { get; private set; }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;

            Move = Vector2.zero;
            Sprint = JumpPressed = FireHeld = false;
            LookDelta = Vector2.zero;
            WeaponSelect = -1;
            WeaponScroll = 0;
            ToggleOverlay = ToggleUi = Screenshot = TogglePause = ToggleSlowMotion = ResetStructure = ClearDebris = false;
            SelectStructure = -1;

            if (kb != null)
            {
                float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
                float y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
                Move = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
                Sprint = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                JumpPressed = kb.spaceKey.wasPressedThisFrame;
                if (kb.digit1Key.wasPressedThisFrame) WeaponSelect = 0;
                if (kb.digit2Key.wasPressedThisFrame) WeaponSelect = 1;
                if (kb.digit3Key.wasPressedThisFrame) WeaponSelect = 2;
                ToggleOverlay = kb.f1Key.wasPressedThisFrame;
                ToggleUi = kb.f8Key.wasPressedThisFrame;
                Screenshot = kb.f12Key.wasPressedThisFrame;
                TogglePause = kb.escapeKey.wasPressedThisFrame;
                if (kb.f5Key.wasPressedThisFrame) SelectStructure = 0;
                if (kb.f6Key.wasPressedThisFrame) SelectStructure = 1;
                if (kb.f7Key.wasPressedThisFrame) SelectStructure = 2;
                ToggleSlowMotion = kb.tKey.wasPressedThisFrame;
                ResetStructure = kb.rKey.wasPressedThisFrame;
                ClearDebris = kb.xKey.wasPressedThisFrame;
            }

            if (mouse != null)
            {
                LookDelta = mouse.delta.ReadValue();
                FireHeld = mouse.leftButton.isPressed;
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0.01f) WeaponScroll = -1;
                else if (scroll < -0.01f) WeaponScroll = 1;
            }
        }
    }
}
