using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class IslandVegetation
{
    sealed class Cell
    {
        public readonly Matrix4x4[] Matrices;
        public readonly Bounds Bounds; public readonly int Low, Medium;
        public Cell(List<Matrix4x4> matrices, Bounds bounds,int low,int medium) { Matrices=matrices.ToArray(); Bounds=bounds; Low=low; Medium=medium; }
    }

    readonly IslandTerrain terrain;
    readonly Transform root;
    readonly Material grassMat, plantMat;
    readonly Texture2D frond;
    readonly Mesh grass, bush, flowers;
    readonly List<Cell> cells=new();
    readonly Plane[] planes=new Plane[6];
    readonly Mesh[,] palmMeshes=new Mesh[IslandConfig.PalmVariants,2];
    readonly Material[] palmMat=new Material[IslandConfig.PalmVariants];
    readonly Matrix4x4[][] palmPoints=new Matrix4x4[IslandConfig.PalmVariants][];
    readonly Matrix4x4[] near=new Matrix4x4[IslandConfig.Palms], far=new Matrix4x4[IslandConfig.Palms];
    readonly System.Random rng;
    public int GrassPlaced { get; private set; }
    public int GrassVisible { get; private set; }
    public int PalmsPlaced { get; private set; }
    public int RocksPlaced { get; private set; }
    public int BushesPlaced { get; private set; }
    public int FlowersPlaced { get; private set; }
    public float MaxPlantAnchorError { get; private set; }
    public float LowestTreeOrRock { get; private set; } = float.PositiveInfinity;

    public IslandVegetation(Transform parent, IslandTerrain terrain,IslandModels models,int seed)
    {
        this.terrain=terrain;
        root=IslandGeometry.Root("Seeded island vegetation",parent);
        rng=new System.Random(seed);
        grassMat=IslandGeometry.Custom("Grass");
        Shader.SetGlobalFloat("_IslandGrassDistance",IslandConfig.GrassDistance);
        plantMat=IslandGeometry.Custom("Props");
        frond=FrondTexture();
        Shader.SetGlobalTexture("_IslandFrond",frond);
        grass=BladeMesh();
        bush=BushMesh();
        flowers=FlowerMesh();
        PopulatePalms(models);
        PopulateRocks(models);
        for(int i=0;i<IslandConfig.Bushes;i++)
        {
            Vector3 p=Point(2f,26f,.53f,.0f);
            Plant("Saltbush",bush,p,Range(.52f,1.19f));
            BushesPlaced++;
        }
        for(int i=0;i<IslandConfig.FlowerClusters;i++)
        {
            Vector3 p=Point(2.25f,13f,.16f,0);
            Plant("Beach flowers",flowers,p,Range(.69f,1.2f));
            FlowersPlaced++;
        }
        FillGrass();
    }

    float Range(float low,float high) => low+(high-low)*(float)rng.NextDouble();
    Vector3 Point(float min,float max,float maxSlope,float minSlope)
    {
        const int tries=2000;
        for(int i=0;i<tries;i++)
        {
            float r=IslandConfig.IslandDiameter*.54f*Mathf.Sqrt(Range(0,1)), a=Range(0,Mathf.PI*2);
            Vector3 p=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
            p.y=terrain.Height(p.x,p.z);
            if(p.y<min||p.y>max || (p-terrain.Camp).sqrMagnitude<21f || (p-terrain.Spawn).sqrMagnitude<6.25f ||
               (p-terrain.Lighthouse).sqrMagnitude<46f) continue;
            float slope=terrain.Slope(p.x,p.z);
            if(slope>maxSlope || slope<minSlope) continue;
            return p;
        }
        throw new System.InvalidOperationException("No point satisfies the island biome constraints.");
    }

    void PopulatePalms(IslandModels models)
    {
        var accepted=new Vector3[IslandConfig.Palms];
        for(int v=0;v<IslandConfig.PalmVariants;v++)
        {
            string name=$"SM_Palm_{v+1:00}";
            var high=IslandModels.Part(models.palms[v],name).GetComponent<MeshRenderer>();
            palmMeshes[v,0]=high.GetComponent<MeshFilter>().sharedMesh;
            palmMeshes[v,1]=IslandModels.Part(models.palms[v],name+"_LOD1").GetComponent<MeshFilter>().sharedMesh;
            palmMat[v]=high.sharedMaterial;
            palmPoints[v]=new Matrix4x4[IslandConfig.Palms/IslandConfig.PalmVariants];
            for(int i=0;i<palmPoints[v].Length;i++)
            {
                Vector3 p=default;
                for(int attempt=0;attempt<60;attempt++)
                {
                    p=i%3==0?Point(.78f,2.25f,.145f,0):Point(2.0f,18f,.49f,.0f);
                    bool nearTree=false;
                    for(int j=0;j<PalmsPlaced;j++) if((accepted[j]-p).sqrMagnitude<7.2f) { nearTree=true; break; }
                    if(!nearTree) break;
                }
                if(v==0 && i<3)
                {
                    Vector2 site=new Vector2(terrain.Spawn.x+8f+i*6.3f,terrain.Spawn.z+5.4f+i*4.5f);
                    p=terrain.FindSite(site,.82f,3.2f,.145f);
                }
                accepted[PalmsPlaced++]=p;
                LowestTreeOrRock=Mathf.Min(LowestTreeOrRock,p.y-.06f);
                float tall=(Range(IslandConfig.PalmMin,IslandConfig.PalmMax)+.06f)/palmMeshes[v,0].bounds.max.y;
                float girth=Range(.83f,1.12f)*Mathf.Lerp(.91f,1.12f,tall-.6f);
                palmPoints[v][i]=Matrix4x4.TRS(p-Vector3.up*.06f,Quaternion.Euler(0,Range(0,360),0),new Vector3(girth,tall,girth));
                MaxPlantAnchorError=Mathf.Max(MaxPlantAnchorError,.06f);
            }
        }
    }

    void PopulateRocks(IslandModels models)
    {
        var vertices=new Vector3[models.rocks.Length][];
        for(int i=0;i<vertices.Length;i++)
            vertices[i]=models.rocks[i].GetComponentInChildren<MeshFilter>().sharedMesh.vertices;
        float contactDepth=IslandConfig.GroundTolerance*.375f;
        for(int i=0;i<IslandConfig.Rocks;i++)
        {
            bool coast=i%3==0;
            Vector3 p=coast?Point(.65f,3.7f,.59f,.04f):Point(4f,43f,2.7f,.18f);
            float size=coast ? Range(.35f,1.53f) : Range(.51f,2.58f);
            int variant=i%IslandConfig.RockVariants;
            var t=IslandModels.Create(models.rocks[variant],"Basalt outcrop",root,p,Quaternion.Euler(0,Range(0,360),0),
                new Vector3(size,size*Range(.55f,.86f),size*Range(.79f,1.33f)));
            var renderer=t.GetComponentInChildren<MeshRenderer>();
            Matrix4x4 meshToWorld=renderer.localToWorldMatrix;
            float penetration=float.NegativeInfinity;
            // Settle the actual footprint so a sloping site still supports the rock.
            foreach(Vector3 vertex in vertices[variant])
            {
                Vector3 world=meshToWorld.MultiplyPoint3x4(vertex);
                penetration=Mathf.Max(penetration,terrain.Height(world.x,world.z)-world.y);
            }
            float adjustment=penetration-contactDepth;
            t.position+=Vector3.up*adjustment;
            MaxPlantAnchorError=Mathf.Max(MaxPlantAnchorError,Mathf.Abs(penetration-adjustment));
            LowestTreeOrRock=Mathf.Min(LowestTreeOrRock,renderer.bounds.min.y);
            RocksPlaced++;
        }
    }

    void Plant(string name,Mesh mesh,Vector3 p,float size)
    {
        var r=IslandGeometry.Render(name,root,mesh,plantMat);
        r.transform.SetPositionAndRotation(p-Vector3.up*.033f,Quaternion.Euler(0,Range(0,360),0));
        r.transform.localScale=Vector3.one*size;
        r.shadowCastingMode=ShadowCastingMode.Off;
    }

    void FillGrass()
    {
        int side=Mathf.CeilToInt(IslandConfig.WorldSize/IslandConfig.GrassCell);
        var buckets=new List<Matrix4x4>[side*side];
        var low=new int[buckets.Length]; var medium=new int[buckets.Length];
        while(GrassPlaced<IslandConfig.GrassHigh)
        {
            Vector3 p=Point(2.22f,33f,.62f,0);
            float coverage=Mathf.PerlinNoise(p.x*.103f+218f,p.z*.103f+311f);
            if(Range(0,1)>Mathf.Lerp(.23f,1f,coverage)) continue;
            int x=(int)((p.x+IslandConfig.WorldSize*.5f)/IslandConfig.GrassCell);
            int z=(int)((p.z+IslandConfig.WorldSize*.5f)/IslandConfig.GrassCell);
            int slot=x+z*side;
            (buckets[slot]??=new List<Matrix4x4>()).Add(Matrix4x4.TRS(p-Vector3.up*.018f,Quaternion.Euler(0,Range(0,360),0),
                Vector3.one*Range(.48f,1.23f)));
            if(GrassPlaced<IslandConfig.GrassLow) low[slot]++;
            if(GrassPlaced<IslandConfig.GrassMedium) medium[slot]++;
            GrassPlaced++;
        }
        for(int slot=0;slot<buckets.Length;slot++)
        {
            List<Matrix4x4> bucket=buckets[slot];
            if(bucket==null) continue;
            var bounds=new Bounds(Origin(bucket[0]),Vector3.zero);
            foreach(Matrix4x4 m in bucket) bounds.Encapsulate(Origin(m));
            bounds.Expand(new Vector3(2.2f,2.4f,2.2f));
            cells.Add(new Cell(bucket,bounds,low[slot],medium[slot]));
        }
    }

    static Vector3 Origin(Matrix4x4 m) => new Vector3(m.m03,m.m13,m.m23);

    public void Draw(Camera camera,int quality)
    {
        Vector3 pos=camera.transform.position;
        GeometryUtility.CalculateFrustumPlanes(camera,planes);
        GrassVisible=0;
        foreach(Cell cell in cells)
        {
            float d=Mathf.Sqrt(cell.Bounds.SqrDistance(pos));
            if(d>=IslandConfig.GrassDistance || !GeometryUtility.TestPlanesAABB(planes,cell.Bounds)) continue;
            int preset=quality==2?cell.Matrices.Length:quality==1?cell.Medium:cell.Low;
            int count=Mathf.RoundToInt(preset*(1-IslandTerrain.Smooth(26f,IslandConfig.GrassDistance,d)*.98f));
            if(count<=0) continue;
            var args=new RenderParams(grassMat) { camera=camera,worldBounds=cell.Bounds,shadowCastingMode=ShadowCastingMode.Off };
            for(int start=0;start<count;start+=IslandConfig.GrassBatch)
                Graphics.RenderMeshInstanced(args,grass,0,cell.Matrices,Mathf.Min(IslandConfig.GrassBatch,count-start),start);
            GrassVisible+=count;
        }
        float lod=quality==2?68f:quality==1?42f:24f;
        for(int v=0;v<IslandConfig.PalmVariants;v++)
        {
            int n=0,f=0;
            foreach(Matrix4x4 m in palmPoints[v])
            {
                Vector3 p=Origin(m);
                if((p-pos).sqrMagnitude<lod*lod) near[n++]=m; else far[f++]=m;
            }
            var args=new RenderParams(palmMat[v]) { camera=camera,shadowCastingMode=ShadowCastingMode.On,receiveShadows=true };
            if(n>0) Graphics.RenderMeshInstanced(args,palmMeshes[v,0],0,near,n);
            if(f>0) Graphics.RenderMeshInstanced(args,palmMeshes[v,1],0,far,f);
        }
    }

    static Mesh BladeMesh()
    {
        var v=new List<Vector3>{new(-.061f,0,0),new(.061f,0,0),new(-.044f,.28f,.049f),new(.036f,.28f,.049f),
            new(-.014f,.54f,.122f),new(.023f,.54f,.122f),new(.044f,.68f,.218f)};
        var uv=new List<Vector2>{new(0,0),new(1,0),new(0,.42f),new(1,.42f),new(0,.80f),new(1,.80f),new(.5f,1)};
        return IslandGeometry.Mesh("One tapered meadow blade",v,new List<int>{0,2,1,1,2,3,2,4,3,3,4,5,4,6,5},uv);
    }

    static Mesh BushMesh()
    {
        var v=new List<Vector3>(); var tri=new List<int>(); var color=new List<Color>();
        var rng=new System.Random(64);
        for(int i=0;i<86;i++)
        {
            float t=(float)rng.NextDouble()*6.28318f, r=.17f+(float)rng.NextDouble()*.63f;
            float y=.08f+(float)rng.NextDouble()*(.69f-r*.59f), spread=.09f+(float)rng.NextDouble()*.18f;
            Vector3 along=new Vector3(Mathf.Cos(t),.28f+(float)rng.NextDouble(),Mathf.Sin(t)), across=Vector3.Cross(along,Vector3.up).normalized;
            Vector3 p=new Vector3(Mathf.Cos(t)*r,y,Mathf.Sin(t)*r);
            Color green=Color.Lerp(new Color(.073f,.139f,.045f),new Color(.29f,.38f,.083f),(float)rng.NextDouble());
            Diamond(v,tri,color,p,along*spread,across*spread*.54f,green);
        }
        var mesh=IslandGeometry.Mesh("Saltbush leaves",v,tri,null); mesh.SetColors(color); return mesh;
    }

    static Mesh FlowerMesh()
    {
        var v=new List<Vector3>(); var tri=new List<int>(); var color=new List<Color>();
        var rng=new System.Random(65);
        for(int i=0;i<24;i++)
        {
            float a=(float)rng.NextDouble()*6.28318f,r=.92f*Mathf.Sqrt((float)rng.NextDouble());
            Vector3 p=new Vector3(Mathf.Cos(a)*r,.10f+(float)rng.NextDouble()*.25f,Mathf.Sin(a)*r);
            var petal=i%3==0?new Color(.84f,.26f,.23f):i%3==1?new Color(.96f,.63f,.15f):new Color(.76f,.44f,.61f);
            Diamond(v,tri,color,p*.5f+new Vector3(p.x*.5f,0,p.z*.5f),Vector3.up*p.y*.5f,Vector3.right*.012f,new Color(.13f,.24f,.038f));
            for(int k=0;k<5;k++)
            {
                float theta=a+k*6.28318f/5;
                Vector3 d=new Vector3(Mathf.Cos(theta),.18f,Mathf.Sin(theta));
                Diamond(v,tri,color,p+d*.034f,d*.054f,Vector3.Cross(d,Vector3.up)*.025f,petal);
            }
            Diamond(v,tri,color,p+Vector3.up*.012f,Vector3.forward*.027f,Vector3.right*.026f,new Color(.93f,.76f,.19f));
        }
        var mesh=IslandGeometry.Mesh("Coastal flower cluster",v,tri,null); mesh.SetColors(color); return mesh;
    }

    static void Diamond(List<Vector3> v,List<int> tri,List<Color> c,Vector3 p,Vector3 a,Vector3 b,Color color)
    {
        int n=v.Count;
        v.Add(p-a); v.Add(p+b); v.Add(p+a); v.Add(p-b); v.Add(p+Vector3.up*.028f);
        for(int i=0;i<4;i++) { tri.Add(n+i);tri.Add(n+4);tri.Add(n+(i+1)%4);c.Add(color); }
        c.Add(color);
    }

    static Texture2D FrondTexture()
    {
        const int w=256,h=128;
        var texture=new Texture2D(w,h,TextureFormat.R8,true,true){name="Procedural pinnate palm cutout",wrapMode=TextureWrapMode.Clamp,anisoLevel=4};
        var pixels=new byte[w*h];
        for(int y=0;y<h;y++)
        for(int x=0;x<w;x++)
        {
            float u=x/(w-1f),side=Mathf.Abs((y/(h-1f)-.5f)*2);
            float slant=Mathf.Repeat((u-side*.087f)*21f,1f);
            float ribbon=(.47f-.30f*side)*Mathf.Sin(Mathf.PI*Mathf.Clamp01(u+.042f)*.95f)+.03f;
            bool leaf=Mathf.Abs(slant-.5f)<ribbon && side<.98f-.05f*Mathf.Sin(u*37f);
            pixels[x+y*w]=(byte)(leaf||side<.033f||u>.96f&&side<.34f?255:0);
        }
        texture.SetPixelData(pixels,0);
        texture.Apply(true,true);
        return texture;
    }

    public void Dispose()
    {
        Object.Destroy(root.gameObject);
        Object.Destroy(grassMat);Object.Destroy(plantMat);
        Object.Destroy(grass);Object.Destroy(bush);Object.Destroy(flowers);Object.Destroy(frond);
    }
}

