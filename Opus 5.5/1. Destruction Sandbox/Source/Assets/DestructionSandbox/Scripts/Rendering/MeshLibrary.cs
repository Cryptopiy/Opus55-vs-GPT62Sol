using System.Collections.Generic;
using UnityEngine;

namespace DestructionSandbox
{
    /// <summary>
    /// All geometry is generated here at start-up (no imported models). Boxes of every size share ONE unit cube
    /// that is scaled per object; arch/cut pieces get convex prism meshes generated from their outline.
    /// Vertex colour alpha = 1 marks the unit cube so shaders can draw bevel-like box edges.
    /// </summary>
    public sealed class MeshLibrary
    {
        public Mesh UnitCube { get; private set; }
        public Mesh Sphere { get; private set; }
        public Mesh Cylinder { get; private set; }
        public Mesh GroundPlane { get; private set; }
        public Mesh DecalQuad { get; private set; }

        public MeshLibrary(float groundSize)
        {
            UnitCube = BuildUnitCube();
            Sphere = BuildSphere(32, 20);
            Cylinder = BuildCylinder(28);
            GroundPlane = BuildPlane(groundSize);
            DecalQuad = BuildPlane(1f);
        }

        private static Mesh BuildUnitCube()
        {
            var verts = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var tris = new List<int>(36);
            AddFace(verts, normals, uvs, tris, Vector3.right, Vector3.up, Vector3.forward);
            AddFace(verts, normals, uvs, tris, Vector3.left, Vector3.up, Vector3.back);
            AddFace(verts, normals, uvs, tris, Vector3.up, Vector3.forward, Vector3.right);
            AddFace(verts, normals, uvs, tris, Vector3.down, Vector3.back, Vector3.right);
            AddFace(verts, normals, uvs, tris, Vector3.forward, Vector3.up, Vector3.left);
            AddFace(verts, normals, uvs, tris, Vector3.back, Vector3.up, Vector3.right);
            var colors = new Color[verts.Count];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color(1f, 1f, 1f, 1f);

            var mesh = new Mesh { name = "UnitCube" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

        // One quad of the unit cube; 'up' and 'right' span the face, winding is clockwise seen from outside.
        private static void AddFace(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 normal, Vector3 up, Vector3 right)
        {
            int start = v.Count;
            Vector3 c = normal * 0.5f;
            Vector3 u = up * 0.5f;
            Vector3 r = right * 0.5f;
            v.Add(c - u - r); v.Add(c + u - r); v.Add(c + u + r); v.Add(c - u + r);
            for (int i = 0; i < 4; i++) n.Add(normal);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0));
            // Ensure outward-facing (clockwise in Unity's left-handed convention when viewed from the normal side).
            Vector3 cross = Vector3.Cross(v[start + 1] - v[start], v[start + 2] - v[start]);
            if (Vector3.Dot(cross, normal) > 0f)
            {
                t.Add(start); t.Add(start + 1); t.Add(start + 2);
                t.Add(start); t.Add(start + 2); t.Add(start + 3);
            }
            else
            {
                t.Add(start); t.Add(start + 2); t.Add(start + 1);
                t.Add(start); t.Add(start + 3); t.Add(start + 2);
            }
        }

