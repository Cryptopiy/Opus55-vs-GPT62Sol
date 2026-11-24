using UnityEngine;
using UnityEngine.Rendering;

public enum IslandWeather { Clear, Cloudy, Rain, Fog, Storm }

public sealed class IslandSkyWeather
{
    readonly Light sunlight;
    readonly Material sky, boltMat;
    readonly Texture2D stars;
    readonly LineRenderer bolt, fork, twig;
    readonly Vector3[] spine = new Vector3[15], branch = new Vector3[7], branch2 = new Vector3[6];
    System.Random random;
    Vector4 atmosphere, from, to;
    float transition, nextWeather, nextLightning, flashLeft, thunderLeft, flashDistance, autoSince;
    float activeWave, fromWave, toWave;
    float nextThunderGain;
    public float Hour = IslandConfig.StartHour, Speed = 1f;
    public bool TimeStopped, Auto;
    public IslandWeather Weather { get; private set; }
    public float Daylight { get; private set; }
    public float Rain => atmosphere.y;
    public float Wind => atmosphere.z;
    public float Wave => activeWave;
    public float Density => atmosphere.w;
    public float RainAudio => Rain;
    public float Thunder { get; private set; }
    public float Clock { get; private set; }
    public int Flashes { get; private set; }
    public bool FlashActive => flashLeft>0;

    public IslandSkyWeather(Transform parent,int seed)
    {
        random=new System.Random(seed);
        sky=IslandGeometry.Custom("Sky");
        stars=CreateStars(seed);
        sky.SetTexture("_Stars",stars);
        RenderSettings.skybox=sky;
        RenderSettings.fog=false;
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientIntensity=1.0f;
        sunlight=IslandGeometry.Root("Sun and moon key",parent).gameObject.AddComponent<Light>();
        sunlight.type=LightType.Directional;
        sunlight.shadows=LightShadows.Soft;
        sunlight.shadowStrength=.87f;
        sunlight.shadowBias=.22f;
        sunlight.shadowNormalBias=.36f;
        RenderSettings.sun=sunlight;

        boltMat=IslandGeometry.Custom("Glow");
        boltMat.SetColor("_Color",new Color(.52f,.74f,1f,1f)*5f);
        bolt=Line("Lightning channel",parent,spine.Length,.40f);
        fork=Line("Lightning fork",parent,branch.Length,.19f);
        twig=Line("Lightning branch",parent,branch2.Length,.12f);
        Shader.SetGlobalFloat("_IslandUnderVisibility",IslandConfig.UnderwaterVisibility);
        Shader.SetGlobalVector("_IslandUnderwaterColor",new Vector4(.018f,.104f,.127f,1));
        SetWeather(IslandWeather.Clear,true);
    }

    LineRenderer Line(string name,Transform p,int points,float width)
    {
        var l=IslandGeometry.Root(name,p).gameObject.AddComponent<LineRenderer>();
        l.sharedMaterial=boltMat;
        l.useWorldSpace=true;
        l.positionCount=points;
        l.widthMultiplier=width;
        l.shadowCastingMode=ShadowCastingMode.Off;
        l.receiveShadows=false;
        l.enabled=false;
        return l;
    }

    static Texture2D CreateStars(int seed)
    {
        const int w=2048,h=1024;
        var texture=new Texture2D(w,h,TextureFormat.R8,false,true) { name="1,500 procedural stars",wrapMode=TextureWrapMode.Repeat };
        byte[] image=new byte[w*h];
        var rng=new System.Random(seed);
        int count=0;
        while(count<IslandConfig.StarCount)
        {
            int x=rng.Next(1,w-1),y=rng.Next(h/2+1,h-1), i=x+y*w;
            // Uniform height on the hemisphere keeps star density even at zenith.
            if(rng.NextDouble()>Mathf.Cos((float)(y-h/2)/h*Mathf.PI) || image[i]!=0) continue;
            int lum=rng.Next(49,227);
            image[i]=(byte)lum;
            count++;
        }
        texture.LoadRawTextureData(image);
        texture.Apply(false,true);
        return texture;
    }

