using System.Globalization;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

namespace LivingIsland
{
    /// <summary>
    /// F1 overlay: FPS, frame time (avg / worst), draw calls, batches, triangles, instanced grass count,
    /// active particles per system, physics bodies, weather/time/quality. Text is rebuilt 4x per second
    /// from a reused StringBuilder.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        public float Fps { get; private set; }
        public float AvgFrameMs { get; private set; }
        public float MaxFrameMs { get; private set; }

        Text text;
        readonly StringBuilder sb = new StringBuilder(1024);
        ProfilerRecorder drawCalls, batches, triangles;
        float accum, worst, timer;
        int frames;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public void Build(Transform canvas)
        {
            UIFactory.Rect(gameObject, canvas, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16), new Vector2(560, 300));
            var bg = UIFactory.Panel(transform, "Background", new Color(0.02f, 0.03f, 0.04f, 0.72f));
            UIFactory.Rect(bg.gameObject, transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            text = UIFactory.Label(transform, "", 16, TextAnchor.UpperLeft, UIFactory.TextColor, 14, 10, 532, 280);
        }

        void OnEnable()
        {
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        }

        void OnDisable()
        {
            drawCalls.Dispose(); batches.Dispose(); triangles.Dispose();
        }

        static string Rec(ProfilerRecorder r) => r.Valid ? r.LastValue.ToString("N0", Inv) : "n/a";

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            accum += dt; frames++; worst = Mathf.Max(worst, dt);
            timer -= dt;
            if (timer > 0f) return;
            timer = 0.25f;
            Fps = frames / Mathf.Max(accum, 1e-5f);
            AvgFrameMs = accum / Mathf.Max(frames, 1) * 1000f;
            MaxFrameMs = worst * 1000f;
            accum = 0f; frames = 0; worst = 0f;
            if (text == null || Game.World == null) return;

            var w = Game.World;
            var weather = Game.Weather;
            sb.Clear();
            sb.Append("FPS ").Append(Fps.ToString("F1", Inv)).Append("   frame ").Append(AvgFrameMs.ToString("F2", Inv))
              .Append(" ms (worst ").Append(MaxFrameMs.ToString("F1", Inv)).Append(" ms)\n");
            sb.Append("Draw calls ").Append(Rec(drawCalls)).Append("   batches ").Append(Rec(batches))
              .Append("   triangles ").Append(Rec(triangles)).Append('\n');
            sb.Append("Grass blades (GPU instanced, 1 indirect draw): ").Append(Game.Grass.DrawnBlades.ToString("N0", Inv))
              .Append(" / budget ").Append(Game.Grass.Budget.ToString("N0", Inv)).Append('\n');
            sb.Append("Palms ").Append(w.Placer.Palms).Append("   rocks ").Append(w.Placer.Rocks).Append("   bushes ")
              .Append(w.Placer.Bushes).Append("   flower patches ").Append(w.Placer.FlowerClusters).Append('\n');
            int fire = w.Campfire != null ? w.Campfire.ActiveParticles : 0;
            sb.Append("Particles: rain ").Append(Game.Rain.RainParticles).Append("  splash ").Append(Game.Rain.ActiveParticles - Game.Rain.RainParticles)
              .Append("  campfire ").Append(fire).Append("  fireflies ").Append(Game.Fireflies.Count).Append("  swim ").Append(Game.SwimFx.ActiveParticles).Append('\n');
            sb.Append("Physics: colliders ").Append(w.ColliderCount).Append("   rigidbodies ").Append(w.RigidbodyCount).Append("   character controllers 1\n");
            sb.Append("Weather ").Append(WeatherSystem.DisplayName(weather.State)).Append(weather.AutoMode ? " (auto)" : "")
              .Append("  blend ").Append((weather.TransitionProgress * 100f).ToString("F0", Inv)).Append("%  wind x")
              .Append(weather.Current.Wind.ToString("F2", Inv)).Append("  waves x").Append(weather.Current.WaveScale.ToString("F2", Inv))
              .Append("  lightning strikes ").Append(Game.Lightning.StrikeCount).Append('\n');
            sb.Append("Time ").Append(Game.TimeOfDay.Hour.ToString("F2", Inv)).Append(" h  speed x").Append(Game.TimeOfDay.Speed.ToString("F1", Inv))
              .Append(Game.TimeOfDay.Paused ? " (paused)" : "").Append("  underwater ").Append(Game.Atmosphere.CameraUnderwater ? "yes" : "no").Append('\n');
            sb.Append("Quality ").Append(QualityManager.Name(Game.Quality.Current)).Append("   seed ").Append(w.Data.Seed)
              .Append("   generated in ").Append(w.LastGenerationSeconds.ToString("F2", Inv)).Append(" s\n");
            sb.Append(w.ValidationSummary);
            text.text = sb.ToString();
        }
    }
}
