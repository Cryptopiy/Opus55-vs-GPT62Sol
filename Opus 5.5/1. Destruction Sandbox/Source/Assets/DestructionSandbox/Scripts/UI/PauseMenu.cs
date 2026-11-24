using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using U = DestructionSandbox.GameConfig.UI;
using PlayerCfg = DestructionSandbox.GameConfig.Player;

namespace DestructionSandbox
{
    /// <summary>
    /// D-24 / common rules: Esc opens the pause menu (Resume, Restart structure, mouse sensitivity, master
    /// volume, Quit). Pausing freezes time (timeScale 0) and audio, and frees the cursor.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        private GameObject panel;
        private Text sensitivityValue;
        private Text volumeValue;

        public bool IsOpen { get; private set; }

        public void Build(Transform parent)
        {
            // Event system driven by the Input System package (the legacy input manager is disabled).
            var es = new GameObject("EventSystem");
            es.transform.SetParent(parent, false);
            es.AddComponent<EventSystem>();
            var module = es.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();

            Canvas canvas = UIFactory.CreateCanvas(parent, "PauseMenu", 20, true);   // never hidden by F8 (U-02)
            panel = canvas.gameObject;
            RectTransform dim = UIFactory.Stretch(canvas.transform, "Dim");
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.6f);

            var c = new Vector2(0.5f, 0.5f);
            UIFactory.Image(canvas.transform, "Window", new Color(0.07f, 0.08f, 0.1f, 0.94f), c, c, Vector2.zero, new Vector2(620f, 640f));
            UIFactory.Label(canvas.transform, "Title", "PAUSED", U.FontSizeTitle, TextAnchor.MiddleCenter, UIFactory.AccentColor, c, c, new Vector2(0f, 250f), new Vector2(560f, 70f));

            UIFactory.Button(canvas.transform, "Resume", new Vector2(0f, 160f), new Vector2(420f, 64f), () => { Game.Audio.PlayUiClick(); Close(); });
            UIFactory.Button(canvas.transform, "Restart Structure", new Vector2(0f, 84f), new Vector2(420f, 64f), () =>
            {
                Game.Audio.PlayUiClick();
                Game.Controller.ResetStructure();
                Close();
            });

            UIFactory.Label(canvas.transform, "SensLabel", "Mouse Sensitivity", U.FontSizeSmall + 2, TextAnchor.MiddleLeft, UIFactory.DimTextColor, c, c, new Vector2(-40f, 10f), new Vector2(420f, 30f));
            sensitivityValue = UIFactory.Label(canvas.transform, "SensValue", "", U.FontSizeSmall + 2, TextAnchor.MiddleRight, UIFactory.TextColor, c, c, new Vector2(40f, 10f), new Vector2(420f, 30f));
            UIFactory.Slider(canvas.transform, "SensitivitySlider", new Vector2(0f, -32f), new Vector2(500f, 32f),
                PlayerCfg.MinSensitivity, PlayerCfg.MaxSensitivity, PlayerCfg.DefaultSensitivity, v =>
                {
                    Game.Player.Sensitivity = v;
                    sensitivityValue.text = v.ToString("0.00");
                });
            sensitivityValue.text = PlayerCfg.DefaultSensitivity.ToString("0.00");

            UIFactory.Label(canvas.transform, "VolLabel", "Master Volume", U.FontSizeSmall + 2, TextAnchor.MiddleLeft, UIFactory.DimTextColor, c, c, new Vector2(-40f, -80f), new Vector2(420f, 30f));
            volumeValue = UIFactory.Label(canvas.transform, "VolValue", "", U.FontSizeSmall + 2, TextAnchor.MiddleRight, UIFactory.TextColor, c, c, new Vector2(40f, -80f), new Vector2(420f, 30f));
            UIFactory.Slider(canvas.transform, "VolumeSlider", new Vector2(0f, -122f), new Vector2(500f, 32f), 0f, 1f, GameConfig.Audio.DefaultMasterVolume, v =>
            {
                Game.Audio.MasterVolume = v;
                volumeValue.text = Mathf.RoundToInt(v * 100f) + "%";
            });
            volumeValue.text = Mathf.RoundToInt(GameConfig.Audio.DefaultMasterVolume * 100f) + "%";

            UIFactory.Button(canvas.transform, "Quit", new Vector2(0f, -220f), new Vector2(420f, 64f), () => { Game.Audio.PlayUiClick(); Quit(); });
            panel.SetActive(false);
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            panel.SetActive(true);
            Time.timeScale = 0f;
            Game.Audio.SetPaused(true);
            GameController.UnlockCursor();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            panel.SetActive(false);
            Game.SlowMo.Apply();                // restores 1.0 or the slow-motion time scale
            Game.Audio.SetPaused(false);
            GameController.LockCursor();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
