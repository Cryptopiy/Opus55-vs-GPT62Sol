using UnityEngine;
using UnityEngine.UI;

namespace WaveSurvival
{
    /// <summary>
    /// S-25 HUD: health bar + number, stamina bar, ammo (magazine / reserve) with weapon icon, 3 weapon slots,
    /// wave / enemies remaining / score, wave banner (S-21), damage vignette (S-06) and short notifications.
    /// Text is only rewritten when the displayed value changes (no per-frame string allocations).
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public RectTransform Root { get; private set; }
        public Crosshair Crosshair { get; private set; }
        public DamageIndicators Indicators { get; private set; }
        public BossBar BossBar { get; private set; }
        public HealthBars HealthBars { get; private set; }
        public Menus Menus { get; private set; }
        public DebugOverlay Overlay { get; private set; }

        Image healthFill, healthLag, staminaFill, vignette, weaponIcon;
        Text healthText, magText, reserveText, weaponName, waveText, enemiesText, scoreText, bannerTitle, bannerCount, notifyText, godText;
        readonly Image[] slotBg = new Image[3];
        readonly Image[] slotIcon = new Image[3];
        readonly Text[] slotKey = new Text[3];
        RectTransform banner;
        float vignetteAlpha, notifyTimer, lagHealth = 1f;
        int lastHp = -1, lastMag = -1, lastReserve = -2, lastWeapon = -1, lastWave = -1, lastEnemies = -1, lastScore = -1, lastCount = -1, lastSlot = -1;
        bool lastBossWave;

        public static Hud Create(Transform parent)
        {
            var go = new GameObject("HUD");
            go.transform.SetParent(parent, false);
            Hud hud = go.AddComponent<Hud>();
            hud.Build();
            return hud;
        }

        void Build()
        {
            UIFactory.EnsureEventSystem(transform);
            Canvas = UIFactory.Canvas(transform, "HudCanvas", 0);
            Root = (RectTransform)Canvas.transform;

            vignette = UIFactory.Image(Root, "DamageVignette", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.75f, 0f, 0f, 0f), GameAssets.VignetteSprite);
            RectTransform vr = vignette.rectTransform;
            vr.anchorMin = Vector2.zero;
            vr.anchorMax = Vector2.one;
            vr.sizeDelta = Vector2.zero;

