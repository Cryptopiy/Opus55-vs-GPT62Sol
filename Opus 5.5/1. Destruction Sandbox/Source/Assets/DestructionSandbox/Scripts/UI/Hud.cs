using UnityEngine;
using UnityEngine.UI;
using U = DestructionSandbox.GameConfig.UI;
using Wpn = DestructionSandbox.GameConfig.Weapons;

namespace DestructionSandbox
{
    /// <summary>
    /// D-23 HUD: crosshair, selected weapon + ammo, cooldown bar, score, destroyed counter and %, combo,
    /// active shard count, session timer (and a slow-motion tag). Texts are refreshed at 10 Hz and only when a
    /// value changed, so the HUD does not allocate strings every frame.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        private Text scoreValue, comboText, structureText, destroyedText, timerText, shardText, weaponText, ammoText, slowMoText;
        private Image destroyedFill, cooldownFill;
        private readonly Image[] slots = new Image[Wpn.WeaponCount];
        private float refreshTimer;
        private int lastScore = -1, lastCombo = -1, lastDestroyed = -1, lastTotal = -1, lastSecond = -1, lastShards = -1, lastAmmo = -1, lastBuild = -1;
        private WeaponType lastWeapon = (WeaponType)(-1);

        public Canvas Canvas { get; private set; }

