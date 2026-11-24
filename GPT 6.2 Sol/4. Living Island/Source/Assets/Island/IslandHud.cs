using Unity.Profiling;
using UnityEngine;

public sealed class IslandHud
{
    static readonly string[] Presets={"Low","Medium","High"};
    static readonly string[] WeatherNames={"Clear","Cloudy","Rain","Fog","Storm"};
    readonly IslandGame game;
    readonly Texture2D[] icons=new Texture2D[WeatherNames.Length];
    readonly ProfilerRecorder draws;
    GUIStyle clockStyle, smallStyle, headingStyle, labelStyle, inputStyle, buttonStyle;
    string clock="12:00", seedText;
    string diagnostics, screenshotNotice;
    float sampleTime, noticeTime, nextClock;
    int frames, lastHour=-1,lastMinute=-1;

    public IslandHud(IslandGame game,int seed)
    {
        this.game=game;
        seedText=seed.ToString();
        for(int i=0;i<icons.Length;i++) icons[i]=Icon(i);
        draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count");
    }

    static Texture2D Icon(int state)
    {
        const int side=48;
        var texture=new Texture2D(side,side,TextureFormat.RGBA32,false) {
            name=WeatherNames[state]+" weather icon", filterMode=FilterMode.Bilinear, wrapMode=TextureWrapMode.Clamp
        };
        var pixels=new Color32[side*side];
        for(int y=0;y<side;y++)
        for(int x=0;x<side;x++)
        {
            Vector2 p=new Vector2((x+.5f)/side*2-1,(y+.5f)/side*2-1);
            float f=0;
            if(state==0)
            {
                f=Mathf.Max(0,1-Mathf.Abs(p.magnitude-.35f)*18);
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI/4;
                    Vector2 d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                    f=Mathf.Max(f,Line(p,d*.49f,d*.76f));
                }
            }
            else
            {
                float shift=state>=2?.18f:0f;
                f=Mathf.Max(Disk(p,new(-.43f,shift),.21f),Mathf.Max(Disk(p,new(-.13f,.14f+shift),.33f),Disk(p,new(.32f,.025f+shift),.26f)));
                f=Mathf.Max(f,Line(p,new(-.40f,shift-.17f),new(.34f,shift-.17f)));
                if(state==2)
                {
                    for(int i=0;i<3;i++) f=Mathf.Max(f,Line(p,new(-.33f+.28f*i,-.25f),new(-.44f+.28f*i,-.54f)));
                }
                if(state==3)
                {
                    f=Mathf.Max(f,Line(p,new(-.61f,-.28f),new(.55f,-.28f)));
                    f=Mathf.Max(f,Line(p,new(-.42f,-.52f),new(.68f,-.52f)));
                }
                if(state==4)
                {
                    f=Mathf.Max(f,Line(p,new(.18f,.02f),new(-.15f,-.34f)));
                    f=Mathf.Max(f,Line(p,new(-.15f,-.34f),new(.13f,-.34f)));
                    f=Mathf.Max(f,Line(p,new(.13f,-.34f),new(-.15f,-.74f)));
                }
            }
            pixels[x+y*side]=new Color32(255,247,216,(byte)(255*Mathf.Clamp01(f)));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false,true);
        return texture;
    }

    static float Disk(Vector2 p,Vector2 c,float radius)=>Mathf.Clamp01((radius-(p-c).magnitude)*29);
    static float Line(Vector2 p,Vector2 a,Vector2 b)
    {
        Vector2 d=b-a;
        return Mathf.Clamp01((.066f-(p-a-d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude)).magnitude)*24);
    }

    public void Advance(float dt)
    {
        noticeTime=Mathf.Max(0,noticeTime-dt);
        nextClock-=dt;
        if(nextClock<=0)
        {
            nextClock=.15f;
            int min=Mathf.FloorToInt(game.Hour*60f);
            int hh=min/60,mm=min%60;
            if(hh!=lastHour||mm!=lastMinute)
            {
                lastHour=hh;lastMinute=mm;
                clock=$"{hh:00}:{mm:00}";
            }
        }
        if(!game.DebugOpen) return;
        sampleTime+=dt;
        frames++;
        if(sampleTime<.28f) return;
        float fps=frames/sampleTime;
        diagnostics=$"{fps:F0} FPS   {1000f/fps:F1} ms";
        long drawCount=draws.Valid?draws.LastValue:0;
#if UNITY_EDITOR
        if(drawCount<=0) drawCount=UnityEditor.UnityStats.drawCalls;
#endif
        if(drawCount>0) diagnostics+=$"   {drawCount} draws";
        diagnostics+=$"\n{game.GrassVisible:N0} / {game.GrassPreset:N0} instanced blades\n{game.PhysicsBodies} physics bodies   {game.ActiveParticles:N0} particles";
        frames=0;sampleTime=0;
    }

    public void Notify(string filename)
    {
        screenshotNotice="Saved " + filename;
        noticeTime=3f;
    }

    void Styles()
    {
        if(clockStyle!=null) return;
        clockStyle=new GUIStyle(GUI.skin.label){fontSize=23,fontStyle=FontStyle.Bold};
        clockStyle.normal.textColor=new Color(.99f,.96f,.85f);
        smallStyle=new GUIStyle(GUI.skin.label){fontSize=13,richText=false,wordWrap=true};
        smallStyle.normal.textColor=new Color(.72f,.82f,.81f);
        headingStyle=new GUIStyle(clockStyle){fontSize=31};
        labelStyle=new GUIStyle(clockStyle){fontSize=15,fontStyle=FontStyle.Normal};
        inputStyle=new GUIStyle(GUI.skin.textField){fontSize=16,padding=new RectOffset(9,9,6,6)};
        buttonStyle=new GUIStyle(GUI.skin.button){fontSize=15,alignment=TextAnchor.MiddleCenter};
    }

    public void Draw(bool showGameplayUi)
    {
        if(!showGameplayUi && !game.Paused) return;
        Styles();
        float scale=Mathf.Clamp(Screen.height/900f,.78f,1.45f);
        Matrix4x4 original=GUI.matrix;
        GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(scale,scale,1));
        float w=Screen.width/scale,h=Screen.height/scale;
        if(showGameplayUi) DrawGameplayUi(w,h);
        if(game.Paused) Pause(w,h);
        GUI.matrix=original;
    }

    void DrawGameplayUi(float w,float h)
    {
        if(!game.Photo)
        {
            Rect pill=new Rect(28,24,121,44);
            Fill(pill,new Color(.022f,.075f,.084f,.78f));
            GUI.DrawTexture(new Rect(35,29,33,33),icons[(int)game.Weather]);
            GUI.Label(new Rect(71,29,75,34),clock,clockStyle);
            if(game.DebugOpen && diagnostics!=null)
            {
                Fill(new Rect(28,76,321,67),new Color(.017f,.054f,.068f,.82f));
                GUI.Label(new Rect(38,80,310,58),diagnostics,smallStyle);
            }
        }
        if(noticeTime>0 && !game.Photo)
        {
            Fill(new Rect(28,h-64,325,37),new Color(.022f,.075f,.084f,.86f));
            GUI.Label(new Rect(38,h-56,310,23),screenshotNotice,smallStyle);
        }

        if(!game.Paused && game.PanelOpen)
        {
            Rect panel=new Rect(w-356,28,326,game.Photo?361:402);
            Fill(panel,new Color(.027f,.082f,.087f,.94f));
            Fill(new Rect(panel.x,panel.y,2,panel.height),new Color(.52f,.81f,.68f));
            if(game.Photo) PhotoSettings(panel); else Settings(panel);
        }
    }

    static void Fill(Rect rect,Color color)
    {
        Color previous=GUI.color; GUI.color=color; GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=previous;
    }

    void Settings(Rect p)
    {
        float x=p.x+19,y=p.y+16;
        GUI.Label(new Rect(x,y,275,40),"Island settings",headingStyle);
        y+=53;
        GUI.Label(new Rect(x,y,278,24),"Quality",labelStyle);
        for(int i=0;i<Presets.Length;i++)
        {
            Color old=GUI.color;
            if(game.Quality==i) GUI.color=new Color(.61f,.94f,.78f);
            if(GUI.Button(new Rect(x+i*96,y+27,89,29),Presets[i],buttonStyle)) game.Quality=i;
            GUI.color=old;
        }
        y+=72;
        TimeSlider(x,ref y);
        game.DaySpeed=Slider(x,ref y,"Cycle speed",$"{game.DaySpeed:0.0}×",game.DaySpeed,0,8);
        bool stopped=GUI.Toggle(new Rect(x,y-4,278,23),game.TimeStopped,"Pause time  ·  T");
        if(stopped!=game.TimeStopped) game.TimeStopped=stopped;
        y+=29;
        bool automatic=GUI.Toggle(new Rect(x,y,278,23),game.AutomaticWeather,"Automatic weather  ·  0");
        if(automatic!=game.AutomaticWeather) game.AutomaticWeather=automatic;
        y+=31;
        GUI.Label(new Rect(x,y,110,25),"Island seed",labelStyle);
        seedText=GUI.TextField(new Rect(x,y+27,147,30),seedText,11,inputStyle);
        bool oldEnabled=GUI.enabled;
        GUI.enabled=int.TryParse(seedText,out int next)&&next!=game.Seed;
        if(GUI.Button(new Rect(x+155,y+27,129,30),"Regenerate",buttonStyle)) game.RequestSeed(next);
        GUI.enabled=oldEnabled;
    }

    void PhotoSettings(Rect p)
    {
        float x=p.x+19,y=p.y+16;
        GUI.Label(new Rect(x,y,285,42),"Photo settings",headingStyle);
        y+=55;
        game.CameraFov=Slider(x,ref y,"Field of view",$"{game.CameraFov:F0}°",game.CameraFov,IslandConfig.MinFov,IslandConfig.MaxFov);
        game.Focus=Slider(x,ref y,"Focus distance",$"{game.Focus:F1} m",game.Focus,.6f,115);
        game.Aperture=Slider(x,ref y,"Aperture",$"f/{game.Aperture:F1}",game.Aperture,1,16);
        game.Exposure=Slider(x,ref y,"Exposure",$"{game.Exposure:+0.0;-0.0;0.0} EV",game.Exposure,-2,2);
        TimeSlider(x,ref y);
        GUI.Label(new Rect(x,y-5,283,23),"Tab closes settings · F12 saves a 4K PNG",smallStyle);
    }

    void TimeSlider(float x,ref float y)
    {
        float next=Slider(x,ref y,"Time of day",clock,game.Hour,0,IslandConfig.HoursPerDay-.01f);
        if(!Mathf.Approximately(game.Hour,next)) game.Hour=next;
    }

    float Slider(float x,ref float y,string title,string value,float current,float min,float max)
    {
        GUI.Label(new Rect(x,y,181,22),title,labelStyle);
        GUI.Label(new Rect(x+197,y,91,22),value,labelStyle);
        float next=GUI.HorizontalSlider(new Rect(x+2,y+25,281,17),current,min,max);
        y+=47;
        return next;
    }

    void Pause(float w,float h)
    {
        Fill(new Rect(0,0,w,h),new Color(.012f,.033f,.051f,.52f));
        Rect p=new Rect((w-315)*.5f,(h-246)*.5f,315,246);
        Fill(p,new Color(.027f,.082f,.087f,.97f));
        GUI.Label(new Rect(p.x+22,p.y+17,271,43),"Paused",headingStyle);
        if(GUI.Button(new Rect(p.x+22,p.y+76,271,39),"Resume",buttonStyle)) game.SetPause(false);
        if(GUI.Button(new Rect(p.x+22,p.y+124,271,39),"Restart",buttonStyle)) game.Restart();
        if(GUI.Button(new Rect(p.x+22,p.y+172,271,39),"Quit",buttonStyle)) game.Quit();
    }

    public void Dispose()
    {
        draws.Dispose();
        foreach(Texture2D icon in icons) Object.Destroy(icon);
    }
}

