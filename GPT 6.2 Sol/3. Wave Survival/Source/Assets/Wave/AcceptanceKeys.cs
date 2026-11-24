using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace WaveSurvival
{
    internal sealed partial class Acceptance
    {
        Keyboard testKeys;
        Mouse testMouse;
        int savedScreenshots;
        static bool restarted;
        Graphic[] originalUi;
        Vector3 hiddenPosition;
        float hiddenTime;
        int shotsBeforeHide, uiStageFrame;
        Enemy hiddenEnemy;
        KeyboardState? pendingKeys;
        MouseState? pendingMouse;

        void PrepareTestInput()
        {
            if (!Application.isEditor) return;
            if (InputSystem.settings.updateMode == InputSettings.UpdateMode.ProcessEventsManually) return;
            // Tick synthetic events in the game loop, where the editor GUI cannot consume them.
            var settings = Object.Instantiate(InputSystem.settings);
            settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
#if UNITY_EDITOR
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#endif
            InputSystem.settings = settings;
        }

        public void UpdateTestInput()
        {
            if (ControlsPlayer || !Application.isEditor) return;
            InputSystem.Update();
            if (pendingKeys.HasValue)
            {
                InputState.Change(testKeys, pendingKeys.Value, InputUpdateType.Manual);
                pendingKeys = null;
            }
            if (pendingMouse.HasValue)
            {
                InputState.Change(testMouse, pendingMouse.Value, InputUpdateType.Manual);
                pendingMouse = null;
            }
            testKeys?.MakeCurrent();
            testMouse?.MakeCurrent();
        }

        void Keys(float elapsed)
        {
            if (restarted)
            {
                if (elapsed < .7f) return;
                Debug.Log($"WAVE_CHECK restart wave={game.Waves.Number} hp={game.Player.HP} mode={game.State}");
                restarted = false;
                Finish();
                return;
            }

            if (stage == 0)
            {
                testKeys = InputSystem.AddDevice<Keyboard>();
                testMouse = InputSystem.AddDevice<Mouse>();
                Press(Key.F1, Key.F2);
                Next();
                return;
            }
            float timer = Time.realtimeSinceStartup - stageAt;
            if (timer < .26f) return;
            switch (stage)
            {
                case 1:
                    Debug.Log($"WAVE_CHECK F1={game.DebugStats} F2={game.DebugAI}");
                    Press(Key.Escape);
                    Next();
                    return;
                case 2:
                    Debug.Log($"WAVE_CHECK Esc={game.State}");
                    Press();
                    Next();
                    return;
                case 3:
                    Press(Key.Escape);
                    Next();
                    return;
                case 4:
                    Debug.Log($"WAVE_CHECK EscResume={game.State}");
                    Press(Key.LeftShift, Key.F3);
                    Next();
                    return;
                case 5:
                    Debug.Log($"WAVE_CHECK ShiftF3={game.Player.Invulnerable}");
                    Press(Key.Digit3);
                    Next();
                    return;
                case 6:
                    if (timer < .52f) return;
                    Debug.Log($"WAVE_CHECK slotKey={game.Guns.Selected + 1}");
                    SendMouse(new MouseState { scroll = new Vector2(0, -1) });
                    Next();
                    return;
                case 7:
                    if (timer < .52f) return;
                    Debug.Log($"WAVE_CHECK wheelSlot={game.Guns.Selected + 1}");
                    Press(Key.F3);
                    Next();
                    return;
                case 8:
                    Debug.Log($"WAVE_CHECK F3 wave={game.Waves.Number}");
                    StartScreenshotCheck();
                    Press(Key.F12);
                    Next();
                    return;
                case 9:
                    if (timer < .7f) return;
                    Debug.Log($"WAVE_CHECK F12 newPngs={CountScreenshots() - savedScreenshots}");
                    restarted = true;
                    game.Restart();
                    return;
            }
        }

        void UiVisibility()
        {
            try
            {
                CheckUiVisibility();
            }
            catch (System.InvalidOperationException failure)
            {
                complete = true;
                Debug.LogError("WAVE_CHECK UI " + failure.Message);
#if UNITY_EDITOR
                UnityEditor.EditorApplication.Exit(1);
#else
                Application.Quit(1);
#endif
            }
        }

        void CheckUiVisibility()
        {
            var hud = game.transform.Find("HUD Canvas");
            var combat = hud.Find("Combat HUD");
            if (stage == 0)
            {
                testKeys = InputSystem.AddDevice<Keyboard>();
                if (restarted)
                {
                    Require(game.State == MatchState.Playing && game.Waves.Number == 1, "Restart did not reload wave 1.");
                    RequireHidden(hud);
                    Press(Key.F8);
                }
                else
                {
                    Require(combat.gameObject.activeInHierarchy, "HUD was hidden at session startup.");
                    testMouse = InputSystem.AddDevice<Mouse>();
                    Press(Key.F1, Key.F2);
                }
                NextUi();
                return;
            }

            float elapsed = Time.realtimeSinceStartup - stageAt;
            if (elapsed < .2f || Time.frameCount <= uiStageFrame + 1) return;
            if (restarted)
            {
                Require(combat.gameObject.activeInHierarchy, "F8 did not show the HUD after restart.");
                restarted = false;
                Debug.Log("WAVE_CHECK UI restart kept overlays hidden and F8 restored them");
                Finish();
                return;
            }

            switch (stage)
            {
                case 1:
                    Require(hud.Find("Performance overlay").gameObject.activeInHierarchy && AiDraws(),
                        $"F1 and F2 must be visible before hiding them. F1={game.DebugStats} F2={game.DebugAI} focus={Application.isFocused}.");
                    Debug.Log($"WAVE_CHECK UI visible at startup res={Screen.width}x{Screen.height} vsync={QualitySettings.vSyncCount}");
                    var tags = combat.Find("Pooled enemy tags");
                    originalUi = System.Array.FindAll(hud.GetComponentsInChildren<Graphic>(),
                        item => item.isActiveAndEnabled && item.name != "Selection" && !item.transform.IsChildOf(tags));
                    Require(originalUi.Length > 0, "No graphics were visible before F8.");
                    hiddenTime = game.Stats.Time;
                    hiddenPosition = game.Player.Position;
                    shotsBeforeHide = game.Stats.Shots;
                    Press(Key.F8, Key.W, Key.Digit3);
                    break;
                case 2:
                    if (elapsed < .55f) return;
                    float distance = Vector3.Distance(game.Player.Position, hiddenPosition);
                    bool gameplayAdvanced = game.State == MatchState.Playing && game.Stats.Time > hiddenTime &&
                        distance > .15f && game.Guns.Selected == 2;
                    if (!gameplayAdvanced && elapsed < 2) return;
                    RequireHidden(hud);
                    Require(game.DebugStats && game.DebugAI, "F8 cleared the debug settings.");
                    Require(gameplayAdvanced,
                        $"Gameplay did not advance while hidden. mode={game.State} time={game.Stats.Time - hiddenTime:F2} movement={distance:F2} slot={game.Guns.Selected + 1}.");
                    Press();
                    SendMouse(new MouseState().WithButton(MouseButton.Left));
                    break;
                case 3:
                    Require(game.Stats.Shots > shotsBeforeHide, "Firing stopped while hidden.");
                    SendMouse(new MouseState());
                    game.Player.Place(new Vector3(-21, .02f, -19), Vector3.forward);
                    Require(Arena.Sample(new Vector3(-21, 0, -11), out Vector3 spawn), "No safe enemy test position.");
                    hiddenEnemy = game.Spawn(EnemyKind.Runner, spawn);
                    Require(hiddenEnemy != null, "Test enemy failed to spawn while hidden.");
                    hiddenPosition = hiddenEnemy.Position;
                    hiddenEnemy.Hurt(1, game.Player.Position, false);
                    game.Player.Invulnerable = false;
                    game.DamagePlayer(5, hiddenEnemy.Position);
                    game.Player.Invulnerable = true;
                    game.Waves.Break(2);
                    game.RegisterHit(false);
                    StartScreenshotCheck();
                    Press(Key.F12);
                    break;
                case 4:
                    if (elapsed < .65f) return;
                    RequireHidden(hud);
                    Require(Vector3.Distance(hiddenEnemy.Position, hiddenPosition) > .15f && game.Waves.IsBreak &&
                        game.Waves.Countdown < GameConfig.WaveBreak, "Enemy AI or the wave countdown stalled while hidden.");
                    Require(CountScreenshots() == savedScreenshots + 1, "F12 did not write exactly one capture.");
                    Debug.Log("WAVE_CHECK UI no graphics or AI renders while hidden, gameplay advanced and F12 wrote a PNG");
                    Press(Key.Escape);
                    break;
                case 5:
                    Require(game.State == MatchState.Paused, "Escape did not pause while the HUD was hidden.");
                    MenuButton(hud, "Paused", "RESUME").onClick.Invoke();
                    Press();
                    break;
                case 6:
                    RequireHidden(hud);
                    Press(Key.F8);
                    break;
                case 7:
                    foreach (Graphic graphic in originalUi)
                        Require(graphic.isActiveAndEnabled, $"The previously visible graphic {graphic.name} failed to return.");
                    Require(AiDraws() && combat.Find("Incoming wave").gameObject.activeInHierarchy &&
                        System.Array.Exists(combat.GetComponentsInChildren<Graphic>(),
                            item => item.name == "Damaged health" && item.isActiveAndEnabled),
                        "The AI overlay, wave banner or new enemy health bar failed to return.");
                    Debug.Log($"WAVE_CHECK UI restored {originalUi.Length} original graphics, NavMesh, wave banner and enemy health bar");
                    Press();
                    break;
                case 8:
                    Press(Key.F8, Key.F3);
                    break;
                case 9:
                    RequireHidden(hud);
                    Require(game.Waves.Number == GameConfig.BossWave, "F3 did not start the boss wave while hidden.");
                    var boss = game.Waves.Boss;
                    if (boss == null && elapsed < 4) return;
                    Require(boss != null, "The Warden failed to spawn while hidden.");
                    boss.Hurt(boss.Maximum, game.Player.Position, false);
                    break;
                case 10:
                    Require(game.State == MatchState.Victory && game.Stats.Score > 0,
                        "Defeating the Warden did not award score and trigger Victory.");
                    MenuButton(hud, "Match results", "ENDLESS MODE").onClick.Invoke();
                    break;
                case 11:
                    RequireHidden(hud);
                    Require(game.State == MatchState.Playing && game.Endless, "Endless mode did not continue while hidden.");
                    game.Player.Invulnerable = false;
                    game.DamagePlayer(GameConfig.Health, game.Player.Position + Vector3.forward);
                    break;
                case 12:
                    Require(game.State == MatchState.Dead, "Lethal damage did not trigger the death screen.");
                    Debug.Log("WAVE_CHECK UI pause, victory and death menus remained usable, including Resume, Endless and Restart");
                    restarted = true;
                    MenuButton(hud, "Match results", "RESTART").onClick.Invoke();
                    return;
            }
            NextUi();
        }

        void NextUi()
        {
            Next();
            uiStageFrame = Time.frameCount;
        }

        bool AiDraws() => System.Array.Exists(game.transform.Find("AI debug visualization").GetComponentsInChildren<Renderer>(),
            item => item.enabled && item.gameObject.activeInHierarchy);

        void RequireHidden(Transform hud)
        {
            var visible = System.Array.Find(hud.GetComponentsInChildren<Graphic>(), item => item.isActiveAndEnabled);
            bool aiDraws = AiDraws();
            Require(visible == null && !aiDraws,
                $"Stage {stage} showed {visible?.name ?? "no graphic"} while hidden. AI renders={aiDraws} mode={game.State}.");
        }

        static Button MenuButton(Transform hud, string menu, string action)
        {
            Button button = hud.Find($"Modal overlay/{menu}/{action}")?.GetComponent<Button>();
            Require(button != null && button.isActiveAndEnabled && button.interactable, $"{menu} action {action} was unavailable.");
            return button;
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new System.InvalidOperationException(message);
        }

        string testScreenshotFolder;

        void StartScreenshotCheck()
        {
            string project = Path.GetDirectoryName(Application.dataPath);
            if (!Application.isEditor)
                project = Path.Combine(Path.GetDirectoryName(project), "Wave Survival");
            testScreenshotFolder = Path.Combine(project, "Screenshots");
            savedScreenshots = CountScreenshots();
        }

        int CountScreenshots() => Directory.Exists(testScreenshotFolder)
            ? Directory.GetFiles(testScreenshotFolder, "*.png").Length : 0;

        void Press(params Key[] keys)
        {
            var state = new KeyboardState(keys);
            if (Application.isEditor) pendingKeys = state;
            else InputSystem.QueueStateEvent(testKeys, state);
        }

        void SendMouse(MouseState state)
        {
            if (Application.isEditor) pendingMouse = state;
            else InputSystem.QueueStateEvent(testMouse, state);
        }

        void ReleaseDevices()
        {
            if (testKeys != null && testKeys.added) InputSystem.RemoveDevice(testKeys);
            if (testMouse != null && testMouse.added) InputSystem.RemoveDevice(testMouse);
        }
    }
}
