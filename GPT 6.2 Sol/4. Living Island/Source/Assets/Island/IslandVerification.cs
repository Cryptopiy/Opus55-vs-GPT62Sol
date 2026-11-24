using System;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class IslandGame
{
    float testTime, stageTime, sampleSeconds, longestFrame, highestJump;
    int stage, frameSamples, logWarnings, logErrors, frameSpikes;
    int renderedFrames;
    Vector3 testPoint, swimStart;
    bool wasSwimming, heardThunder, photographedLightning;
    int checkedFlashes;
    float walkDistance, runDistance;

    void ReadTestOptions()
    {
        string[] args=Environment.GetCommandLineArgs();
        int flag=Array.IndexOf(args,"-islandTest");
        if(flag<0 || flag+1>=args.Length) return;
        test=args[flag+1];
        Application.logMessageReceived+=CountTestLog;
        if(!headless) RenderPipelineManager.endCameraRendering+=CountRender;
        if(test=="Controls") StartControlCheck();
        if(test=="Core") Time.captureDeltaTime=1f/60f;
        if(test=="Day" || test=="Storm" || test=="Soak")
        {
            DebugOpen=true;
            Hour=test=="Storm"?1.45f:IslandConfig.StartHour;
            sky.SetWeather(test=="Storm"?IslandWeather.Storm:IslandWeather.Clear,true);
        }
    }

    void CountTestLog(string message,string stack,LogType type)
    {
        if(type==LogType.Warning) logWarnings++;
        else if(type!=LogType.Log) logErrors++;
    }

    void CountRender(ScriptableRenderContext context,Camera camera)
    {
        if(camera==player.Camera) renderedFrames++;
    }

    void AdvanceTest(float dt,ref Vector2 input,ref Vector2 look,ref bool sprint,ref bool jump)
    {
        testTime+=test=="Core"?dt:Time.unscaledDeltaTime;
        stageTime+=test=="Core"?dt:Time.unscaledDeltaTime;
        input=look=Vector2.zero;
        sprint=jump=false;
        if(test=="Core") CoreCheck(ref input,ref sprint,ref jump);
        else if(test=="Day" || test=="Storm" || test=="Soak") Benchmark(ref input,ref look);
        else if(test=="Acceptance") Acceptance(ref input); else if(test=="Preview") Preview();
    }

    void Preview()
    {
        Hour=IslandConfig.StartHour;
        if(stage==0 && testTime>3.2f)
        {
            DebugOpen=true;
            captureRequested=true;
            Stage(1);
        }
        if(stage==1 && stageTime>1.6f)
        {
            RenderPng(IslandConfig.NormalWidth,IslandConfig.NormalHeight,"-Preview");
            Debug.Log($"[Island] Preview grass {vegetation.GrassVisible}; viewport {Screen.width}x{Screen.height}.");
            Stage(2);
        }
        if(stage==2 && stageTime>.43f) EndTest();
    }

    void CoreCheck(ref Vector2 input,ref bool sprint,ref bool jump)
    {
        if(stage==0)
        {
            float heading=91f;
            Vector3 flat=ground.Camp;
            player.Teleport(flat+Vector3.up*.045f,heading);
            testPoint=player.Position;
            Stage(1);
        }
        else if(stage==1)
        {
            input=Vector2.up;
            if(stageTime<2f) return;
            Vector3 p=player.Position;
            walkDistance=PlanarDistance(p,testPoint);
            testPoint=p;
            Stage(2);
        }
        else if(stage==2)
        {
            input=Vector2.up;sprint=true;
            if(stageTime<1.3f) return;
            runDistance=PlanarDistance(player.Position,testPoint);
            testPoint=player.Position;
            Stage(3);
        }
        else
        {
            if(stageTime<.04f) jump=true;
            highestJump=Mathf.Max(highestJump,player.Position.y-testPoint.y);
            if(stageTime<1.2f) return;
            Debug.Log($"[Island] Core move: walk {walkDistance:F2}m in 2s, run {runDistance:F2}m in 1.3s, jump height {highestJump:F2}m.");
            float cliff=MinimumCliffProfileSlope();
            Debug.Log($"[Island] Terrain: peak {ground.Mesh.bounds.max.y:F2}m, floor {ground.Height(240,240):F2}m, beach slope {Mathf.Atan(ground.Slope(ground.Spawn.x,ground.Spawn.z))*Mathf.Rad2Deg:F2}°, minimum cliff face slope across {IslandConfig.CliffLength}m {cliff:F2}° (passes {cliff>IslandConfig.CliffSlope}). Objects anchor error {vegetation.MaxPlantAnchorError:F3}m.");
            float originalHour=7f;
            sky.Hour=originalHour;sky.TimeStopped=false;
            sky.Advance(IslandConfig.DaySeconds,player.Camera.transform.position,false);
            Debug.Log($"[Island] {IslandConfig.DaySeconds}s day cycle returned to {sky.Hour:F2}h from {originalHour:F2}h.");
            sky.SetAuto(true);
            sky.Advance(IslandConfig.AutoMax,player.Camera.transform.position,false);
            Debug.Log($"[Island] Auto weather changed Clear to {sky.Weather} within {IslandConfig.AutoMax} seconds.");
            sky.SetAuto(false);
            sky.SetWeather(IslandWeather.Storm);
            sky.Advance(IslandConfig.WeatherBlend,player.Camera.transform.position,false);
            Debug.Log($"[Island] Storm transition resolved to rain {sky.Rain:F2}, wind {sky.Wind:F2}, waves {sky.Wave:F2}.");
            SetQuality(0);Debug.Log($"[Island] Low: {GrassPreset} blades, scale {pipe.renderScale}, shadows {pipe.shadowDistance}m.");
            SetQuality(1);Debug.Log($"[Island] Medium: {GrassPreset} blades, scale {pipe.renderScale}, shadows {pipe.shadowDistance}m.");
            SetQuality(2);Debug.Log($"[Island] High: {GrassPreset} blades, scale {pipe.renderScale}, shadows {pipe.shadowDistance}m.");
            Regenerate(IslandConfig.AlternateSeed);
            EndTest();
        }
    }

    float MinimumCliffProfileSlope()
    {
        float minimum=float.PositiveInfinity;
        for(float along=-IslandConfig.CliffLength*.5f;along<=IslandConfig.CliffLength*.5f;along+=.5f)
        {
            float face=0;
            for(float inland=1.5f;inland<8f;inland+=.25f)
            {
                Vector2 p=ground.CliffProfilePoint(along,inland);
                face=Mathf.Max(face,ground.Slope(p.x,p.y));
            }
            minimum=Mathf.Min(minimum,face);
        }
        return Mathf.Atan(minimum)*Mathf.Rad2Deg;
    }

    void Benchmark(ref Vector2 input,ref Vector2 look)
    {
        bool soak=test=="Soak";
        if(soak)
        {
            if(testTime<65f)
            {
                look=new Vector2(Time.unscaledDeltaTime*19f,0);
                input=testTime%12f<4f?Vector2.up:Vector2.zero;
            }
            if(stage==0 && testTime>5f && sky.Weather==IslandWeather.Clear) ChangeWeather(IslandWeather.Cloudy);
            if(stage==0 && testTime>18f) { ChangeWeather(IslandWeather.Rain);Stage(1); }
            if(stage==1 && testTime>36f) { ChangeWeather(IslandWeather.Storm);Hour=1.45f;Stage(2); }
            if(stage==2 && testTime>58f) { ChangeWeather(IslandWeather.Fog);Stage(3); }
            if(stage==3 && testTime>76f) { ChangeWeather(IslandWeather.Clear);sky.TimeStopped=false;Stage(4); }
        }
        else if(testTime>IslandConfig.RenderWarmup)
        {
            // A steady camera pan includes both shoreline optics and vegetation in the sample.
            look=new Vector2(Mathf.Sin(testTime*.35f)*Time.unscaledDeltaTime*140f,0);
        }
        if(testTime>=IslandConfig.RenderWarmup)
        {
            frameSamples++;
            float interval=Time.unscaledDeltaTime;
            sampleSeconds+=interval;
            longestFrame=Mathf.Max(longestFrame,interval);
            if(interval>.050f) frameSpikes++;
        }
        if(testTime<(soak?IslandConfig.SoakSeconds:IslandConfig.BenchmarkSeconds)) return;
        float fps=frameSamples/sampleSeconds;
        Debug.Log($"[Island benchmark] {test}: {Screen.width}x{Screen.height}; High; VSync {QualitySettings.vSyncCount}; {fps:F2} FPS; meets {IslandConfig.MinimumFPS:F0} FPS: {fps>=IslandConfig.MinimumFPS}; longest {longestFrame*1000f:F2} ms; >50 ms {frameSpikes}; {sampleSeconds:F1} s sampled; {testTime:F1} s played; camera renders {renderedFrames}; grass {vegetation.GrassVisible}/{vegetation.GrassPlaced}; rain {effects.RainActive}; flashes {sky.Flashes}.");
        EndTest();
    }

    void Acceptance(ref Vector2 input)
    {
        switch(stage)
        {
            case 0:
                Hour=IslandConfig.StartHour;
                DebugOpen=true;
                if(testTime<2.7f) return;
                RenderPng(IslandConfig.NormalWidth,IslandConfig.NormalHeight,"-Beach");
                player.Teleport(ground.Spawn+Vector3.up*.035f,SwimHeading());
                Debug.Log($"[Island acceptance] Noon, Clear. Walking to the water from {player.Position}, yaw {player.Yaw:F1}°.");
                Stage(1);
                break;
            case 1:
                input=Vector2.up;
                if(player.Swimming && !wasSwimming) { wasSwimming=true; swimStart=player.Position; }
                float distance=wasSwimming?PlanarDistance(player.Position,swimStart):0f;
                if(distance<IslandConfig.SwimAcceptanceDistance && stageTime<36f) return;
                Debug.Log($"[Island acceptance] Swimming {distance:F2}m; entered swim state: {wasSwimming}; surface at player {IslandOcean.Elevation(player.Position,sky.Clock,sky.Wave):F2}m.");
                SetPhoto(true);
                Vector3 p=player.Position+player.Camera.transform.forward*2f;
                p.y=-1.46f;
                player.Camera.transform.SetPositionAndRotation(p,Quaternion.Euler(16f,player.Yaw+144f,0));
                Stage(2);
                break;
            case 2:
                if(stageTime<.65f) return;
                Debug.Log($"[Island acceptance] Camera underwater: {submerged}; camera y {player.Camera.transform.position.y:F2}m.");
                RenderPng(IslandConfig.NormalWidth,IslandConfig.NormalHeight,"-Underwater");
                SetPhoto(false);
                Vector3 night=ground.Camp+new Vector3(-3.8f,0,-2.75f);
                night.y=ground.Height(night.x,night.z)+.045f;
                player.Teleport(night,IslandConfig.StartHeading);
                Hour=18.03f;
                Stage(3);
                break;
            case 3:
                if(stageTime<1.65f) return;
                RenderPng(IslandConfig.NormalWidth,IslandConfig.NormalHeight,"-Dusk");
                Hour=23.65f;
                Stage(4);
                break;
            case 4:
                if(stageTime<2.8f) return;
                RenderPng(IslandConfig.NormalWidth,IslandConfig.NormalHeight,"-Night");
                Debug.Log($"[Island acceptance] Night: daylight {sky.Daylight:F3}; fireflies {effects.BugsActive}; all particles {effects.Active}.");
                ChangeWeather(IslandWeather.Rain);
                Stage(6);
                break;
            case 6:
                if(stageTime<IslandConfig.WeatherBlend+1.15f) return;
                Debug.Log($"[Island acceptance] Rain: {effects.RainActive} streaks; wetness {sky.Rain:F2}.");
                ChangeWeather(IslandWeather.Storm);
                checkedFlashes=sky.Flashes;
                Stage(7);
                break;
            case 7:
                if(!photographedLightning && sky.Flashes>checkedFlashes && sky.FlashActive)
                {
                    RenderPng(IslandConfig.NormalWidth,IslandConfig.NormalHeight,"-Lightning");
                    photographedLightning=true;
                }
                if(sky.Thunder>0) heardThunder=true;
                if((sky.Flashes<=checkedFlashes || !heardThunder || stageTime<IslandConfig.WeatherBlend+.9f) && stageTime<20f) return;
                Debug.Log($"[Island acceptance] Storm: lightning {sky.Flashes-checkedFlashes} and thunder cue {heardThunder}; wind {sky.Wind:F2}, waves {sky.Wave:F2}.");
                RenderPng(IslandConfig.NormalWidth,IslandConfig.NormalHeight,"-Storm");
                ChangeWeather(IslandWeather.Fog);
                Stage(8);
                break;
            case 8:
                if(stageTime<IslandConfig.WeatherBlend+.5f) return;
                Debug.Log($"[Island acceptance] Fog extinction {sky.Density:F5}; calculated sea-level visibility {3.912f/sky.Density:F2}m.");
                SetPhoto(true);
                CameraFov=51f; Focus=12.5f; Aperture=2.3f; Exposure=.03f;
                Hour=17.05f;
                sky.SetWeather(IslandWeather.Clear,true);
                player.Teleport(ground.Spawn+Vector3.up*.06f,IslandConfig.StartHeading);
                player.Camera.transform.SetPositionAndRotation(ground.Spawn+new Vector3(2.2f,2.1f,1.1f),Quaternion.Euler(6.9f,86f,0));
                Stage(9);
                break;
            case 9:
                if(stageTime<1.0f) return;
                RenderPng(IslandConfig.CaptureWidth,IslandConfig.CaptureHeight,"-4K-Photo");
                SetPhoto(false);
                RequestSeed(IslandConfig.AlternateSeed);
                Stage(10);
                break;
            case 10:
                if(stageTime<1.4f) return;
                Debug.Log($"[Island acceptance] Seed {seed}; {vegetation.PalmsPlaced} trees and {vegetation.RocksPlaced} rocks; max anchor error {vegetation.MaxPlantAnchorError:F3}m, lowest {vegetation.LowestTreeOrRock:F3}m.");
                RenderPng(IslandConfig.NormalWidth,IslandConfig.NormalHeight,"-Seed778");
                EndTest();
                break;
        }
    }

    float SwimHeading()
    {
        Vector3 start=player.Position;
        float best=180f,distance=100f;
        for(int heading=0;heading<360;heading+=5)
        {
            var direction=Quaternion.Euler(0,heading,0)*Vector3.forward;
            for(int d=2;d<44 && d<distance;d++)
            {
                Vector3 p=start+direction*d,far=start+direction*(d+32);
                if(ground.Height(p.x,p.z)>-IslandConfig.SwimDepth || ground.Height(far.x,far.z)>-IslandConfig.SwimDepth) continue;
                distance=d;best=heading;break;
            }
        }
        return best;
    }

    static float PlanarDistance(Vector3 a,Vector3 b)=>new Vector2(a.x-b.x,a.z-b.z).magnitude;
    void Stage(int next) { stage=next;stageTime=0; }

    void EndTest()
    {
        if(!headless && renderedFrames==0) Debug.LogError("[Island] The test never rendered the player camera.");
        Debug.Log($"[Island] {test} completed. Game log warnings {logWarnings}; errors {logErrors}.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(logErrors>0?1:0);
#else
        Application.Quit(logErrors>0?1:0);
#endif
    }
}

