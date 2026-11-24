using UnityEngine;
using UnityEngine.Rendering;

public sealed class IslandTerrain
{
    readonly float[] heights = new float[IslandConfig.TerrainSamples * IslandConfig.TerrainSamples];
    const int Side = IslandConfig.TerrainSamples;
    const float Half = IslandConfig.WorldSize * .5f;
    const float Step = IslandConfig.WorldSize / (Side - 1);
    static readonly float beachGrade = Mathf.Tan(IslandConfig.BeachSlope*Mathf.Deg2Rad)*.797f;
    static readonly Vector2[] coast = SampleCoast();
    public Mesh Mesh { get; }
    public Vector3 Lighthouse { get; private set; }
    public Vector3 Camp { get; private set; }
    public Vector3 Spawn { get; private set; }
    public Vector3 Boat { get; private set; }

    public IslandTerrain(int seed)
    {
        var rng = new System.Random(seed);
        float offsetX = rng.Next(200, 12000) + .37f, offsetZ = rng.Next(200, 12000) + .63f;
        float peak = float.MinValue;
        for (int z = 0; z < Side; z++)
        for (int x = 0; x < Side; x++)
        {
            float h = Shape(x * Step - Half, z * Step - Half, offsetX, offsetZ);
            heights[x + z * Side] = h;
            peak = Mathf.Max(peak, h);
        }
        var vertices = new Vector3[heights.Length];
        var normals = new Vector3[heights.Length];
        var indices = new int[(Side - 1) * (Side - 1) * 6];
        for (int i = 0; i < heights.Length; i++)
            if (heights[i] > 0f) heights[i] *= IslandConfig.MountainHeight / peak;
        for (int z = 0, face = 0; z < Side; z++)
        for (int x = 0; x < Side; x++)
        {
            int i = x + z * Side;
            vertices[i] = new Vector3(x * Step - Half, heights[i], z * Step - Half);
            float dx = heights[Mathf.Min(x + 1, Side - 1) + z * Side] - heights[Mathf.Max(0, x - 1) + z * Side];
            float dz = heights[x + Mathf.Min(z + 1, Side - 1) * Side] - heights[x + Mathf.Max(0, z - 1) * Side];
            normals[i] = new Vector3(-dx, 2f * Step, -dz).normalized;
            if (x == Side - 1 || z == Side - 1) continue;
            indices[face++] = i; indices[face++] = i + Side; indices[face++] = i + 1;
            indices[face++] = i + 1; indices[face++] = i + Side; indices[face++] = i + Side + 1;
        }

        Mesh = new Mesh { name = "Seeded island heightfield", indexFormat = IndexFormat.UInt32 };
        Mesh.vertices = vertices;
        Mesh.normals = normals;
        Mesh.triangles = indices;
        Lighthouse = FindSite(IslandConfig.LighthouseSite, 10f, 45f, .48f);
        Camp = FindSite(IslandConfig.CampSite, .8f, 2f, .14f);
        Spawn = FindSite(IslandConfig.SpawnSite, .68f, 1.24f, .14f);
        Boat = FindSite(IslandConfig.BoatSite, -.55f, -.24f, .21f);
    }

    static float Shape(float x, float z, float ox, float oz)
    {
        float radius = IslandConfig.IslandDiameter * .5f;
        float angle = Mathf.Atan2(z, x);
        float r = Mathf.Sqrt(x*x + z*z);
        float shoreline=CoastRadius(angle,out float headland);
        float shore=shoreline-r;
        float cliff = 1f - Smooth(IslandConfig.CliffLength * .5f, IslandConfig.CliffLength * .5f + 15f,
            Mathf.Abs(Mathf.DeltaAngle(IslandConfig.CliffBearing, angle * Mathf.Rad2Deg)) * Mathf.Deg2Rad * radius);
        float cape = headland * Smooth(1.2f,8.8f,shore) * (1-Smooth(14f,49f,shore));
        float floor = Mathf.Max(IslandConfig.Seafloor, shore * beachGrade);
        float shelf = Mathf.Clamp(shore * beachGrade, IslandConfig.Seafloor, IslandConfig.BeachHeight);
        float hills = 0f, weight = .52f, frequency = .0125f;
        for (int octave = 0; octave < IslandConfig.FbmOctaves; octave++)
        {
            hills += (Mathf.PerlinNoise(x * frequency + ox, z * frequency + oz) - .48f) * weight;
            weight *= .51f;
            frequency *= 2.09f;
        }

        Vector2 m = new Vector2(x,z) - IslandConfig.Summit;
        float mountain = Mathf.Exp(-(m.x*m.x/3400f + m.y*m.y/2450f));
        float ridge = 1f - Mathf.Abs(Mathf.PerlinNoise(x*.033f+ox+71f,z*.033f+oz-11f)*2f-1f);
        float inland = Smooth(12f, 66f, shore) * (8f + 20f*hills + mountain * (22f + 19f*ridge*ridge));
        float scarp = cliff * Smooth(1f, 8f, shore) * (16f + 8f*hills);
        float seabed = Mathf.Lerp(floor, IslandConfig.Seafloor, Smooth(24f, 42f, -shore));
        return (shore < 0f ? seabed : shelf) + inland + scarp + 17.6f*cape;
    }

