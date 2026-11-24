using System.Collections;
using UnityEngine;

namespace LivingIsland
{
    /// <summary>
    /// "-autotest audio": walks the player over each surface type and records which footstep sets played,
    /// and logs the ambience mixer (ocean by shore distance, wind, birds, crickets, rain, thunder, campfire,
    /// underwater muffling) in the situations that should change them (I-27, I-31, I-18, I-19).
    /// </summary>
    public sealed partial class AutoTest
    {
        IEnumerator WalkOn(SurfaceType surface, Vector3 at, float seconds)
        {
            Game.Player.Teleport(at + Vector3.up * 0.1f, Game.Player.transform.eulerAngles.y);
            Game.CameraRig.Snap();
            int before = Game.Audio.StepCounts[(int)surface];
            float t0 = Time.time;
            while (Time.time - t0 < seconds)
            {
                // Walk in a small circle so the player stays on the same ground type.
                Game.CameraRig.Yaw += Time.deltaTime * 70f;
                GameInput.ScriptedMove = Vector2.up;
                yield return null;
            }
            GameInput.ScriptedMove = Vector2.zero;
            int played = Game.Audio.StepCounts[(int)surface] - before;
            Log("audio footsteps on " + surface + ": " + played + " steps with the " + surface + " sound set");
        }

        Vector3 FindRockPropTop()
        {
            // Standing on a placed rock prop must play the rock set.
            foreach (var mc in Game.World.GetComponentsInChildren<MeshCollider>())
            {
                if (!mc.name.StartsWith("SM_Rock")) continue;
                Bounds b = mc.bounds;
                if (b.size.x > 2f && b.size.z > 2f && b.size.y < 1.6f && b.min.y > 1f)
                    return new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
            }
            return Vector3.zero;
        }

        IEnumerator AudioCheck()
        {
            var d = Game.World.Data;
            Game.TimeOfDay.SetHour(11f);
            Game.Weather.SnapTo(WeatherState.Clear);
            yield return new WaitForSeconds(2.5f);

            // Ocean loudness follows the distance to the shore line.
            Vector3 beach = Game.World.SpawnPoint;
            yield return new WaitForSeconds(1.5f);
            float oceanBeach = Game.Audio.OceanVolume;
            Vector3 inland = FindSurface(SurfaceType.Grass, 8f, 30f, d.Layout.MountainCenter, 60f);
            Game.Player.Teleport(inland + Vector3.up * 0.1f, 0f);
            Game.CameraRig.Snap();
            yield return new WaitForSeconds(3f);
            float oceanInland = Game.Audio.OceanVolume;
            Log(string.Format(Inv, "audio ocean volume: {0:F2} on the beach vs {1:F2} inland ({2:F0} m from shore)",
                oceanBeach, oceanInland, d.DistanceToShore(Game.MainCamera.transform.position)));

            // Footsteps by surface.
            yield return WalkOn(SurfaceType.Sand, beach, 3f);
            yield return WalkOn(SurfaceType.Grass, FindSurface(SurfaceType.Grass, 4f, 20f, d.Layout.Center, 90f), 3f);
            Vector3 rockTop = FindRockPropTop();
            if (rockTop != Vector3.zero) yield return WalkOn(SurfaceType.Rock, rockTop, 1.5f);
            Vector2 shallow = d.Layout.Center + d.Layout.BayDir * (GameConfig.IslandDiameter * 0.5f - GameConfig.BayInlandDepth + 4f);
            Vector3 wet = new Vector3(shallow.x, 0f, shallow.y);
            for (int i = 0; i < 60; i++)
            {
                float depth = GameConfig.SeaLevel - d.HeightAt(wet.x, wet.z);
                if (depth > 0.15f && depth < 0.8f) break;
                wet += new Vector3(d.Layout.BayDir.x, 0f, d.Layout.BayDir.y) * (depth < 0.15f ? 1f : -1f);
            }
            wet.y = d.HeightAt(wet.x, wet.z);
            yield return WalkOn(SurfaceType.Water, wet, 3f);
            Log(string.Format("audio footstep totals: sand {0}, grass {1}, dirt {2}, rock {3}, water {4}",
                Game.Audio.StepCounts[0], Game.Audio.StepCounts[1], Game.Audio.StepCounts[2], Game.Audio.StepCounts[3], Game.Audio.StepCounts[4]));

            // Day birds, rain and wind follow the weather, thunder after lightning, crickets at night.
            Log(string.Format(Inv, "audio day: birds called {0} times, wind {1:F2}, crickets {2:F2}, rain {3:F2}",
                Game.Audio.BirdCalls, Game.Audio.WindVolume, Game.Audio.CricketVolume, Game.Audio.RainVolume));
            Game.Weather.SnapTo(WeatherState.Storm);
            Game.Lightning.Strike();
            yield return new WaitForSeconds(5f);
            Log(string.Format(Inv, "audio storm: rain {0:F2}, wind {1:F2}, thunder clips played {2} (strike at {3:F0} m)",
                Game.Audio.RainVolume, Game.Audio.WindVolume, Game.Audio.ThunderPlayed, Game.Lightning.LastStrikeDistance));
            Game.Weather.SnapTo(WeatherState.Clear);
            Game.TimeOfDay.SetHour(23f);
            FindAnyObjectByType<GameBootstrap>().RespawnPlayer();
            yield return new WaitForSeconds(4f);
            Log(string.Format(Inv, "audio night at the campfire: crickets {0:F2}, campfire (3D) {1:F2}, rain {2:F2}",
                Game.Audio.CricketVolume, Game.Audio.FireVolume, Game.Audio.RainVolume));

            // Underwater muffling.
            Game.Photo.SetActive(true);
            Game.Photo.SetView(new Vector3(shallow.x + d.Layout.BayDir.x * 20f, -1.5f, shallow.y + d.Layout.BayDir.y * 20f), 0f, 10f);
            yield return new WaitForSeconds(1.5f);
            Log(string.Format(Inv, "audio underwater={0}: low-pass cutoff {1:F0} Hz", Game.Atmosphere.CameraUnderwater, Game.Audio.UnderwaterCutoff));
            Game.Photo.SetActive(false);
        }
    }
}
