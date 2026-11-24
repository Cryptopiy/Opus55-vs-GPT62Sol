using UnityEngine;
using UnityEngine.Rendering;

public sealed class IslandOcean
{
    const float Extent=1250f;
    readonly Mesh mesh;
    readonly Material material;
    readonly Transform surface;

    public IslandOcean(Transform parent)
    {
        material=IslandGeometry.Custom("Water");
        int side=IslandConfig.WaterSamples;
        var vertices=new Vector3[side*side];
        var indices=new int[(side-1)*(side-1)*6];
        for(int z=0,f=0;z<side;z++)
        for(int x=0;x<side;x++)
        {
            int i=x+z*side;
            vertices[i]=new Vector3(Grid(x),0,Grid(z));
            if(x==side-1 || z==side-1) continue;
            indices[f++]=i; indices[f++]=i+side; indices[f++]=i+1;
            indices[f++]=i+1; indices[f++]=i+side; indices[f++]=i+side+1;
        }
        mesh=new Mesh { name="400 x 400 camera-centered grid",indexFormat=IndexFormat.UInt32 };
        mesh.vertices=vertices;
        mesh.triangles=indices;
        mesh.bounds=new Bounds(Vector3.zero,new Vector3(Extent*2,3f,Extent*2));
        var r=IslandGeometry.Render("Gerstner ocean",parent,mesh,material);
        r.shadowCastingMode=ShadowCastingMode.Off;
        r.receiveShadows=false;
        surface=r.transform;
        Shader.SetGlobalVectorArray("_IslandWaves",IslandConfig.Waves);
        Shader.SetGlobalInt("_IslandWaveCount",IslandConfig.WaveCount);
        Shader.SetGlobalVector("_IslandShallow",IslandConfig.Shallow.linear);
        Shader.SetGlobalVector("_IslandDeep",IslandConfig.Deep.linear);
        Shader.SetGlobalFloat("_IslandFoamDepth",IslandConfig.FoamDepth);
    }

    static float Grid(int i)
    {
        float u=(2f*i)/(IslandConfig.WaterSamples-1)-1;
        return Extent*u*(.014f+.986f*u*u);
    }

    public void Follow(Vector3 camera) => surface.position=new Vector3(camera.x,IslandConfig.SeaLevel,camera.z);

    public static float Elevation(Vector3 p,float time,float amplitude)
    {
        float result=IslandConfig.SeaLevel;
        foreach(Vector4 wave in IslandConfig.Waves)
        {
            float k=(2f*Mathf.PI)/wave.w;
            result+=Mathf.Sin(k*(p.x*wave.x+p.z*wave.y)-Mathf.Sqrt(9.81f*k)*time)*wave.z*amplitude;
        }
        return result;
    }

    public void Dispose()
    {
        Object.Destroy(mesh);
        Object.Destroy(material);
    }
}

