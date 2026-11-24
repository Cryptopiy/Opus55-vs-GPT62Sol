using UnityEngine;
using UnityEngine.InputSystem;

namespace LivingIsland
{
    /// <summary>
    /// Thin wrapper over the Input System devices (the project uses the new Input System only).
    /// Hotkeys are suppressed while a UI text field has focus; the automated test can inject movement.
    /// </summary>
    public static class GameInput
    {
        public static bool TextEntryActive;
        public static Vector2 ScriptedMove;      // used by AutoTest; zero when a human plays
        public static bool ScriptedRun;
        public static bool ScriptedJump;

        static Keyboard Kb => Keyboard.current;
        static Mouse M => Mouse.current;

        public static bool Down(Key key) => !TextEntryActive && Kb != null && Kb[key].wasPressedThisFrame;
        public static bool Held(Key key) => !TextEntryActive && Kb != null && Kb[key].isPressed;

        /// <summary>Like <see cref="Down"/>, but also while a text field has focus (the F8 UI switch works at any moment).</summary>
        public static bool DownAlways(Key key) => Kb != null && Kb[key].wasPressedThisFrame;

        public static Vector2 MoveAxis
        {
            get
            {
                if (ScriptedMove != Vector2.zero) return Vector2.ClampMagnitude(ScriptedMove, 1f);
                Vector2 v = Vector2.zero;
                if (Held(Key.W)) v.y += 1f;
                if (Held(Key.S)) v.y -= 1f;
                if (Held(Key.D)) v.x += 1f;
                if (Held(Key.A)) v.x -= 1f;
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        public static bool Run => ScriptedRun || Held(Key.LeftShift) || Held(Key.RightShift);
        public static bool Jump => ScriptedJump || Down(Key.Space);

        public static Vector2 MouseDelta => M != null ? M.delta.ReadValue() : Vector2.zero;
        public static bool RightMouseHeld => M != null && M.rightButton.isPressed;
        public static float Scroll => M != null ? M.scroll.ReadValue().y : 0f;
    }
}
