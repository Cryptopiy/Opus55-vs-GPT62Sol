using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

namespace LivingIsland.EditorTools
{
    /// <summary>
    /// Imports the Blender-exported binary glTF (.glb) props from Assets/Models without any external package.
    /// - parses the GLB container (JSON + BIN chunks) and the glTF accessors/buffer views;
    /// - converts glTF right-handed coordinates to Unity (mirror X, swap triangle winding, flip V);
    /// - merges a mesh's primitives into one Unity mesh with one submesh per material, keeps COLOR_0 (wind weights);
    /// - builds the node hierarchy; several root nodes named *_LOD0 / *_LOD1 become one LODGroup;
    /// - creates materials with the project's URP shaders, chosen by the Blender material name.
    /// Root-node translations are dropped so every prop pivots at its modelled origin.
    /// </summary>
    [ScriptedImporter(4, "glb")]
    public sealed class GlbImporter : ScriptedImporter
    {
        const uint JsonChunk = 0x4E4F534A, BinChunk = 0x004E4942;

        sealed class Ctx
        {
            public Dictionary<string, object> Root;
            public byte[] Bytes;
            public int BinOffset;
            public List<object> Accessors, Views, Meshes, Materials, Nodes;
            public Material[] UnityMaterials;
            public Mesh[] UnityMeshes;
            public AssetImportContext Import;
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            meshMaterialMap.Clear();
            byte[] bytes = File.ReadAllBytes(ctx.assetPath);
            if (bytes.Length < 20 || BitConverter.ToUInt32(bytes, 0) != 0x46546C67)
            {
                ctx.LogImportError("Not a binary glTF file: " + ctx.assetPath);
                return;
            }
            string json = null;
            int binOffset = -1;
            int offset = 12;
            uint total = BitConverter.ToUInt32(bytes, 8);
            while (offset + 8 <= total)
            {
                int len = (int)BitConverter.ToUInt32(bytes, offset);
                uint type = BitConverter.ToUInt32(bytes, offset + 4);
                if (type == JsonChunk) json = Encoding.UTF8.GetString(bytes, offset + 8, len);
                else if (type == BinChunk) binOffset = offset + 8;
                offset += 8 + len;
            }

            var c = new Ctx { Bytes = bytes, BinOffset = binOffset, Import = ctx };
            c.Root = (Dictionary<string, object>)MiniJson.Parse(json);
            c.Accessors = List(c.Root, "accessors");
            c.Views = List(c.Root, "bufferViews");
            c.Meshes = List(c.Root, "meshes");
            c.Materials = List(c.Root, "materials");
            c.Nodes = List(c.Root, "nodes");

            c.UnityMaterials = new Material[c.Materials.Count];
            for (int m = 0; m < c.Materials.Count; m++)
            {
                c.UnityMaterials[m] = BuildMaterial((Dictionary<string, object>)c.Materials[m], ctx);
                ctx.AddObjectToAsset("material_" + m, c.UnityMaterials[m]);
            }
            c.UnityMeshes = new Mesh[c.Meshes.Count];
            for (int m = 0; m < c.Meshes.Count; m++)
            {
                c.UnityMeshes[m] = BuildMesh(c, (Dictionary<string, object>)c.Meshes[m]);
                ctx.AddObjectToAsset("mesh_" + m, c.UnityMeshes[m]);
            }

            // Scene roots.
            var scenes = List(c.Root, "scenes");
            int sceneIndex = c.Root.ContainsKey("scene") ? Int(c.Root["scene"]) : 0;
            var rootNodes = List((Dictionary<string, object>)scenes[sceneIndex], "nodes");
            string baseName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            GameObject root;
            if (rootNodes.Count == 1)
            {
                root = BuildNode(c, Int(rootNodes[0]), null, true);
                root.name = baseName;
            }
            else
            {
                root = new GameObject(baseName);
                foreach (object n in rootNodes) BuildNode(c, Int(n), root.transform, true);
                SetupLods(root);
            }
            ctx.AddObjectToAsset("root", root);
            ctx.SetMainObject(root);
        }

        static List<object> List(Dictionary<string, object> d, string key) =>
            d.TryGetValue(key, out object v) && v is List<object> l ? l : new List<object>();

        static int Int(object o) => Convert.ToInt32(o);
        static float Flt(object o) => Convert.ToSingle(o);

        static float[] Floats(Dictionary<string, object> d, string key, params float[] fallback)
        {
            if (!d.TryGetValue(key, out object v) || !(v is List<object> l)) return fallback;
            var r = new float[l.Count];
            for (int i = 0; i < l.Count; i++) r[i] = Flt(l[i]);
            return r;
        }

