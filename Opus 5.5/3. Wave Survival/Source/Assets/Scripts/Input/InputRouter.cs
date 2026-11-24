using UnityEngine;
using UnityEngine.InputSystem;

namespace WaveSurvival
{
    /// <summary>One frame of gameplay input (the debug/menu keys are read directly by their systems).</summary>
    public struct InputState
    {
        public Vector2 Move;
        public Vector2 Look;          // mouse counts this frame
        public bool FireHeld, FirePressed, AimHeld, ReloadPressed, JumpPressed, SprintHeld, CrouchHeld;
        public int SelectWeapon;      // -1 = none, 0..2 = slot
        public float Scroll;
    }

    /// <summary>Source of gameplay input: the real keyboard/mouse, or the test bot (AutoTest).</summary>
    public interface IInputSource
    {
        void Read(ref InputState state);
    }

    /// <summary>Keyboard + mouse through the Input System package (the project uses the new input backend only).</summary>
    public sealed class HardwareInput : IInputSource
    {
        public void Read(ref InputState s)
        {
            s = default;
            s.SelectWeapon = -1;
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            s.Move = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            s.Look = mouse.delta.ReadValue();
            s.FireHeld = mouse.leftButton.isPressed;
            s.FirePressed = mouse.leftButton.wasPressedThisFrame;
            s.AimHeld = mouse.rightButton.isPressed;
            s.ReloadPressed = kb.rKey.wasPressedThisFrame;
            s.JumpPressed = kb.spaceKey.wasPressedThisFrame;
            s.SprintHeld = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            s.CrouchHeld = kb.cKey.isPressed;
            if (kb.digit1Key.wasPressedThisFrame) s.SelectWeapon = 0;
            else if (kb.digit2Key.wasPressedThisFrame) s.SelectWeapon = 1;
            else if (kb.digit3Key.wasPressedThisFrame) s.SelectWeapon = 2;
            s.Scroll = mouse.scroll.ReadValue().y;
        }
    }

    /// <summary>Polls the active input source once per frame before any gameplay script runs.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class InputRouter : MonoBehaviour
    {
        public static InputState State;
        static IInputSource source;
        static readonly HardwareInput hardware = new HardwareInput();

        public static IInputSource Source
        {
            get => source ?? hardware;
            set => source = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { State = default; source = null; }

        void Update()
        {
            if (Game.IsPlaying)
            {
                Source.Read(ref State);
            }
            else
            {
                State = default;
                State.SelectWeapon = -1;
            }
        }
    }
}
