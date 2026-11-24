using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace LivingIsland
{
    /// <summary>
    /// F12 screenshots into &lt;project&gt;/Screenshots with a timestamped file name. In photo mode the
    /// camera is rendered into a 3840x2160 RenderTexture (not a copy of the window) and encoded to PNG.
    /// </summary>
    public static class ScreenshotService
    {
        public static string LastPath { get; private set; } = "";

        public static string Folder
        {
            get
            {
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        static string NewPath(string tag)
        {
            string name = "LivingIsland_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + (string.IsNullOrEmpty(tag) ? "" : "_" + tag) + ".png";
            return Path.Combine(Folder, name);
        }

        /// <summary>Window screenshot (written by Unity at the end of the frame).</summary>
        public static string CaptureWindow(string tag = null)
        {
            string path = NewPath(tag);
            ScreenCapture.CaptureScreenshot(path);
            LastPath = path;
            Debug.Log("[LI] Screenshot saved: " + path);
            return path;
        }

        /// <summary>Renders the camera off-screen at the given size and saves a PNG.</summary>
        public static string CaptureCamera(Camera cam, int width, int height, string tag = null)
        {
            string path = NewPath(tag);
            var desc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 };
            var rt = RenderTexture.GetTemporary(desc);
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request))
            {
                RenderPipeline.SubmitRenderRequest(cam, request);
            }
            else
            {
                var prev = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = prev;
            }
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            tex.Apply(false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.Destroy(tex);
            File.WriteAllBytes(path, png);
            LastPath = path;
            Debug.Log("[LI] High-resolution capture " + width + "x" + height + " saved: " + path);
            return path;
        }
    }
}
