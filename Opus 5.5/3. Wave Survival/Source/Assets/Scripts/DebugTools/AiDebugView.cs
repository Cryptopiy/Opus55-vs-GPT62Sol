using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace WaveSurvival
{
    /// <summary>
    /// S-27 AI debug (F2): state name above every enemy, a line to its current destination, the baked NavMesh drawn
    /// as a translucent overlay, and each shooter's chosen cover point (green), peek point (orange) and the last
    /// evaluated candidates (small dots: green = hidden from the player, red = visible).
    /// </summary>
    public sealed class AiDebugView : MonoBehaviour
    {
        const int MaxLabels = 40;
        const int MaxMarkers = 220;

        public bool Enabled { get; private set; }
        Canvas canvas;
        readonly Text[] labels = new Text[MaxLabels];
        readonly EnemyBase[] labelOwner = new EnemyBase[MaxLabels];
        readonly EnemyState[] labelState = new EnemyState[MaxLabels];
        readonly string[] labelDetail = new string[MaxLabels];
        readonly int[] labelFixes = new int[MaxLabels];
        readonly LineRenderer[] lines = new LineRenderer[MaxLabels];
        readonly Transform[] markers = new Transform[MaxMarkers];
        readonly MeshRenderer[] markerRenderers = new MeshRenderer[MaxMarkers];
        Material green, red, orange;
        GameObject navMeshView;

        public static AiDebugView Create(Transform parent)
        {
            var go = new GameObject("AiDebug");
            go.transform.SetParent(parent, false);
            AiDebugView v = go.AddComponent<AiDebugView>();
            v.Build();
            return v;
        }

        void Build()
        {
            canvas = UIFactory.Canvas(transform, "AiDebugCanvas", 5);
            for (int i = 0; i < MaxLabels; i++)
            {
                labels[i] = UIFactory.Text(canvas.transform, "Label", Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(260f, 44f), 17, TextAnchor.LowerCenter, Color.white);
                labels[i].gameObject.SetActive(false);
                var lg = new GameObject("DestLine");
                lg.transform.SetParent(transform, false);
                LineRenderer l = lg.AddComponent<LineRenderer>();
                l.positionCount = 2;
                l.useWorldSpace = true;
                l.widthMultiplier = 0.05f;
                l.sharedMaterial = GameAssets.DebugLine;
                l.shadowCastingMode = ShadowCastingMode.Off;
                lg.SetActive(false);
                lines[i] = l;
            }
            green = MaterialFactory.Particle("DebugGreen", null, new Color(0.2f, 1f, 0.3f, 1f), true);
            red = MaterialFactory.Particle("DebugRed", null, new Color(1f, 0.2f, 0.15f, 1f), true);
            orange = MaterialFactory.Particle("DebugOrange", null, new Color(1f, 0.6f, 0.1f, 1f), true);
            for (int i = 0; i < MaxMarkers; i++)
            {
                var m = new GameObject("Marker");
                m.transform.SetParent(transform, false);
                m.AddComponent<MeshFilter>().sharedMesh = GameAssets.Sphere;
                var mr = m.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.sharedMaterial = green;
                m.SetActive(false);
                markers[i] = m.transform;
                markerRenderers[i] = mr;
            }
            canvas.gameObject.SetActive(false);
            // Labels, lines, NavMesh view and markers all live under this object: F8 hides them as one overlay root.
            UiVisibility.RegisterOverlayRoot(gameObject);
        }

        public void Toggle()
        {
            Enabled = !Enabled;
            canvas.gameObject.SetActive(Enabled);
            if (Enabled && navMeshView == null) BuildNavMeshView();
            if (navMeshView != null) navMeshView.SetActive(Enabled);
            if (!Enabled) HideAll(0, 0);
        }

        void BuildNavMeshView()
        {
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            var mesh = new Mesh { name = "NavMeshDebug", indexFormat = IndexFormat.UInt32 };
            Vector3[] v = tri.vertices;
            for (int i = 0; i < v.Length; i++) v[i] += Vector3.up * 0.06f; // float above the floor to avoid z-fighting
            mesh.vertices = v;
            mesh.triangles = tri.indices;
            mesh.RecalculateBounds();
            AssetTracker.Track(mesh);
            navMeshView = new GameObject("NavMeshView");
            navMeshView.transform.SetParent(transform, false);
            navMeshView.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = navMeshView.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GameAssets.NavDebug;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        void LateUpdate()
        {
            if (!Enabled || Game.Player == null) return;
            Camera cam = Game.Player.Camera;
            RectTransform root = (RectTransform)canvas.transform;
            float scale = root.rect.height / Screen.height;
            var list = Game.Enemies.Active;
            int used = 0, markerUsed = 0;
            for (int i = 0; i < list.Count && used < MaxLabels; i++)
            {
                EnemyBase e = list[i];
                if (!e.IsAlive) continue;
                int slot = used++;
                UpdateLabel(slot, e, cam, scale);
                LineRenderer l = lines[slot];
                if (e.HasDebugTarget)
                {
                    if (!l.gameObject.activeSelf) l.gameObject.SetActive(true);
                    l.SetPosition(0, e.transform.position + Vector3.up * 0.15f);
                    l.SetPosition(1, e.DebugTarget + Vector3.up * 0.15f);
                    Color c = StateColor(e.Fsm.CurrentId);
                    l.startColor = c;
                    l.endColor = c;
                }
                else if (l.gameObject.activeSelf) l.gameObject.SetActive(false);

                if (e is ShooterEnemy s) markerUsed = DrawCover(s, markerUsed);
            }
            HideAll(used, markerUsed);
        }

        void UpdateLabel(int slot, EnemyBase e, Camera cam, float scale)
        {
            Text t = labels[slot];
            Vector3 sp = cam.WorldToScreenPoint(e.HealthBarAnchor + Vector3.up * 0.25f);
            if (sp.z <= 0.1f)
            {
                if (t.gameObject.activeSelf) t.gameObject.SetActive(false);
                return;
            }
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            t.rectTransform.anchoredPosition = new Vector2(sp.x * scale, sp.y * scale);
            EnemyState st = e.Fsm.CurrentId;
            string detail = e.StateDetail;
            int fixes = e.Stuck.Fixes;
            // Rebuild the string only when something visible changed.
            if (labelOwner[slot] != e || labelState[slot] != st || !ReferenceEquals(labelDetail[slot], detail) || labelFixes[slot] != fixes)
            {
                labelOwner[slot] = e;
                labelState[slot] = st;
                labelDetail[slot] = detail;
                labelFixes[slot] = fixes;
                string text = e.Type + ": " + st + (detail != null ? " (" + detail + ")" : "");
                if (fixes > 0) text += "\nstuck-fix x" + fixes;
                t.text = text;
                t.color = StateColor(st);
            }
        }

        int DrawCover(ShooterEnemy s, int used)
        {
            if (s.HasCover) used = Marker(used, s.CoverPoint + Vector3.up * 0.3f, 0.45f, green);
            if (s.Fsm.CurrentId == EnemyState.Peek) used = Marker(used, s.PeekPoint + Vector3.up * 0.3f, 0.35f, orange);
            if (Time.time - s.CandidateTime < 3f)
                for (int i = 0; i < s.CandidateCount && used < MaxMarkers; i++)
                    used = Marker(used, s.Candidates[i].Pos + Vector3.up * 0.2f, 0.16f, s.Candidates[i].Hidden ? green : red);
            return used;
        }

        int Marker(int used, Vector3 pos, float size, Material mat)
        {
            if (used >= MaxMarkers) return used;
            Transform m = markers[used];
            if (!m.gameObject.activeSelf) m.gameObject.SetActive(true);
            m.position = pos;
            m.localScale = Vector3.one * size;
            if (markerRenderers[used].sharedMaterial != mat) markerRenderers[used].sharedMaterial = mat;
            return used + 1;
        }

        void HideAll(int fromLabel, int fromMarker)
        {
            for (int i = fromLabel; i < MaxLabels; i++)
            {
                if (labels[i].gameObject.activeSelf) labels[i].gameObject.SetActive(false);
                if (lines[i].gameObject.activeSelf) lines[i].gameObject.SetActive(false);
                labelOwner[i] = null;
            }
            for (int i = fromMarker; i < MaxMarkers; i++)
                if (markers[i].gameObject.activeSelf) markers[i].gameObject.SetActive(false);
        }

        static Color StateColor(EnemyState s)
        {
            switch (s)
            {
                case EnemyState.Chase: return new Color(1f, 0.85f, 0.3f);
                case EnemyState.Attack: return new Color(1f, 0.3f, 0.25f);
                case EnemyState.TakeCover: return new Color(0.3f, 1f, 0.45f);
                case EnemyState.Peek: return new Color(1f, 0.6f, 0.15f);
                case EnemyState.Charge: return new Color(1f, 0.2f, 0.6f);
                case EnemyState.Stunned: return new Color(0.6f, 0.6f, 1f);
                default: return Color.white;
            }
        }
    }
}
