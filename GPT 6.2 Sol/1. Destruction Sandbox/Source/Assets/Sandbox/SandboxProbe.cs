using System;
using System.Collections;
using UnityEngine;
using static DestructionRange.SandboxConfig;

namespace DestructionRange
{
    internal static class SandboxProbe
    {
        public static void TryStart(Sandbox game)
        {
            string[] args = Environment.GetCommandLineArgs();
            int output = Array.IndexOf(args, "-sandbox-project");
            if (output >= 0 && output + 1 < args.Length) game.ScreenshotRoot = args[output + 1];
            int index = Array.IndexOf(args, "-sandbox-mode");
            if (index < 0 || index + 1 >= args.Length) return;
            if (args[index + 1] == "core") game.StartCoroutine(Core(game));
            else if (args[index + 1] == "detail") game.StartCoroutine(new DetailRun(game).Execute());
            else if (args[index + 1] is "acceptance" or "systems" or "stable-arch" or "stable-pyramid" or "endurance")
                game.StartCoroutine(new AcceptanceRun(game).Execute(args[index + 1]));
        }

        static IEnumerator Core(Sandbox game)
        {
            yield return new WaitForSeconds(SettleLimit);
            game.Pieces.Inspect(out float drift, out float speed, out int invalid);
            int total = game.Pieces.Total;
            int sleeping = game.Pieces.Sleeping;
            game.Player.Aim(new Vector3(0, 1.65f, 0));
            bool fired = game.Guns.Fire();
            yield return new WaitForSeconds(3);
            int destroyed = game.Run.Destroyed;
            bool passed = total == TowerWidth * TowerWidth * TowerFloors && sleeping == total &&
                drift < RebuildTolerance && speed < SleepSpeed && invalid == 0 && fired && destroyed > 0;
            Debug.Log($"CORE total={total} sleeping={sleeping} drift={drift:F4} speed={speed:F4} fired={fired} destroyed={destroyed} shards={game.Pieces.ActiveShards} pass={passed}");
            Exit(passed ? 0 : 1);
        }

        internal static void Exit(int code)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }
    }
}
