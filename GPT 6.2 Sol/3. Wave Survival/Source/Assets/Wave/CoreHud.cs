using System;
using System.IO;
using UnityEngine;

namespace WaveSurvival
{
    internal sealed partial class CoreGame
    {
        Hud hud;
        partial void SetupHud() => hud = new Hud(this);
        partial void UpdateHud() => hud.Refresh();
        partial void Screenshot() => Capture();
        public string Capture(string label = "")
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            if (!Application.isEditor)
            {
                string workspaceProject = Path.Combine(Path.GetDirectoryName(root) ?? root, "Wave Survival");
                if (Directory.Exists(Path.Combine(workspaceProject, "ProjectSettings"))) root = workspaceProject;
            }
            string folder = Path.Combine(root, "Screenshots");
            Directory.CreateDirectory(folder);
            string date = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff", System.Globalization.CultureInfo.InvariantCulture);
            string filepath = Path.Combine(folder, "Bastion_" + date + label + ".png");
            ScreenCapture.CaptureScreenshot(filepath);
            return filepath;
        }

        void OnDestroy()
        {
            acceptance?.Dispose();
            hud?.Dispose();
            Geometry.ReleaseAssets();
        }
    }
}

