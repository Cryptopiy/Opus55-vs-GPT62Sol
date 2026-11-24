using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace WaveSurvival
{
    public enum GameState { Playing, Paused, PlayerDead, Victory }

    /// <summary>
    /// Top-level game state: pause menu (Esc), death screen (S-07), victory screen and endless mode (S-24),
    /// restart (scene reload) and quit. Owns Time.timeScale and the cursor lock.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public GameState State { get; private set; } = GameState.Playing;
        float endScreenTimer = -1f;
        GameState pendingEndState;

        public static GameManager Create(Transform parent)
        {
            var go = new GameObject("GameManager");
            go.transform.SetParent(parent, false);
            return go.AddComponent<GameManager>();
        }

        public void StartGame()
        {
            State = GameState.Playing;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            ApplyCursor();
            Game.Waves.Begin();
        }

        void Update()
        {
            if (State == GameState.Playing) Game.Stats.PlayTime += Time.deltaTime;

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (State == GameState.Playing) Pause();
                else if (State == GameState.Paused) Resume();
            }

            // Delayed end screens use unscaled time (the death camera / boss collapse play first).
            if (endScreenTimer >= 0f)
            {
                endScreenTimer -= Time.unscaledDeltaTime;
                if (endScreenTimer < 0f) ShowEndScreen();
            }
        }

        public void Pause()
        {
            if (State != GameState.Playing) return;
            State = GameState.Paused;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Game.Hud.Menus.ShowPause(true);
            ApplyCursor();
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            State = GameState.Playing;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Game.Hud.Menus.ShowPause(false);
            ApplyCursor();
        }

        public void OnPlayerDied()
        {
            if (State != GameState.Playing) return;
            State = GameState.PlayerDead;
            pendingEndState = GameState.PlayerDead;
            endScreenTimer = GameConfig.Player.DeathCameraTime;
            Game.Audio.Play2D(SoundId.PlayerHurt, 1f, 0.6f);
            ApplyCursor();
        }

        public void OnBossDefeated(EnemyBase boss)
        {
            if (State != GameState.Playing) return;
            Game.Enemies.KillAllExcept(boss);
            Game.Projectiles.ClearAll();
            Game.Stats.WavesCleared = Mathf.Max(Game.Stats.WavesCleared, GameConfig.Waves.BossWave);
            Game.Stats.Score += GameConfig.Score.WaveClearPerWave * GameConfig.Waves.BossWave;
            pendingEndState = GameState.Victory;
            endScreenTimer = GameConfig.Waves.VictoryDelay;
        }

        void ShowEndScreen()
        {
            endScreenTimer = -1f;
            if (pendingEndState == GameState.Victory && State != GameState.Playing) return;
            State = pendingEndState;
            Time.timeScale = 0f;
            if (State == GameState.Victory)
            {
                Game.Audio.Play2D(SoundId.Victory, 0.9f);
                Game.Hud.Menus.ShowVictory();
            }
            else
            {
                Game.Hud.Menus.ShowDeath();
            }
            ApplyCursor();
        }

        /// <summary>S-24 "Endless mode": back to playing, waves continue at N = 6.</summary>
        public void StartEndless()
        {
            Game.Hud.Menus.HideAll();
            State = GameState.Playing;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Game.Waves.ContinueEndless();
            ApplyCursor();
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void Quit()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void ApplyCursor()
        {
            bool locked = State == GameState.Playing || State == GameState.PlayerDead && endScreenTimer >= 0f;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus) ApplyCursor();
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}
