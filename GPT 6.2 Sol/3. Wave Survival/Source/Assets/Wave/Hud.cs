using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using static WaveSurvival.GameConfig;
using static WaveSurvival.HudWidgets;

namespace WaveSurvival
{
    internal sealed partial class Hud
    {
        // A scene restart rebuilds the HUD, so retain the choice for the whole play session.
        static bool hidden;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetVisibility() => hidden = false;

        public static void ToggleVisibility() => hidden = !hidden;

        readonly CoreGame game;
        readonly Canvas canvas;
        readonly RectTransform root, gameplay;
        readonly Text wave, enemies, score, health, magazine, reserve, weaponName, bannerText, countdown;
        readonly Image healthFill, staminaFill, bossFill;
        readonly RectTransform incoming, bossPanel, sight, hit, damageArrow, perfPanel;
        readonly RawImage damageVeil;
        readonly Text bossPhase, perf;
        readonly Image[] slotBack = new Image[3], slotIcon = new Image[3], slotRail = new Image[3], marker = new Image[4];
        readonly RectTransform[] crosshair = new RectTransform[4];
        readonly Sprite[] weaponIcons = new Sprite[3];
        readonly Image ammoIcon;
        readonly DrawCounters drawCalls = new();
        int lastWave = -1, lastHealth = -1, lastAmmo = -99, lastReserve = -99, lastWeapon = -1, lastCount = -1, lastPhase = -1;
        int lastScore = -1;
        long lastEnemies = -1;
        float nextPerf, frameAccum;
        int frameCount;

        public Hud(CoreGame game)
        {
            this.game = game;
            root = Box(game.transform, "HUD Canvas", Center, Vector2.zero, Vector2.zero);
            canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scale = root.gameObject.AddComponent<CanvasScaler>();
            scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(FullWidth, FullHeight);
            scale.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var events = Geometry.Root("Menu input", game.transform).gameObject;
            events.AddComponent<EventSystem>();
            var input = events.AddComponent<InputSystemUIInputModule>();
            input.AssignDefaultActions();

            gameplay = Stretch(root, "Combat HUD");
            var veil = Stretch(gameplay, "Damage vignette");
            damageVeil = veil.gameObject.AddComponent<RawImage>();
            damageVeil.raycastTarget = false;
            damageVeil.texture = Effects.Mask("Red damage edges", 128, (x, y) =>
                Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.47f, 1.18f, Mathf.Sqrt(x * x + y * y))));
            damageVeil.enabled = false;
            BuildWorldHud();

            var waveCard = Box(gameplay, "Wave monitor", TL, new(42, -37), new(246, 101));
            ColorBlock(waveCard, Panel);
            Block(waveCard, "Wave stripe", TL, Vector2.zero, new(4, 101), Teal);
            Label(waveCard, "WAVE", 16, TL, new(19, -9), new(93, 25), Muted, bold: true);
            wave = Label(waveCard, "", 45, TL, new(17, -29), new(82, 62), Paper, bold: true);
            Block(waveCard, "Divider", TL, new(102, -18), new(1, 64), Glass);
            Label(waveCard, "HOSTILES", 15, TL, new(120, -15), new(110, 26), Muted, bold: true);
            enemies = Label(waveCard, "", 29, TL, new(120, -40), new(110, 41), Gold, bold: true);
            var scoreCard = Box(gameplay, "Score", TR, new(-42, -37), new(177, 65));
            ColorBlock(scoreCard, Panel);
            Block(scoreCard, "Score stripe", TR, Vector2.zero, new(4, 65), Teal);
            Label(scoreCard, "SCORE", 15, TR, new(-17, -6), new(143, 22), Muted, TextAnchor.MiddleRight, true);
            score = Label(scoreCard, "", 29, TR, new(-17, -24), new(143, 36), Paper, TextAnchor.MiddleRight, true);

