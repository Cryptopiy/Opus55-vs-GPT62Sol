using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;
using U = DestructionSandbox.GameConfig.UI;

namespace DestructionSandbox
{
    /// <summary>
    /// F1 debug overlay: FPS, frame time (avg / worst), draw calls / batches / SetPass (when the profiler
    /// counters are available: editor and development players), physics bodies (awake / active), blocks,
    /// shards, projectiles, particles, audio voices and pool sizes (used to verify D-28: no leaks).
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        private GameObject root;
        private Text text;
        private readonly StringBuilder sb = new StringBuilder(1024);
        private ProfilerRecorder drawCalls, batches, setPass;
        private float timer;
        private int frames;
        private float frameTimeSum;
        private float worstFrame;

        public bool Visible { get; private set; }
        public float LastFps { get; private set; }
        public float LastFrameMs { get; private set; }
        public float LastWorstMs { get; private set; }
        public string LastText { get; private set; } = string.Empty;

        public void Build(Transform parent)
        {
            Canvas canvas = UIFactory.CreateCanvas(parent, "DebugOverlay", 10);
            root = canvas.gameObject;
            var tl = new Vector2(0f, 1f);
            UIFactory.Image(canvas.transform, "Panel", new Color(0f, 0f, 0f, 0.62f), tl, tl, new Vector2(24f, -160f), new Vector2(470f, 520f));
            text = UIFactory.Label(canvas.transform, "Stats", "", U.FontSizeSmall, TextAnchor.UpperLeft, new Color(0.75f, 1f, 0.75f), tl, tl,
                new Vector2(40f, -172f), new Vector2(440f, 500f), false);
            root.SetActive(false);

            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        }

        private void OnDestroy()
        {
            drawCalls.Dispose();
            batches.Dispose();
            setPass.Dispose();
        }

        public void Toggle()
        {
            Visible = !Visible;
            root.SetActive(Visible);
            timer = U.OverlayRefreshInterval;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            frames++;
            frameTimeSum += dt;
            worstFrame = Mathf.Max(worstFrame, dt);
            timer += dt;
            if (timer < U.OverlayRefreshInterval) return;

            LastFps = frames / Mathf.Max(frameTimeSum, 1e-5f);
            LastFrameMs = frameTimeSum / Mathf.Max(1, frames) * 1000f;
            LastWorstMs = worstFrame * 1000f;
            frames = 0;
            frameTimeSum = 0f;
            worstFrame = 0f;
            timer = 0f;
            if (Visible) Refresh();
        }

        private static string Counter(ProfilerRecorder r)
        {
            return r.Valid && r.LastValue > 0 ? NumberText.Get((int)r.LastValue) : "n/a";
        }

        /// <summary>Builds the overlay text (also used by the self-test log).</summary>
        public string BuildText()
        {
            Game.Structures.Measure(out int blocksAwake, out _, out _);
            int shardsAwake = Game.Shards.CountAwake();
            int projectiles = Game.Projectiles.ActiveCount();
            int blocks = Game.Structures.ActiveBlockCount;
            int shards = Game.Shards.ActiveCount;

            sb.Clear();
            sb.Append("FPS            ").Append(LastFps.ToString("0.0")).Append('\n');
            sb.Append("Frame time     ").Append(LastFrameMs.ToString("0.00")).Append(" ms  (worst ").Append(LastWorstMs.ToString("0.0")).Append(" ms)\n");
            sb.Append("Draw calls     ").Append(Counter(drawCalls)).Append("   batches ").Append(Counter(batches)).Append("   SetPass ").Append(Counter(setPass)).Append('\n');
            sb.Append("Time scale     ").Append(Time.timeScale.ToString("0.00")).Append("   fixed dt ").Append((Time.fixedDeltaTime * 1000f).ToString("0.00")).Append(" ms\n");
            sb.Append("Physics        ").Append(PerfProbe.LastFramePhysicsMs.ToString("0.00")).Append(" ms (").Append(PerfProbe.LastFrameSteps)
              .Append(" steps)   fracture ").Append(PerfProbe.LastFrameFractureMs.ToString("0.00")).Append(" ms\n");
            sb.Append('\n');
            sb.Append("Physics bodies ").Append(blocksAwake + shardsAwake).Append(" awake / ").Append(blocks + shards + projectiles).Append(" active\n");
            sb.Append("Blocks         ").Append(blocks).Append(" active (").Append(blocksAwake).Append(" awake)\n");
            sb.Append("Shards         ").Append(shards).Append(" / ").Append(GameConfig.Fracture.ShardBudget).Append(" (").Append(shardsAwake).Append(" awake)\n");
            sb.Append("Projectiles    ").Append(projectiles).Append("   gravity orbs ").Append(Game.Orbs.ActiveCount).Append('\n');
            sb.Append("Particles      ").Append(Game.Effects.ParticleCount()).Append('\n');
            sb.Append("Audio voices   ").Append(Game.Audio.BusyVoices()).Append(" / ").Append(Game.Audio.VoiceCount).Append('\n');
            sb.Append('\n');
            sb.Append("Block pool     ").Append(Game.Structures.PoolSize).Append(" (").Append(Game.Structures.FreeCount).Append(" free)\n");
            sb.Append("Shard pool     ").Append(Game.Shards.PoolSize).Append(" (").Append(Game.Shards.FreeCount).Append(" free)\n");
            sb.Append("Projectile pool ").Append(Game.Projectiles.TotalPooled).Append("   popups ").Append(GameConfig.Score.MaxPopups).Append('\n');
            sb.Append("Rebuilds       ").Append(Game.Structures.BuildCount).Append('\n');
            sb.Append("Broken         ").Append(Game.Fracture.BlocksBroken).Append(" blocks, ").Append(Game.Fracture.ShardsBroken).Append(" shards\n");
            sb.Append("Managed heap   ").Append((System.GC.GetTotalMemory(false) / (1024f * 1024f)).ToString("0.0")).Append(" MB\n");
            LastText = sb.ToString();
            return LastText;
        }

        private void Refresh()
        {
            text.text = BuildText();
        }
    }
}