            HealthBars = new HealthBars(Root);
            Crosshair = new Crosshair(Root);
            Indicators = new DamageIndicators(Root);
            BuildVitals();
            BuildWeapons();
            BuildWaveInfo();
            BossBar = new BossBar(Root);
            Overlay = new DebugOverlay(Root);
            Menus = new Menus(transform); // separate canvas, never hidden by F8 (pause/death/victory must stay usable)
            UiVisibility.RegisterCanvas(Canvas);
        }

        void BuildVitals()
        {
            Vector2 bl = Vector2.zero;
            UIFactory.Image(Root, "VitalsBg", bl, bl, new Vector2(28f, 28f), new Vector2(470f, 92f), UIFactory.Panel);
            UIFactory.Image(Root, "HealthIcon", bl, bl, new Vector2(44f, 62f), new Vector2(40f, 40f), new Color(1f, 0.25f, 0.2f), GameAssets.HealthCrossSprite);
            UIFactory.Image(Root, "HealthBg", bl, bl, new Vector2(96f, 66f), new Vector2(300f, 30f), new Color(0.1f, 0.02f, 0.02f, 0.9f));
            healthLag = UIFactory.Image(Root, "HealthLag", bl, bl, new Vector2(98f, 68f), new Vector2(296f, 26f), new Color(1f, 0.85f, 0.6f, 0.75f));
            healthFill = UIFactory.Image(Root, "HealthFill", bl, bl, new Vector2(98f, 68f), new Vector2(296f, 26f), new Color(0.88f, 0.16f, 0.12f));
            healthText = UIFactory.Text(Root, "HealthText", bl, bl, new Vector2(408f, 60f), new Vector2(80f, 44f), 38, TextAnchor.MiddleLeft, Color.white);
            UIFactory.Text(Root, "StaminaLabel", bl, bl, new Vector2(44f, 34f), new Vector2(60f, 20f), 14, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.7f)).text = "STAMINA";
            UIFactory.Image(Root, "StaminaBg", bl, bl, new Vector2(122f, 38f), new Vector2(272f, 12f), new Color(0.05f, 0.06f, 0.03f, 0.9f));
            staminaFill = UIFactory.Image(Root, "StaminaFill", bl, bl, new Vector2(123f, 39f), new Vector2(270f, 10f), new Color(0.85f, 0.85f, 0.25f));
            godText = UIFactory.Text(Root, "GodMode", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(300f, 30f), 22, TextAnchor.UpperRight, new Color(1f, 0.85f, 0.2f));
            godText.text = "GOD MODE (F4)";
            godText.enabled = false;
        }

        void BuildWeapons()
        {
            Vector2 br = new Vector2(1f, 0f);
            UIFactory.Image(Root, "AmmoBg", br, br, new Vector2(-28f, 28f), new Vector2(430f, 130f), UIFactory.Panel);
            weaponIcon = UIFactory.Image(Root, "WeaponIcon", br, br, new Vector2(-280f, 52f), new Vector2(160f, 60f), new Color(1f, 1f, 1f, 0.9f), GameAssets.WeaponIcons[0]);
            magText = UIFactory.Text(Root, "Mag", br, br, new Vector2(-150f, 48f), new Vector2(110f, 70f), 64, TextAnchor.MiddleRight, Color.white);
            reserveText = UIFactory.Text(Root, "Reserve", br, br, new Vector2(-44f, 52f), new Vector2(104f, 40f), 30, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.75f));
            weaponName = UIFactory.Text(Root, "WeaponName", br, br, new Vector2(-44f, 118f), new Vector2(380f, 26f), 20, TextAnchor.MiddleRight, UIFactory.Accent);
            for (int i = 0; i < 3; i++)
            {
                Vector2 pos = new Vector2(-28f - (2 - i) * 146f, 170f);
                slotBg[i] = UIFactory.Image(Root, "Slot" + i, br, br, pos, new Vector2(138f, 58f), UIFactory.Panel);
                slotIcon[i] = UIFactory.Image(slotBg[i].rectTransform, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(10f, 0f), new Vector2(104f, 39f), new Color(1f, 1f, 1f, 0.6f), GameAssets.WeaponIcons[i]);
                slotKey[i] = UIFactory.Text(slotBg[i].rectTransform, "Key", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -4f), new Vector2(24f, 24f), 20, TextAnchor.UpperLeft, Color.white);
                slotKey[i].text = StringCache.Int(i + 1);
            }
        }

        void BuildWaveInfo()
        {
            Vector2 tl = new Vector2(0f, 1f);
            UIFactory.Image(Root, "WaveBg", tl, tl, new Vector2(28f, -28f), new Vector2(300f, 118f), UIFactory.Panel);
            waveText = UIFactory.Text(Root, "Wave", tl, tl, new Vector2(46f, -34f), new Vector2(270f, 46f), 40, TextAnchor.UpperLeft, UIFactory.Accent);
            enemiesText = UIFactory.Text(Root, "Enemies", tl, tl, new Vector2(46f, -80f), new Vector2(270f, 30f), 24, TextAnchor.UpperLeft, Color.white);
            scoreText = UIFactory.Text(Root, "Score", tl, tl, new Vector2(46f, -110f), new Vector2(270f, 30f), 24, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.8f));

            Vector2 c = new Vector2(0.5f, 1f);
            banner = UIFactory.Rect(Root, "Banner", c, c, new Vector2(0f, -210f), new Vector2(900f, 200f));
            bannerTitle = UIFactory.Text(banner, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(900f, 70f), 58, TextAnchor.MiddleCenter, UIFactory.Accent);
            bannerCount = UIFactory.Text(banner, "Count", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 110f), 96, TextAnchor.MiddleCenter, Color.white);
            banner.gameObject.SetActive(false);

            notifyText = UIFactory.Text(Root, "Notify", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(600f, 40f), 28, TextAnchor.MiddleCenter, Color.white);
            notifyText.enabled = false;
        }

        // ---------------- events ----------------

        public void OnPlayerDamaged(Vector3 source, float amount)
        {
            vignetteAlpha = Mathf.Min(1f, vignetteAlpha + GameConfig.Player.VignettePulse * Mathf.Clamp(amount / 15f, 0.5f, 1.5f));
            Indicators.Show(source);
        }

        public void ShowHitMarker(bool kill) => Crosshair.HitMarker(kill);

        public void Notify(string text, Color color)
        {
            notifyText.text = text;
            notifyText.color = color;
            notifyText.enabled = true;
            notifyTimer = 1.6f;
        }

        // ---------------- per-frame ----------------

        void LateUpdate()
        {
            PlayerRig p = Game.Player;
            if (p == null) return;
            float dt = Time.unscaledDeltaTime;
            UpdateVitals(p, dt);
            UpdateWeapons(p.Weapons);
            UpdateWaveInfo();
            Crosshair.Update(p, dt);
            Indicators.Update(p, dt);
            BossBar.Update(Game.Enemies.Boss);
            HealthBars.Update(p.Camera, Root);
            Overlay.Update(dt);
            godText.enabled = Game.GodMode;

            vignetteAlpha = Mathf.Max(0f, vignetteAlpha - GameConfig.Player.VignetteFade * Time.deltaTime);
            float lowHp = p.Health.Hp < 30f && p.IsAlive ? 0.25f + 0.1f * Mathf.Sin(Time.time * 5f) : 0f;
            vignette.color = new Color(0.75f, 0f, 0f, Mathf.Max(vignetteAlpha, lowHp));
            if (notifyTimer > 0f)
            {
                notifyTimer -= dt;
                Color c = notifyText.color;
                c.a = Mathf.Clamp01(notifyTimer / 0.4f);
                notifyText.color = c;
                if (notifyTimer <= 0f) notifyText.enabled = false;
            }
        }

        void UpdateVitals(PlayerRig p, float dt)
        {
            float hp01 = p.Health.Hp / GameConfig.Player.MaxHealth;
            SetWidth(healthFill, 296f * hp01);
            lagHealth = Mathf.Max(hp01, Mathf.MoveTowards(lagHealth, hp01, dt * 0.6f)); // white "damage taken" trail
            SetWidth(healthLag, 296f * lagHealth);
            int hp = Mathf.CeilToInt(p.Health.Hp);
            if (hp != lastHp)
            {
                lastHp = hp;
                healthText.text = StringCache.Int(hp);
                healthText.color = hp < 30 ? new Color(1f, 0.35f, 0.3f) : Color.white;
            }
            float st = p.Motor.Stamina / GameConfig.Player.StaminaMax;
            SetWidth(staminaFill, 270f * st);
            staminaFill.color = st < 0.25f ? new Color(0.9f, 0.4f, 0.2f) : new Color(0.85f, 0.85f, 0.25f);
        }

        void UpdateWeapons(WeaponController w)
        {
            WeaponSlot s = w.Current;
            int idx = w.CurrentIndex;
            if (idx != lastWeapon)
            {
                lastWeapon = idx;
                weaponIcon.sprite = GameAssets.WeaponIcons[idx];
                weaponName.text = s.Def.Name;
                lastMag = -1;
                lastReserve = -2;
            }
            if (s.Mag != lastMag)
            {
                lastMag = s.Mag;
                magText.text = StringCache.Int(s.Mag);
                magText.color = s.Mag == 0 ? new Color(1f, 0.3f, 0.25f) : s.Mag <= s.Def.MagSize / 4 ? new Color(1f, 0.75f, 0.3f) : Color.white;
            }
            int reserve = s.Def.InfiniteReserve ? -1 : s.Reserve;
            if (reserve != lastReserve)
            {
                lastReserve = reserve;
                reserveText.text = reserve < 0 ? "/ ∞" : "/ " + StringCache.Int(reserve);
            }
            int target = w.SwitchTarget;
            if (target != lastSlot)
            {
                lastSlot = target;
                for (int i = 0; i < 3; i++)
                {
                    bool sel = i == target;
                    slotBg[i].color = sel ? new Color(0.95f, 0.55f, 0.15f, 0.55f) : UIFactory.Panel;
                    slotIcon[i].color = new Color(1f, 1f, 1f, sel ? 1f : 0.45f);
                    slotKey[i].color = sel ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                }
            }
        }

        void UpdateWaveInfo()
        {
            WaveManager waves = Game.Waves;
            int wave = waves.Wave;
            if (wave != lastWave || waves.IsBossWave != lastBossWave)
            {
                lastWave = wave;
                lastBossWave = waves.IsBossWave;
                waveText.text = waves.IsBossWave ? "WAVE " + StringCache.Int(wave) + " – BOSS" : "WAVE " + StringCache.Int(wave);
            }
            int remaining = waves.Phase == WavePhase.Countdown ? 0 : waves.Remaining;
            if (remaining != lastEnemies)
            {
                lastEnemies = remaining;
                enemiesText.text = "ENEMIES  " + StringCache.Int(remaining);
            }
            if (Game.Stats.Score != lastScore)
            {
                lastScore = Game.Stats.Score;
                scoreText.text = "SCORE  " + StringCache.Int(lastScore);
            }
            bool showBanner = waves.Phase == WavePhase.Countdown && Game.Manager.State == GameState.Playing;
            if (banner.gameObject.activeSelf != showBanner)
            {
                banner.gameObject.SetActive(showBanner);
                if (showBanner)
                    bannerTitle.text = wave == GameConfig.Waves.BossWave && !waves.Endless
                        ? "WAVE " + StringCache.Int(wave) + " INCOMING – THE WARDEN"
                        : "WAVE " + StringCache.Int(wave) + " INCOMING";
            }
            if (showBanner)
            {
                int count = Mathf.CeilToInt(waves.Countdown);
                if (count != lastCount)
                {
                    lastCount = count;
                    bannerCount.text = StringCache.Int(count);
                }
                float pulse = 1f + 0.08f * Mathf.Max(0f, Mathf.Sin(waves.Countdown * Mathf.PI * 2f));
                bannerCount.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        void OnDestroy() => Overlay?.Dispose();

        static void SetWidth(Image img, float w)
        {
            Vector2 s = img.rectTransform.sizeDelta;
            if (!Mathf.Approximately(s.x, w)) img.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, w), s.y);
        }
    }
}
