using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static DestructionRange.SandboxConfig;

namespace DestructionRange
{
    internal sealed class RangeHud : System.IDisposable
    {
        static readonly Color Ink = new(.036f, .064f, .075f, .93f);
        static readonly Color InkDark = new(.023f, .043f, .052f, .97f);
        static readonly Color Paper = new(.94f, .96f, .90f);
        static readonly Color Muted = new(.66f, .75f, .74f);
        static readonly Color Mint = new(.60f, .89f, .77f);
        static readonly Color Orange = new(1f, .63f, .33f);
        static readonly Color Line = new(.58f, .79f, .75f, .25f);
        static readonly Vector2 TL = new(0, 1), TR = Vector2.one, BL = Vector2.zero, BR = new(1, 0);
        static readonly Vector2 Center = new(.5f, .5f);

        readonly Sandbox game;
        readonly Font font;
        readonly GameObject gameplayUi;
        readonly RectTransform hud, pause, debug, scorePopupCanvas, crosshair;
        readonly Text layout, score, destroyed, combo, weapon, ammo, cooldownText, shards, timer, debugText, slow;
        readonly Image cooldownFill, destructionFill;
        readonly Text[] layoutKeys = new Text[LayoutNames.Length], weaponKeys = new Text[Weapons.Length];
        readonly Popup[] popups = new Popup[PopupCap];
        readonly Button[] menuButtons = new Button[3];
        readonly Slider[] menuSliders = new Slider[2];
        int buttonCount, sliderCount;
        readonly ProfilerRecorder draws;
        int nextPopup, lastShards = -1, lastTime = -1, lastScore = -1, lastDestroyed = -1, lastCombo = -1, lastAmmo = int.MinValue;
        Weapon lastWeapon = (Weapon)(-1);
        Layout lastLayout = (Layout)(-1);
        bool lastSlow;
        int cooldownBucket = int.MinValue, hudFrames;
        float frameSeconds;

        public RangeHud(Sandbox game)
        {
            this.game = game;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Gameplay canvases share one parent so newly activated popups inherit its visibility.
            gameplayUi = game.Node("Gameplay interface");
            hud = Canvas("Heads-up display", 10, gameplayUi.transform);
            scorePopupCanvas = Canvas("Score popups", 11, gameplayUi.transform);
            var panel = Card(hud, TL, new(38, -35), new(322, 96));
            Label(panel, TL, new(16, -11), new(200, 20), "STRUCTURE", 15, Muted, true);
            layout = Label(panel, TL, new(16, -29), new(280, 34), "", 29, Paper, true);
            for (int i = 0; i < layoutKeys.Length; i++)
                layoutKeys[i] = Label(panel, TL, new(17 + i * 96, -65), new(94, 23),
                    (i == 0 ? "F5 TOWER" : i == 1 ? "F6 ARCH" : "F7 PYRAMID"), 13, Muted, true);

            panel = Card(hud, TR, new(-38, -35), new(310, 143));
            Label(panel, TL, new(16, -12), new(140, 22), "SCORE", 15, Muted, true);
            score = Label(panel, TL, new(15, -30), new(215, 49), "", 43, Paper, true);
            combo = Label(panel, TR, new(-16, -20), new(100, 38), "", 29, Orange, true, TextAnchor.MiddleRight);
            destroyed = Label(panel, TL, new(16, -83), new(275, 29), "", 19, Paper);
            destructionFill = Meter(panel, TL, new(17, -118), new(275, 4), Mint);

            panel = Card(hud, BL, new(38, 44), new(375, 131));
            Label(panel, TL, new(16, -12), new(140, 19), "AMMUNITION", 14, Muted, true);
            weapon = Label(panel, TL, new(15, -32), new(270, 35), "", 26, Paper, true);
            ammo = Label(panel, TR, new(-17, -23), new(74, 46), "", 32, Paper, true, TextAnchor.MiddleRight);
            for (int i = 0; i < weaponKeys.Length; i++)
                weaponKeys[i] = Label(panel, TL, new(17 + i * 81, -70), new(82, 24),
                    (i == 0 ? "1 CANNON" : i == 1 ? "2 SHELL" : "3 ORB"), 14, Muted, true);
            cooldownFill = Meter(panel, TL, new(17, -107), new(248, 5), Orange);
            cooldownText = Label(panel, TR, new(-16, -98), new(80, 21), "", 14, Orange, true, TextAnchor.MiddleRight);
            Label(hud, BL, new(39, 18), new(500, 22), "WASD Move   •   Shift Sprint   •   Space Jump", 15, Paper);

            panel = Card(hud, BR, new(-38, 44), new(237, 80));
            Label(panel, TL, new(15, -9), new(95, 20), "SHARDS", 14, Muted, true);
            Label(panel, TR, new(-16, -9), new(90, 20), "SESSION", 14, Muted, true, TextAnchor.MiddleRight);
            shards = Label(panel, TL, new(15, -28), new(103, 43), "", 31, Paper, true);
            timer = Label(panel, TR, new(-16, -28), new(113, 43), "", 31, Paper, true, TextAnchor.MiddleRight);
            Label(hud, BR, new(-39, 18), new(414, 22), "T Slow time   •   R Reset   •   X Clear debris", 15, Paper, false, TextAnchor.MiddleRight);
            slow = Label(hud, BR, new(-39, 135), new(237, 23), "SLOW MOTION  ×0.25", 16, Orange, true, TextAnchor.MiddleRight);
            slow.gameObject.SetActive(false);

            crosshair = Rect(hud, Center, Vector2.zero, Vector2.one, "Crosshair");
            foreach (Vector2 dir in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                Vector2 size = dir.x != 0 ? new(11, 2) : new(2, 11);
                Fill(crosshair, Center, dir * 14 + new Vector2(0, -1), size + new Vector2(2, 2), new Color(.04f, .07f, .07f, .73f));
                Fill(crosshair, Center, dir * 14, size, Paper);
            }
            Fill(crosshair, Center, Vector2.zero, new(3, 3), Orange);

            debug = Card(hud, TL, new(38, -144), new(322, 155));
            Label(debug, TL, new(16, -10), new(285, 26), "F1  •  LIVE DIAGNOSTICS", 16, Mint, true);
            debugText = Label(debug, TL, new(16, -37), new(289, 105), "", 17, Paper, false, TextAnchor.UpperLeft);
            debug.gameObject.SetActive(false);
            if (!Application.isBatchMode)
                draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);