        // ------------------------------------------------------------------ accessors
        static int ComponentCount(string type)
        {
            switch (type) { case "SCALAR": return 1; case "VEC2": return 2; case "VEC3": return 3; case "VEC4": return 4; case "MAT4": return 16; }
            return 1;
        }

        static int ComponentSize(int componentType) => componentType == 5126 || componentType == 5125 ? 4 : componentType == 5123 || componentType == 5122 ? 2 : 1;

        /// <summary>Reads any accessor as floats (normalised integers are mapped to 0..1).</summary>
        static float[] ReadFloats(Ctx c, int accessorIndex, out int components)
        {
            var acc = (Dictionary<string, object>)c.Accessors[accessorIndex];
            components = ComponentCount((string)acc["type"]);
            int count = Int(acc["count"]);
            int ctype = Int(acc["componentType"]);
            bool normalized = acc.TryGetValue("normalized", out object nv) && nv is bool b && b;
            var view = (Dictionary<string, object>)c.Views[Int(acc["bufferView"])];
            int start = c.BinOffset + (view.ContainsKey("byteOffset") ? Int(view["byteOffset"]) : 0) + (acc.ContainsKey("byteOffset") ? Int(acc["byteOffset"]) : 0);
            int csize = ComponentSize(ctype);
            int stride = view.ContainsKey("byteStride") ? Int(view["byteStride"]) : csize * components;
            var result = new float[count * components];
            for (int i = 0; i < count; i++)
            for (int k = 0; k < components; k++)
            {
                int p = start + i * stride + k * csize;
                float v;
                switch (ctype)
                {
                    case 5126: v = BitConverter.ToSingle(c.Bytes, p); break;
                    case 5125: v = BitConverter.ToUInt32(c.Bytes, p); break;
                    case 5123: v = BitConverter.ToUInt16(c.Bytes, p); if (normalized) v /= 65535f; break;
                    case 5122: v = BitConverter.ToInt16(c.Bytes, p); if (normalized) v = Mathf.Max(v / 32767f, -1f); break;
                    case 5121: v = c.Bytes[p]; if (normalized) v /= 255f; break;
                    default: v = (sbyte)c.Bytes[p]; if (normalized) v = Mathf.Max(v / 127f, -1f); break;
                }
                result[i * components + k] = v;
            }
            return result;
        }

        static int[] ReadIndices(Ctx c, int accessorIndex)
        {
            float[] f = ReadFloats(c, accessorIndex, out _);
            var r = new int[f.Length];
            for (int i = 0; i < f.Length; i++) r[i] = (int)f[i];
            return r;
        }

        // ------------------------------------------------------------------ meshes
        static Mesh BuildMesh(Ctx c, Dictionary<string, object> meshDef)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var submeshIndices = new List<int[]>();
            var submeshMaterials = new List<int>();
            bool anyNormals = true, anyColors = false;

            foreach (object primObj in List(meshDef, "primitives"))
            {
                var prim = (Dictionary<string, object>)primObj;
                var attrs = (Dictionary<string, object>)prim["attributes"];
                int baseVertex = positions.Count;
                float[] pos = ReadFloats(c, Int(attrs["POSITION"]), out _);
                int vcount = pos.Length / 3;
                for (int i = 0; i < vcount; i++) positions.Add(new Vector3(-pos[i * 3], pos[i * 3 + 1], pos[i * 3 + 2]));

                if (attrs.ContainsKey("NORMAL"))
                {
                    float[] n = ReadFloats(c, Int(attrs["NORMAL"]), out _);
                    for (int i = 0; i < vcount; i++) normals.Add(new Vector3(-n[i * 3], n[i * 3 + 1], n[i * 3 + 2]));
                }
                else { anyNormals = false; for (int i = 0; i < vcount; i++) normals.Add(Vector3.up); }

                if (attrs.ContainsKey("TEXCOORD_0"))
                {
                    float[] uv = ReadFloats(c, Int(attrs["TEXCOORD_0"]), out _);
                    for (int i = 0; i < vcount; i++) uvs.Add(new Vector2(uv[i * 2], 1f - uv[i * 2 + 1]));
                }
                else for (int i = 0; i < vcount; i++) uvs.Add(Vector2.zero);

                if (attrs.ContainsKey("COLOR_0"))
                {
                    anyColors = true;
                    float[] col = ReadFloats(c, Int(attrs["COLOR_0"]), out int cc);
                    for (int i = 0; i < vcount; i++)
                        colors.Add(new Color(col[i * cc], col[i * cc + 1], col[i * cc + 2], cc == 4 ? col[i * cc + 3] : 1f));
                }
                else for (int i = 0; i < vcount; i++) colors.Add(new Color(0f, 0f, 0f, 1f));

                int[] idx = prim.ContainsKey("indices") ? ReadIndices(c, Int(prim["indices"])) : Sequence(vcount);
                // Mirroring X flips handedness, so swap two corners of every triangle to keep faces outward.
                for (int t = 0; t + 2 < idx.Length; t += 3)
                {
                    int tmp = idx[t + 1];
                    idx[t + 1] = idx[t + 2];
                    idx[t + 2] = tmp;
                }
                for (int t = 0; t < idx.Length; t++) idx[t] += baseVertex;
                submeshIndices.Add(idx);
                submeshMaterials.Add(prim.ContainsKey("material") ? Int(prim["material"]) : -1);
            }

