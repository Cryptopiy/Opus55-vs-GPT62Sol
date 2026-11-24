using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LivingIsland
{
    /// <summary>
    /// Root of the minimal UI (I-32), built on two screen-space canvases and the Input System UI module:
    ///   interface canvas: HUD (clock + weather icon), F1 debug overlay, Tab settings, photo-mode panel; it is
    ///                     registered with <see cref="UIVisibility"/>, so F8 hides all of it;
    ///   system canvas:    Esc pause menu and the start fade-in; never hidden, so the game cannot get stuck.
    /// Also owns cursor locking (locked while playing, free whenever a panel needs the mouse; the pointer is not
    /// drawn while the interface is hidden, except over the pause menu).
    /// </summary>
    public sealed class UIRoot : MonoBehaviour
    {
        public bool SettingsOpen { get; private set; }
        public bool DebugVisible { get; private set; }

        Hud hud;
        DebugOverlay debugOverlay;
        PauseMenu pauseMenu;
        SettingsPanel settings;
        PhotoPanel photoPanel;
        Canvas interfaceCanvas;

        public void Init()
        {
            UIFactory.Init();
            interfaceCanvas = CreateCanvas("Interface Canvas", 10);
            Transform ui = interfaceCanvas.transform;
            Transform system = CreateCanvas("System Canvas", 20).transform;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                var module = es.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }

            hud = new GameObject("HUD", typeof(RectTransform)).AddComponent<Hud>();
            hud.Build(ui);
            debugOverlay = new GameObject("DebugOverlay", typeof(RectTransform)).AddComponent<DebugOverlay>();
            debugOverlay.Build(ui);
            settings = new GameObject("Settings", typeof(RectTransform)).AddComponent<SettingsPanel>();
            settings.Build(ui);
            photoPanel = new GameObject("PhotoPanel", typeof(RectTransform)).AddComponent<PhotoPanel>();
            photoPanel.Build(ui);
            pauseMenu = new GameObject("PauseMenu", typeof(RectTransform)).AddComponent<PauseMenu>();
            pauseMenu.Build(system);

            debugOverlay.gameObject.SetActive(false);
            settings.gameObject.SetActive(false);
            photoPanel.gameObject.SetActive(false);
            pauseMenu.gameObject.SetActive(false);

            var fadeGo = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            UIFactory.Rect(fadeGo, system, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            fade = fadeGo.GetComponent<Image>();
            fade.color = Color.black;
            fade.raycastTarget = false;
            fadeGo.transform.SetAsLastSibling();

            // The F8 state survives restarts; applying it here, before the first frame, keeps UI built while
            // hidden hidden.
            UIVisibility.Register(interfaceCanvas);
            UIVisibility.Changed += UpdateCursor;
            UpdateCursor();
        }

        Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        void OnDestroy()
        {
            UIVisibility.Changed -= UpdateCursor;
            UIVisibility.Unregister(interfaceCanvas);
        }

        Image fade;

        /// <summary>Full-screen black overlay used for the startup fade-in (0 = invisible).</summary>
        public void SetFade(float alpha)
        {
            fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
            fade.enabled = alpha > 0.001f;
        }

        public void ToggleDebugOverlay()
        {
            DebugVisible = !DebugVisible;
            debugOverlay.gameObject.SetActive(DebugVisible && !Game.Photo.Active);
        }

        public void SetPaused(bool paused)
        {
            Game.Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            pauseMenu.gameObject.SetActive(paused);
            if (paused && SettingsOpen) SetSettingsOpen(false);
            UpdateCursor();
        }

        public void SetSettingsOpen(bool open)
        {
            SettingsOpen = open;
            settings.gameObject.SetActive(open);
            if (open) settings.Refresh();
            UpdateCursor();
        }

        public void OnPhotoModeChanged(bool on)
        {
            // Photo mode hides the gameplay UI; only its own panel remains (never in the 4K capture).
            hud.gameObject.SetActive(!on);
            debugOverlay.gameObject.SetActive(DebugVisible && !on);
            photoPanel.gameObject.SetActive(on);
            if (on) photoPanel.Refresh();
            if (on && SettingsOpen) SetSettingsOpen(false);
            UpdateCursor();
        }

        /// <summary>Lets the automated test hide every panel for clean screenshots.</summary>
        public void SetPhotoPanelVisible(bool visible) => photoPanel.gameObject.SetActive(visible && Game.Photo.Active);

        void UpdateCursor()
        {
            bool free = Game.Paused || SettingsOpen || (Game.Photo != null && Game.Photo.Active);
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            // F8: while the interface is hidden the pointer is not drawn either, except over the pause menu.
            Cursor.visible = free && (!UIVisibility.Hidden || Game.Paused);
            if (Game.CameraRig != null) Game.CameraRig.InputEnabled = !free;
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus) UpdateCursor();
        }
    }
}
