using System.Collections.Generic;
using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// Procedural meshes. Arena meshes use metric UVs (texture tiles stay the same physical size on every face)
    /// unlike Unity's primitives, which stretch one texture over each face.
    /// </summary>
    public static class MeshFactory
    {
        sealed class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int> T = new List<int>();

            /// <summary>Adds a quad whose corners are clockwise when seen from its front (Unity's front-face winding).</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int i = V.Count;
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                N.Add(n); N.Add(n); N.Add(n); N.Add(n);
                UV.Add(ua); UV.Add(ub); UV.Add(uc); UV.Add(ud);
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
                T.Add(i); T.Add(i + 2); T.Add(i + 3);
            }

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
            {
                int i = V.Count;
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                V.Add(a); V.Add(b); V.Add(c);
                N.Add(n); N.Add(n); N.Add(n);
                UV.Add(ua); UV.Add(ub); UV.Add(uc);
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name, hideFlags = HideFlags.DontUnloadUnusedAsset };
                if (V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(V);
                m.SetNormals(N);
                m.SetUVs(0, UV);
                m.SetTriangles(T, 0);
                m.RecalculateTangents();
                m.RecalculateBounds();
                return AssetTracker.Track(m);
            }
        }

        /// <summary>
        /// Box centred on the origin. With <paramref name="unitUVs"/> each face maps 0..1 (crates); otherwise UVs are
        /// metres divided by <paramref name="tileU"/>/<paramref name="tileV"/> (walls, floor, platform).
        /// </summary>
        public static Mesh Box(string name, Vector3 size, float tileU = 1f, float tileV = 1f, bool unitUVs = false)
        {
            var b = new Builder();
            Vector3 h = size * 0.5f;
            // Each face: origin corner, axis along u, axis along v; outward normal = cross(dv, du) (verified per face).
            AddFace(b, new Vector3(h.x, -h.y, h.z), Vector3.left * size.x, Vector3.up * size.y, unitUVs, tileU, tileV);     // +Z
            AddFace(b, new Vector3(-h.x, -h.y, -h.z), Vector3.right * size.x, Vector3.up * size.y, unitUVs, tileU, tileV);  // -Z
            AddFace(b, new Vector3(h.x, -h.y, -h.z), Vector3.forward * size.z, Vector3.up * size.y, unitUVs, tileU, tileV); // +X
            AddFace(b, new Vector3(-h.x, -h.y, h.z), Vector3.back * size.z, Vector3.up * size.y, unitUVs, tileU, tileV);    // -X
            AddFace(b, new Vector3(-h.x, h.y, -h.z), Vector3.right * size.x, Vector3.forward * size.z, unitUVs, tileU, tileU); // +Y
            AddFace(b, new Vector3(-h.x, -h.y, h.z), Vector3.right * size.x, Vector3.back * size.z, unitUVs, tileU, tileU);  // -Y
            return b.Build(name);
        }

        static void AddFace(Builder b, Vector3 o, Vector3 du, Vector3 dv, bool unit, float tu, float tv)
        {
            float lu = du.magnitude, lv = dv.magnitude;
            Vector2 u1 = unit ? new Vector2(1f, 0f) : new Vector2(lu / tu, 0f);
            Vector2 v1 = unit ? new Vector2(0f, 1f) : new Vector2(0f, lv / tv);
            b.Quad(o, o + dv, o + du + dv, o + du, Vector2.zero, v1, u1 + v1, u1);
        }

        /// <summary>Ramp wedge: rises from height 0 at -Z to <paramref name="height"/> at +Z.</summary>
        public static Mesh Wedge(string name, float width, float height, float length, float tile)
        {
            var b = new Builder();
            float hw = width * 0.5f, hl = length * 0.5f;
            Vector3 bl = new Vector3(-hw, 0f, -hl), br = new Vector3(hw, 0f, -hl);
            Vector3 tl = new Vector3(-hw, height, hl), tr = new Vector3(hw, height, hl);
            Vector3 bl2 = new Vector3(-hw, 0f, hl), br2 = new Vector3(hw, 0f, hl);
            float slopeLen = Mathf.Sqrt(height * height + length * length);
            // Sloped top surface.
            b.Quad(bl, tl, tr, br, Vector2.zero, new Vector2(0f, slopeLen / tile), new Vector2(width / tile, slopeLen / tile), new Vector2(width / tile, 0f));
            // Back face (+Z).
            b.Quad(br2, tr, tl, bl2, Vector2.zero, new Vector2(0f, height / tile), new Vector2(width / tile, height / tile), new Vector2(width / tile, 0f));
            // Sides.
            b.Tri(bl, bl2, tl, Vector2.zero, new Vector2(length / tile, 0f), new Vector2(length / tile, height / tile));
            b.Tri(br, tr, br2, Vector2.zero, new Vector2(length / tile, height / tile), new Vector2(length / tile, 0f));
            // Bottom.
            b.Quad(bl, br, br2, bl2, Vector2.zero, new Vector2(width / tile, 0f), new Vector2(width / tile, length / tile), new Vector2(0f, length / tile));
            return b.Build(name);
        }

        /// <summary>Jersey-barrier prism along X: base thickness, narrower top (S-02 barrier 4 x 1.2 x 0.6).</summary>
        public static Mesh Barrier(string name, float length, float height, float baseWidth, float topWidth, float tile)
        {
            var b = new Builder();
            float hl = length * 0.5f;
            // Profile (z, y) from front-bottom, around to back-bottom.
            Vector2[] prof =
            {
                new Vector2(-baseWidth * 0.5f, 0f), new Vector2(-baseWidth * 0.5f, height * 0.18f),
                new Vector2(-topWidth * 0.5f, height * 0.42f), new Vector2(-topWidth * 0.5f, height),
                new Vector2(topWidth * 0.5f, height), new Vector2(topWidth * 0.5f, height * 0.42f),
                new Vector2(baseWidth * 0.5f, height * 0.18f), new Vector2(baseWidth * 0.5f, 0f),
            };
            float vAcc = 0f;
            for (int i = 0; i < prof.Length - 1; i++)
            {
                Vector2 p0 = prof[i], p1 = prof[i + 1];
                float seg = Vector2.Distance(p0, p1);
                Vector3 a = new Vector3(-hl, p0.y, p0.x), d = new Vector3(hl, p0.y, p0.x);
                Vector3 bb = new Vector3(-hl, p1.y, p1.x), c = new Vector3(hl, p1.y, p1.x);
                b.Quad(a, bb, c, d, new Vector2(0f, vAcc / tile), new Vector2(0f, (vAcc + seg) / tile),
                       new Vector2(length / tile, (vAcc + seg) / tile), new Vector2(length / tile, vAcc / tile));
                vAcc += seg;
            }
            // End caps (fan of the convex profile).
            for (int s = 0; s < 2; s++)
            {
                float x = s == 0 ? -hl : hl;
                for (int i = 1; i < prof.Length - 1; i++)
                {
                    Vector3 a = new Vector3(x, prof[0].y, prof[0].x);
                    Vector3 bq = new Vector3(x, prof[i].y, prof[i].x);
                    Vector3 c = new Vector3(x, prof[i + 1].y, prof[i + 1].x);
                    Vector2 ua = new Vector2(prof[0].x, prof[0].y) / tile, ub = new Vector2(prof[i].x, prof[i].y) / tile, uc = new Vector2(prof[i + 1].x, prof[i + 1].y) / tile;
                    if (s == 0) b.Tri(a, c, bq, ua, uc, ub); else b.Tri(a, bq, c, ua, ub, uc); // -X cap, +X cap
                }
            }
            return b.Build(name);
        }

        /// <summary>Capped cylinder with metric UVs (pillars). Pivot at the bottom centre.</summary>
        public static Mesh Cylinder(string name, float radius, float height, int segments, float tile)
        {
            var b = new Builder();
            float circ = 2f * Mathf.PI * radius;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 p0 = d0 * radius, p1 = d1 * radius;
                float u0 = circ * i / segments / tile, u1 = circ * (i + 1) / segments / tile;
                int vi = b.V.Count;
                b.V.Add(p0); b.V.Add(p0 + Vector3.up * height); b.V.Add(p1 + Vector3.up * height); b.V.Add(p1);
                b.N.Add(d0); b.N.Add(d0); b.N.Add(d1); b.N.Add(d1);
                b.UV.Add(new Vector2(u0, 0f)); b.UV.Add(new Vector2(u0, height / tile)); b.UV.Add(new Vector2(u1, height / tile)); b.UV.Add(new Vector2(u1, 0f));
                b.T.Add(vi); b.T.Add(vi + 1); b.T.Add(vi + 2);
                b.T.Add(vi); b.T.Add(vi + 2); b.T.Add(vi + 3);
                Vector3 top = Vector3.up * height;
                b.Tri(top, top + p1, top + p0, new Vector2(0.5f, 0.5f), new Vector2(0.5f + d1.x * 0.5f, 0.5f + d1.z * 0.5f), new Vector2(0.5f + d0.x * 0.5f, 0.5f + d0.z * 0.5f));
            }
            return b.Build(name);
        }

        /// <summary>Flat annulus in the XZ plane (telegraph rings, AI debug markers). UV.x runs around, UV.y across.</summary>
        public static Mesh Ring(string name, float inner, float outer, int segments)
        {
            var b = new Builder();
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                b.Quad(d0 * inner, d1 * inner, d1 * outer, d0 * outer,
                    new Vector2((float)i / segments, 0f), new Vector2((float)(i + 1) / segments, 0f),
                    new Vector2((float)(i + 1) / segments, 1f), new Vector2((float)i / segments, 1f));
            }
            return b.Build(name);
        }

        /// <summary>Unit quad whose visible side faces -Z (like Unity's Quad); decals use LookRotation(-normal).</summary>
        public static Mesh Quad(string name)
        {
            var b = new Builder();
            b.Quad(new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                   Vector2.zero, new Vector2(0f, 1f), Vector2.one, new Vector2(1f, 0f));
            return b.Build(name);
        }
    }
}