    public void SetWeather(IslandWeather value,bool immediate=false)
    {
        bool entering=value!=Weather;
        Weather=value;
        transition=immediate ? IslandConfig.WeatherBlend : 0f;
        from=atmosphere;
        fromWave=activeWave;
        to=IslandConfig.WeatherProfiles[(int)value];
        toWave=value==IslandWeather.Storm?IslandConfig.StormWaves:1f;
        if(immediate) { atmosphere=to; activeWave=toWave; }
        if(value==IslandWeather.Storm && entering) nextLightning=Clock+Range(IslandConfig.LightningMin,IslandConfig.LightningMax);
    }

    public void SetAuto(bool value)
    {
        Auto=value;
        autoSince=Clock;
        nextWeather=autoSince+Range(IslandConfig.AutoMin,IslandConfig.AutoMax);
    }

    public void Reseed(int seed) => random=new System.Random(seed);
    float Range(float a,float b) => a+(b-a)*(float)random.NextDouble();

    public void Advance(float dt,Vector3 camera,bool submerged,Vector3 forward=default)
    {
        Clock+=dt;
        if(!TimeStopped) Hour=Mathf.Repeat(Hour+dt*Speed*IslandConfig.HoursPerDay/IslandConfig.DaySeconds,IslandConfig.HoursPerDay);
        if(Auto && Clock>=nextWeather)
        {
            SetWeather((IslandWeather)(((int)Weather+random.Next(1,5))%5));
            nextWeather=Clock+Range(IslandConfig.AutoMin,IslandConfig.AutoMax);
        }
        transition=Mathf.Min(transition+dt,IslandConfig.WeatherBlend);
        float blend=IslandTerrain.Smooth(0,IslandConfig.WeatherBlend,transition);
        atmosphere=Vector4.Lerp(from,to,blend);
        activeWave=Mathf.Lerp(fromWave,toWave,blend);
        Thunder=0;
        if(thunderLeft>0)
        {
            thunderLeft-=dt;
            if(thunderLeft<=0) Thunder=nextThunderGain;
        }
        flashLeft=Mathf.Max(0,flashLeft-dt);
        if(Weather==IslandWeather.Storm && Clock>=nextLightning)
        {
            Lightning(camera,forward);
            nextLightning=Clock+Range(IslandConfig.LightningMin,IslandConfig.LightningMax);
        }
        bolt.enabled=fork.enabled=twig.enabled=flashLeft>0;
        float flash=flashLeft/IslandConfig.FlashSeconds;
        float angle=(Hour-6f)*(2f*Mathf.PI/IslandConfig.HoursPerDay);
        float tilt=IslandConfig.OrbitTilt*Mathf.Deg2Rad;
        Vector3 direction=new Vector3(-Mathf.Cos(angle),Mathf.Sin(angle)*Mathf.Cos(tilt),Mathf.Sin(angle)*Mathf.Sin(tilt));
        float altitude=direction.y;
        Daylight=IslandTerrain.Smooth(-.13f,.32f,altitude);
        float dusk=1-IslandTerrain.Smooth(-.015f,.43f,Mathf.Abs(altitude));
        Color zenith=Color.Lerp(new Color(.014f,.032f,.093f),new Color(.112f,.379f,.708f),Daylight);
        zenith=Color.Lerp(zenith,new Color(.18f,.118f,.26f),dusk*.68f);
        Color horizon=Color.Lerp(new Color(.057f,.087f,.159f),new Color(.49f,.74f,.82f),Daylight);
        horizon=Color.Lerp(horizon,new Color(.92f,.423f,.20f),dusk*IslandTerrain.Smooth(-.16f,.16f,altitude)*.90f);
        zenith=Color.Lerp(zenith,Color.Lerp(new Color(.035f,.045f,.082f),new Color(.26f,.32f,.37f),Daylight),atmosphere.x*.77f);
        horizon=Color.Lerp(horizon,Color.Lerp(new Color(.062f,.082f,.12f),new Color(.57f,.64f,.62f),Daylight),atmosphere.x*.85f);
        Color direct=Color.Lerp(new Color(1f,.47f,.19f),new Color(1f,.946f,.826f),IslandTerrain.Smooth(-.03f,.53f,altitude));
        Color fog=Color.Lerp(horizon,Color.Lerp(new Color(.081f,.107f,.152f),new Color(.63f,.72f,.69f),Daylight),Mathf.Clamp01(atmosphere.w*31f));
        Vector3 primary=altitude>-.04f?direction:-direction;
        sunlight.transform.rotation=Quaternion.LookRotation(-primary);
        sunlight.color=Color.Lerp(new Color(.46f,.65f,1f),direct,IslandTerrain.Smooth(-.13f,.20f,altitude));
        sunlight.intensity=Mathf.Lerp(IslandConfig.MoonIntensity,1.58f,IslandTerrain.Smooth(-.06f,.65f,altitude))*(1-Mathf.Max(0,atmosphere.x-IslandConfig.WeatherProfiles[0].x)*.68f)+flash*3.5f;
        RenderSettings.ambientSkyColor=Color.Lerp(new Color(.20f,.29f,.47f),new Color(.71f,.80f,.87f),Daylight);
        RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.13f,.20f,.31f),new Color(.63f,.69f,.64f),Daylight);
        RenderSettings.ambientGroundColor=Color.Lerp(new Color(.10f,.12f,.20f),new Color(.45f,.39f,.30f),Daylight);
        Shader.SetGlobalVector("_IslandSun",direction);
        Shader.SetGlobalVector("_IslandMoon",-direction);
        Shader.SetGlobalVector("_IslandHorizon",horizon);
        Shader.SetGlobalVector("_IslandZenith",zenith);
        Shader.SetGlobalVector("_IslandCloudLight",direct);
        Shader.SetGlobalVector("_IslandFog",fog);
        Shader.SetGlobalVector("_IslandWind",new Vector4(IslandConfig.WindDirection.x,IslandConfig.WindDirection.y,atmosphere.z,IslandConfig.PlayerBend));
        Shader.SetGlobalVector("_IslandWater",new Vector4(activeWave,Mathf.Clamp01((atmosphere.z-.8f)*.32f),0,0));
        Shader.SetGlobalFloat("_IslandDay",Daylight);
        Shader.SetGlobalFloat("_IslandCloud",atmosphere.x);
        Shader.SetGlobalFloat("_IslandWet",atmosphere.y);
        Shader.SetGlobalFloat("_IslandRain",atmosphere.y);
        Shader.SetGlobalFloat("_IslandFlash",flash);
        Shader.SetGlobalFloat("_IslandFogDensity",atmosphere.w);
        Shader.SetGlobalFloat("_IslandUnderwater",submerged?1:0);
        Shader.SetGlobalFloat("_IslandWeatherTime",Clock);
    }

    void Lightning(Vector3 camera,Vector3 forward)
    {
        flashLeft=IslandConfig.FlashSeconds;
        Flashes++;
        // Put the local squall on the visible horizon so the flash has a readable origin.
        float bearing=Mathf.Atan2(forward.z,forward.x)+Range(-.52f,.52f);
        float distance=Range(342f,1372f);
        Vector3 strike=new Vector3(camera.x+Mathf.Cos(bearing)*distance,0,camera.z+Mathf.Sin(bearing)*distance);
        for (int i=0;i<spine.Length;i++)
        {
            float t=i/(spine.Length-1f);
            spine[i]=strike+new Vector3(Range(-10,10)*Mathf.Sin(t*3.14f)+(1-t)*23f,(1-t)*distance*.28f,Range(-10,10));
        }
        for (int i=0;i<branch.Length;i++)
        {
            float t=i/(branch.Length-1f);
            branch[i]=spine[5]+new Vector3(t*distance*.068f+Range(-8,8)*t,-t*distance*.074f,Range(-13,13)*t);
        }
        for (int i=0;i<branch2.Length;i++)
        {
            float t=i/(branch2.Length-1f);
            branch2[i]=spine[8]+new Vector3(-t*distance*.054f+Range(-8,8)*t,-t*distance*.046f,Range(-10,10)*t);
        }
        bolt.SetPositions(spine); fork.SetPositions(branch); twig.SetPositions(branch2);
        thunderLeft=Mathf.Clamp(distance/343f,IslandConfig.ThunderMin,IslandConfig.ThunderMax);
        flashDistance=distance;
        nextThunderGain=Mathf.Lerp(.88f,.37f,Mathf.InverseLerp(342f,1372f,flashDistance));
    }

    public void Dispose()
    {
        if(RenderSettings.skybox==sky) RenderSettings.skybox=null;
        Object.Destroy(sky); Object.Destroy(stars); Object.Destroy(boltMat);
    }
}

