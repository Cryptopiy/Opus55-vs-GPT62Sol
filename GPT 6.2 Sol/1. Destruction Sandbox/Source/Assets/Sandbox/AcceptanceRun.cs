using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using static DestructionRange.SandboxConfig;

namespace DestructionRange
{
    internal sealed class AcceptanceRun
    {
        readonly Sandbox game;
        readonly Keyboard keyboard;
        readonly Mouse mouse;
        bool passed = true;
        int peakShards;
        float maxSpeed;
        int invalid;
        int heavyFrames, peakCombo = 1, peakVoices;
        float heavySeconds, heavyFrameMax, measuredFps;
        bool rendered => !Application.isBatchMode;

        public AcceptanceRun(Sandbox game)
        {
            this.game = game;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            keyboard = InputSystem.AddDevice<Keyboard>("Sandbox test keyboard");
            mouse = InputSystem.AddDevice<Mouse>("Sandbox test mouse");
            game.ProbeKeyboard = keyboard;
            game.ProbeMouse = mouse;
        }

        public IEnumerator Execute(string mode)
        {
            float started = Time.unscaledTime;
            Debug.Log($"CHECK startup resolution={Screen.width}x{Screen.height} vsync={QualitySettings.vSyncCount} fixed={Time.fixedDeltaTime:F6} batch={Application.isBatchMode}");
            if (mode == "endurance")
                yield return Endurance();
            else if (mode == "stable-arch" || mode == "stable-pyramid")
            {
                yield return KeyPress(mode == "stable-arch" ? Key.F6 : Key.F7);
                yield return Stand(StandTime);
            }
            else
            {
                int defaultGraphics = VisibleGraphics().Length;
                Check($"U-01 visible at startup graphics={defaultGraphics}", defaultGraphics > 0);
                yield return Stand(mode == "acceptance" ? StandTime : SettleLimit);
                yield return Cannon();
                yield return Shell();
                yield return Orb();
                yield return PyramidAndReset();
                yield return InterfaceVisibility();
            }
            if (rendered && mode is "acceptance" or "systems")
            {
                int expectedMask = (1 << System.Enum.GetValues(typeof(Sound)).Length) - 1;
                Check($"audio startedMask={game.StartedSounds} livePeak={peakVoices} capacity={VoiceCap}",
                    game.StartedSounds == expectedMask && peakVoices > 0 && peakVoices <= VoiceCap);
            }
            Debug.Log($"CHECK RESULT {mode} pass={passed} seconds={Time.unscaledTime - started:F2} peakShards={peakShards} maxSpeed={maxSpeed:F2} invalid={invalid}");
            if (rendered && !Application.isEditor && passed && mode is "acceptance" or "systems")
            {
                game.SetPaused(true);
                game.Hud.TestButton(2).onClick.Invoke();
            }
            else SandboxProbe.Exit(passed ? 0 : 1);
        }

        IEnumerator Stand(float seconds)
        {
            float stop = Time.unscaledTime + seconds;
            float maxDrift = 0, maxSettledSpeed = 0;
            int minSleeping = game.Pieces.Total;
            while (Time.unscaledTime < stop)
            {
                game.Pieces.Inspect(out float drift, out float speed, out int bad);
                maxDrift = Mathf.Max(maxDrift, drift);
                invalid = Mathf.Max(invalid, bad);
                if (Time.unscaledTime >= stop - seconds + SettleLimit)
                    maxSettledSpeed = Mathf.Max(maxSettledSpeed, speed);
                minSleeping = Mathf.Min(minSleeping, game.Pieces.Sleeping);
                yield return new WaitForSecondsRealtime(.25f);
            }
            bool success = minSleeping == game.Pieces.Total && maxDrift < RebuildTolerance && maxSettledSpeed < SleepSpeed;
            Check($"stand layout={game.Current} seconds={seconds} total={game.Pieces.Total} sleeping={minSleeping} drift={maxDrift:F5} speed={maxSettledSpeed:F4}", success);
            if (rendered && game.Current == Layout.Tower)
            {
                yield return KeyPress(Key.F12);
                yield return new WaitForSecondsRealtime(.6f);
            }
        }

