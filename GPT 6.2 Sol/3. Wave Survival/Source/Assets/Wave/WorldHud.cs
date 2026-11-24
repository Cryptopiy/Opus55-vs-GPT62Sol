using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static WaveSurvival.GameConfig;
using static WaveSurvival.HudWidgets;

namespace WaveSurvival
{
    internal sealed partial class Hud
    {
        struct Tag
        {
            public RectTransform Root;
            public Image Bar, Track;
            public Text State;
            public LineRenderer Destination, Cover;
        }

        readonly Tag[] tags = new Tag[AliveCap];
        static readonly string[] States = { "IDLE", "CHASE", "ATTACK", "TAKE COVER", "PEEK", "CHARGE", "STUNNED", "DEAD" };
        Transform navDebug;
        GameObject navSurface;

        void BuildWorldHud()
        {
            var anchor = Stretch(gameplay, "Pooled enemy tags");
            navDebug = Geometry.Root("AI debug visualization", game.transform);
            var lines = Effects.Ink(new(.23f, 1.4f, 1.22f, .89f));
            var coverPoints = Effects.Ink(new(2.3f, .72f, .2f, .94f));
            for (int i = 0; i < tags.Length; i++)
            {
                var tag = Box(anchor, "Enemy tag", Center, Vector2.zero, new(130, 40));
                var back = Block(tag, "Health track", Center, new(0, -4), new(73, 7), Panel);
                var bar = Block(back.transform, "Damaged health", BL, new(1, 1), new(71, 5), Red);
                tags[i] = new Tag
                {
                    Root = tag, Track = back, Bar = bar,
                    State = Label(tag, "", 16, new(.5f, 1), new(0, 4), new(155, 23), Teal, TextAnchor.MiddleCenter, true),
                    Destination = DebugLine("Agent destination", 2, lines),
                    Cover = DebugLine("Chosen cover point", 13, coverPoints)
                };
                tags[i].Cover.loop = true;
                tag.gameObject.SetActive(false);
            }
            navDebug.gameObject.SetActive(false);
        }

        LineRenderer DebugLine(string name, int count, Material material)
        {
            var line = Geometry.Root(name, navDebug).gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = count;
            line.widthMultiplier = .035f;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.enabled = false;
            return line;
        }

        void HideAI() => navDebug.gameObject.SetActive(false);

        void UpdateWorldHud()
        {
            bool debug = game.DebugAI;
            if (debug && navSurface == null) BuildNavOverlay();
            navDebug.gameObject.SetActive(debug);
            int active = 0;
            foreach (Enemy enemy in game.Enemies)
            {
                if (enemy == null || !enemy.Alive) continue;
                ref Tag tag = ref tags[active++];
                bool healthVisible = enemy.HP < enemy.Maximum;
                Vector3 point = game.Player.Camera.WorldToScreenPoint(enemy.Eyes + Vector3.up * .36f);
                bool visible = point.z > 0 && point.x >= 0 && point.x <= Screen.width && point.y >= 0 && point.y <= Screen.height
                    && (debug || healthVisible && Arena.Clear(game.Player.Eyes, enemy.Eyes));
                tag.Root.gameObject.SetActive(visible && (healthVisible || debug));
                if (tag.Root.gameObject.activeSelf)
                {
                    tag.Root.anchoredPosition = new Vector2(point.x - Screen.width * .5f, point.y - Screen.height * .5f) / canvas.scaleFactor;
                    tag.Track.enabled = tag.Bar.enabled = healthVisible;
                    if (healthVisible) Width(tag.Bar, 71 * enemy.HP / enemy.Maximum);
                    tag.State.enabled = debug;
                    if (debug) tag.State.text = States[(int)enemy.State];
                }
                if (!debug) continue;
                tag.Destination.enabled = enemy.HasGoal;
                if (enemy.HasGoal)
                {
                    tag.Destination.SetPosition(0, enemy.Position + Vector3.up * .24f);
                    tag.Destination.SetPosition(1, enemy.Goal + Vector3.up * .13f);
                }
                tag.Cover.enabled = enemy.HasCover;
                if (enemy.HasCover)
                {
                    for (int i = 0; i < tag.Cover.positionCount; i++)
                    {
                        float theta = i * (2 * Mathf.PI / tag.Cover.positionCount);
                        tag.Cover.SetPosition(i, enemy.CoverPoint + new Vector3(Mathf.Cos(theta) * .60f, .15f, Mathf.Sin(theta) * .60f));
                    }
                }
            }
            for (; active < tags.Length; active++)
            {
                tags[active].Root.gameObject.SetActive(false);
                if (!debug) continue;
                tags[active].Destination.enabled = tags[active].Cover.enabled = false;
            }
        }

        void BuildNavOverlay()
        {
            var triangulation = NavMesh.CalculateTriangulation();
            var mesh = Geometry.Own(new Mesh { name = "Runtime NavMesh triangles" });
            var points = triangulation.vertices;
            for (int i = 0; i < points.Length; i++) points[i].y += .035f;
            mesh.vertices = points;
            mesh.triangles = triangulation.indices;
            var indices = new int[triangulation.indices.Length * 2];
            for (int i = 0; i < triangulation.indices.Length; i += 3)
            {
                int a = triangulation.indices[i], b = triangulation.indices[i + 1], c = triangulation.indices[i + 2];
                indices[i * 2] = a; indices[i * 2 + 1] = b;
                indices[i * 2 + 2] = b; indices[i * 2 + 3] = c;
                indices[i * 2 + 4] = c; indices[i * 2 + 5] = a;
            }
            mesh.subMeshCount = 2;
            mesh.SetIndices(indices, MeshTopology.Lines, 1);
            navSurface = Geometry.Root("Walkable NavMesh", navDebug).gameObject;
            navSurface.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = navSurface.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { Effects.Ink(new(.08f, .85f, .65f, .11f)), Effects.Ink(new(.14f, 1, .83f, .44f)) };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}