            var vitals = Box(gameplay, "Vitals", BL, new(42, 38), new(283, 126));
            ColorBlock(vitals, Panel);
            Block(vitals, "Vitals stripe", BL, Vector2.zero, new(4, 126), Teal);
            Label(vitals, "HEALTH", 16, TL, new(17, -12), new(132, 27), Muted, bold: true);
            health = Label(vitals, "", 36, TR, new(-15, -7), new(95, 44), Paper, TextAnchor.MiddleRight, true);
            Block(vitals, "Health track", TL, new(17, -53), new(249, 11), Glass);
            healthFill = Block(vitals, "Health fill", TL, new(17, -53), new(249, 11), Teal);
            Label(vitals, "STAMINA", 14, TL, new(17, -72), new(171, 24), Muted, bold: true);
            Block(vitals, "Stamina track", TL, new(17, -101), new(249, 7), Glass);
            staminaFill = Block(vitals, "Stamina fill", TL, new(17, -101), new(249, 7), Gold);

            var ammo = Box(gameplay, "Ammunition counter", BR, new(-42, 38), new(302, 126));
            ColorBlock(ammo, Panel);
            Block(ammo, "Ammo stripe", BR, Vector2.zero, new(4, 126), Teal);
            weaponName = Label(ammo, "", 16, TL, new(13, -10), new(267, 25), Muted, bold: true);
            ammoIcon = Block(ammo, "Weapon icon", BL, new(13, 23), new(96, 46), Teal);
            ammoIcon.preserveAspect = true;
            magazine = Label(ammo, "", 47, BR, new(-92, 12), new(90, 57), Paper, TextAnchor.MiddleRight, true);
            Label(ammo, "/", 25, BR, new(-68, 20), new(21, 41), Muted, TextAnchor.MiddleCenter);
            reserve = Label(ammo, "", 25, BR, new(-15, 20), new(52, 41), Muted, TextAnchor.MiddleRight, true);
            for (int i = 0; i < weaponIcons.Length; i++)
            {
                weaponIcons[i] = WeaponIcon(i);
                Vector2 bottom = new(.5f, 0);
                var key = Box(gameplay, GameConfig.Weapons[i].Name, bottom, new((i - 1) * 94, 38), new(84, 56));
                slotBack[i] = ColorBlock(key, Panel);
                slotRail[i] = Block(key, "Selection", TL, Vector2.zero, new(84, 2), Teal);
                Label(key, (i + 1).ToString(), 13, TL, new(7, -5), new(17, 20), Paper, bold: true);
                slotIcon[i] = Block(key, "Weapon icon", Center, new(5, -3), new(58, 27), Muted);
                slotIcon[i].sprite = weaponIcons[i];
                slotIcon[i].preserveAspect = true;
            }

            bossPanel = Box(gameplay, "Warden", new(.5f, 1), new(0, -36), new(580, 82));
            ColorBlock(bossPanel, Panel);
            Block(bossPanel, "Warden top rule", TL, Vector2.zero, new(580, 2), Red);
            Label(bossPanel, "WARDEN", 23, TL, new(18, -8), new(260, 38), Paper, bold: true);
            bossPhase = Label(bossPanel, "", 16, TR, new(-18, -11), new(196, 32), Red, TextAnchor.MiddleRight, true);
            Block(bossPanel, "Warden track", TL, new(18, -51), new(544, 12), Glass);
            bossFill = Block(bossPanel, "Warden health", TL, new(18, -51), new(544, 12), Red);
            Block(bossPanel, "Phase I boundary", TL, new(18 + 544 * BossPhase2, -48), new(2, 18), Paper);
            Block(bossPanel, "Phase II boundary", TL, new(18 + 544 * BossPhase3, -48), new(2, 18), Paper);

            float[] phasePositions = { (1 + BossPhase2) * .5f, (BossPhase2 + BossPhase3) * .5f, BossPhase3 * .5f };
            string[] numerals = { "I", "II", "III" };
            for (int i = 0; i < numerals.Length; i++)
                Label(bossPanel, numerals[i], 11, TL, new(18 + 544 * phasePositions[i] - 17, -64),
                    new(34, 16), Muted, TextAnchor.MiddleCenter, true);