        IEnumerator Cannon()
        {
            yield return KeyPress(Key.Digit1);
            yield return KeyPress(Key.F1);
            Vector2 meter = Vector2.zero;
            int frames = 0;
            float elapsed = 0;
            for (int shot = 0; shot < AcceptanceCannonShots; shot++)
            {
                float x = Mathf.Lerp(-1.45f, 1.45f, shot / (float)(AcceptanceCannonShots - 1));
                game.Player.Aim(new Vector3(x, 3.15f, -1.9f));
                yield return Fire();
                float end = Time.time + Weapons[(int)Weapon.Cannon].Cooldown;
                while (Time.time < end)
                {
                    AccumulateFrame(ref meter, ref frames, ref elapsed);
                    yield return null;
                }
            }
            float settle = Time.time + 7f;
            while (Time.time < settle)
            {
                AccumulateFrame(ref meter, ref frames, ref elapsed);
                yield return null;
            }
            int wood = game.Pieces.Fractured(Matter.Wood);
            int glass = game.Pieces.Fractured(Matter.Glass);
            float fps = frames / Mathf.Max(.001f, elapsed);
            float loadedFps = heavyFrames / Mathf.Max(.001f, heavySeconds);
            Check($"cannon fired={game.Guns.Fired} destroyed={game.Run.Destroyed}/{game.Pieces.Total} woodFractures={wood} glassFractures={glass} score={game.Run.Score} fps={fps:F1} loadedFps={loadedFps:F1} loadedFrames={heavyFrames} loadedMaxFrame={heavyFrameMax:F2}ms maxFrame={meter.x:F2}ms peakShards={peakShards} evictions={game.Pieces.ShardEvictions}",
                game.Guns.Fired == AcceptanceCannonShots && game.Run.Destroyed > game.Pieces.Total / 2 && wood > 0 && glass > 0);
            Check($"combo peak={peakCombo} afterCollapse={game.Run.Combo}", peakCombo == ComboMax && game.Run.Combo == 1);
            if (rendered)
            {
                yield return KeyPress(Key.F12);
                yield return new WaitForSecondsRealtime(.6f);
            }
        }

        IEnumerator Shell()
        {
            yield return KeyPress(Key.R);
            yield return KeyPress(Key.Digit2);
            game.Player.Aim(new Vector3(0, 6, -2));
            yield return Fire();
            Vector2 meter = Vector2.zero;
            int frames = 0;
            float elapsed = 0;
            bool captured = false;
            float waveStarted = -1;
            float end = Time.time + 3.5f;
            while (Time.time < end)
            {
                AccumulateFrame(ref meter, ref frames, ref elapsed);
                if (waveStarted < 0 && game.ActiveWaves > 0 && game.Pieces.ActiveShards > 0)
                    waveStarted = Time.unscaledTime;
                if (rendered && !captured && waveStarted >= 0 && Time.unscaledTime - waveStarted >= .18f)
                {
                    captured = true;
                    yield return KeyPress(Key.F12);
                }
                yield return null;
            }
            Check($"shell hits={game.Guns.Detonations} ammo={game.Guns.Ammo} destroyed={game.Run.Destroyed} maxFrame={meter.x:F2}ms",
                game.Guns.Detonations == 1 && game.Guns.Ammo == Weapons[(int)Weapon.Shell].Ammo - 1 &&
                game.Run.Destroyed > 0 && (!rendered || meter.x <= StutterLimitMs));
        }

        IEnumerator Orb()
        {
            yield return KeyPress(Key.F6);
            yield return KeyPress(Key.Digit3);
            game.Player.Aim(new Vector3(4.8f, 2.1f, 0));
            yield return Fire();
            float end = Time.time + 6.5f;
            float pulledDrift = 0;
            bool captured = false;
            while (Time.time < end)
            {
                Observe(out float drift);
                if (game.Guns.Anchors > 0 && game.Guns.Releases == 0)
                {
                    pulledDrift = Mathf.Max(pulledDrift, drift);
                    if (rendered && !captured && drift > .3f)
                    {
                        captured = true;
                        yield return KeyPress(Key.F12);
                    }
                }
                yield return null;
            }
            Check($"orb layout={game.Current} blocks={game.Pieces.Total} anchors={game.Guns.Anchors} releases={game.Guns.Releases} pulledDrift={pulledDrift:F2} destroyed={game.Run.Destroyed}",
                game.Current == Layout.Arch && game.Guns.Anchors == 1 && game.Guns.Releases == 1 && pulledDrift > .1f);
        }

