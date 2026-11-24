using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WaveSurvival
{
    /// <summary>
    /// Builds the arena from procedural meshes (S-01, S-02): floor, 6 m concrete walls with panel lines, ceiling with
    /// beams, central platform with 2 ramps, 12 cover objects of 3 shapes, 6 spawn gates and 8 hanging lamps (S-03).
    /// </summary>
    public static class ArenaBuilder
    {
        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

        static Mesh CachedMesh(string key, System.Func<Mesh> make)
        {
            if (meshCache.TryGetValue(key, out Mesh m) && m != null) return m;
            m = make();
            meshCache[key] = m;
            return m;
        }

        public static ArenaInfo Build(Transform root)
        {
            var info = new ArenaInfo();
            float size = GameConfig.Arena.Size, half = GameConfig.Arena.Half, t = GameConfig.Arena.WallThickness;
            float wallH = GameConfig.Arena.WallHeight;
            float pw = GameConfig.Arena.WallPanelWidth, ph = GameConfig.Arena.WallPanelHeight, ft = GameConfig.Arena.FloorTile;

            // Floor and ceiling.
            Mesh floorMesh = CachedMesh("floor", () => MeshFactory.Box("Floor", new Vector3(size + 2 * t, 1f, size + 2 * t), ft, ft));
            Solid(root, "Floor", floorMesh, GameAssets.Floor, new Vector3(0f, -0.5f, 0f), 0f, SurfaceKind.Concrete, false);
            Mesh ceilMesh = CachedMesh("ceiling", () => MeshFactory.Box("Ceiling", new Vector3(size + 2 * t, GameConfig.Arena.CeilingThickness, size + 2 * t), ft, ft));
            Solid(root, "Ceiling", ceilMesh, GameAssets.Ceiling, new Vector3(0f, wallH + GameConfig.Arena.CeilingThickness * 0.5f, 0f), 0f, SurfaceKind.Metal, false);

            // Walls (S-01): inner faces exactly at +-25 m.
            Mesh wallNS = CachedMesh("wallNS", () => MeshFactory.Box("WallNS", new Vector3(size + 2 * t, wallH, t), pw, ph));
            Mesh wallEW = CachedMesh("wallEW", () => MeshFactory.Box("WallEW", new Vector3(t, wallH, size), pw, ph));
            Solid(root, "WallN", wallNS, GameAssets.Wall, new Vector3(0f, wallH * 0.5f, half + t * 0.5f), 0f, SurfaceKind.Concrete, false);
            Solid(root, "WallS", wallNS, GameAssets.Wall, new Vector3(0f, wallH * 0.5f, -half - t * 0.5f), 0f, SurfaceKind.Concrete, false);
            Solid(root, "WallE", wallEW, GameAssets.Wall, new Vector3(half + t * 0.5f, wallH * 0.5f, 0f), 0f, SurfaceKind.Concrete, false);
            Solid(root, "WallW", wallEW, GameAssets.Wall, new Vector3(-half - t * 0.5f, wallH * 0.5f, 0f), 0f, SurfaceKind.Concrete, false);

            // Hazard skirting along the wall bases.
            Mesh skirtNS = CachedMesh("skirtNS", () => MeshFactory.Box("SkirtNS", new Vector3(size, 0.22f, 0.04f), 1f, 0.25f));
            Mesh skirtEW = CachedMesh("skirtEW", () => MeshFactory.Box("SkirtEW", new Vector3(0.04f, 0.22f, size), 1f, 0.25f));
            Visual(root, "SkirtN", skirtNS, GameAssets.Hazard, new Vector3(0f, 0.11f, half - 0.02f), Quaternion.identity, Vector3.one, false);
            Visual(root, "SkirtS", skirtNS, GameAssets.Hazard, new Vector3(0f, 0.11f, -half + 0.02f), Quaternion.identity, Vector3.one, false);
            Visual(root, "SkirtE", skirtEW, GameAssets.Hazard, new Vector3(half - 0.02f, 0.11f, 0f), Quaternion.identity, Vector3.one, false);
            Visual(root, "SkirtW", skirtEW, GameAssets.Hazard, new Vector3(-half + 0.02f, 0.11f, 0f), Quaternion.identity, Vector3.one, false);

            BuildBeams(root);
            BuildPlatform(root);
            BuildCover(root, info);
            BuildGates(root, info);
            BuildLamps(root, info);
            return info;
        }

        static void BuildBeams(Transform root)
        {
            float size = GameConfig.Arena.Size, y = GameConfig.Arena.WallHeight - 0.25f;
            Mesh beamX = CachedMesh("beamX", () => MeshFactory.Box("BeamX", new Vector3(size, 0.5f, 0.35f), 4f, 4f));
            // Cross beams are slightly thinner so the two beam grids intersect without coplanar (z-fighting) faces.
            Mesh beamZ = CachedMesh("beamZ", () => MeshFactory.Box("BeamZ", new Vector3(0.35f, 0.45f, size), 4f, 4f));
            float[] zs = { -24f, -12f, 0f, 12f, 24f };
            float[] xs = { -18f, -6f, 6f, 18f };
            foreach (float z in zs) Solid(root, "BeamX", beamX, GameAssets.Beam, new Vector3(0f, y, z), 0f, SurfaceKind.Metal, false);
            foreach (float x in xs) Solid(root, "BeamZ", beamZ, GameAssets.Beam, new Vector3(x, y, 0f), 0f, SurfaceKind.Metal, false);
        }

        static void BuildPlatform(Transform root)
        {
            float ps = GameConfig.Arena.PlatformSize, phgt = GameConfig.Arena.PlatformHeight;
            Mesh plat = CachedMesh("platform", () => MeshFactory.Box("Platform", new Vector3(ps, phgt, ps), GameConfig.Arena.FloorTile, GameConfig.Arena.FloorTile));
            Solid(root, "Platform", plat, GameAssets.Floor, new Vector3(0f, phgt * 0.5f, 0f), 0f, SurfaceKind.Concrete, true);

            // Hazard trim around the platform edge.
            Mesh trimX = CachedMesh("trimX", () => MeshFactory.Box("TrimX", new Vector3(ps + 0.06f, 0.2f, 0.03f), 1f, 0.25f));
            Mesh trimZ = CachedMesh("trimZ", () => MeshFactory.Box("TrimZ", new Vector3(0.03f, 0.2f, ps + 0.06f), 1f, 0.25f));
            float hp = ps * 0.5f + 0.015f, ty = phgt - 0.12f;
            Visual(root, "TrimN", trimX, GameAssets.Hazard, new Vector3(0f, ty, hp), Quaternion.identity, Vector3.one, false);
            Visual(root, "TrimS", trimX, GameAssets.Hazard, new Vector3(0f, ty, -hp), Quaternion.identity, Vector3.one, false);
            Visual(root, "TrimE", trimZ, GameAssets.Hazard, new Vector3(hp, ty, 0f), Quaternion.identity, Vector3.one, false);
            Visual(root, "TrimW", trimZ, GameAssets.Hazard, new Vector3(-hp, ty, 0f), Quaternion.identity, Vector3.one, false);

            // Ramps (S-02): rise 1.5 m over 4 m = 20.6 deg, below the 25 deg limit.
            float run = GameConfig.Arena.RampRun;
            float slope = Mathf.Atan2(phgt, run) * Mathf.Rad2Deg;
            if (slope > GameConfig.Arena.RampMaxSlope) Debug.LogError("Ramp slope exceeds the S-02 limit: " + slope);
            Mesh ramp = CachedMesh("ramp", () => MeshFactory.Wedge("Ramp", GameConfig.Arena.RampWidth, phgt, run, 2f));
            float rz = ps * 0.5f + run * 0.5f;
            Solid(root, "RampNorth", ramp, GameAssets.Tread, new Vector3(0f, 0f, rz), 180f, SurfaceKind.Metal, true, true);
            Solid(root, "RampSouth", ramp, GameAssets.Tread, new Vector3(0f, 0f, -rz), 0f, SurfaceKind.Metal, true, true);
        }

        static void BuildCover(Transform root, ArenaInfo info)
        {
            Vector3 crate = GameConfig.Arena.CrateSize, barrier = GameConfig.Arena.BarrierSize;
            Mesh crateMesh = CachedMesh("crate", () => MeshFactory.Box("Crate", crate, 1f, 1f, true));
            Mesh barrierMesh = CachedMesh("barrier", () => MeshFactory.Barrier("Barrier", barrier.x, barrier.y, barrier.z, GameConfig.Arena.BarrierTopWidth, 3f));
            Mesh pillarMesh = CachedMesh("pillar", () => MeshFactory.Cylinder("Pillar", GameConfig.Arena.PillarRadius, GameConfig.Arena.PillarHeight, 24, 3f));
            Mesh bandMesh = CachedMesh("band", () => MeshFactory.Cylinder("PillarBand", GameConfig.Arena.PillarRadius + 0.025f, 0.45f, 24, 1.2f));
            var anchors = new List<Vector3>();

            foreach (Vector4 c in GameConfig.Arena.Cover)
            {
                Vector3 pos = new Vector3(c.x, 0f, c.y);
                Quaternion rot = Quaternion.Euler(0f, c.z, 0f);
                int kind = (int)c.w;
                float radius;
                if (kind == 0)
                {
                    Solid(root, "Crate", crateMesh, GameAssets.Crate, pos + Vector3.up * crate.y * 0.5f, c.z, SurfaceKind.Metal, true);
                    radius = crate.x * 0.5f;
                    for (int i = 0; i < 8; i++) anchors.Add(pos + rot * (Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * (radius * (i % 2 == 0 ? 1f : 1.41f) + 0.85f)));
                }
                else if (kind == 1)
                {
                    Solid(root, "Barrier", barrierMesh, GameAssets.Barrier, pos, c.z, SurfaceKind.Concrete, true, true);
                    radius = barrier.x * 0.5f;
                    for (int i = -1; i <= 1; i++)
                    for (int s = -1; s <= 1; s += 2)
                        anchors.Add(pos + rot * new Vector3(i * 1.4f, 0f, s * (barrier.z * 0.5f + 0.85f)));
                    anchors.Add(pos + rot * new Vector3(barrier.x * 0.5f + 0.85f, 0f, 0f));
                    anchors.Add(pos + rot * new Vector3(-barrier.x * 0.5f - 0.85f, 0f, 0f));
                }
                else
                {
                    Solid(root, "Pillar", pillarMesh, GameAssets.Wall, pos, 0f, SurfaceKind.Concrete, true, true);
                    Visual(root, "PillarBand", bandMesh, GameAssets.Hazard, pos, Quaternion.identity, Vector3.one, true);
                    radius = GameConfig.Arena.PillarRadius;
                    for (int i = 0; i < 8; i++) anchors.Add(pos + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * (radius + 0.85f));
                }
                info.Covers.Add(new CoverInfo { Center = pos, Radius = radius, Kind = kind });
            }
            info.CoverAnchors = anchors.ToArray();
        }

        static void BuildGates(Transform root, ArenaInfo info)
        {
            float dw = GameConfig.Arena.GateDoorWidth, dh = GameConfig.Arena.GateDoorHeight;
            float post = GameConfig.Arena.GatePostWidth, depth = GameConfig.Arena.GateDepth;
            Mesh postMesh = CachedMesh("gatePost", () => MeshFactory.Box("GatePost", new Vector3(post, dh + post, depth), 1f, 1f));
            Mesh lintelMesh = CachedMesh("gateLintel", () => MeshFactory.Box("GateLintel", new Vector3(dw + post * 2f, post, depth), 1f, 1f));
            Mesh stripMesh = CachedMesh("gateStrip", () => MeshFactory.Box("GateStrip", new Vector3(post, dh * 0.55f, 0.02f), 0.5f, 0.5f));
            Mesh doorMesh = CachedMesh("gateDoor", () => MeshFactory.Box("GateDoor", new Vector3(dw, dh, 0.06f), 1f, 1f));
            Vector3[] gates = GameConfig.Arena.Gates;
            for (int i = 0; i < gates.Length; i++)
            {
                var go = new GameObject("Gate" + i);
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(new Vector3(gates[i].x, 0f, gates[i].y), Quaternion.Euler(0f, gates[i].z, 0f));
                Transform g = go.transform;
                float px = dw * 0.5f + post * 0.5f;
                LocalSolid(g, "PostL", postMesh, GameAssets.GateFrame, new Vector3(-px, (dh + post) * 0.5f, depth * 0.5f), SurfaceKind.Metal);
                LocalSolid(g, "PostR", postMesh, GameAssets.GateFrame, new Vector3(px, (dh + post) * 0.5f, depth * 0.5f), SurfaceKind.Metal);
                LocalSolid(g, "Lintel", lintelMesh, GameAssets.GateFrame, new Vector3(0f, dh + post * 0.5f, depth * 0.5f), SurfaceKind.Metal);
                LocalVisual(g, "StripL", stripMesh, GameAssets.Hazard, new Vector3(-px, dh * 0.3f, depth + 0.01f));
                LocalVisual(g, "StripR", stripMesh, GameAssets.Hazard, new Vector3(px, dh * 0.3f, depth + 0.01f));
                MeshRenderer door = LocalVisual(g, "Door", doorMesh, GameAssets.GateDoor, new Vector3(0f, dh * 0.5f, 0.04f));

                var lightGo = new GameObject("GateLight");
                lightGo.transform.SetParent(g, false);
                lightGo.transform.localPosition = new Vector3(0f, dh + 0.8f, 0.9f);
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = GameConfig.Lighting.GateColor;
                light.range = GameConfig.Lighting.GateLightRange;
                light.intensity = GameConfig.Lighting.GateLightIdle;
                light.shadows = LightShadows.None;

                SpawnGate gate = go.AddComponent<SpawnGate>();
                gate.Index = i;
                gate.Light = light;
                gate.Door = door;
                float spread = GameConfig.Arena.GateSpawnSpread;
                for (int s = 0; s < 4; s++)
                    gate.Slots[s] = g.TransformPoint(new Vector3((s - 1.5f) * spread, 0f, GameConfig.Arena.GateSpawnOffset + (s % 2) * 0.6f));
                info.Gates.Add(gate);
            }
        }

        static void BuildLamps(Transform root, ArenaInfo info)
        {
            Vector2[] xz = GameConfig.Lighting.LampXZ;
            float top = GameConfig.Arena.WallHeight - 0.5f;
            for (int i = 0; i < xz.Length; i++)
            {
                var go = new GameObject("Lamp" + i);
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(xz[i].x, 0f, xz[i].y);
                Transform l = go.transform;
                float y = GameConfig.Lighting.LampHeight;
                float cableLen = top - (y + 0.15f);
                Visual(l, "Cable", GameAssets.Cylinder, GameAssets.Cable, l.position + Vector3.up * (y + 0.15f + cableLen * 0.5f), Quaternion.identity, new Vector3(0.03f, cableLen * 0.5f, 0.03f), false);
                Visual(l, "Shade", GameAssets.Cylinder, GameAssets.LampHousing, l.position + Vector3.up * (y + 0.12f), Quaternion.identity, new Vector3(0.8f, 0.05f, 0.8f), false);
                Visual(l, "Cap", GameAssets.Cylinder, GameAssets.LampHousing, l.position + Vector3.up * (y + 0.2f), Quaternion.identity, new Vector3(0.36f, 0.06f, 0.36f), false);
                MeshRenderer bulb = Visual(l, "Bulb", GameAssets.Sphere, GameAssets.LampBulb, l.position + Vector3.up * (y + 0.02f), Quaternion.identity, Vector3.one * 0.2f, false);

                var lightGo = new GameObject("LampLight");
                lightGo.transform.SetParent(l, false);
                lightGo.transform.localPosition = new Vector3(0f, y - 0.1f, 0f);
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = GameConfig.Lighting.LampColor;
                light.range = GameConfig.Lighting.LampRange;
                light.intensity = GameConfig.Lighting.LampIntensity;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.92f;
                // 8 lamps x 6 cube faces must fit the 4096 additional-light atlas at 512 px (no URP rescale message).
                light.GetUniversalAdditionalLightData().additionalLightsShadowResolutionTier =
                    UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium;

                FlickerLight fl = go.AddComponent<FlickerLight>();
                fl.Light = light;
                fl.Bulb = bulb;
                fl.Seed = i;
                fl.Flicker = System.Array.IndexOf(GameConfig.Lighting.FlickerLamps, i) >= 0;
                info.Lamps.Add(fl);
            }
        }

        // ---------- object helpers ----------

        static GameObject Solid(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, float yaw,
            SurfaceKind surface, bool castShadows, bool meshCollider = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (meshCollider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = true;
            }
            else
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = mesh.bounds.center;
                bc.size = mesh.bounds.size;
            }
            if (surface != SurfaceKind.Concrete) go.AddComponent<SurfaceTag>().Kind = surface;
            go.isStatic = true;
            return go;
        }

        static MeshRenderer Visual(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Vector3 scale, bool castShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return mr;
        }

        static void LocalSolid(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos, SurfaceKind surface)
        {
            GameObject go = Solid(parent, name, mesh, mat, Vector3.zero, 0f, surface, true);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
        }

        static MeshRenderer LocalVisual(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos)
        {
            MeshRenderer mr = Visual(parent, name, mesh, mat, Vector3.zero, Quaternion.identity, Vector3.one, false);
            mr.transform.localPosition = localPos;
            mr.transform.localRotation = Quaternion.identity;
            return mr;
        }
    }
}
