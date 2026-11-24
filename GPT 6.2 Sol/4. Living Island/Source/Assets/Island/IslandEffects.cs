using UnityEngine;
using UnityEngine.Rendering;

public sealed class IslandEffects
{
    readonly ParticleSystem rain, groundDrops, swimDrops, flame, embers, smoke, bugs;
    readonly Material air, additive, rainMaterial;
    readonly ParticleSystem.Particle[] flight = new ParticleSystem.Particle[IslandConfig.Fireflies];
    readonly Vector3[] fireflyHomes=new Vector3[IslandConfig.Fireflies];
    readonly Transform rainAnchor;
    readonly System.Random random=new(2509);
    IslandTerrain terrain;
    Vector3 fire;
    float splashBudget, pulse;
    public int Active => rain.particleCount+groundDrops.particleCount+swimDrops.particleCount+flame.particleCount+
        embers.particleCount+smoke.particleCount+bugs.particleCount;
    public int RainActive => rain.particleCount;
    public int BugsActive => bugs.particleCount;

    public IslandEffects(Transform parent,IslandTerrain ground,Vector3 camp)
    {
        air=IslandGeometry.Custom("Particles");
        additive=IslandGeometry.Custom("Particles");
        additive.SetFloat("_DstBlend",(float)BlendMode.One);
        rainMaterial=IslandGeometry.Custom("Particles");
        rainMaterial.SetFloat("_NearFade",2.15f);
        rainAnchor=IslandGeometry.Root("Rain and spray pool",parent);
        rain=System("Rain streaks",rainAnchor,IslandConfig.HeavyRainCount,rainMaterial,1.27f,.022f,13.2f,new Color(.63f,.78f,.89f,.49f),ParticleSystemSimulationSpace.World);
        var shape=rain.shape;
        shape.enabled=true; shape.shapeType=ParticleSystemShapeType.Box;
        shape.scale=new Vector3(23f,13f,.1f);
        shape.rotation=new Vector3(90,0,0);
        var rainRenderer=rain.GetComponent<ParticleSystemRenderer>();
        rainRenderer.renderMode=ParticleSystemRenderMode.Stretch;
        rainRenderer.lengthScale=4.6f;
        rainRenderer.velocityScale=.035f;
        groundDrops=System("Ground splashes",parent,500,air,.36f,.047f,.20f,new Color(.67f,.84f,.87f,.60f),ParticleSystemSimulationSpace.World);
        swimDrops=System("Swim spray",parent,128,air,.46f,.07f,.6f,new Color(.76f,.95f,.96f,.82f),ParticleSystemSimulationSpace.World);

        flame=System("Fire tongues",parent,110,additive,.55f,.42f,.8f,new Color(1f,.46f,.091f,.74f));
        FireShape(flame,.33f,16f,128f);
        Fade(flame,new Color(1,.78f,.29f,.94f),new Color(.93f,.14f,.024f,0f),1f,.0f);
        embers=System("Rising embers",parent,90,additive,1.22f,.054f,1.65f,new Color(1f,.45f,.045f,.94f));
        FireShape(embers,.27f,27f,46f);
        Fade(embers,new Color(1,.59f,.11f,1),new Color(1,.22f,.05f,0),1f,.27f);
        smoke=System("Wind-carried woodsmoke",parent,90,air,4.6f,.24f,.56f,new Color(.31f,.26f,.22f,.36f));
        FireShape(smoke,.18f,22f,19f);
        Fade(smoke,new Color(.37f,.315f,.274f,.35f),new Color(.51f,.52f,.54f,0),.4f,4.2f);
        bugs=System("Firefly drift",parent,IslandConfig.Fireflies,additive,20f,.13f,0,Color.white,ParticleSystemSimulationSpace.World);
        SetGround(ground,camp);
    }

    static ParticleSystem System(string name,Transform parent,int cap,Material material,float life,float size,float speed,Color color,
        ParticleSystemSimulationSpace space=ParticleSystemSimulationSpace.Local)
    {
        var t=IslandGeometry.Root(name,parent);
        var ps=t.gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;
        main.playOnAwake=false;
        main.loop=true;
        main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
        main.maxParticles=cap;
        main.startLifetime=life;
        main.startSize=size;
        main.startSpeed=speed;
        main.startColor=color;
        main.simulationSpace=space;
        main.scalingMode=ParticleSystemScalingMode.Hierarchy;
        var em=ps.emission; em.enabled=false;
        var sh=ps.shape; sh.enabled=false;
        var r=ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial=material;
        r.shadowCastingMode=ShadowCastingMode.Off;
        r.receiveShadows=false;
        ps.Play();
        return ps;
    }

    static void FireShape(ParticleSystem ps,float radius,float angle,float rate)
    {
        ps.transform.rotation=Quaternion.Euler(-90,0,0);
        var sh=ps.shape; sh.enabled=true;sh.shapeType=ParticleSystemShapeType.Cone;
        sh.angle=angle;sh.radius=radius;
        var em=ps.emission;em.enabled=true;em.rateOverTime=rate;
    }

