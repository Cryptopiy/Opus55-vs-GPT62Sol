using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

namespace WaveSurvival
{
    /// <summary>
    /// F1 debug overlay (section 4): FPS, frame time (avg / worst), draw calls / batches / set-pass calls (render
    /// counters via ProfilerRecorder), physics bodies, enemies by type, projectiles, particles, decals, tracers,
    /// pickups, health bars and GC allocations. Refreshed every 0.5 s with a reused StringBuilder.
    /// </summary>
    public sealed class DebugOverlay
    {
        readonly GameObject root;
        readonly Text text;
        readonly StringBuilder sb = new StringBuilder(1024);
        ProfilerRecorder drawCalls, batches, setPass, triangles, gcAlloc;
        float timer, accTime, worst;
        int frames;
        public bool Visible { get; private set; }
        public float LastFps { get; private set; }
        public float LastAvgMs { get; private set; }
        public float LastWorstMs { get; private set; }

        public DebugOverlay(RectTransform parent)
        {
            Vector2 tr = new Vector2(1f, 1f);
            Image bg = UIFactory.Image(parent, "DebugOverlay", tr, tr, new Vector2(-24f, -64f), new Vector2(430f, 470f), new Color(0f, 0f, 0f, 0.7f));
            root = bg.gameObject;
            text = UIFactory.Text(bg.rectTransform, "Text", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(400f, 450f), 19, TextAnchor.UpperLeft, new Color(0.75f, 1f, 0.75f), FontStyle.Normal);
            root.SetActive(false);
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        public void Toggle()
        {
            Visible = !Visible;
            root.SetActive(Visible);
        }

        public long GcAllocatedInFrame => gcAlloc.Valid ? gcAlloc.LastValue : -1;

        public void Update(float dt)
        {
            frames++;
            accTime += dt;
            worst = Mathf.Max(worst, dt);
            timer += dt;
            if (timer < GameConfig.Hud.FpsUpdateInterval) return;
            LastFps = frames / Mathf.Max(1e-5f, accTime);
            LastAvgMs = accTime / Mathf.Max(1, frames) * 1000f;
            LastWorstMs = worst * 1000f;
            timer = 0f;
            accTime = 0f;
            frames = 0;
            worst = 0f;
            if (!Visible) return;

            EnemyManager em = Game.Enemies;
            int bodies = em.AliveCount + Game.Fx.ActiveShells + 1; // kinematic enemy bodies + shell rigidbodies + player controller
            sb.Clear();
            sb.Append("<b>DEBUG (F1)</b>\n");
            sb.Append("FPS: ").Append(Mathf.RoundToInt(LastFps)).Append("   frame: ").Append(LastAvgMs.ToString("0.0")).Append(" ms (worst ").Append(LastWorstMs.ToString("0.0")).Append(")\n");
            sb.Append("Draw calls: ").Append(Counter(drawCalls)).Append("   batches: ").Append(Counter(batches)).Append('\n');
            sb.Append("SetPass: ").Append(Counter(setPass)).Append("   tris: ").Append(Counter(triangles)).Append('\n');
            sb.Append("GC alloc/frame: ").Append(gcAlloc.Valid ? gcAlloc.LastValue.ToString() : "n/a").Append(" B   GC runs: ").Append(System.GC.CollectionCount(0)).Append('\n');
            sb.Append("Physics bodies: ").Append(bodies).Append("  (shells ").Append(Game.Fx.ActiveShells).Append(")\n");
            sb.Append("Enemies alive: ").Append(em.AliveCount).Append(" / ").Append(GameConfig.Waves.MaxAlive)
              .Append("  R").Append(em.AliveByType[0]).Append(" S").Append(em.AliveByType[1]).Append(" T").Append(em.AliveByType[2]).Append(" W").Append(em.AliveByType[3]).Append('\n');
            sb.Append("Enemy objects (incl. dying): ").Append(em.Active.Count).Append("   pooled: ").Append(em.CreatedCount).Append('\n');
            sb.Append("Projectiles: ").Append(Game.Projectiles.ActiveCount).Append("   tracers: ").Append(Game.Fx.ActiveTracers).Append('\n');
            sb.Append("Particles: ").Append(Game.Fx.ParticleCount).Append("   decals: ").Append(Game.Fx.ActiveDecals).Append('\n');
            sb.Append("Pickups: ").Append(Game.Pickups.ActiveCount).Append("   health bars: ").Append(Game.Hud.HealthBars.ActiveCount).Append('\n');
            sb.Append("Wave: ").Append(Game.Waves.Wave).Append("  queue ").Append(Game.Waves.QueueRemaining).Append("  spawning ").Append(Game.Waves.PendingCount).Append('\n');
            sb.Append("AI debug (F2): ").Append(Game.AiDebug != null && Game.AiDebug.Enabled ? "ON" : "off").Append("   God (F4): ").Append(Game.GodMode ? "ON" : "off");
            text.text = sb.ToString();
        }

        static string Counter(ProfilerRecorder r) => r.Valid ? r.LastValue.ToString() : "n/a";

        public void Dispose()
        {
            drawCalls.Dispose();
            batches.Dispose();
            setPass.Dispose();
            triangles.Dispose();
            gcAlloc.Dispose();
        }
    }
}