            var mesh = new Mesh { name = meshDef.ContainsKey("name") ? (string)meshDef["name"] : "mesh" };
            mesh.indexFormat = positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            if (anyColors) mesh.SetColors(colors);
            mesh.subMeshCount = submeshIndices.Count;
            for (int s = 0; s < submeshIndices.Count; s++) mesh.SetTriangles(submeshIndices[s], s, false);
            if (!anyNormals) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            // Remember which material each submesh uses (read back when building renderers).
            meshMaterialMap[mesh] = submeshMaterials.ToArray();
            return mesh;
        }

        static readonly Dictionary<Mesh, int[]> meshMaterialMap = new Dictionary<Mesh, int[]>();

        static int[] Sequence(int n)
        {
            var r = new int[n];
            for (int i = 0; i < n; i++) r[i] = i;
            return r;
        }

        // ------------------------------------------------------------------ nodes
        static GameObject BuildNode(Ctx c, int nodeIndex, Transform parent, bool isRoot)
        {
            var node = (Dictionary<string, object>)c.Nodes[nodeIndex];
            var go = new GameObject(node.ContainsKey("name") ? (string)node["name"] : "node" + nodeIndex);
            if (parent != null) go.transform.SetParent(parent, false);

            if (node.ContainsKey("matrix"))
            {
                float[] m = Floats(node, "matrix");
                var mat = new Matrix4x4();
                for (int i = 0; i < 16; i++) mat[i % 4, i / 4] = m[i];   // glTF matrices are column-major
                ApplyTrs(go.transform, mat.GetColumn(3), mat.rotation, mat.lossyScale, isRoot);
            }
            else
            {
                float[] t = Floats(node, "translation", 0f, 0f, 0f);
                float[] r = Floats(node, "rotation", 0f, 0f, 0f, 1f);
                float[] s = Floats(node, "scale", 1f, 1f, 1f);
                ApplyTrs(go.transform, new Vector3(t[0], t[1], t[2]), new Quaternion(r[0], r[1], r[2], r[3]), new Vector3(s[0], s[1], s[2]), isRoot);
            }

            if (node.ContainsKey("mesh"))
            {
                Mesh mesh = c.UnityMeshes[Int(node["mesh"])];
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                int[] matIdx = meshMaterialMap[mesh];
                var mats = new Material[matIdx.Length];
                for (int s = 0; s < matIdx.Length; s++)
                    mats[s] = matIdx[s] >= 0 ? c.UnityMaterials[matIdx[s]] : AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;
                bool transparent = false;
                foreach (var m in mats) if (m != null && m.shader != null && m.shader.name == "Island/Glass") transparent = true;
                if (transparent) mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            foreach (object child in List(node, "children")) BuildNode(c, Int(child), go.transform, false);
            return go;
        }

        static void ApplyTrs(Transform t, Vector3 pos, Quaternion rot, Vector3 scale, bool isRoot)
        {
            // glTF -> Unity: mirror X. Root translation is dropped (props pivot at their origin).
            t.localPosition = isRoot ? Vector3.zero : new Vector3(-pos.x, pos.y, pos.z);
            t.localRotation = new Quaternion(rot.x, -rot.y, -rot.z, rot.w);
            t.localScale = scale;
        }

        static void SetupLods(GameObject root)
        {
            Renderer[] lod0 = null, lod1 = null;
            foreach (Transform child in root.transform)
            {
                if (child.name.EndsWith("_LOD0", StringComparison.OrdinalIgnoreCase)) lod0 = child.GetComponentsInChildren<Renderer>();
                else if (child.name.EndsWith("_LOD1", StringComparison.OrdinalIgnoreCase)) lod1 = child.GetComponentsInChildren<Renderer>();
            }
            if (lod0 == null || lod1 == null) return;
            var group = root.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = true;
            // LOD0 above 16% of screen height, LOD1 down to 0.4% (palms never vanish across the island).
            group.SetLODs(new[] { new LOD(0.16f, lod0), new LOD(0.004f, lod1) });
            group.RecalculateBounds();
        }

        // ------------------------------------------------------------------ materials
        static Shader LoadShader(AssetImportContext ctx, string file)
        {
            string path = "Assets/Shaders/" + file + ".shader";
            ctx.DependsOnArtifact(path);
            var s = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (s == null)
            {
                // Only possible on a first import before the shader exists: the artifact dependency declared
                // above re-runs this importer as soon as the shader is imported, replacing this placeholder.
                s = Shader.Find("Universal Render Pipeline/Lit");
            }
            return s;
        }

        static Material BuildMaterial(Dictionary<string, object> def, AssetImportContext ctx)
        {
            string name = def.ContainsKey("name") ? (string)def["name"] : "Material";
            var pbr = def.ContainsKey("pbrMetallicRoughness") ? (Dictionary<string, object>)def["pbrMetallicRoughness"] : new Dictionary<string, object>();
            float[] bc = Floats(pbr, "baseColorFactor", 1f, 1f, 1f, 1f);
            float metallic = pbr.ContainsKey("metallicFactor") ? Flt(pbr["metallicFactor"]) : 1f;
            float roughness = pbr.ContainsKey("roughnessFactor") ? Flt(pbr["roughnessFactor"]) : 1f;
            float[] em = Floats(def, "emissiveFactor", 0f, 0f, 0f);
            float emStrength = 1f;
            if (def.ContainsKey("extensions") && def["extensions"] is Dictionary<string, object> ext &&
                ext.ContainsKey("KHR_materials_emissive_strength") && ext["KHR_materials_emissive_strength"] is Dictionary<string, object> es &&
                es.ContainsKey("emissiveStrength"))
                emStrength = Flt(es["emissiveStrength"]);

            // glTF factors are linear; Material.SetColor expects sRGB in a linear-space project.
            Color baseColor = new Color(bc[0], bc[1], bc[2], bc.Length > 3 ? bc[3] : 1f).gamma;
            Color emission = new Color(em[0], em[1], em[2], 1f) * emStrength;
            string n = name.ToLowerInvariant();
            Material m;
            if (n.Contains("palmtrunk"))
            {
                m = new Material(LoadShader(ctx, "Bark"));
                m.SetColor("_BaseColor", baseColor);
            }
            else if (n.Contains("palmfrond"))
            {
                m = new Material(LoadShader(ctx, "Foliage"));
                // Blender's bright viewport green is toned down to a natural sunlit frond albedo.
                m.SetColor("_BaseColor", (new Color(bc[0], bc[1], bc[2]) * 0.6f).gamma);
                m.SetFloat("_LeafPattern", 0f);
            }
            else if (n.Contains("rock"))
            {
                m = new Material(LoadShader(ctx, "Rock"));
                m.SetColor("_BaseColor", baseColor);
                m.SetFloat("_Smoothness", Mathf.Clamp01(1f - roughness));
            }
            else if (n.Contains("glass"))
            {
                m = new Material(LoadShader(ctx, "Glass"));
                m.SetColor("_BaseColor", baseColor);
            }
            else
            {
                m = new Material(LoadShader(ctx, "Prop"));
                m.SetColor("_BaseColor", baseColor);
                m.SetFloat("_Smoothness", Mathf.Clamp01(1f - roughness));
                m.SetFloat("_Metallic", Mathf.Clamp01(metallic));
                bool woody = n.Contains("wood") || n.Contains("boat") || n.Contains("charcoal") || n.Contains("ash") || n.Contains("coconut");
                m.SetFloat("_NoiseAmount", woody ? 0.6f : 0.2f);
                m.SetFloat("_Porosity", woody ? 0.8f : 0.4f);
                if (n.Contains("lamp"))
                {
                    m.SetColor("_EmissionColor", emission * 0.15f);
                    m.SetColor("_NightEmissionColor", (emission.maxColorComponent > 0.01f ? emission : new Color(1f, 0.85f, 0.5f) * 5f) * 1.2f);
                }
                else if (n.Contains("window"))
                {
                    m.SetColor("_NightEmissionColor", new Color(1f, 0.72f, 0.38f) * 3.2f);
                }
                else m.SetColor("_EmissionColor", emission);
            }
            m.name = name;
            m.enableInstancing = true;
            return m;
        }
    }
}
