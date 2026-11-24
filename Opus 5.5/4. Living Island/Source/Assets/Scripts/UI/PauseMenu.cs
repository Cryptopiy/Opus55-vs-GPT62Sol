using UnityEngine;

namespace LivingIsland
{
    /// <summary>Esc pause menu: Resume / Restart / Quit (time scale is 0 while it is open).</summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        public void Build(Transform canvas)
        {
            UIFactory.Rect(gameObject, canvas, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var dim = UIFactory.Panel(transform, "Dim", new Color(0f, 0f, 0f, 0.45f));
            dim.sprite = null;
            UIFactory.Rect(dim.gameObject, transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            var panel = UIFactory.Panel(transform, "Panel", UIFactory.PanelColor);
            UIFactory.Rect(panel.gameObject, transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 300));
            var t = panel.transform;
            UIFactory.Label(t, "Paused", 30, TextAnchor.MiddleCenter, UIFactory.TextColor, 0, 18, 340, 44).fontStyle = FontStyle.Bold;
            UIFactory.Button(t, "Resume", 50, 84, 240, 48, () => Game.UI.SetPaused(false));
            UIFactory.Button(t, "Restart", 50, 146, 240, 48, () => FindAnyObjectByType<GameBootstrap>().Restart());
            UIFactory.Button(t, "Quit", 50, 208, 240, 48, () => FindAnyObjectByType<GameBootstrap>().Quit());
        }
    }
}
