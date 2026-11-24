using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed partial class IslandGame
{
    static bool controlRestart;
    Keyboard verificationKeyboard;

    void StartControlCheck()
    {
        verificationKeyboard=InputSystem.AddDevice<Keyboard>();
        if(controlRestart)
        {
            controlRestart=false;
            StartCoroutine(CheckRestart());
            return;
        }
        StartCoroutine(CheckControls());
    }

    IEnumerator CheckControls()
    {
        yield return new WaitForSecondsRealtime(1f);
        yield return Tap(Key.F1);
        Require(DebugOpen,"F1 overlay");
        yield return Tap(Key.Tab);
        Require(PanelOpen && !Photo,"Tab settings");
        Quality=0;Require(GrassPreset==IslandConfig.GrassLow && Mathf.Approximately(pipe.renderScale,.73f),"live Low preset");
        Quality=1;Require(GrassPreset==IslandConfig.GrassMedium && Mathf.Approximately(pipe.shadowDistance,IslandConfig.MediumShadows),"live Medium preset");
        Quality=2;
        yield return Tap(Key.Tab);
        yield return Tap(Key.T);
        float held=Hour;
        yield return new WaitForSecondsRealtime(.25f);
        Require(TimeStopped && Mathf.Approximately(Hour,held),"T freezes the day clock");
        yield return Tap(Key.T);
        Require(!TimeStopped,"T resumes the day clock");
        Key[] hotkeys={Key.Digit1,Key.Digit2,Key.Digit3,Key.Digit4,Key.Digit5};
        for(int i=0;i<hotkeys.Length;i++)
        {
            yield return Tap(hotkeys[i]);
            Require((int)Weather==i,$"manual {WeatherNamesOrIndex(i)}");
        }
        yield return Tap(Key.Digit0);
        Require(AutomaticWeather,"0 toggles auto weather");
        yield return Tap(Key.Digit1);
        Require(!AutomaticWeather && Weather==IslandWeather.Clear,"manual weather exits auto");
        yield return Tap(Key.P);
        Vector3 body=player.Position,camera=player.Camera.transform.position;
        yield return Hold(Key.E,.36f);
        Require(Photo && player.Position==body && player.Camera.transform.position.y>camera.y+1f,"photo freezes the player and E flies upward");
        yield return Hold(Key.Q,.36f);
        Require(player.Camera.transform.position.y<camera.y+.8f,"Q flies down");
        yield return Tap(Key.Tab);
        Require(PanelOpen && Photo,"Tab shows photo controls");
        yield return Tap(Key.P);
        Require(!Photo && !PanelOpen,"P returns to the explorer");

        yield return CheckUiVisibility();

        string folder=Path.GetDirectoryName(ScreenshotPath(""));
        int before=Directory.GetFiles(folder,"Island-*.png").Length;
        yield return Tap(Key.F12);
        yield return new WaitForSecondsRealtime(.8f);
        Require(Directory.GetFiles(folder,"Island-*.png").Length==before+1,"F12 saves a timestamped window capture");

        yield return Tap(Key.Escape);
        Require(Paused && Time.timeScale==0,"Esc pauses");
        yield return Tap(Key.Escape);
        Require(!Paused && Time.timeScale==1,"Esc resumes");
        yield return Tap(Key.F8);
        RequestSeed(IslandConfig.AlternateSeed);
        yield return null;
        yield return null;
        Require(seed==IslandConfig.AlternateSeed && gameplayUiHidden,"seed regeneration preserves hidden UI");
        // A new scene has new log counters, so report pre-restart failures first.
        if(logErrors>0) { EndTest(); yield break; }
        controlRestart=true;
        Restart();
    }

    IEnumerator CheckRestart()
    {
        yield return new WaitForSecondsRealtime(.5f);
        Require(seed==IslandConfig.Seed && Quality==2 && Weather==IslandWeather.Clear && !Paused,"Restart returns to the default island");
        Require(gameplayUiHidden,"hidden UI survives a scene restart");
        yield return Tap(Key.F8);
        Require(!gameplayUiHidden,"F8 restores the UI after restart");
        Debug.Log("[Island controls] Keyboard shortcuts, live settings, photo flight, screenshot, pause and restart exercised.");
        EndTest();
    }

    IEnumerator CheckUiVisibility()
    {
        Require(!gameplayUiHidden,"gameplay UI is visible by default");
        var start=player.Position;
        float hour=Hour;
        var cursorLock=Cursor.lockState;
        yield return Tap(Key.F8);
        yield return Hold(Key.W,.35f);
        Require(gameplayUiHidden && Time.timeScale==1 && Cursor.lockState==cursorLock &&
            PlanarDistance(start,player.Position)>.5f && Hour>hour,"F8 hides UI without affecting walking, cursor lock or time");
        yield return Tap(Key.Digit2);
        Require(Weather==IslandWeather.Cloudy,"weather hotkey works with UI hidden");
        yield return Tap(Key.Digit1);
        yield return Tap(Key.F8);
        yield return Tap(Key.Tab);
        yield return CheckOptionalUiFrames("HUD, diagnostics, notice and settings",true);
        yield return Tap(Key.Tab);

        yield return Tap(Key.F8);
        yield return Tap(Key.P);
        Require(Photo && gameplayUiHidden,"P works with UI hidden");
        yield return Tap(Key.Tab);
        Require(PanelOpen,"Tab opens photo settings with UI hidden");
        yield return Tap(Key.F8);
        yield return CheckOptionalUiFrames("photo settings",false);
        yield return Tap(Key.P);

        yield return Tap(Key.F8);
        yield return Tap(Key.Escape);
        Require(Paused && gameplayUiHidden && Cursor.visible,"pause remains usable with gameplay UI hidden");
        yield return new WaitForEndOfFrame();
        Color32[] pause=ReadScreenPixels();
        suppressUi=true;
        yield return null;
        yield return new WaitForEndOfFrame();
        Color32[] world=ReadScreenPixels();
        suppressUi=false;
        Require(PixelChanges(world,pause)>world.Length/20,"pause menu is rendered while gameplay UI is hidden");
        yield return CheckCleanScreenshot(world);
        yield return Tap(Key.F8);
        Require(Paused && !gameplayUiHidden && Time.timeScale==0,"F8 works from the pause menu");
        yield return Tap(Key.Escape);
        Debug.Log("[Island controls] Verified rendered UI, clean F12 captures, pause and hidden gameplay hotkeys.");
    }

    IEnumerator CheckOptionalUiFrames(string layers,bool windowCapture)
    {
        // Keep the 3D frame steady so the test can compare actual Game view pixels.
        Time.timeScale=0f;
        try
        {
            if(windowCapture) hud.Notify("Visibility check");
            yield return null;
            yield return new WaitForEndOfFrame();
            Color32[] shown=ReadScreenPixels();
            yield return Tap(Key.F8);
            Require(PanelOpen && gameplayUiHidden && !Paused && Time.timeScale==0,"F8 preserves the open panel and game state");

            suppressUi=true;
            yield return null;
            yield return new WaitForEndOfFrame();
            Color32[] world=ReadScreenPixels();
            suppressUi=false;
            yield return null;
            yield return new WaitForEndOfFrame();
            Color32[] hidden=ReadScreenPixels();
            Require(PixelChanges(world,shown)>world.Length/100,"visible control frame contains "+layers);
            Require(PixelChanges(world,hidden)<=world.Length/1000,"hidden frame contains none of "+layers);

            if(windowCapture)
            {
                yield return CheckCleanScreenshot(world);
                yield return new WaitForEndOfFrame();
                Require(PixelChanges(world,ReadScreenPixels())<=world.Length/1000,"new screenshot notice stays hidden");
            }
            yield return Tap(Key.F8);
            yield return new WaitForEndOfFrame();
            Require(PixelChanges(shown,ReadScreenPixels())<=world.Length/1000,"F8 restores "+layers);
        }
        finally
        {
            suppressUi=false;
            Time.timeScale=1f;
        }
    }

    IEnumerator CheckCleanScreenshot(Color32[] world)
    {
        string folder=Path.GetDirectoryName(ScreenshotPath(""));
        string[] before=Directory.GetFiles(folder,"Island-*.png");
        yield return Tap(Key.F12);
        yield return new WaitForSecondsRealtime(.8f);
        string path=Array.Find(Directory.GetFiles(folder,"Island-*.png"), file=>Array.IndexOf(before,file)<0);
        Require(path!=null,"F12 saves a PNG with the UI hidden");
        if(path==null) yield break;

        var screenshot=new Texture2D(2,2);
        try
        {
            Require(screenshot.LoadImage(File.ReadAllBytes(path)),"hidden F12 PNG decodes");
            int changed=PixelChanges(world,screenshot.GetPixels32());
            Require(changed<=world.Length/1000,$"hidden F12 PNG matches the world-only frame ({changed} changed pixels)");
        }
        finally { Destroy(screenshot); }
    }

    Color32[] ReadScreenPixels()
    {
        Texture2D frame=ScreenCapture.CaptureScreenshotAsTexture();
        Color32[] pixels=frame.GetPixels32();
        Destroy(frame);
        return pixels;
    }

    static int PixelChanges(Color32[] a,Color32[] b)
    {
        if(a.Length!=b.Length) return int.MaxValue;
        int changed=0;
        for(int i=0;i<a.Length;i++)
            if(Math.Abs(a[i].r-b[i].r)>24 || Math.Abs(a[i].g-b[i].g)>24 || Math.Abs(a[i].b-b[i].b)>24) changed++;
        return changed;
    }

    static IslandWeather WeatherNamesOrIndex(int index) => (IslandWeather)index;

    IEnumerator Tap(Key key) => Hold(key,.09f);

    IEnumerator Hold(Key key,float duration)
    {
        InputSystem.QueueStateEvent(verificationKeyboard,new KeyboardState(key));
        yield return new WaitForSecondsRealtime(duration);
        InputSystem.QueueStateEvent(verificationKeyboard,new KeyboardState());
        yield return new WaitForSecondsRealtime(.09f);
    }

    static void Require(bool condition,string name)
    {
        if(!condition) Debug.LogError("[Island controls] Failed " + name);
    }
}

