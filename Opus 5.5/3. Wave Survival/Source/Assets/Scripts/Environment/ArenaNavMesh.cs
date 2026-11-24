using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace WaveSurvival
{
    /// <summary>
    /// S-04: bakes the NavMesh at runtime with NavMeshSurface components. The humanoid surface uses the project's
    /// "Humanoid" agent type (radius 0.5, height 1.8). A second surface for the "Boss" agent type (radius 1.1) keeps the
    /// x2.2 Warden from clipping into walls and cover. Collection is limited to a volume below the ceiling so no
    /// walkable islands appear on the roof or wall tops.
    /// </summary>
    public static class ArenaNavMesh
    {
        public static void Build(GameObject arenaRoot, ArenaInfo info)
        {
            int bossId = FindAgentType(GameConfig.Nav.BossAgentType);
            info.HumanoidAgent = 0;
            info.BossAgent = bossId;
            info.HumanoidFilter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
            info.BossFilter = new NavMeshQueryFilter { agentTypeID = bossId == -1 ? 0 : bossId, areaMask = NavMesh.AllAreas };

            NavMeshSurface humanoid = CreateSurface(arenaRoot, "NavMesh_Humanoid", 0, GameConfig.Nav.VoxelSize);
            humanoid.BuildNavMesh();
            if (bossId != -1)
            {
                NavMeshSurface boss = CreateSurface(arenaRoot, "NavMesh_Boss", bossId, GameConfig.Nav.BossVoxelSize);
                boss.BuildNavMesh();
            }
            else
            {
                Debug.LogError("NavMesh agent type 'Boss' is missing from ProjectSettings/NavMeshAreas.asset");
            }

            // Snap cover anchor points (used as shooter cover candidates) onto the humanoid NavMesh.
            var snapped = new List<Vector3>(info.CoverAnchors.Length);
            foreach (Vector3 a in info.CoverAnchors)
                if (NavMesh.SamplePosition(a, out NavMeshHit hit, 0.8f, info.HumanoidFilter) && ArenaInfo.Inside(hit.position, 0.5f))
                    snapped.Add(hit.position);
            info.CoverAnchors = snapped.ToArray();

            for (int g = 0; g < info.Gates.Count; g++)
            {
                SpawnGate gate = info.Gates[g];
                for (int s = 0; s < gate.Slots.Length; s++)
                    if (info.SampleHumanoid(gate.Slots[s], GameConfig.Nav.SampleRadius, out Vector3 p)) gate.Slots[s] = p;
            }
            info.NavMeshReady = true;
        }

        static NavMeshSurface CreateSurface(GameObject arenaRoot, string name, int agentType, float voxel)
        {
            var go = new GameObject(name);
            go.transform.SetParent(arenaRoot.transform, false);
            NavMeshSurface s = go.AddComponent<NavMeshSurface>();
            s.agentTypeID = agentType;
            s.collectObjects = CollectObjects.Volume;
            float h = GameConfig.Arena.NavVolumeHeight;
            float extent = GameConfig.Arena.Size + GameConfig.Arena.WallThickness * 2f + 0.5f; // include the walls so the mesh is eroded along them
            s.center = new Vector3(0f, h * 0.5f - 0.5f, 0f);
            s.size = new Vector3(extent, h, extent);
            s.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            s.layerMask = GameConfig.Layers.EnvironmentMask;
            s.overrideVoxelSize = true;
            s.voxelSize = voxel;
            return s;
        }

        public static int FindAgentType(string name)
        {
            int count = NavMesh.GetSettingsCount();
            for (int i = 0; i < count; i++)
            {
                NavMeshBuildSettings s = NavMesh.GetSettingsByIndex(i);
                if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == name) return s.agentTypeID;
            }
            return -1;
        }
    }
}
