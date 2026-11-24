using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>
    /// Deterministic, seed-driven placement (I-09, I-12, I-13) of 300 palms (beach/grassland), 150 rocks
    /// (slope/height rules, random scale/rotation, tilted to the ground), 60 bushes and 20 flower patches.
    /// Every candidate is rejected if it is in the water, too steep, too close to another object or to a
    /// point of interest. Objects are set on the exact mesh height and verified by PlacementValidator.
    /// </summary>
    public sealed class PropPlacer
    {
        public int Palms, Rocks, Bushes, FlowerClusters;
        public PlacementValidator Validator;

        readonly IslandAssets assets;
        readonly MaterialLibrary mats;
        readonly Mesh[] bushMeshes = new Mesh[4];
        readonly float[] palmModelHeights = new float[3];

        public PropPlacer(IslandAssets a, MaterialLibrary m)
        {
            assets = a;
            mats = m;
            for (int i = 0; i < bushMeshes.Length; i++) bushMeshes[i] = FoliageFactory.BuildBush(i);
            for (int i = 0; i < palmModelHeights.Length; i++)
                palmModelHeights[i] = a.palms[i] != null ? ModelHeight(a.palms[i]) : C.PalmModelHeight;
        }

        static float ModelHeight(GameObject prefab)
        {
            float h = 0f;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) h = Mathf.Max(h, mf.sharedMesh.bounds.max.y);
            return h > 0.5f ? h : C.PalmModelHeight;
        }

        static bool Excluded(List<Vector3> exclusions, float x, float z, float extra)
        {
            for (int i = 0; i < exclusions.Count; i++)
            {
                Vector3 e = exclusions[i];
                float dx = x - e.x, dz = z - e.y, r = e.z + extra;
                if (dx * dx + dz * dz < r * r) return true;
            }
            return false;
        }

        static bool TooClose(List<Vector3> placed, float x, float z, float spacing)
        {
            float s2 = spacing * spacing;
            for (int i = 0; i < placed.Count; i++)
            {
                float dx = x - placed[i].x, dz = z - placed[i].z;
                if (dx * dx + dz * dz < s2) return true;
            }
            return false;
        }

        public void Place(IslandData d, PoiPlan plan, Transform root)
        {
            Validator = new PlacementValidator(d);
            var rng = new DetRandom(d.Seed, 100);
            var clump = new Noise2D(d.Seed * 7 + 11);
            var palmsPos = new List<Vector3>(C.PalmCount);
            var rocksPos = new List<Vector3>(C.RockCount);
            var bushesPos = new List<Vector3>(C.BushCount);
            float extent = C.IslandDiameter * 0.5f + 20f;
            Vector2 c = d.Layout.Center;

            // ------------------------------------------------------------------ palms
            var palmRoot = new GameObject("Palms").transform; palmRoot.SetParent(root, false);
            for (int attempt = 0; attempt < 80000 && palmsPos.Count < C.PalmCount; attempt++)
            {
                float x = c.x + rng.Range(-extent, extent), z = c.y + rng.Range(-extent, extent);
                float h = d.HeightAt(x, z);
                if (h < C.PalmMinGround || h > 26f) continue;
                if (d.SlopeDegAt(x, z) > C.PalmMaxSlopeDeg) continue;
                // Palms love the coast: full chance near the beach, sparser inland, clumped by noise.
                float coast = h < 7f ? 1f : 0.3f;
                float clumping = 0.25f + 0.75f * Mathf.Clamp01(clump.Fbm(x * 0.02f, z * 0.02f, 3) * 0.8f + 0.55f);
                if (rng.Value > coast * clumping) continue;
                if (Excluded(plan.Exclusions, x, z, 1.5f) || TooClose(palmsPos, x, z, C.PalmMinSpacing)) continue;
                int variant = rng.Range(0, 3);
                float targetHeight = rng.Range(C.PalmHeightMin, C.PalmHeightMax);
                float scale = targetHeight / palmModelHeights[variant];
                var pos = new Vector3(x, h - C.PlacementSink, z);
                var palm = Spawn(assets.palms[variant], pos, Quaternion.Euler(0f, rng.Range(0f, 360f), 0f), scale, palmRoot);
                if (palm == null) break;
                var capsule = palm.AddComponent<CapsuleCollider>();
                capsule.radius = 0.24f / scale;           // collider lives in the scaled local space
                capsule.height = 3f / scale;
                capsule.center = new Vector3(0f, 1.5f / scale, 0f);
                palmsPos.Add(pos);
                Validator.Check(palm.transform, FirstMesh(palm), true);
            }
            Palms = palmsPos.Count;

            // ------------------------------------------------------------------ rocks
            var rockRoot = new GameObject("Rocks").transform; rockRoot.SetParent(root, false);
            for (int attempt = 0; attempt < 80000 && rocksPos.Count < C.RockCount; attempt++)
            {
                float x = c.x + rng.Range(-extent, extent), z = c.y + rng.Range(-extent, extent);
                float h = d.HeightAt(x, z);
                if (h < C.RockMinGround) continue;                       // never in the water
                float slope = d.SlopeDegAt(x, z);
                if (slope > 50f) continue;
                // More rocks on slopes and on the mountain, a few scattered on beaches and meadows.
                float w = 0.2f + 0.8f * IslandGenerator.SmoothStep(10f, 34f, slope) + (h > 24f ? 0.5f : 0f);
                if (rng.Value > Mathf.Clamp01(w)) continue;
                if (Excluded(plan.Exclusions, x, z, 1f) || TooClose(rocksPos, x, z, 3f) || TooClose(palmsPos, x, z, 2f)) continue;
                float scale = Mathf.Lerp(C.RockScaleMin, C.RockScaleMax, rng.Value * rng.Value);
                if (slope > 35f) scale = Mathf.Min(scale, 1.4f);
                Vector3 n = d.NormalAt(x, z);
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, n, 0.85f)) * Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);
                float sink = Mathf.Min(0.07f * scale, 0.16f);
                var pos = new Vector3(x, h - sink, z);
                var rock = Spawn(assets.rocks[rng.Range(0, 5)], pos, rot, scale, rockRoot);
                if (rock == null) break;
                AddMeshCollider(rock, false);   // static triangle collider: rock hulls exceed PhysX's 255-polygon convex limit
                rocksPos.Add(pos);
                Validator.Check(rock.transform, FirstMesh(rock), true);
            }
            Rocks = rocksPos.Count;

            // ------------------------------------------------------------------ bushes
            var bushRoot = new GameObject("Bushes").transform; bushRoot.SetParent(root, false);
            for (int attempt = 0; attempt < 40000 && bushesPos.Count < C.BushCount; attempt++)
            {
                float x = c.x + rng.Range(-extent, extent), z = c.y + rng.Range(-extent, extent);
                float h = d.HeightAt(x, z);
                if (h < 2.6f || h > 30f || d.SlopeDegAt(x, z) > 26f) continue;
                if (Excluded(plan.Exclusions, x, z, 1f) || TooClose(bushesPos, x, z, 3f) || TooClose(palmsPos, x, z, 1.8f) || TooClose(rocksPos, x, z, 2f)) continue;
                var go = new GameObject("Bush") { layer = Layers.Props };
                go.transform.SetParent(bushRoot, false);
                go.transform.SetPositionAndRotation(new Vector3(x, h - 0.08f, z), Quaternion.Euler(0f, rng.Range(0f, 360f), 0f));
                go.transform.localScale = Vector3.one * rng.Range(0.8f, 1.35f);
                var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = bushMeshes[rng.Range(0, bushMeshes.Length)];
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mats.Bush; mr.shadowCastingMode = ShadowCastingMode.On;
                bushesPos.Add(go.transform.position);
                Validator.Check(go.transform, mf, true);
            }
            Bushes = bushesPos.Count;

            // ------------------------------------------------------------------ flower patches
            var flowerRoot = new GameObject("Flowers").transform; flowerRoot.SetParent(root, false);
            var flowerCentres = new List<Vector3>();
            for (int attempt = 0; attempt < 30000 && flowerCentres.Count < C.FlowerClusterCount; attempt++)
            {
                float x = c.x + rng.Range(-extent, extent), z = c.y + rng.Range(-extent, extent);
                float h = d.HeightAt(x, z);
                if (h < 3f || h > 24f || d.SlopeDegAt(x, z) > 16f) continue;
                if (Excluded(plan.Exclusions, x, z, 3f) || TooClose(flowerCentres, x, z, 12f) || TooClose(rocksPos, x, z, 3f)) continue;
                var go = new GameObject("FlowerPatch") { layer = Layers.Props };
                go.transform.SetParent(flowerRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = FoliageFactory.BuildFlowerCluster(d, new Vector2(x, z), C.FlowersPerCluster, C.FlowerClusterRadius, d.Seed * 131 + flowerCentres.Count);
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mats.Flower; mr.shadowCastingMode = ShadowCastingMode.Off;
                flowerCentres.Add(new Vector3(x, h, z));
            }
            FlowerClusters = flowerCentres.Count;
        }

        public static MeshFilter FirstMesh(GameObject go)
        {
            var lod = go.GetComponent<LODGroup>();
            if (lod != null)
            {
                var lods = lod.GetLODs();
                if (lods.Length > 0 && lods[0].renderers.Length > 0) return lods[0].renderers[0].GetComponent<MeshFilter>();
            }
            return go.GetComponentInChildren<MeshFilter>();
        }

        public static GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, float scale, Transform parent)
        {
            if (prefab == null) { Debug.LogError("[LI] Missing Blender prop prefab (Assets/Models)"); return null; }
            var go = Object.Instantiate(prefab, pos, rot, parent);
            go.transform.localScale = Vector3.one * scale;
            SetLayer(go, Layers.Props);
            return go;
        }

        public static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
        }

        public static void AddMeshCollider(GameObject go, bool convex)
        {
            var mf = FirstMesh(go);
            if (mf == null) return;
            // On the mesh's own object so any child offset in the imported hierarchy is respected.
            var col = mf.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = mf.sharedMesh;
            col.convex = convex;
        }
    }
}
