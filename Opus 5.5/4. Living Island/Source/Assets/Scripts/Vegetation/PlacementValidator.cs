using System.Collections.Generic;
using UnityEngine;

namespace LivingIsland
{
    /// <summary>
    /// Measures I-13 on the real placed meshes: for every object, the lowest ring of mesh vertices is
    /// transformed to world space and compared with the exact terrain height below it.
    /// "Float" = lowest bottom vertex above the ground; "sink" = ground-contact pivot below the ground.
    /// </summary>
    public sealed class PlacementValidator
    {
        public int Checked, Floating, Sinking, InWater;
        public float MaxFloat, MaxSink;

        readonly Dictionary<Mesh, Vector3[]> bottomCache = new Dictionary<Mesh, Vector3[]>();
        readonly IslandData data;

        public PlacementValidator(IslandData d) { data = d; }

        /// <summary>Local-space vertices within the bottom 6% of the mesh height (cached per mesh).</summary>
        Vector3[] BottomVertices(Mesh mesh)
        {
            if (bottomCache.TryGetValue(mesh, out var cached)) return cached;
            var result = new List<Vector3>();
            if (mesh.isReadable)
            {
                Vector3[] v = mesh.vertices;
                float minY = float.MaxValue, maxY = float.MinValue;
                for (int i = 0; i < v.Length; i++) { minY = Mathf.Min(minY, v[i].y); maxY = Mathf.Max(maxY, v[i].y); }
                float limit = minY + (maxY - minY) * 0.06f;
                for (int i = 0; i < v.Length; i++) if (v[i].y <= limit) result.Add(v[i]);
            }
            var arr = result.ToArray();
            bottomCache[mesh] = arr;
            return arr;
        }

        /// <summary>Checks one placed object whose ground-contact pivot is its transform position.</summary>
        public void Check(Transform obj, MeshFilter meshFilter, bool mustBeDry)
        {
            Checked++;
            Vector3 pivot = obj.position;
            float groundAtPivot = data.HeightAt(pivot.x, pivot.z);
            float sink = groundAtPivot - pivot.y;
            MaxSink = Mathf.Max(MaxSink, sink);
            if (sink > GameConfig.MaxFloatOrSink) Sinking++;
            if (mustBeDry && groundAtPivot < GameConfig.SeaLevel + 0.3f) InWater++;

            if (meshFilter == null || meshFilter.sharedMesh == null) return;
            Vector3[] bottom = BottomVertices(meshFilter.sharedMesh);
            if (bottom.Length == 0) return;
            Matrix4x4 m = meshFilter.transform.localToWorldMatrix;
            float lowestGap = float.MaxValue;
            for (int i = 0; i < bottom.Length; i++)
            {
                Vector3 w = m.MultiplyPoint3x4(bottom[i]);
                lowestGap = Mathf.Min(lowestGap, w.y - data.HeightAt(w.x, w.z));
            }
            MaxFloat = Mathf.Max(MaxFloat, lowestGap);
            if (lowestGap > 0.05f) Floating++;
        }

        public override string ToString()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "checked {0} objects | floating (> 0.05 m gap) {1} (max gap {2:F3} m) | sunk > {3:F1} m: {4} (max {5:F3} m) | in water: {6}",
                Checked, Floating, Mathf.Max(0f, MaxFloat), GameConfig.MaxFloatOrSink, Sinking, Mathf.Max(0f, MaxSink), InWater);
        }
    }
}