        public void Build(Transform parent)
        {
            Canvas = UIFactory.CreateCanvas(parent, "HUD", 0);
            Transform root = Canvas.transform;
            Color text = UIFactory.TextColor;
            Color dim = UIFactory.DimTextColor;
            Color accent = UIFactory.AccentColor;

            // Crosshair.
            var c = new Vector2(0.5f, 0.5f);
            UIFactory.Image(root, "CrossDot", Color.white, c, c, Vector2.zero, new Vector2(4f, 4f));
            UIFactory.Image(root, "CrossL", Color.white, c, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(14f, 2.5f));
            UIFactory.Image(root, "CrossR", Color.white, c, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(14f, 2.5f));
            UIFactory.Image(root, "CrossU", Color.white, c, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(2.5f, 14f));
            UIFactory.Image(root, "CrossD", Color.white, c, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(2.5f, 14f));

            // Top-left: score + combo.
            var tl = new Vector2(0f, 1f);
            UIFactory.Image(root, "ScorePanel", UIFactory.PanelColor, tl, tl, new Vector2(24f, -24f), new Vector2(300f, 120f));
            UIFactory.Label(root, "ScoreLabel", "SCORE", U.FontSizeSmall, TextAnchor.UpperLeft, dim, tl, tl, new Vector2(40f, -34f), new Vector2(260f, 30f));
            scoreValue = UIFactory.Label(root, "ScoreValue", "0", U.FontSizeLarge + 6, TextAnchor.UpperLeft, text, tl, tl, new Vector2(40f, -58f), new Vector2(260f, 50f));
            comboText = UIFactory.Label(root, "Combo", "COMBO x1", U.FontSizeMedium, TextAnchor.UpperLeft, accent, tl, tl, new Vector2(40f, -106f), new Vector2(260f, 34f));

            // Top-centre: structure + destroyed counter with a progress bar.
            var tc = new Vector2(0.5f, 1f);
            UIFactory.Image(root, "DestroyPanel", UIFactory.PanelColor, tc, tc, new Vector2(0f, -24f), new Vector2(560f, 104f));
            structureText = UIFactory.Label(root, "Structure", "TOWER", U.FontSizeSmall, TextAnchor.UpperCenter, dim, tc, tc, new Vector2(0f, -34f), new Vector2(540f, 28f));
            destroyedText = UIFactory.Label(root, "Destroyed", "Destroyed: 0 / 0 (0%)", U.FontSizeMedium + 2, TextAnchor.UpperCenter, text, tc, tc, new Vector2(0f, -62f), new Vector2(540f, 36f));
            UIFactory.Image(root, "DestroyBarBg", new Color(1f, 1f, 1f, 0.12f), tc, tc, new Vector2(0f, -106f), new Vector2(500f, 8f));
            destroyedFill = UIFactory.Image(root, "DestroyBarFill", accent, tc, new Vector2(0f, 1f), new Vector2(-250f, -106f), new Vector2(0f, 8f));

            // Top-right: timer + shards.
            var tr = new Vector2(1f, 1f);
            UIFactory.Image(root, "InfoPanel", UIFactory.PanelColor, tr, tr, new Vector2(-24f, -24f), new Vector2(300f, 104f));
            timerText = UIFactory.Label(root, "Timer", "00:00", U.FontSizeLarge, TextAnchor.UpperRight, text, tr, tr, new Vector2(-40f, -32f), new Vector2(260f, 46f));
            shardText = UIFactory.Label(root, "Shards", "Shards: 0 / 1500", U.FontSizeSmall, TextAnchor.UpperRight, dim, tr, tr, new Vector2(-40f, -84f), new Vector2(260f, 30f));

            // Bottom-right: weapon panel.
            var br = new Vector2(1f, 0f);
            UIFactory.Image(root, "WeaponPanel", UIFactory.PanelColor, br, br, new Vector2(-24f, 24f), new Vector2(380f, 150f));
            weaponText = UIFactory.Label(root, "Weapon", "CANNONBALL", U.FontSizeMedium + 2, TextAnchor.UpperRight, text, br, br, new Vector2(-40f, 134f), new Vector2(340f, 36f));
            ammoText = UIFactory.Label(root, "Ammo", "AMMO  INF", U.FontSizeMedium, TextAnchor.UpperRight, accent, br, br, new Vector2(-40f, 98f), new Vector2(340f, 32f));
            UIFactory.Image(root, "CooldownBg", new Color(1f, 1f, 1f, 0.12f), br, br, new Vector2(-40f, 54f), new Vector2(340f, 10f));
            cooldownFill = UIFactory.Image(root, "CooldownFill", Color.white, br, new Vector2(0f, 0f), new Vector2(-380f, 54f), new Vector2(340f, 10f));
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = UIFactory.Image(root, "Slot" + i, new Color(1f, 1f, 1f, 0.15f), br, br, new Vector2(-40f - (slots.Length - 1 - i) * 60f, 76f), new Vector2(52f, 14f));
                UIFactory.Label(slots[i].transform, "Key", (i + 1).ToString(), U.FontSizeSmall - 6, TextAnchor.MiddleCenter, text,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 14f), false);
            }

            // Bottom-centre: slow-motion tag.
            var bc = new Vector2(0.5f, 0f);
            slowMoText = UIFactory.Label(root, "SlowMo", "SLOW MOTION  x0.25", U.FontSizeMedium, TextAnchor.LowerCenter, new Color(0.55f, 0.85f, 1f), bc, bc, new Vector2(0f, 40f), new Vector2(500f, 36f));
            slowMoText.enabled = false;
        }

        private void Update()
        {
            // The cooldown bar is smooth (every frame); text fields are throttled.
            WeaponController w = Game.Weapons;
            float cd = w.CooldownFraction(w.Current);
            cooldownFill.rectTransform.sizeDelta = new Vector2(340f * (1f - cd), 10f);
            cooldownFill.color = cd <= 0f && w.HasAmmo(w.Current) ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.55f, 0.35f);
            slowMoText.enabled = Game.SlowMo.Active;

            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer < U.HudRefreshInterval) return;
            refreshTimer = 0f;
            Refresh();
        }

        private void Refresh()
        {
            ScoreSystem s = Game.Score;
            if (s.Score != lastScore) { lastScore = s.Score; scoreValue.text = NumberText.Get(s.Score); }
            if (s.Combo != lastCombo)
            {
                lastCombo = s.Combo;
                comboText.text = "COMBO x" + NumberText.Get(s.Combo);
                comboText.fontSize = GameConfig.UI.FontSizeMedium + (s.Combo - 1) * 3;
            }

            StructureManager st = Game.Structures;
            if (st.DestroyedBlocks != lastDestroyed || st.TotalBlocks != lastTotal || st.BuildCount != lastBuild)
            {
                lastDestroyed = st.DestroyedBlocks;
                lastTotal = st.TotalBlocks;
                lastBuild = st.BuildCount;
                int pct = st.TotalBlocks > 0 ? Mathf.RoundToInt(100f * st.DestroyedBlocks / st.TotalBlocks) : 0;
                destroyedText.text = "Destroyed: " + NumberText.Get(st.DestroyedBlocks) + " / " + NumberText.Get(st.TotalBlocks) + " (" + NumberText.Get(pct) + "%)";
                destroyedFill.rectTransform.sizeDelta = new Vector2(500f * pct / 100f, 8f);
                structureText.text = st.CurrentName.ToUpperInvariant();
            }

            int second = Mathf.FloorToInt(Game.SessionTime);
            if (second != lastSecond)
            {
                lastSecond = second;
                timerText.text = NumberText.Clock(second);
            }

            int shards = Game.Shards.ActiveCount;
            if (shards != lastShards)
            {
                lastShards = shards;
                shardText.text = "Shards: " + NumberText.Get(shards) + " / " + NumberText.Get(GameConfig.Fracture.ShardBudget);
            }

            WeaponController w = Game.Weapons;
            int ammo = w.Current == WeaponType.Shell ? w.ShellAmmo : -1;
            if (w.Current != lastWeapon || ammo != lastAmmo)
            {
                lastWeapon = w.Current;
                lastAmmo = ammo;
                weaponText.text = WeaponController.DisplayName(w.Current);
                ammoText.text = ammo < 0 ? "AMMO  INFINITE" : "AMMO  " + NumberText.Get(ammo) + " / " + NumberText.Get(Wpn.ShellAmmo);
                for (int i = 0; i < slots.Length; i++)
                    slots[i].color = i == (int)w.Current ? UIFactory.AccentColor : new Color(1f, 1f, 1f, 0.15f);
            }
        }
    }

    /// <summary>Caches number strings so HUD updates do not allocate for already seen values.</summary>
    public static class NumberText
    {
        private static readonly string[] cache = new string[4096];
        private static readonly string[] clock = new string[60 * 100];

        public static string Get(int value)
        {
            if (value >= 0 && value < cache.Length) return cache[value] ?? (cache[value] = value.ToString());
            return value.ToString();
        }

        public static string Clock(int seconds)
        {
            if (seconds >= 0 && seconds < clock.Length)
                return clock[seconds] ?? (clock[seconds] = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00"));
            return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        }
    }
}
