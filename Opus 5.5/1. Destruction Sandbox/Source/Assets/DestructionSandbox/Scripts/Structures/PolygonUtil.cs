using System.Collections.Generic;
using UnityEngine;

namespace DestructionSandbox
{
    /// <summary>2D convex polygon helpers used to cut the arch wall pieces (build time only, not per frame).</summary>
    public static class PolygonUtil
    {
        public static float SignedArea(IList<Vector2> poly)
        {
            float a = 0f;
            for (int i = 0, n = poly.Count; i < n; i++)
            {
                Vector2 p = poly[i], q = poly[(i + 1) % n];
                a += p.x * q.y - q.x * p.y;
            }
            return a * 0.5f;
        }

        public static Vector2 Centroid(IList<Vector2> poly)
        {
            float a = 0f, cx = 0f, cy = 0f;
            for (int i = 0, n = poly.Count; i < n; i++)
            {
                Vector2 p = poly[i], q = poly[(i + 1) % n];
                float cross = p.x * q.y - q.x * p.y;
                a += cross;
                cx += (p.x + q.x) * cross;
                cy += (p.y + q.y) * cross;
            }
            if (Mathf.Abs(a) < 1e-9f)
            {
                Vector2 avg = Vector2.zero;
                for (int i = 0; i < poly.Count; i++) avg += poly[i];
                return avg / Mathf.Max(1, poly.Count);
            }
            return new Vector2(cx / (3f * a), cy / (3f * a));
        }

        /// <summary>Keeps the part of 'poly' where Dot(n, p) &lt;= d (Sutherland-Hodgman, one plane).</summary>
        public static List<Vector2> ClipHalfPlane(IList<Vector2> poly, Vector2 n, float d)
        {
            var result = new List<Vector2>(poly.Count + 2);
            int count = poly.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % count];
                float da = Vector2.Dot(n, a) - d;
                float db = Vector2.Dot(n, b) - d;
                bool ina = da <= 0f, inb = db <= 0f;
                if (ina) result.Add(a);
                if (ina != inb)
                {
                    float t = da / (da - db);
                    result.Add(a + (b - a) * t);
                }
            }
            RemoveDuplicates(result);
            return result;
        }

        public static List<Vector2> ClipRect(IList<Vector2> poly, Vector2 min, Vector2 max)
        {
            List<Vector2> r = ClipHalfPlane(poly, new Vector2(1, 0), max.x);
            if (r.Count >= 3) r = ClipHalfPlane(r, new Vector2(-1, 0), -min.x);
            if (r.Count >= 3) r = ClipHalfPlane(r, new Vector2(0, 1), max.y);
            if (r.Count >= 3) r = ClipHalfPlane(r, new Vector2(0, -1), -min.y);
            return r;
        }

        public static List<Vector2> Rect(float x0, float y0, float x1, float y1)
        {
            return new List<Vector2> { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) };
        }

        public static void Bounds(IList<Vector2> poly, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < poly.Count; i++)
            {
                min = Vector2.Min(min, poly[i]);
                max = Vector2.Max(max, poly[i]);
            }
        }

        /// <summary>True when the polygon is (within tolerance) its own axis-aligned bounding rectangle.</summary>
        public static bool IsAxisAlignedRect(IList<Vector2> poly, float tolerance = 1e-4f)
        {
            Bounds(poly, out Vector2 min, out Vector2 max);
            float rectArea = (max.x - min.x) * (max.y - min.y);
            return Mathf.Abs(Mathf.Abs(SignedArea(poly)) - rectArea) <= tolerance;
        }

        private static void RemoveDuplicates(List<Vector2> poly)
        {
            for (int i = poly.Count - 1; i >= 0 && poly.Count > 0; i--)
            {
                int next = (i + 1) % poly.Count;
                if (next != i && (poly[i] - poly[next]).sqrMagnitude < 1e-10f) poly.RemoveAt(i);
            }
        }
    }
}
