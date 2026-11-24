using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed partial class IslandGame
{
    bool suppressUi;

    static string ScreenshotPath(string label)
    {
        string project=Path.GetDirectoryName(Application.dataPath);
        string[] args=Environment.GetCommandLineArgs();
        int flag=Array.IndexOf(args,"-islandProject");
        if(flag>=0 && flag+1<args.Length) project=args[flag+1];
        string folder=Path.Combine(project,"Screenshots");
        Directory.CreateDirectory(folder);
        string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        return Path.Combine(folder,$"Island-{stamp}{label}.png");
    }

    void Capture()
    {
        if(Photo) RenderPng(IslandConfig.CaptureWidth,IslandConfig.CaptureHeight,"-4K");
        // Keep the live pause menu usable while exporting a world-only paused frame.
        else if(gameplayUiHidden && Paused) RenderPng(Screen.width,Screen.height,"");
        else
        {
            string path=ScreenshotPath("");
            ScreenCapture.CaptureScreenshot(path);
            StartCoroutine(ShowCaptureNotice(Path.GetFileName(path)));
        }
    }

    System.Collections.IEnumerator ShowCaptureNotice(string name)
    {
        yield return null;
        hud?.Notify(name);
    }

    string RenderPng(int width,int height,string label)
    {
        string path=ScreenshotPath(label);
        var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,1);
        var previous=RenderTexture.active;
        var camera=player.Camera;
        Texture2D pixels=null;
        try
        {
            suppressUi=true;
            camera.aspect=width/(float)height;
            vegetation.Draw(camera,quality);
            var request=new UniversalRenderPipeline.SingleCameraRequest { destination=target };
            RenderPipeline.SubmitRenderRequest(camera,request);
            RenderTexture.active=target;
            pixels=new Texture2D(width,height,TextureFormat.RGB24,false,false);
            pixels.ReadPixels(new Rect(0,0,width,height),0,0,false);
            File.WriteAllBytes(path,ImageConversion.EncodeToPNG(pixels));
            Debug.Log($"[Island] Rendered {width} × {height} PNG {path}");
        }
        finally
        {
            camera.ResetAspect();
            suppressUi=false;
            RenderTexture.active=previous;
            if(pixels) Destroy(pixels);
            RenderTexture.ReleaseTemporary(target);
        }
        return path;
    }
}

