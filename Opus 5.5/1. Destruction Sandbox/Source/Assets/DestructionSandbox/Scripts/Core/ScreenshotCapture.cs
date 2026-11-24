using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace DestructionSandbox
{
    /// <summary>
    /// F12: saves a PNG to &lt;project&gt;/Screenshots/ with a timestamp in the file name. It captures what is drawn,
    /// so it follows the F8 UI state (U-05); with the UI hidden even an open menu is left out of the shot.
    /// </summary>
    public static class ScreenshotCapture
    {
        /// <summary>Editor: the Unity project folder. Player: the folder that contains the executable.</summary>
        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        public static string Capture(string tag = null)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return null;
            string dir = Path.Combine(ProjectRoot, "Screenshots");
            Directory.CreateDirectory(dir);
            string name = "Screenshot_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff") + (string.IsNullOrEmpty(tag) ? "" : "_" + tag) + ".png";
            string path = Path.Combine(dir, name);
            if (Game.Ui != null) Game.Ui.PrepareScreenshot();
            ScreenCapture.CaptureScreenshot(path);
            return path;
        }
    }
}