        private static Mesh BuildSphere(int longitude, int latitude)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int y = 0; y <= latitude; y++)
            {
                float v = (float)y / latitude;
                float theta = v * Mathf.PI;
                for (int x = 0; x <= longitude; x++)
                {
                    float u = (float)x / longitude;
                    float phi = u * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    verts.Add(n * 0.5f);
                    normals.Add(n);
                    uvs.Add(new Vector2(u, 1f - v));
                }
            }
            int row = longitude + 1;
            for (int y = 0; y < latitude; y++)
            {
                for (int x = 0; x < longitude; x++)
                {
                    int a = y * row + x, b = a + row, c = a + 1, d = b + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(c); tris.Add(d); tris.Add(b);
                }
            }
            OrientTriangles(verts, normals, tris);
            var mesh = new Mesh { name = "Sphere" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(NotBoxColors(verts.Count));
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

        /// <summary>Unit cylinder along +Z (length 1, diameter 1), centred on the origin.</summary>
        private static Mesh BuildCylinder(int segments)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                var n = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                verts.Add(n * 0.5f + new Vector3(0, 0, -0.5f)); normals.Add(n); uvs.Add(new Vector2((float)i / segments, 0));
                verts.Add(n * 0.5f + new Vector3(0, 0, 0.5f)); normals.Add(n); uvs.Add(new Vector2((float)i / segments, 1));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(c); tris.Add(d); tris.Add(b);
            }
            AddCap(verts, normals, uvs, tris, segments, 0.5f, Vector3.forward);
            AddCap(verts, normals, uvs, tris, segments, -0.5f, Vector3.back);
            OrientTriangles(verts, normals, tris);
            var mesh = new Mesh { name = "Cylinder" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(NotBoxColors(verts.Count));
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

        private static void AddCap(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, int segments, float z, Vector3 normal)
        {
            int center = v.Count;
            v.Add(new Vector3(0, 0, z)); n.Add(normal); uv.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, z)); n.Add(normal);
                uv.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = center + 1 + i, b = a + 1;
                t.Add(center); t.Add(a); t.Add(b);   // orientation is fixed afterwards by OrientTriangles
            }
        }

        /// <summary>White vertex colours with alpha 0: tells the block shader this mesh is not a unit cube.</summary>
        private static Color[] NotBoxColors(int count)
        {
            var colors = new Color[count];
            for (int i = 0; i < count; i++) colors[i] = new Color(1f, 1f, 1f, 0f);
            return colors;
        }

        /// <summary>
        /// Unity treats a triangle as front-facing when Cross(p1 - p0, p2 - p0) points along its outward normal.
        /// Flipping every triangle that disagrees with its first vertex normal makes all builders winding-safe.
        /// </summary>
        private static void OrientTriangles(List<Vector3> verts, List<Vector3> normals, List<int> tris)
        {
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 p0 = verts[tris[i]], p1 = verts[tris[i + 1]], p2 = verts[tris[i + 2]];
                Vector3 faceN = Vector3.Cross(p1 - p0, p2 - p0);
                Vector3 reference = normals[tris[i]] + normals[tris[i + 1]] + normals[tris[i + 2]];
                if (Vector3.Dot(faceN, reference) < 0f) { int tmp = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = tmp; }
            }
        }

        /// <summary>Square plane in XZ facing +Y, centred on the origin, with 0..1 UVs.</summary>
        private static Mesh BuildPlane(float size)
        {
            float h = size * 0.5f;
            var mesh = new Mesh { name = "Plane" };
            mesh.SetVertices(new List<Vector3> { new Vector3(-h, 0, -h), new Vector3(-h, 0, h), new Vector3(h, 0, h), new Vector3(h, 0, -h) });
            mesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            mesh.SetColors(NotBoxColors(4));
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

        /// <summary>
        /// Convex prism: 'outline' is a convex polygon in local XY (any winding), extruded symmetrically along Z
        /// by 'depth'. Flat-shaded, colour alpha 0 (no box-edge effect in the shader).
        /// </summary>
        public static Mesh BuildPrism(IList<Vector2> outline, float depth, string name)
        {
            int count = outline.Count;
            var poly = new Vector2[count];
            for (int i = 0; i < count; i++) poly[i] = outline[i];
            if (PolygonUtil.SignedArea(poly) < 0f) System.Array.Reverse(poly);   // force counter-clockwise

            float hz = depth * 0.5f;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();

            // Front face (z = -hz) looks toward -Z; back face (z = +hz) toward +Z.
            int front = verts.Count;
            for (int i = 0; i < count; i++) { verts.Add(new Vector3(poly[i].x, poly[i].y, -hz)); normals.Add(Vector3.back); }
            for (int i = 1; i < count - 1; i++) { tris.Add(front); tris.Add(front + i); tris.Add(front + i + 1); }
            int back = verts.Count;
            for (int i = 0; i < count; i++) { verts.Add(new Vector3(poly[i].x, poly[i].y, hz)); normals.Add(Vector3.forward); }
            for (int i = 1; i < count - 1; i++) { tris.Add(back); tris.Add(back + i + 1); tris.Add(back + i); }

            for (int i = 0; i < count; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % count];
                Vector2 e = b - a;
                var n = new Vector3(e.y, -e.x, 0f).normalized;    // outward for a CCW polygon
                int s = verts.Count;
                verts.Add(new Vector3(a.x, a.y, -hz)); verts.Add(new Vector3(b.x, b.y, -hz));
                verts.Add(new Vector3(b.x, b.y, hz)); verts.Add(new Vector3(a.x, a.y, hz));
                for (int k = 0; k < 4; k++) normals.Add(n);
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
                tris.Add(s); tris.Add(s + 3); tris.Add(s + 2);
            }

            OrientTriangles(verts, normals, tris);

            var colors = new Color[verts.Count];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color(1f, 1f, 1f, 0f);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }
    }
}