            for (int i = 0; i < popups.Length; i++)
            {
                Text text = Label(scorePopupCanvas, Center, Vector2.zero, new(114, 47), "", 21, Orange, true, TextAnchor.MiddleCenter);
                var edge = text.gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(.025f, .045f, .049f, .9f);
                edge.effectDistance = new Vector2(1.1f, -1.1f);
                text.gameObject.SetActive(false);
                popups[i] = new Popup(text);
            }
            pause = BuildPause();
        }

        RectTransform BuildPause()
        {
            var canvas = Canvas("Pause menu", 50);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var system = game.Node("UI input");
            system.AddComponent<EventSystem>();
            system.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var backdrop = Rect(canvas, BL, Vector2.zero, Vector2.zero, "Backdrop");
            backdrop.anchorMax = TR;
            var dim = backdrop.gameObject.AddComponent<Image>();
            dim.color = new Color(.015f, .032f, .043f, .72f);
            var card = Card(backdrop, Center, Vector2.zero, new(510, 508), InkDark);
            Label(card, TL, new(32, -21), new(425, 25), "DESTRUCTION RANGE", 16, Mint, true);
            Label(card, TL, new(29, -47), new(430, 66), "PAUSED", 55, Paper, true);
            Fill(card, TL, new(32, -123), new(446, 1), Line);
            SliderControl(card, "Mouse sensitivity", .25f, 3f, game.Player.Sensitivity, -142,
                value => game.Player.Sensitivity = value, value => $"{value:0.00}×");
            SliderControl(card, "Master volume", 0, 1, game.MasterGain, -215,
                value => game.MasterGain = value, value => $"{value:P0}");
            ButtonControl(card, "Resume", -294, true, () => game.SetPaused(false));
            ButtonControl(card, "Restart structure", -357, false, game.Restart);
            ButtonControl(card, "Quit", -420, false, Quit);
            canvas.gameObject.SetActive(false);
            return canvas;
        }

