using UnityEngine;
using UnityEngine.UI;
using static WaveSurvival.HudWidgets;

namespace WaveSurvival
{
    internal sealed partial class Hud
    {
        RectTransform screen, pause, result;
        Text resultTitle, resultSubtitle, rank;
        RectTransform endlessButton, resultQuit;
        Text[] values;
        MatchState lastMenu = (MatchState)(-1);

        void BuildMenus()
        {
            screen = Stretch(root, "Modal overlay");
            ColorBlock(screen, new Color(.012f, .027f, .03f, .80f));
            pause = Card(screen, "Paused", 620, 507, Teal);
            Label(pause, "BASTION 06  /  WAVE SURVIVAL", 16, TL, new(35, -24), new(550, 34), Teal, bold: true);
            Label(pause, "Paused", 68, TL, new(33, -55), new(553, 99), Paper, bold: true);
            Block(pause, "Separator", TL, new(36, -168), new(548, 1), Glass);
            Label(pause, "WASD  Move       SHIFT  Sprint       C  Crouch       SPACE  Jump\n" +
                "LMB  Fire        RMB  Aim           R  Reload       1–3  Weapons",
                18, TL, new(36, -182), new(548, 69), Muted, TextAnchor.MiddleLeft);
            Action(pause, "RESUME", new(36, -274), new(548, 59), Panel, Teal, game.Resume);
            Action(pause, "RESTART", new(36, -348), new(548, 56), Paper, Glass, game.Restart);
            Action(pause, "QUIT", new(36, -419), new(548, 53), Paper, Glass, game.Quit);

            result = Card(screen, "Match results", 734, 673, Gold);
            resultSubtitle = Label(result, "", 16, TL, new(36, -26), new(530, 32), Gold, bold: true);
            resultTitle = Label(result, "", 59, TL, new(32, -58), new(640, 94), Paper, bold: true);
            var rankPanel = Box(result, "Rank", TR, new(-35, -29), new(108, 112));
            ColorBlock(rankPanel, Glass);
            Label(rankPanel, "RANK", 15, new(.5f, 1), new(0, -4), new(108, 28), Muted, TextAnchor.MiddleCenter, true);
            rank = Label(rankPanel, "", 59, new(.5f, 0), new(0, 3), new(108, 80), Gold, TextAnchor.MiddleCenter, true);

            string[] rows = { "WAVES CLEARED", "RUNNERS", "SHOOTERS", "TANKS", "WARDEN", "ACCURACY", "HEADSHOTS", "TIME", "DAMAGE TAKEN", "SCORE" };
            values = new Text[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                int col = i / 5;
                int row = i % 5;
                var metric = Box(result, rows[i], TL, new(35 + col * 336, -180 - row * 60), new(325, 48));
                ColorBlock(metric, Glass);
                Label(metric, rows[i], 15, TL, new(12, -2), new(171, 44), Muted, bold: true);
                values[i] = Label(metric, "", 23, TR, new(-11, -2), new(141, 44), Paper, TextAnchor.MiddleRight, true);
            }
            Action(result, "RESTART", new(35, -530), new(325, 54), Panel, Gold, game.Restart);
            endlessButton = (RectTransform)Action(result, "ENDLESS MODE", new(371, -530), new(325, 54),
                Paper, Glass, game.StartEndless).transform;
            resultQuit = (RectTransform)Action(result, "QUIT", new(35, -599), new(661, 44), Paper, Glass, game.Quit).transform;
        }

        static RectTransform Card(Transform parent, string label, float width, float height, Color accent)
        {
            var rect = Box(parent, label, Center, Vector2.zero, new(width, height));
            ColorBlock(rect, Panel);
            Block(rect, "Accent line", TL, Vector2.zero, new(width, 3), accent);
            return rect;
        }

        void UpdateMenus()
        {
            MatchState current = game.State;
            if (current == lastMenu) return;
            lastMenu = current;
            screen.gameObject.SetActive(current != MatchState.Playing);
            pause.gameObject.SetActive(current == MatchState.Paused);
            bool victory = current == MatchState.Victory;
            result.gameObject.SetActive(victory || current == MatchState.Dead);
            if (!result.gameObject.activeSelf) return;
            resultTitle.text = victory ? "Victory" : "You died";
            resultSubtitle.text = victory ? "WARDEN ELIMINATED" : "BASTION 06  /  MISSION REPORT";
            endlessButton.gameObject.SetActive(victory);
            resultQuit.anchoredPosition = victory ? new(35, -599) : new(371, -530);
            resultQuit.sizeDelta = victory ? new(661, 44) : new(325, 54);
            var stats = game.Stats;
            rank.text = stats.Rank;
            values[0].text = stats.WavesCleared.ToString();
            for (int i = 0; i < stats.Kills.Length; i++) values[1 + i].text = stats.Kills[i].ToString();
            values[5].text = (100 * stats.Accuracy).ToString("F1") + "%";
            values[6].text = stats.Headshots.ToString();
            int seconds = Mathf.FloorToInt(stats.Time);
            values[7].text = $"{seconds / 60:D2}:{seconds % 60:D2}";
            values[8].text = stats.Damage.ToString("F0");
            values[9].text = stats.Score.ToString("D6");
        }
    }
}
