using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace WaveSurvival
{
    public enum SurfaceKind { Concrete, Metal, Flesh }

    /// <summary>Marks a collider's surface type for impact particles and decals (S-12). Untagged = concrete.</summary>
    public sealed class SurfaceTag : MonoBehaviour
    {
        public SurfaceKind Kind;
    }

    public struct CoverInfo
    {
        public Vector3 Center;
        public float Radius;   // horizontal half-extent used to place cover candidate points around it
        public int Kind;       // 0 crate, 1 barrier, 2 pillar
    }

    /// <summary>Runtime description of the built arena, shared by AI, spawning and debug views.</summary>
    public sealed class ArenaInfo
    {
        public readonly List<SpawnGate> Gates = new List<SpawnGate>();
        public readonly List<CoverInfo> Covers = new List<CoverInfo>();
        public readonly List<FlickerLight> Lamps = new List<FlickerLight>();
        public Vector3[] CoverAnchors = new Vector3[0];
        public int HumanoidAgent;
        public int BossAgent = -1;
        public NavMeshQueryFilter HumanoidFilter;
        public NavMeshQueryFilter BossFilter;
        public bool NavMeshReady;

        /// <summary>True if the point lies inside the walls (with a margin) and above the floor.</summary>
        public static bool Inside(Vector3 p, float margin)
        {
            float lim = GameConfig.Arena.Half - margin;
            return p.x > -lim && p.x < lim && p.z > -lim && p.z < lim && p.y > -0.5f && p.y < GameConfig.Arena.WallHeight;
        }

        public bool SampleHumanoid(Vector3 p, float radius, out Vector3 result)
        {
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, radius, HumanoidFilter) && Inside(hit.position, 0.3f))
            {
                result = hit.position;
                return true;
            }
            result = p;
            return false;
        }

        public bool SampleBoss(Vector3 p, float radius, out Vector3 result)
        {
            if (BossAgent != -1 && NavMesh.SamplePosition(p, out NavMeshHit hit, radius, BossFilter) && Inside(hit.position, 0.9f))
            {
                result = hit.position;
                return true;
            }
            result = p;
            return false;
        }
    }
}