        IEnumerator PyramidAndReset()
        {
            yield return KeyPress(Key.F7);
            yield return KeyPress(Key.T);
            bool slow = game.Slow && Mathf.Approximately(Time.timeScale, SlowScale) &&
                Mathf.Approximately(Time.fixedDeltaTime, FixedStep * SlowScale);
            yield return KeyPress(Key.Digit1);
            game.Player.Aim(new Vector3(0, 1.9f, -5));
            yield return Fire();
            yield return new WaitForSecondsRealtime(3.2f);
            Observe(out _);
            Check($"pyramid layout={game.Current} blocks={game.Pieces.Total} slow={slow} projectile={game.Guns.Active} destroyed={game.Run.Destroyed}",
                game.Current == Layout.Pyramid && game.Pieces.Total == BlockCapacity && slow && game.Guns.Active > 0);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.X));
            yield return null;
            // Check the clear in the input frame, before ongoing collapse can spawn fresh shards.
            Check($"clear shards={game.Pieces.ActiveShards} blocks={game.Pieces.ActiveBlocks}", game.Pieces.ActiveShards == 0);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return KeyPress(Key.T);
            yield return KeyPress(Key.R);
            bool stable = game.DebugVisible;
            int children = game.transform.childCount;
            yield return MeasureFps();
            float fpsBefore = measuredFps;
            for (int i = 0; i < BenchResets; i++)
            {
                yield return KeyPress(Key.R);
                stable &= game.Pieces.Total == BlockCapacity && game.Pieces.ActiveBlocks == BlockCapacity &&
                    game.Pieces.ActiveShards == 0 && game.Guns.Active == 0 && game.Run.Destroyed == 0 &&
                    game.transform.childCount == children;
            }
            yield return MeasureFps();
            Check($"reset count={BenchResets} blocks={game.Pieces.ActiveBlocks} shardCount={game.Pieces.ActiveShards} children={children} fpsBefore={fpsBefore:F1} fpsAfter={measuredFps:F1}", stable && measuredFps >= .85f * fpsBefore);
            yield return KeyPress(Key.Escape);
            Check($"pause paused={game.Paused} scale={Time.timeScale}", game.Paused && Time.timeScale == 0);
            if (rendered) yield return KeyPress(Key.F12);
            game.Hud.TestSlider(0).value = 1.45f;
            game.Hud.TestSlider(1).value = .55f;
            Check($"pause sliders sensitivity={game.Player.Sensitivity:F2} volume={game.MasterGain:F2}",
                Mathf.Approximately(game.Player.Sensitivity, 1.45f) && Mathf.Approximately(game.MasterGain, .55f));
            game.Hud.TestButton(0).onClick.Invoke();
            yield return null;
            Check($"resume paused={game.Paused} scale={Time.timeScale}", !game.Paused && Time.timeScale == 1);
            yield return KeyPress(Key.Escape);
            game.Hud.TestButton(1).onClick.Invoke();
            Check($"pause restart paused={game.Paused} count={game.Pieces.ActiveBlocks}", !game.Paused && game.Pieces.ActiveBlocks == BlockCapacity);
            Check($"limits peakShards={peakShards} speed={maxSpeed:F2} invalid={invalid}", peakShards <= ShardCapacity && maxSpeed <= TopSpeed + .1f && invalid == 0);
        }

        IEnumerator KeyPress(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        IEnumerator InterfaceVisibility()
        {
            yield return KeyPress(Key.T);
            Graphic[] before = VisibleGraphics();
            if (rendered)
            {
                yield return KeyPress(Key.F12);
                yield return new WaitForSecondsRealtime(.35f);
            }

            float elapsed = game.Run.Elapsed;
            int score = game.Run.Score;
            yield return KeyPress(Key.F8);
            int hidden = VisibleGraphics().Length;
            Check($"U-02 hidden graphics={hidden}/{before.Length}", hidden == 0 && before.Length > 0);
            Check("U-03 F8 keeps the clock running and preserves score",
                game.Run.Elapsed > elapsed && game.Run.Score == score && !game.Paused &&
                Time.timeScale == SlowScale && Time.fixedDeltaTime == FixedStep * SlowScale);
            if (rendered)
            {
                yield return KeyPress(Key.F12);
                yield return new WaitForSecondsRealtime(.35f);
            }

            yield return KeyPress(Key.F1);
            bool debugOff = !game.DebugVisible && VisibleGraphics().Length == 0;
            yield return KeyPress(Key.F1);
            bool debugStaysHidden = debugOff && game.DebugVisible && VisibleGraphics().Length == 0;
            yield return KeyPress(Key.F6);
            bool layoutHidden = game.Current == Layout.Arch && VisibleGraphics().Length == 0;
            yield return KeyPress(Key.Digit2);
            yield return Fire();
            bool wasInFlight = game.Guns.Active > 0;
            yield return KeyPress(Key.R);
            bool restartHidden = wasInFlight && game.Guns.Active == 0 && game.Guns.Fired == 0 &&
                game.Guns.Ammo == Weapons[(int)Weapon.Shell].Ammo && VisibleGraphics().Length == 0;

            yield return KeyPress(Key.Escape);
            Graphic[] pauseGraphics = VisibleGraphics();
            bool pauseUsable = game.Paused && pauseGraphics.Length > 0 &&
                game.Hud.TestButton(0).isActiveAndEnabled && Array.TrueForAll(before, graphic => !graphic.isActiveAndEnabled);
            yield return KeyPress(Key.F8);
            pauseUsable &= game.Paused && Array.TrueForAll(pauseGraphics, IsVisible);
            yield return KeyPress(Key.F8);
            pauseUsable &= game.Paused && VisibleGraphics().Length == pauseGraphics.Length;
            game.Hud.TestButton(0).onClick.Invoke();
            yield return null;
            Check("U-02 pause remains usable with either setting", pauseUsable && !game.Paused && VisibleGraphics().Length == 0);

            game.Player.Aim(new Vector3(4.8f, 2.1f, 0));
            yield return Fire();
            float deadline = Time.time + ShellFuse + .5f;
            while (game.Run.Score == 0 && Time.time < deadline) yield return null;
            Text[] newPopups = Array.FindAll(game.GetComponentsInChildren<Text>(true),
                item => item.gameObject.activeSelf && item.text.StartsWith("+", StringComparison.Ordinal));
            Check($"U-03 hotkeys and scoring while hidden score={game.Run.Score} ammo={game.Guns.Ammo}",
                debugStaysHidden && layoutHidden && restartHidden && game.Run.Score > 0 &&
                game.Guns.Ammo == Weapons[(int)Weapon.Shell].Ammo - 1);
            Check($"U-04 hidden after rebuild, respawn and new popups={newPopups.Length}",
                layoutHidden && restartHidden && newPopups.Length > 0 && VisibleGraphics().Length == 0);

            yield return KeyPress(Key.F8);
            int restored = Array.FindAll(before, IsVisible).Length;
            bool popupReturned = Array.Exists(newPopups, IsVisible);
            Check($"U-06 restored graphics={restored}/{before.Length} popupReturned={popupReturned}",
                before.Length > 0 && restored == before.Length && popupReturned);
            yield return KeyPress(Key.T);
        }

        Graphic[] VisibleGraphics() => Array.FindAll(game.GetComponentsInChildren<Graphic>(), IsVisible);

        static bool IsVisible(Graphic graphic) =>
            graphic.isActiveAndEnabled && graphic.canvas != null && graphic.canvas.isActiveAndEnabled;

        IEnumerator Endurance()
        {
            float start = Time.unscaledTime;
            int cycles = 0;
            int shots = 0;
            var keys = new[] { Key.F5, Key.F6, Key.F7 };
            var weapons = new[] { Key.Digit2, Key.Digit3, Key.Digit1 };
            var aims = new[] { new Vector3(0, 6, -2), new Vector3(4.8f, 2.1f, 0), new Vector3(0, 3, -4.5f) };
            while (Time.unscaledTime < start + EnduranceTime - .2f)
            {
                int layout = cycles % keys.Length;
                yield return KeyPress(keys[layout]);
                yield return KeyPress(weapons[layout]);
                game.Player.Aim(aims[layout]);
                yield return Fire();
                shots += game.Guns.Fired;
                float next = Mathf.Min(start + EnduranceTime, Time.unscaledTime + 7.5f);
                while (Time.unscaledTime < next)
                {
                    Observe(out _);
                    yield return null;
                }
                cycles++;
            }
            Check($"endurance seconds={Time.unscaledTime - start:F2} cycles={cycles} shots={shots} invalid={invalid}", invalid == 0 && cycles >= 10 && shots == cycles);
        }

        IEnumerator Fire()
        {
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
        }

        IEnumerator MeasureFps()
        {
            yield return new WaitForSecondsRealtime(.15f);
            float start = Time.unscaledTime;
            const int samples = 60;
            for (int i = 0; i < samples; i++) yield return null;
            measuredFps = samples / Mathf.Max(.00001f, Time.unscaledTime - start);
        }

        void AccumulateFrame(ref Vector2 meter, ref int frames, ref float elapsed)
        {
            float dt = Time.unscaledDeltaTime;
            meter.x = Mathf.Max(meter.x, dt * 1000);
            frames++;
            elapsed += dt;
            if (game.Pieces.ActiveShards >= ShardCapacity * 2 / 3)
            {
                heavyFrames++;
                heavySeconds += dt;
                heavyFrameMax = Mathf.Max(heavyFrameMax, dt * 1000);
            }
            Observe(out _);
        }

        void Observe(out float drift)
        {
            game.Pieces.Inspect(out drift, out float speed, out int bad);
            invalid = Mathf.Max(invalid, bad);
            maxSpeed = Mathf.Max(maxSpeed, speed);
            peakShards = Mathf.Max(peakShards, game.Pieces.ActiveShards);
            peakCombo = Mathf.Max(peakCombo, game.Run.Combo);
            if (rendered) peakVoices = Mathf.Max(peakVoices, game.PlayingVoices);
        }

        void Check(string label, bool success)
        {
            passed &= success;
            Debug.Log($"CHECK {label} pass={success}");
        }
    }
}
