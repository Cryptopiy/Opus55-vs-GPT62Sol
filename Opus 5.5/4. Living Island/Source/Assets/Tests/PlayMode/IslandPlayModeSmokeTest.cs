using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LivingIsland.Tests
{
    /// <summary>
    /// Editor Play Mode smoke test (run in batch mode with -runTests -testPlatform PlayMode):
    /// loads Assets/Scenes/Island.unity exactly as pressing Play does, lets the game run, and fails on any
    /// warning, error or exception written to the console.
    /// </summary>
    public sealed class IslandPlayModeSmokeTest
    {
        readonly List<string> problems = new List<string>();

        void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Log) problems.Add(type + ": " + message);
        }

        [UnityTest]
        [Timeout(150000)]
        public IEnumerator IslandRunsWithoutWarningsOrErrors()
        {
            Application.logMessageReceived += OnLog;
            SceneManager.LoadScene("Island");
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 45f) yield return null;
            Application.logMessageReceived -= OnLog;
            Debug.Log("[LI] PLAYMODE frames " + Time.frameCount + ", problems " + problems.Count);
            foreach (string p in problems) Debug.Log("[LI] PLAYMODE problem: " + p);
            Assert.IsEmpty(problems, "Console warnings/errors during play: " + string.Join("\n", problems));
        }
    }
}
