using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public sealed partial class IslandGame : MonoBehaviour
{
    IslandTerrain ground;
    IslandPlayer player;
    IslandOcean ocean;
    IslandSkyWeather sky;
    IslandEffects effects;
    IslandLandmarks landmarks;
    IslandVegetation vegetation;
    IslandModels models;
    IslandAudio audio;
    IslandPost post;
    IslandHud hud;
    Transform terrainRoot;
    Material terrainMaterial;
    MeshCollider terrainCollider;
    UniversalRenderPipelineAsset pipe;
    RenderPipelineAsset previousPipeline;
    int quality=2, seed=IslandConfig.Seed;
    int pendingSeed;
    bool seedRequested, captureRequested, submerged, headless;
    static bool gameplayUiHidden;
    string test;

    // The recording preference belongs to the play session, so scene reloads keep it.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetGameplayUiVisibility() => gameplayUiHidden=false;

    public bool Photo { get; private set; }
    public bool PanelOpen { get; private set; }
    public bool Paused { get; private set; }
    public bool DebugOpen { get; private set; }
    public int Quality { get=>quality; set=>SetQuality(value); }
    public int Seed=>seed;
    public float Hour { get=>sky.Hour; set { sky.Hour=value; sky.TimeStopped=true; } }
    public bool TimeStopped { get=>sky.TimeStopped; set=>sky.TimeStopped=value; }
    public float DaySpeed { get=>sky.Speed; set=>sky.Speed=value; }
    public IslandWeather Weather =>sky.Weather;
    public bool AutomaticWeather { get=>sky.Auto; set=>sky.SetAuto(value); }
    public float CameraFov { get=>player.Camera.fieldOfView; set=>player.Camera.fieldOfView=value; }
    public float Focus { get=>post.Focus;set=>post.Focus=value; }
    public float Aperture { get=>post.Aperture;set=>post.Aperture=value; }
    public float Exposure { get=>post.Exposure;set=>post.Exposure=value; }
    public int GrassVisible=>vegetation.GrassVisible;
    public int GrassPreset=>quality==2?IslandConfig.GrassHigh:quality==1?IslandConfig.GrassMedium:IslandConfig.GrassLow;
    public int PhysicsBodies=>(terrainCollider.enabled?1:0)+(player.Controller.enabled?1:0)+landmarks.PhysicsBodies;
    public int ActiveParticles=>effects.Active;

    void Awake()
    {
        QualitySettings.vSyncCount=1;
        Application.runInBackground=true;
        headless=SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null;
#if UNITY_EDITOR
        // The Game view has its own presentation settings, separate from the player quality profile.
        if (!headless) Application.targetFrameRate=Mathf.Max(1,Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value));
#endif
        previousPipeline=QualitySettings.renderPipeline;
        pipe=Instantiate((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline);
        pipe.name="Living Island live URP settings";
        pipe.shadowCascadeCount=IslandConfig.ShadowCascades;
        QualitySettings.renderPipeline=pipe;
        terrainMaterial=IslandGeometry.Custom("Terrain");
        Shader.SetGlobalVector("_IslandBeach",new Vector4(IslandConfig.BeachHeight,IslandConfig.BeachSlope,0,0));
        models=IslandModels.Load();
        ground=new IslandTerrain(seed);
        terrainRoot=IslandGeometry.Render("Island terrain",transform,ground.Mesh,terrainMaterial).transform;
        terrainCollider=terrainRoot.gameObject.AddComponent<MeshCollider>();
        terrainCollider.sharedMesh=ground.Mesh;
        Physics.SyncTransforms();
        player=new IslandPlayer(transform,ground.Spawn);
        ocean=new IslandOcean(transform);
        sky=new IslandSkyWeather(transform,seed);
        landmarks=new IslandLandmarks(transform,ground,models);
        vegetation=new IslandVegetation(transform,ground,models,seed);
        effects=new IslandEffects(transform,ground,landmarks.FirePosition);
        audio=new IslandAudio(transform,landmarks.FirePosition);
        post=new IslandPost(transform);
        if(!headless) hud=new IslandHud(this,seed);
        SetQuality(quality);
        ReadTestOptions();
        SetCursor(string.IsNullOrEmpty(test));
        Debug.Log($"[Island] Seed {seed}, {ground.Mesh.vertexCount} terrain samples, grass {vegetation.GrassPlaced}, palms {vegetation.PalmsPlaced}, rocks {vegetation.RocksPlaced}, bushes {vegetation.BushesPlaced}, flowers {vegetation.FlowersPlaced}. Spawn {ground.Spawn} boat {ground.Boat} camp {ground.Camp} beacon {ground.Lighthouse}");
        PublishEnvironment(0);
    }

    void SetQuality(int value)
    {
        quality=value;
        pipe.shadowDistance=quality==2?IslandConfig.HighShadows:quality==1?IslandConfig.MediumShadows:IslandConfig.LowShadows;
        pipe.renderScale=quality==2?1f:quality==1?.87f:.73f;
        Shader.SetGlobalFloat("_IslandWaterQuality",quality==2?1f:quality==1?.54f:.16f);
    }

    public void RequestSeed(int value) { seedRequested=true; pendingSeed=value; }

    void Regenerate(int next)
    {
        if(next==seed) return;
        float start=Time.realtimeSinceStartup;
        var old=ground;
        ground=new IslandTerrain(next);
        terrainRoot.GetComponent<MeshFilter>().sharedMesh=ground.Mesh;
        terrainCollider.sharedMesh=ground.Mesh;
        Physics.SyncTransforms();
        vegetation.Dispose();
        landmarks.Dispose();
        vegetation=new IslandVegetation(transform,ground,models,next);
        landmarks=new IslandLandmarks(transform,ground,models);
        effects.SetGround(ground,landmarks.FirePosition);
        audio.SetCamp(landmarks.FirePosition);
        sky.Reseed(next);
        seed=next;
        Destroy(old.Mesh);
        player.Teleport(ground.Spawn+Vector3.up*.045f,IslandConfig.StartHeading);
        Debug.Log($"[Island] Regenerated seed {seed} in {Time.realtimeSinceStartup-start:F3} s. Anchor error {vegetation.MaxPlantAnchorError:F3}m; lowest tree/rock {vegetation.LowestTreeOrRock:F3}m.");
    }

    void Update()
    {
        var kb=verificationKeyboard??Keyboard.current;
        var mouse=Mouse.current;
        if(string.IsNullOrEmpty(test)||test=="Controls") HandleKeys(kb);
        if(seedRequested) { seedRequested=false; Regenerate(pendingSeed); }
        float dt=Time.deltaTime;
        hud?.Advance(Time.unscaledDeltaTime);
        if(Paused) return;
        Vector2 input=Move(kb), look=mouse?.delta.ReadValue()??Vector2.zero;
        bool sprint=(kb?.leftShiftKey.isPressed??false)||(kb?.rightShiftKey.isPressed??false);
        bool jump=kb?.spaceKey.wasPressedThisFrame??false;

        if(!string.IsNullOrEmpty(test)&&test!="Controls") AdvanceTest(dt,ref input,ref look,ref sprint,ref jump);
        if(Photo)
        {
            if(!PanelOpen && (string.IsNullOrEmpty(test)||test=="Controls"))
            {
                float lift=(kb?.eKey.isPressed==true?1:0)-(kb?.qKey.isPressed==true?1:0);
                player.Fly(dt,input,look,lift,sprint);
            }
        }
        else
        {
            if(!PanelOpen) player.Step(dt,input,look,sprint,jump,ground,IslandOcean.Elevation(player.Position,sky.Clock,sky.Wave));
            player.Follow(dt);
        }
        PublishEnvironment(dt);
    }

    void HandleKeys(Keyboard keys)
    {
        if(keys==null) return;
        if(keys.escapeKey.wasPressedThisFrame) SetPause(!Paused);
        if(keys.f1Key.wasPressedThisFrame) DebugOpen=!DebugOpen;
        if(keys.f8Key.wasPressedThisFrame)
        {
            gameplayUiHidden=!gameplayUiHidden;
            RefreshCursorVisibility();
        }
        if(keys.f12Key.wasPressedThisFrame) captureRequested=true;
        if(Paused) return;
        if(keys.pKey.wasPressedThisFrame) SetPhoto(!Photo);
        if(keys.tabKey.wasPressedThisFrame)
        {
            PanelOpen=!PanelOpen;
            SetCursor(!PanelOpen);
        }
        if(PanelOpen) return;
        if(keys.tKey.wasPressedThisFrame) sky.TimeStopped=!sky.TimeStopped;
        if(keys.digit0Key.wasPressedThisFrame) sky.SetAuto(!sky.Auto);
        if(keys.digit1Key.wasPressedThisFrame) ChangeWeather(IslandWeather.Clear);
        if(keys.digit2Key.wasPressedThisFrame) ChangeWeather(IslandWeather.Cloudy);
        if(keys.digit3Key.wasPressedThisFrame) ChangeWeather(IslandWeather.Rain);
        if(keys.digit4Key.wasPressedThisFrame) ChangeWeather(IslandWeather.Fog);
        if(keys.digit5Key.wasPressedThisFrame) ChangeWeather(IslandWeather.Storm);
    }

    void ChangeWeather(IslandWeather value)
    {
        sky.SetAuto(false);
        sky.SetWeather(value);
    }

    void SetPhoto(bool value)
    {
        Photo=value;post.Photo=value;PanelOpen=false;
        if(Photo) player.BeginPhoto();
        else { player.Camera.fieldOfView=IslandConfig.DefaultFov; player.Follow(0,true); }
        if(string.IsNullOrEmpty(test)) SetCursor(true);
    }

    public void SetPause(bool value)
    {
        Paused=value;Time.timeScale=value?0:1;
        audio.Pause(value);
        SetCursor(!value&&!PanelOpen);
    }

    public void Restart()
    {
        Time.timeScale=1;
        // Reload the scene before bootstrapping so runtime generations and settings are fully reset.
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Quit()
    {
        Time.timeScale=1;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit();
#endif
    }

    void PublishEnvironment(float dt)
    {
        Vector3 p=player.Position, camera=player.Camera.transform.position;
        Shader.SetGlobalVector("_IslandPlayer",new Vector4(p.x,p.y,p.z,player.Swimming?1:0));
        ocean.Follow(camera);
        submerged=camera.y<IslandOcean.Elevation(camera,sky.Clock,sky.Wave)-.02f;
        sky.Advance(dt,camera,submerged,player.Camera.transform.forward);
        landmarks.Advance(sky);
        post.Update(sky.Daylight);
        if(dt>0)
        {
            effects.Advance(dt,player,sky);
            audio.Advance(dt,player,ground,sky,submerged);
        }
    }

    void LateUpdate()
    {
        if(headless) return;
        vegetation.Draw(player.Camera,quality);
        if(captureRequested) { captureRequested=false; Capture(); }
    }

    void OnGUI()
    {
        if(!suppressUi) hud?.Draw(!gameplayUiHidden);
    }

    static Vector2 Move(Keyboard keys)=>keys==null?Vector2.zero:
        new Vector2((keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0),(keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0));

    void SetCursor(bool locked)
    {
        Cursor.lockState=locked?CursorLockMode.Locked:CursorLockMode.None;
        RefreshCursorVisibility();
    }

    void RefreshCursorVisibility() =>
        Cursor.visible=Cursor.lockState!=CursorLockMode.Locked && (Paused || !gameplayUiHidden);

    void OnDestroy()
    {
        Application.logMessageReceived-=CountTestLog;
        RenderPipelineManager.endCameraRendering-=CountRender;
        if(verificationKeyboard!=null) InputSystem.RemoveDevice(verificationKeyboard);
        Cursor.lockState=CursorLockMode.None;
        Cursor.visible=true;
        Time.timeScale=1;
        Time.captureDeltaTime=0f;
        if(QualitySettings.renderPipeline==pipe) QualitySettings.renderPipeline=previousPipeline;
        if(pipe) Destroy(pipe);
        if(ground!=null) Destroy(ground.Mesh);
        if(terrainMaterial) Destroy(terrainMaterial);
        ocean?.Dispose();sky?.Dispose();vegetation?.Dispose();landmarks?.Dispose();effects?.Dispose();
        player?.Dispose();audio?.Dispose();post?.Dispose();hud?.Dispose();
    }
}

