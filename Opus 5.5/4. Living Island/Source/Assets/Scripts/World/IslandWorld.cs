using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LivingIsland
{
    /// <summary>
    /// Owns everything that depends on the seed (I-01, I-13): height function -> POI pads -> terrain
    /// mesh/collider/GPU height texture -> lighthouse, campfire, rowboat -> palms, rocks, bushes, flowers.
    /// Regeneration rebuilds all of it in place and reports the measured time and validation results.
    /// </summary>
    public sealed class IslandWorld : MonoBehaviour
    {
        public IslandData Data { get; private set; }
        public IslandReport Report { get; private set; }
        public PoiPlan Plan { get; private set; }
        public PropPlacer Placer { get; private set; }
        public float LastGenerationSeconds { get; private set; }
        public Vector3 SpawnPoint => Plan.Spawn;
        public float SpawnYaw => Plan.SpawnYaw;
        public Lighthouse Lighthouse { get; private set; }
        public Campfire Campfire { get; private set; }
        public Rowboat Boat { get; private set; }
        public int ColliderCount { get; private set; }
        public int RigidbodyCount { get; private set; }
        public string ValidationSummary { get; private set; } = "";

        IslandAssets assets;
        MaterialLibrary mats;
        TerrainBuilder terrain;
        GameObject placedRoot;

        public void Init(IslandAssets a, MaterialLibrary m)
        {
            assets = a;
            mats = m;
            terrain = new TerrainBuilder(m.Terrain, Layers.Terrain);
            terrain.Root.transform.SetParent(transform, false);
            Placer = new PropPlacer(a, m);
        }

        /// <summary>Builds (or rebuilds) the whole island for a seed; returns the generation time in seconds.</summary>
        public float Generate(int seed)
        {
            var sw = Stopwatch.StartNew();
            IslandData data = IslandGenerator.Generate(seed);
            PoiPlan plan = PoiPlanner.Plan(data);
            PoiPlanner.FlattenPads(data, plan);
            data.Recompute();
            terrain.Build(data);
            Data = data;
            Plan = plan;
            WaveModel.Island = data;

            if (placedRoot != null) Destroy(placedRoot);
            placedRoot = new GameObject("Placed (seed " + seed + ")");
            placedRoot.transform.SetParent(transform, false);

            Placer.Place(data, plan, placedRoot.transform);
            Lighthouse = Lighthouse.Create(assets, mats, plan, placedRoot.transform);
            if (Lighthouse != null) Placer.Validator.Check(Lighthouse.transform, PropPlacer.FirstMesh(Lighthouse.gameObject), true);
            Campfire = Campfire.Create(assets, mats, data, plan, placedRoot.transform, Placer.Validator);
            Boat = Rowboat.Create(assets, data, plan, placedRoot.transform);

            sw.Stop();
            LastGenerationSeconds = (float)sw.Elapsed.TotalSeconds;
            Report = IslandAnalysis.Analyze(data, LastGenerationSeconds);
            CountPhysics();
            ValidationSummary = Placer.Validator.ToString();
            Debug.Log("[LI] Island " + Report);
            Debug.Log(string.Format("[LI] Placement seed {0}: palms {1}, rocks {2}, bushes {3}, flower patches {4} | {5}",
                seed, Placer.Palms, Placer.Rocks, Placer.Bushes, Placer.FlowerClusters, ValidationSummary));
            return LastGenerationSeconds;
        }

        void CountPhysics()
        {
            // Counted once per generation (not per frame): terrain + props colliders, kinematic bodies.
            ColliderCount = 1 + placedRoot.GetComponentsInChildren<Collider>(true).Length;
            RigidbodyCount = placedRoot.GetComponentsInChildren<Rigidbody>(true).Length;
        }

        void OnDestroy()
        {
            if (terrain != null && terrain.HeightTexture != null) Destroy(terrain.HeightTexture);
        }
    }
}