            incoming = Box(gameplay, "Incoming wave", Center, new(0, 237), new(468, 104));
            ColorBlock(incoming, Panel);
            Block(incoming, "Incoming top rule", TL, Vector2.zero, new(468, 2), Gold);
            bannerText = Label(incoming, "", 29, new(.5f, 1), new(0, -7), new(428, 48), Paper, TextAnchor.MiddleCenter, true);
            countdown = Label(incoming, "", 28, new(.5f, 0), new(0, 9), new(403, 44), Gold, TextAnchor.MiddleCenter, true);

            sight = Box(gameplay, "Crosshair", Center, Vector2.zero, Vector2.zero);
            hit = Box(gameplay, "Hit marker", Center, Vector2.zero, Vector2.zero);
            for (int i = 0; i < crosshair.Length; i++)
            {
                bool vertical = i % 2 != 0;
                crosshair[i] = Block(sight, "Crosshair segment", Center, Vector2.zero,
                    vertical ? new(2, 9) : new(9, 2), Paper).rectTransform;
                marker[i] = Block(hit, "Hit confirmation", Center, Quaternion.Euler(0, 0, i * 90 + 45) * Vector3.right * 15,
                    new(11, 3), Paper);
                marker[i].rectTransform.localRotation = Quaternion.Euler(0, 0, i * 90 + 45);
            }
            damageArrow = Box(gameplay, "Damage bearing", Center, Vector2.zero, Vector2.zero);
            for (int side = -1; side <= 1; side += 2)
            {
                var dash = Block(damageArrow, "Directional arrow", Center, new(side * 7.5f, 0), new(20, 4), Red);
                dash.rectTransform.localRotation = Quaternion.Euler(0, 0, -side * 45);
            }