    static float CoastRadius(float angle,out float headland)
    {
        float radius=IslandConfig.IslandDiameter*.5f;
        float degrees=angle*Mathf.Rad2Deg;
        float coast=radius+5.5f*Mathf.Sin(angle*3f+.3f)+5f*Mathf.Cos(angle*5f+.6f);
        float bay=Mathf.Exp(-Mathf.Pow(Mathf.DeltaAngle(-88f,degrees)*Mathf.Deg2Rad*radius/(IslandConfig.BayWidth*.52f),2f));
        headland=Mathf.Exp(-Mathf.Pow(Mathf.DeltaAngle(-65f,degrees)/8.8f,2f));
        return coast-37f*bay+5.8f*headland;
    }

    static Vector2[] SampleCoast()
    {
        var points=new Vector2[360];
        for(int i=0;i<points.Length;i++)
        {
            float angle=i*2f*Mathf.PI/points.Length;
            points[i]=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*CoastRadius(angle,out _);
        }
        return points;
    }

    public Vector2 CliffProfilePoint(float along, float inland)
    {
        float angle=IslandConfig.CliffBearing*Mathf.Deg2Rad + along/(IslandConfig.IslandDiameter*.5f);
        return (CoastRadius(angle,out _)-inland)*new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
    }

    public float DistanceToCoast(Vector3 point)
    {
        Vector2 xz=new Vector2(point.x,point.z);
        float nearest=float.PositiveInfinity;
        for(int i=0;i<coast.Length;i++)
        {
            Vector2 from=coast[i], span=coast[(i+1)%coast.Length]-from;
            float t=Mathf.Clamp01(Vector2.Dot(xz-from,span)/span.sqrMagnitude);
            nearest=Mathf.Min(nearest,(xz-from-span*t).sqrMagnitude);
        }
        return Mathf.Sqrt(nearest);
    }

    public static float Smooth(float a, float b, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, value));

    public float Height(float x, float z)
    {
        float px = Mathf.Clamp((x+Half)/Step,0,Side-1.001f), pz = Mathf.Clamp((z+Half)/Step,0,Side-1.001f);
        int ix = (int)px, iz = (int)pz, i = ix + iz * Side;
        float u = px - ix, v = pz - iz;
        // Use the same diagonal and interpolation as the rendered mesh and collider.
        return u+v <= 1f
            ? heights[i] + u*(heights[i+1]-heights[i]) + v*(heights[i+Side]-heights[i])
            : heights[i+Side+1] + (1-u)*(heights[i+Side]-heights[i+Side+1]) + (1-v)*(heights[i+1]-heights[i+Side+1]);
    }

    public float Slope(float x,float z)
    {
        float dx = (Height(x+.4f,z)-Height(x-.4f,z))/.8f, dz = (Height(x,z+.4f)-Height(x,z-.4f))/.8f;
        return Mathf.Sqrt(dx*dx+dz*dz);
    }

    public Vector3 FindSite(Vector2 site, float min, float max, float slope)
    {
        Vector3 best = new Vector3(site.x,Height(site.x,site.y),site.y);
        float score = float.PositiveInfinity;
        for(int z=-44;z<=44;z++)
        for(int x=-44;x<=44;x++)
        {
            float cost = x*x + z*z;
            if (cost >= score) continue;
            float sx = site.x+x, sz = site.y+z, y = Height(sx,sz);
            if (y < min || y > max || Slope(sx,sz) > slope) continue;
            best = new Vector3(sx,y,sz);
            score = cost;
        }
        return best;
    }
}

