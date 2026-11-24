using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace WaveSurvival
{
    /// <summary>Pause menu (Esc: Resume / Restart / Quit), death screen (S-07) and victory screen (S-24).</summary>
    public sealed class Menus
    {
        readonly GameObject pause, death, victory;
        readonly Text deathStats, victoryStats, rankText;
        readonly StringBuilder sb = new StringBuilder(512);

        public Menus(Transform parent)
        {
            Canvas canvas = UIFactory.Canvas(parent, "MenuCanvas", 10);
            RectTransform root = (RectTransform)canvas.transform;

            pause = MakeScreen(root, "PauseMenu", "PAUSED", new Color(1f, 0.62f, 0.18f));
            UIFactory.Button(pause.transform, "RESUME", new Vector2(0f, 60f), new Vector2(360f, 70f), () => Game.Manager.Resume());
            UIFactory.Button(pause.transform, "RESTART", new Vector2(0f, -30f), new Vector2(360f, 70f), () => Game.Manager.Restart());
            UIFactory.Button(pause.transform, "QUIT", new Vector2(0f, -120f), new Vector2(360f, 70f), () => Game.Manager.Quit());
            Hint(pause.transform, "Controls: WASD move  -  Shift sprint  -  C crouch  -  Space jump  -  LMB fire  -  RMB aim  -  R reload  -  1/2/3/wheel weapons\nF1 debug overlay  -  F2 AI debug  -  F3 jump to wave 5  -  F4 god mode  -  F12 screenshot");

            death = MakeScreen(root, "DeathScreen", "YOU DIED", new Color(0.95f, 0.2f, 0.15f));
            deathStats = StatsText(death.transform);
            UIFactory.Button(death.transform, "RESTART", new Vector2(-200f, -300f), new Vector2(340f, 70f), () => Game.Manager.Restart());
            UIFactory.Button(death.transform, "QUIT", new Vector2(200f, -300f), new Vector2(340f, 70f), () => Game.Manager.Quit());

            victory = MakeScreen(root, "VictoryScreen", "VICTORY – THE WARDEN FALLS", new Color(1f, 0.8f, 0.3f));
            victoryStats = StatsText(victory.transform);
            rankText = UIFactory.Text(victory.transform, "Rank", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(520f, 40f), new Vector2(260f, 220f), 180, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.3f));
            UIFactory.Text(victory.transform, "RankLabel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(520f, 170f), new Vector2(260f, 40f), 26, TextAnchor.MiddleCenter, Color.white).text = "RANK";
            UIFactory.Button(victory.transform, "RESTART", new Vector2(-380f, -300f), new Vector2(340f, 70f), () => Game.Manager.Restart());
            UIFactory.Button(victory.transform, "ENDLESS MODE", new Vector2(0f, -300f), new Vector2(340f, 70f), () => Game.Manager.StartEndless());
            UIFactory.Button(victory.transform, "QUIT", new Vector2(380f, -300f), new Vector2(340f, 70f), () => Game.Manager.Quit());
            HideAll();
        }

        static GameObject MakeScreen(RectTransform root, string name, string title, Color titleColor)
        {
            RectTransform r = UIFactory.Stretch(root, name);
            Image dim = r.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.72f);
            Text t = UIFactory.Text(r, "Title", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(1400f, 110f), 84, TextAnchor.MiddleCenter, titleColor);
            t.text = title;
            return r.gameObject;
        }

        static Text StatsText(Transform parent) =>
            UIFactory.Text(parent, "Stats", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-60f, 20f), new Vector2(700f, 400f), 30, TextAnchor.MiddleLeft, Color.white, FontStyle.Normal);

        static void Hint(Transform parent, string text)
        {
            Text t = UIFactory.Text(parent, "Hint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1700f, 80f), 22, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.75f), FontStyle.Normal);
            t.text = text;
        }

        public void HideAll()
        {
            pause.SetActive(false);
            death.SetActive(false);
            victory.SetActive(false);
        }

        public void ShowPause(bool on) => pause.SetActive(on);

        public void ShowDeath()
        {
            HideAll();
            deathStats.text = BuildStats(false);
            death.SetActive(true);
        }

        public void ShowVictory()
        {
            HideAll();
            victoryStats.text = BuildStats(true);
            rankText.text = Game.Stats.Rank();
            victory.SetActive(true);
        }

        /// <summary>S-24 stats: waves cleared, kills by type, accuracy, headshots, time, damage taken (+ rank on victory).</summary>
        string BuildStats(bool withRank)
        {
            GameStats s = Game.Stats;
            int[] k = s.Kills;
            int minutes = Mathf.FloorToInt(s.PlayTime / 60f), seconds = Mathf.FloorToInt(s.PlayTime % 60f);
            sb.Clear();
            sb.Append("Waves cleared:  ").Append(s.WavesCleared).Append('\n');
            sb.Append("Kills:  ").Append(s.TotalKills).Append("   (Runners ").Append(k[0]).Append(", Shooters ").Append(k[1])
              .Append(", Tanks ").Append(k[2]).Append(", Warden ").Append(k[3]).Append(")\n");
            sb.Append("Accuracy:  ").Append(Mathf.RoundToInt(s.Accuracy * 100f)).Append("%   (").Append(s.ShotsHit).Append(" / ").Append(s.ShotsFired).Append(" shots)\n");
            sb.Append("Headshots:  ").Append(s.Headshots).Append('\n');
            sb.Append("Time:  ").Append(minutes).Append(':').Append(seconds.ToString("00")).Append('\n');
            sb.Append("Damage taken:  ").Append(Mathf.RoundToInt(s.DamageTaken)).Append('\n');
            sb.Append("Score:  ").Append(s.Score);
            if (withRank)
                sb.Append("\n\nRank thresholds: S = acc ").Append(Mathf.RoundToInt(GameConfig.Rank.SAccuracy * 100f)).Append("% / dmg ").Append(GameConfig.Rank.SDamageTaken)
                  .Append(" / ").Append(GameConfig.Rank.STime / 60f).Append(" min,  A = ").Append(Mathf.RoundToInt(GameConfig.Rank.AAccuracy * 100f)).Append("% / ")
                  .Append(GameConfig.Rank.ADamageTaken).Append(" / ").Append(GameConfig.Rank.ATime / 60f).Append(" min,  B = ")
                  .Append(Mathf.RoundToInt(GameConfig.Rank.BAccuracy * 100f)).Append("% / ").Append(GameConfig.Rank.BDamageTaken);
            return sb.ToString();
        }
    }
}
