using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class IslandGeometry
{
    static readonly Dictionary<PrimitiveType, Mesh> primitives = new();

    public static Transform Root(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    public static Material Lit(string name, Color srgb)
    {
        var mat = Custom("Props");
        mat.name = name;
        mat.SetColor("_BaseColor", srgb);
        mat.SetFloat("_UseVertexColor",0);
        return mat;
    }

    public static Material Custom(string name)
    {
        var shader = Resources.Load<Shader>("IslandShaders/" + name);
        return new Material(shader) { name = name, enableInstancing = true };
    }

    public static Mesh Primitive(PrimitiveType kind)
    {
        if (primitives.TryGetValue(kind, out Mesh mesh) && mesh) return mesh;
        var reference = GameObject.CreatePrimitive(kind);
        reference.SetActive(false);
        primitives[kind] = mesh = reference.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(reference);
        return mesh;
    }

    public static MeshRenderer Render(string name, Transform parent, Mesh mesh, Material material)
    {
        var t = Root(name, parent);
        t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = t.gameObject.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        return r;
    }

    public static Transform Part(string name, Transform parent, PrimitiveType kind, Material material, Vector3 local, Vector3 scale)
    {
        var t = Render(name,parent,Primitive(kind),material).transform;
        t.localPosition = local;
        t.localScale = scale;
        return t;
    }

    public static Mesh Mesh(string name, List<Vector3> vertices, List<int> indices, List<Vector2> uv)
    {
        var mesh = new Mesh { name = name };
        if (vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        if (uv != null) mesh.SetUVs(0,uv);
        mesh.SetTriangles(indices,0);
        mesh.RecalculateNormals();
        return mesh;
    }
}

