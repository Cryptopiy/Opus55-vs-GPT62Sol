using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using static WaveSurvival.GameConfig;

namespace WaveSurvival
{
    internal sealed partial class Arena
    {
        public readonly Transform Root;
        public readonly Vector3[] Gates = new Vector3[GateCount];
        public readonly Vector3[] CoverAnchors = new Vector3[CoverCount];
        public Material Concrete, Metal, Dark, Amber, Cyan;
        const float Half = ArenaSize * .5f;
        int coverIndex;

        public Arena()
        {
            Root = Geometry.Root("Bastion 06 | containment hall");
            Concrete = Geometry.Lit("Precast concrete", Geometry.Hex(0x687477));
            Metal = Geometry.Lit("Oxidized steel", Geometry.Hex(0x36464a), .42f, .42f);
            Dark = Geometry.Lit("Carbon steel", Geometry.Hex(0x19262b), .32f, .5f);
            Amber = Geometry.Lit("Sodium amber", Geometry.Hex(0xffa13d), .35f, .15f, 2);
            Cyan = Geometry.Lit("Signal teal", Geometry.Hex(0x61f8e2), .4f, .15f, 2);
            Box("Floor", new(0, -.25f, 0), new(ArenaSize, .5f, ArenaSize), Concrete);
            for (int wall = 0; wall < 4; wall++)
            {
                var section = Geometry.Root("Perimeter wall", Root);
                section.localRotation = Quaternion.Euler(0, wall * 90, 0);
                Geometry.Part(section, "Concrete wall", new(0, WallHeight * .5f, Half + .25f),
                    new(ArenaSize + 1, WallHeight, .5f), Concrete, solid: true);
            }
            Box("Central platform", new(0, PlatformHeight * .5f, 0),
                new(PlatformSize, PlatformHeight, PlatformSize), Metal);
            Ramp(-1);
            Ramp(1);
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                var rotation = Quaternion.Euler(0, quadrant * 90, 0);
                Cover("Armored crate", rotation * new Vector3(8, 0, 12), Crate, Metal, quadrant * 90);
                Cover("Blast barrier", rotation * new Vector3(15, 0, 8), Barrier, Concrete, quadrant * 90 + 30);
                Cover("Support column", rotation * new Vector3(16, 0, 16),
                    new(PillarRadius * 2, PillarHeight, PillarRadius * 2), Concrete, 0, true);
            }

            Vector3[] gatePositions =
            {
                new(-14, 0, Half), new(14, 0, Half), new(Half, 0, 0),
                new(14, 0, -Half), new(-14, 0, -Half), new(-Half, 0, 0)
            };
            for (int i = 0; i < Gates.Length; i++)
            {
                Vector3 point = gatePositions[i];
                Vector3 inward = Mathf.Abs(point.z) > Mathf.Abs(point.x)
                    ? new Vector3(0, 0, -Mathf.Sign(point.z)) : new Vector3(-Mathf.Sign(point.x), 0, 0);
                Gates[i] = point + inward * 2.7f;
                var frame = Geometry.Root("Gate " + (i + 1), Root);
                frame.localPosition = point + inward * .24f;
                frame.localRotation = Quaternion.LookRotation(inward);
                Geometry.Part(frame, "Door", new(0, 1.57f, 0), new(3.4f, 3.14f, .18f), Dark);
                Geometry.Part(frame, "Left jamb", new(-1.85f, 1.55f, .2f), new(.33f, 3.1f, .5f), Metal);
                Geometry.Part(frame, "Right jamb", new(1.85f, 1.55f, .2f), new(.33f, 3.1f, .5f), Metal);
                Geometry.Part(frame, "Crossbar", new(0, 3.2f, .2f), new(4, .4f, .5f), Metal);
                DecorateGate(frame, i);
            }
            Decorate();
            var surface = Root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = WorldMask;
            surface.BuildNavMesh();
        }

        partial void DecorateGate(Transform frame, int index);
        partial void Decorate();
        partial void DecorateCover(Transform cover, bool round);

        Transform Box(string name, Vector3 center, Vector3 size, Material material)
            => Geometry.Part(Root, name, center, size, material, solid: true);

        void Cover(string name, Vector3 ground, Vector3 size, Material material, float yaw, bool round = false)
        {
            CoverAnchors[coverIndex++] = ground;
            var cover = Geometry.Part(Root, name, ground + Vector3.up * (size.y * .5f),
                round ? new(size.x, size.y * .5f, size.z) : size, material,
                round ? PrimitiveType.Cylinder : PrimitiveType.Cube, true);
            cover.localRotation = Quaternion.Euler(0, yaw, 0);
            DecorateCover(cover, round);
        }

        void Ramp(int side)
        {
            // The box's top edge meets the platform exactly, avoiding a NavMesh seam.
            float angle = Mathf.Atan2(PlatformHeight, RampRun) * Mathf.Rad2Deg;
            float length = new Vector2(RampRun, PlatformHeight).magnitude;
            var rotation = Quaternion.Euler(side * angle, 0, 0);
            Vector3 topMid = new(0, PlatformHeight * .5f, side * (PlatformSize + RampRun) * .5f);
            var ramp = Box("Access ramp", topMid - rotation * Vector3.up * .17f, new(3.6f, .34f, length), Metal);
            ramp.localRotation = rotation;
        }

        public bool IsMetal(Transform shape) => shape.GetComponent<Renderer>()?.sharedMaterial == Metal;

        public static bool Clear(Vector3 from, Vector3 to)
            => !Physics.Linecast(from, to, WorldMask, QueryTriggerInteraction.Ignore);

        public static bool Sample(Vector3 point, out Vector3 result, float range = 2.5f)
        {
            bool found = NavMesh.SamplePosition(point, out NavMeshHit hit, range, NavMesh.AllAreas);
            result = hit.position;
            return found;
        }
    }
}