        void SliderControl(RectTransform parent, string title, float min, float max, float initial, int top,
            System.Action<float> apply, System.Func<float, string> format)
        {
            Label(parent, TL, new(32, top), new(300, 24), title, 21, Paper, true);
            Text amount = Label(parent, TR, new(-33, top), new(105, 24), format(initial), 17, Mint, false, TextAnchor.MiddleRight);
            RectTransform row = Rect(parent, TL, new(32, top - 28), new(446, 32), title + " slider");
            Image input = row.gameObject.AddComponent<Image>();
            input.color = Color.clear;
            Image track = Fill(row, BL, new(0, 13), new(446, 6), Line);
            var fill = Fill(track.rectTransform, BL, Vector2.zero, new(446, 6), Mint);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            var area = Rect(row, BL, new(0, 16), new(446, 1), "Handle track");
            var handle = Fill(area, Center, Vector2.zero, new(12, 20), Paper);
            var slider = row.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = initial;
            slider.onValueChanged.AddListener(value => { apply(value); amount.text = format(value); });
            menuSliders[sliderCount++] = slider;
        }

        void ButtonControl(RectTransform parent, string title, int top, bool primary, UnityEngine.Events.UnityAction action)
        {
            Image surface = Fill(parent, TL, new(32, top), new(446, 49), primary ? Mint : new Color(.11f, .19f, .21f, 1));
            surface.raycastTarget = true;
            Label(surface.rectTransform, TL, new(15, 0), new(420, 49), title, 22, primary ? InkDark : Paper, true);
            var button = surface.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            ColorBlock colors = button.colors;
            colors.highlightedColor = primary ? new Color(.90f, 1, .95f) : new Color(1.3f, 1.4f, 1.4f);
            colors.pressedColor = new Color(.73f, .81f, .80f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(() => { action(); game.Click(); });
            menuButtons[buttonCount++] = button;
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        public void Pause(bool paused)
        {
            pause.gameObject.SetActive(paused);
            crosshair.gameObject.SetActive(!paused);
        }

        public void ToggleGameplayVisibility() => gameplayUi.SetActive(!gameplayUi.activeSelf);

        public void Reset()
        {
            foreach (Popup popup in popups) popup.Text.gameObject.SetActive(false);
        }

        public void Pop(Vector3 point, int points)
        {
            int slot = nextPopup++ % popups.Length;
            var popup = popups[slot];
            popup.Position = point;
            popup.Offset = new Vector2((slot % 5 - 2) * 7f, (slot / 5 - 1.5f) * 5f);
            popup.Text.text = "+" + points;
            popup.Time = 1.16f;
            popup.Text.gameObject.SetActive(true);
        }

        public void Tick()
        {
            float dt = Time.unscaledDeltaTime;
            frameSeconds += dt;
            hudFrames++;
            debug.gameObject.SetActive(game.DebugVisible);
            if (frameSeconds >= .15f)
            {
                if (game.DebugVisible)
                {
                    int bodies = game.Pieces.ActiveBlocks + game.Pieces.ActiveShards + game.Guns.Active;
                    string drawCount = draws.Valid ? draws.LastValue.ToString() : "—";
                    debugText.text = $"{hudFrames / frameSeconds:F1} FPS    {frameSeconds * 1000 / hudFrames:F2} ms\n" +
                        $"Draw calls  {drawCount}    Bodies  {bodies:N0}\n" +
                        $"Blocks  {game.Pieces.ActiveBlocks:N0}    Sleeping  {game.Pieces.Sleeping:N0}\n" +
                        $"Particles  {game.ActiveParticles:N0}    Shards  {game.Pieces.ActiveShards:N0}";
                }
                frameSeconds = 0;
                hudFrames = 0;
            }

            if (lastLayout != game.Current)
            {
                lastLayout = game.Current;
                layout.text = LayoutNames[(int)lastLayout];
                for (int i = 0; i < layoutKeys.Length; i++) layoutKeys[i].color = i == (int)lastLayout ? Mint : Muted;
                lastDestroyed = -1;
            }

            if (lastWeapon != game.Guns.Selected)
            {
                lastWeapon = game.Guns.Selected;
                weapon.text = Weapons[(int)lastWeapon].Name;
                for (int i = 0; i < weaponKeys.Length; i++) weaponKeys[i].color = i == (int)lastWeapon ? Mint : Muted;
            }

            SetNumber(ammo, game.Guns.Ammo, ref lastAmmo);
            SetNumber(score, game.Run.Score, ref lastScore);
            SetNumber(shards, game.Pieces.ActiveShards, ref lastShards);
            if (lastCombo != game.Run.Combo)
            {
                lastCombo = game.Run.Combo;
                combo.text = "×" + lastCombo;
                combo.color = lastCombo > 1 ? Orange : Muted;
            }
            if (lastDestroyed != game.Run.Destroyed)
            {
                lastDestroyed = game.Run.Destroyed;
                float ratio = lastDestroyed / (float)Mathf.Max(1, game.Pieces.Total);
                destroyed.text = $"Destroyed: {lastDestroyed} / {game.Pieces.Total} ({Mathf.FloorToInt(100 * ratio)}%)";
                SizeMeter(destructionFill, ratio);
            }

            float elapsed = game.Run.Elapsed;
            int whole = Mathf.FloorToInt(elapsed);
            if (whole != lastTime)
            {
                lastTime = whole;
                timer.text = $"{whole / 60:00}:{whole % 60:00}";
            }
            SizeMeter(cooldownFill, game.Guns.Charge);
            int remaining = Mathf.CeilToInt(game.Guns.Remaining * 10);
            int bucket = game.Guns.Ammo == 0 ? -1 : remaining;
            if (bucket != cooldownBucket)
            {
                cooldownBucket = bucket;
                cooldownText.text = bucket < 0 ? "EMPTY" : bucket == 0 ? "READY" : $"{bucket * .1f:0.0}s";
                cooldownFill.color = bucket == 0 ? Mint : Orange;
            }
            if (lastSlow != game.Slow)
            {
                lastSlow = game.Slow;
                slow.gameObject.SetActive(lastSlow);
            }
            if (!game.Paused) UpdatePopups(dt);
        }

        void UpdatePopups(float dt)
        {
            foreach (Popup popup in popups)
            {
                if (!popup.Text.gameObject.activeSelf) continue;
                popup.Time -= dt;
                if (popup.Time <= 0) { popup.Text.gameObject.SetActive(false); continue; }
                Vector3 screen = game.Player.Camera.WorldToScreenPoint(popup.Position + Vector3.up * (1.4f - popup.Time));
                popup.Text.enabled = screen.z > 0;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(scorePopupCanvas, screen, null, out Vector2 pos);
                popup.Text.rectTransform.anchoredPosition = pos + popup.Offset;
                Color color = Orange;
                color.a = Mathf.Clamp01(popup.Time * 2.3f) * .94f;
                popup.Text.color = color;
            }
        }

        static void SetNumber(Text text, int value, ref int previous)
        {
            if (previous == value) return;
            previous = value;
            text.text = value < 0 ? "∞" : value.ToString("N0");
        }

        RectTransform Canvas(string name, int order, Transform parent = null)
        {
            GameObject go = game.Node(name, parent);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(FrameWidth, FrameHeight);
            scaler.matchWidthOrHeight = .5f;
            return (RectTransform)go.transform;
        }

        static RectTransform Rect(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }

        static Image Fill(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var image = Rect(parent, anchor, pos, size, "Surface").gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Image Meter(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var track = Fill(parent, anchor, pos, size, Line);
            var fill = Fill(track.rectTransform, BL, Vector2.zero, Vector2.zero, color);
            SizeMeter(fill, 0);
            return fill;
        }

        static void SizeMeter(Image fill, float fraction)
        {
            fill.rectTransform.anchorMax = new Vector2(fraction, 1);
        }

        static RectTransform Card(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color? color = null)
        {
            var rect = Fill(parent, anchor, pos, size, color ?? Ink).rectTransform;
            Fill(rect, TL, Vector2.zero, new(size.x, 1), Line);
            Fill(rect, TL, Vector2.zero, new(3, size.y), Mint);
            return rect;
        }

        Text Label(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, string value,
            int fontSize, Color color, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var text = Rect(parent, anchor, pos, size, value.Length > 0 ? value : "Value").gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.color = color;
            text.text = value;
            return text;
        }

        public void Dispose()
        {
            if (draws.Valid) draws.Dispose();
        }

        public Slider TestSlider(int index) => menuSliders[index];
        public Button TestButton(int index) => menuButtons[index];

        sealed class Popup
        {
            public readonly Text Text;
            public Vector3 Position;
            public Vector2 Offset;
            public float Time;
            public Popup(Text text) => Text = text;
        }
    }
}