            perfPanel = Box(root, "Performance overlay", TL, new(42, -156), new(395, 126));
            ColorBlock(perfPanel, Panel);
            perf = Label(perfPanel, "", 18, TL, new(16, -8), new(363, 110), Paper);
            BuildMenus();
            Refresh();
        }

        public void Refresh()
        {
            bool showGameplay = !hidden && game.State == MatchState.Playing;
            bool showPerformance = !hidden && game.DebugStats;
            gameplay.gameObject.SetActive(showGameplay);
            perfPanel.gameObject.SetActive(showPerformance);
            UpdateMenus();
            if (!showGameplay)
            {
                HideAI();
                if (showPerformance) UpdatePerf();
                return;
            }
            var rounds = game.Waves;
            if (rounds.Number != lastWave)
            {
                wave.text = rounds.Number.ToString("D2");
                bannerText.text = $"WAVE {rounds.Number} INCOMING";
                lastWave = rounds.Number;
            }
            if (lastEnemies != rounds.Remaining)
            {
                lastEnemies = rounds.Remaining;
                enemies.text = lastEnemies.ToString();
            }
            if (lastScore != game.Stats.Score)
            {
                lastScore = game.Stats.Score;
                score.text = lastScore.ToString("D6");
            }
            int hp = Mathf.CeilToInt(game.Player.HP);
            if (lastHealth != hp)
            {
                lastHealth = hp;
                health.text = hp.ToString();
                healthFill.color = hp <= 30 ? Red : Teal;
                Width(healthFill, 249 * hp / Health);
            }
            Width(staminaFill, 249 * game.Player.Energy / Stamina);
            UpdateAmmo();

            incoming.gameObject.SetActive(rounds.IsBreak);
            int timer = Mathf.CeilToInt(rounds.Countdown);
            if (rounds.IsBreak && timer != lastCount)
            {
                lastCount = timer;
                countdown.text = timer.ToString("D2");
            }
            Enemy boss = rounds.Boss;
            bool bossActive = boss != null && boss.Alive;
            bossPanel.gameObject.SetActive(bossActive);
            if (bossActive)
            {
                Width(bossFill, 544 * boss.HP / boss.Maximum);
                if (lastPhase != boss.Phase)
                {
                    lastPhase = boss.Phase;
                    bossPhase.text = boss.Phase == 1 ? "PHASE I / III" : boss.Phase == 2 ? "PHASE II / III" : "PHASE III / III";
                }
            }
            UpdateCombatSignals();
            UpdateWorldHud();
            if (showPerformance) UpdatePerf();
        }

        static void Width(Image bar, float width)
        {
            Vector2 size = bar.rectTransform.sizeDelta;
            if (Mathf.Approximately(size.x, width)) return;
            bar.rectTransform.sizeDelta = new Vector2(width, size.y);
        }

        void UpdateAmmo()
        {
            var guns = game.Guns;
            if (lastWeapon != guns.Selected)
            {
                lastWeapon = guns.Selected;
                ammoIcon.sprite = weaponIcons[lastWeapon];
                weaponName.text = GameConfig.Weapons[lastWeapon].Name;
                for (int i = 0; i < weaponIcons.Length; i++)
                {
                    slotRail[i].enabled = i == lastWeapon;
                    slotBack[i].color = i == lastWeapon ? Glass : Panel;
                    slotIcon[i].color = i == lastWeapon ? Teal : Muted;
                }
            }
            if (lastAmmo != guns.Magazine)
            {
                lastAmmo = guns.Magazine;
                magazine.text = lastAmmo.ToString("D2");
                magazine.color = lastAmmo <= 2 ? Gold : Paper;
            }
            if (lastReserve != guns.Reserve)
            {
                lastReserve = guns.Reserve;
                reserve.text = lastReserve < 0 ? "∞" : lastReserve.ToString();
            }
            weaponName.color = guns.Reloading ? Gold : Muted;
        }

        void UpdateCombatSignals()
        {
            sight.gameObject.SetActive(game.Player.Aim < .64f);
            float radius = 8 + Mathf.Tan(game.Guns.Spread * Mathf.Deg2Rad) * 560;
            crosshair[0].anchoredPosition = new(radius, 0);
            crosshair[1].anchoredPosition = new(0, radius);
            crosshair[2].anchoredPosition = new(-radius, 0);
            crosshair[3].anchoredPosition = new(0, -radius);
            hit.gameObject.SetActive(Time.time < game.HitUntil);
            if (hit.gameObject.activeSelf)
                foreach (Image dash in marker) dash.color = game.KillMarker ? Red : Paper;
            float damage = game.Player.DamagePulse;
            damageVeil.enabled = damage > 0;
            damageArrow.gameObject.SetActive(damage > 0);
            if (damage <= 0) return;
            damageVeil.color = new Color(.88f, .045f, .053f, damage * .68f);
            Vector3 origin = game.Player.Root.InverseTransformDirection(game.Player.DamageDirection - game.Player.Position);
            float heading = Mathf.Atan2(origin.x, origin.z);
            damageArrow.anchoredPosition = new Vector2(Mathf.Sin(heading), Mathf.Cos(heading)) * 121;
            damageArrow.localRotation = Quaternion.Euler(0, 0, -heading * Mathf.Rad2Deg);
        }

        void UpdatePerf()
        {
            frameAccum += Time.unscaledDeltaTime;
            frameCount++;
            if (Time.unscaledTime < nextPerf) return;
            nextPerf = Time.unscaledTime + .35f;
            float frame = frameAccum / Mathf.Max(1, frameCount);
            frameAccum = 0;
            frameCount = 0;
            long count = drawCalls.Count;
            string draw = count > 0 ? count.ToString() : "n/a";
            perf.text = $"{1 / Mathf.Max(.0001f, frame):F0} FPS   {1000 * frame:F2} ms   DRAW {draw}\n" +
                $"ENEMIES {game.Alive}   PROJECTILES {game.ProjectilesActive}\n" +
                $"PARTICLES {game.ParticleCount}   BODIES {game.Alive * 2 + 1}   GOD {(game.Player.Invulnerable ? "ON" : "OFF")}";
        }

        public void Dispose() => drawCalls.Dispose();
    }
}


