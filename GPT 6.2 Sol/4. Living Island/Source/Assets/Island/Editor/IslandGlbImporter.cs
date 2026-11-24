using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

[ScriptedImporter(1,"glb")]
public sealed class IslandGlbImporter : ScriptedImporter
{
    [Serializable] class Document { public Node[] nodes; public Model[] meshes; public View[] bufferViews; public Access[] accessors; public Surface[] materials; }
    [Serializable] class Node { public string name; public int mesh; }
    [Serializable] class Model { public Shape[] primitives; }
    [Serializable] class Shape { public Attributes attributes; public int indices,material; }
    [Serializable] class Attributes { public int POSITION,NORMAL,TEXCOORD_0; }
    [Serializable] class Access { public int bufferView,byteOffset,count,componentType; }
    [Serializable] class View { public int byteOffset,byteStride; }
    [Serializable] class Surface { public string name; public Pbr pbrMetallicRoughness; public float[] emissiveFactor; }
    [Serializable] class Pbr { public float[] baseColorFactor; }
    const string ShaderPath = "Assets/Island/Resources/IslandShaders/Props.shader";

    public override void OnImportAsset(AssetImportContext ctx)
    {
        ctx.DependsOnSourceAsset(ShaderPath);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (!shader) return;
        using var reader = new BinaryReader(File.OpenRead(ctx.assetPath));
        if (reader.ReadUInt32() != 0x46546C67 || reader.ReadUInt32() != 2) throw new InvalidDataException("Expected GLB 2.0.");
        reader.ReadUInt32();
        int jsonLen=reader.ReadInt32();
        reader.ReadUInt32();
        var doc=JsonUtility.FromJson<Document>(Encoding.UTF8.GetString(reader.ReadBytes(jsonLen)));
        int binLen=reader.ReadInt32();
        reader.ReadUInt32();
        byte[] bin=reader.ReadBytes(binLen);
        string name=Path.GetFileNameWithoutExtension(ctx.assetPath);
        var root = new GameObject(name);
        ctx.AddObjectToAsset("root",root);
        ctx.SetMainObject(root);

        bool palm=name.StartsWith("SM_Palm_",StringComparison.Ordinal);
        var materials=new[]{ MakeMaterial(ctx,shader,"Coastal surface",palm?1f:0f,0f),
            MakeMaterial(ctx,shader,"Window glass",0f,2f),MakeMaterial(ctx,shader,"Beacon optic",0f,4f) };
        foreach(Node node in doc.nodes)
        {
            var verts=new List<Vector3>(); var normals=new List<Vector3>(); var colors=new List<Color>();
            var uvs=new List<Vector2>(); var triangles=new[]{new List<int>(),new List<int>(),new List<int>()};
            foreach(Shape shape in doc.meshes[node.mesh].primitives)
            {
                int start=verts.Count;
                Attributes a=shape.attributes;
                var surface=doc.materials[shape.material];
                float[] f=surface.pbrMetallicRoughness?.baseColorFactor;
                Color color=f==null ? Color.white : new Color(f[0],f[1],f[2],1f);
                if (surface.name.StartsWith("PalmFrond",StringComparison.Ordinal)) color.a=.45f;
                int group=surface.name.StartsWith("NightWindow",StringComparison.Ordinal)?1:surface.name.StartsWith("Beacon lamp",StringComparison.Ordinal)?2:0;
                int count=doc.accessors[a.POSITION].count;
                for(int i=0;i<count;i++)
                {
                    verts.Add(ReadVector(bin,doc,a.POSITION,i,true));
                    normals.Add(ReadVector(bin,doc,a.NORMAL,i,true));
                    Vector3 uv=ReadVector(bin,doc,a.TEXCOORD_0,i,false);
                    uvs.Add(new Vector2(uv.x,1f-uv.y));
                    colors.Add(color);
                }
                Access index=doc.accessors[shape.indices];
                for(int i=0;i<index.count;i+=3)
                {
                    triangles[group].Add(start+Index(bin,doc,index,i));
                    triangles[group].Add(start+Index(bin,doc,index,i+2));
                    triangles[group].Add(start+Index(bin,doc,index,i+1));
                }
            }
            var mesh=new Mesh {name=node.name,indexFormat=verts.Count>ushort.MaxValue?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetUVs(0,uvs);
            var used=new List<Material>();
            foreach(var values in triangles) if(values.Count>0) used.Add(materials[Array.IndexOf(triangles,values)]);
            mesh.subMeshCount=used.Count;
            for(int i=0,sub=0;i<triangles.Length;i++) if(triangles[i].Count>0) mesh.SetTriangles(triangles[i],sub++);
            mesh.RecalculateBounds();
            ctx.AddObjectToAsset(node.name+" mesh",mesh);
            var child=new GameObject(node.name);
            child.transform.SetParent(root.transform,false);
            child.AddComponent<MeshFilter>().sharedMesh=mesh;
            child.AddComponent<MeshRenderer>().sharedMaterials=used.ToArray();
        }
    }

    static Material MakeMaterial(AssetImportContext ctx,Shader shader,string name,float palm,float glow)
    {
        var mat=new Material(shader){name=name,enableInstancing=true};
        mat.SetFloat("_Vegetation",palm);
        mat.SetFloat("_Glow",glow);
        ctx.AddObjectToAsset(name,mat);
        return mat;
    }

    static Vector3 ReadVector(byte[] bytes,Document doc,int id,int i,bool vec3)
    {
        Access a=doc.accessors[id]; View v=doc.bufferViews[a.bufferView];
        int start=v.byteOffset+a.byteOffset+i*(v.byteStride!=0?v.byteStride:vec3?12:8);
        return new Vector3(BitConverter.ToSingle(bytes,start),BitConverter.ToSingle(bytes,start+4),vec3?-BitConverter.ToSingle(bytes,start+8):0);
    }

    static int Index(byte[] bytes,Document doc,Access a,int i)
    {
        int start=doc.bufferViews[a.bufferView].byteOffset+a.byteOffset;
        return a.componentType switch {
            5121 => bytes[start+i],
            5123 => BitConverter.ToUInt16(bytes,start+2*i),
            5125 => (int)BitConverter.ToUInt32(bytes,start+4*i),
            _ => throw new InvalidDataException("Unsupported index component")
        };
    }

    public static void BakeLibrary()
    {
        const string path="Assets/Island/Resources/IslandModels.asset";
        var bank=AssetDatabase.LoadAssetAtPath<IslandModels>(path);
        if (!bank) { bank=ScriptableObject.CreateInstance<IslandModels>(); AssetDatabase.CreateAsset(bank,path); }
        bank.lighthouse=Load("SM_Lighthouse"); bank.rowboat=Load("SM_Rowboat"); bank.driftwood=Load("SM_Driftwood");
        bank.palms=new GameObject[IslandConfig.PalmVariants]; bank.rocks=new GameObject[IslandConfig.RockVariants];
        for(int i=0;i<bank.palms.Length;i++) bank.palms[i]=Load($"SM_Palm_{i+1:00}");
        for(int i=0;i<bank.rocks.Length;i++) bank.rocks[i]=Load($"SM_Rock_{i+1:00}");
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
    }

    static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/{name}.glb");
}