    static void Fade(ParticleSystem ps,Color start,Color end,float startSize,float endSize)
    {
        var color=ps.colorOverLifetime; color.enabled=true;
        var grad=new Gradient();
        grad.SetKeys(new[]{new GradientColorKey(start,0),new GradientColorKey(end,1)},
            new[]{new GradientAlphaKey(start.a,0),new GradientAlphaKey(end.a,1)});
        color.color=new ParticleSystem.MinMaxGradient(grad);
        var size=ps.sizeOverLifetime; size.enabled=true;
        size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,startSize,1,endSize));
    }

    public void SetGround(IslandTerrain terrain,Vector3 camp)
    {
        this.terrain=terrain;
        fire=camp;
        flame.transform.position=fire+Vector3.up*.29f;
        embers.transform.position=fire+Vector3.up*.34f;
        smoke.transform.position=fire+Vector3.up*.73f;
        pulse=0;
        global::System.Array.Clear(fireflyHomes,0,fireflyHomes.Length);
    }

    public void Advance(float dt,IslandPlayer player,IslandSkyWeather sky)
    {
        Vector3 camera=player.Camera.transform.position;
        rainAnchor.SetPositionAndRotation(camera+Vector3.up*9,Quaternion.Euler(2.5f,0f,-sky.Wind*4.5f));
        var emission=rain.emission;
        emission.enabled=sky.Rain>.02f;
        emission.rateOverTime=sky.Rain*IslandConfig.HeavyRainCount/1.23f;
        splashBudget+=dt*sky.Rain*210f;
        int requested=Mathf.Min((int)splashBudget,40);
        splashBudget-=requested;
        for(int i=0;i<requested;i++)
        {
            Vector3 p=camera+new Vector3(Range(-9,9),0,Range(-9,9));
            p.y=terrain.Height(p.x,p.z)+.045f;
            if(p.y >= IslandConfig.SeaLevel) Spray(groundDrops,p,Range(.3f,.83f));
        }
        if(player.Splash)
        {
            Vector3 p=player.Position;
            p.y=IslandOcean.Elevation(p,sky.Clock,sky.Wave)+.048f;
            for(int i=0;i<13;i++) Spray(swimDrops,p+new Vector3(Range(-.27f,.27f),0,Range(-.27f,.27f)),Range(.74f,2.03f));
        }
        Vector3 wind=new Vector3(IslandConfig.WindDirection.x,0,IslandConfig.WindDirection.y)*sky.Wind;
        smoke.transform.rotation=Quaternion.LookRotation(Vector3.up+wind*.27f);
        Flight(dt,player.Position,sky);
    }

    void Spray(ParticleSystem ps,Vector3 p,float speed)
    {
        var ep=new ParticleSystem.EmitParams {
            position=p, velocity=new Vector3(Range(-.86f,.86f),speed,Range(-.86f,.86f)), startLifetime=Range(.12f,.34f)
        };
        ps.Emit(ep,1);
    }

    void Flight(float dt,Vector3 player,IslandSkyWeather sky)
    {
        float dark=1-IslandTerrain.Smooth(.026f,.32f,sky.Daylight);
        if(dark<.025f || player.y<-1f) { if(bugs.particleCount>0) bugs.Clear(); pulse=0; return; }
        pulse-=dt;
        if(pulse<=0)
        {
            pulse=.31f;
            for(int i=0;i<flight.Length;i++)
            {
                Vector3 p=fireflyHomes[i];
                if(p!=Vector3.zero && (p-player).sqrMagnitude<IslandConfig.FireflyRange*IslandConfig.FireflyRange*.86f) continue;
                fireflyHomes[i]=Vector3.zero;
                for(int attempt=0;attempt<13;attempt++)
                {
                    float a=Range(0,Mathf.PI*2),r=Range(1.5f,IslandConfig.FireflyRange-3);
                    p=player+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
                    p.y=terrain.Height(p.x,p.z)+Range(.45f,2.7f);
                    if(p.y<2.35f || (p-player).sqrMagnitude>=IslandConfig.FireflyRange*IslandConfig.FireflyRange) continue;
                    fireflyHomes[i]=p;
                    break;
                }
            }
        }
        int count=0;
        for(int i=0;i<flight.Length;i++)
        {
            if(fireflyHomes[i]==Vector3.zero) continue;
            float t=sky.Clock+i*.723f;
            Vector3 p=fireflyHomes[i]+new Vector3(Mathf.Sin(t*.69f+i)*.81f,Mathf.Sin(t*1.21f)*.32f,Mathf.Cos(t*.53f+i*.48f)*.72f);
            if((p-player).sqrMagnitude>IslandConfig.FireflyRange*IslandConfig.FireflyRange) continue;
            float wave=.21f+.79f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*1.99f)),3f);
            ref ParticleSystem.Particle dot=ref flight[count++];
            dot.position=p;
            dot.startLifetime=2f;dot.remainingLifetime=1.5f;
            dot.startSize=.068f+wave*.115f;
            dot.startColor=new Color(.73f,1f,.28f,wave*dark*.93f);
        }
        bugs.SetParticles(flight,count);
    }

    float Range(float a,float b)=>a+(b-a)*(float)random.NextDouble();

    public void Dispose()
    {
        Object.Destroy(air);Object.Destroy(additive);Object.Destroy(rainMaterial);
    }
}

