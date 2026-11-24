using UnityEngine;
using MatCfg = DestructionSandbox.GameConfig.Materials;
using ScoreCfg = DestructionSandbox.GameConfig.Score;

namespace DestructionSandbox
{
    public enum BlockMaterialType { Stone = 0, Wood = 1, Glass = 2 }

    /// <summary>Per-material constants (all values come from <see cref="GameConfig"/>).</summary>
    public static class BlockMaterials
    {
        public const int Count = 3;

        public static float Density(BlockMaterialType t)
        {
            switch (t)
            {
                case BlockMaterialType.Wood: return MatCfg.WoodDensity;
                case BlockMaterialType.Glass: return MatCfg.GlassDensity;
                default: return MatCfg.StoneDensity;
            }
        }

        public static float BreakImpulse(BlockMaterialType t)
        {
            switch (t)
            {
                case BlockMaterialType.Wood: return MatCfg.WoodBreakImpulse;
                case BlockMaterialType.Glass: return MatCfg.GlassBreakImpulse;
                default: return MatCfg.StoneBreakImpulse;
            }
        }

        public static int Points(BlockMaterialType t)
        {
            switch (t)
            {
                case BlockMaterialType.Wood: return ScoreCfg.WoodPoints;
                case BlockMaterialType.Glass: return ScoreCfg.GlassPoints;
                default: return ScoreCfg.StonePoints;
            }
        }
    }

    /// <summary>A fracture piece described as an oriented box in the parent's local space.</summary>
    public struct ShardBox
    {
        public Vector3 LocalCenter;
        public Vector3 Size;
        public float Volume;
    }

    /// <summary>Immutable description of one structure piece (built once at start-up, reused by every rebuild).</summary>
    public sealed class PieceDef
    {
        public BlockMaterialType Material;
        public Vector3 Position;          // centre of the box / centroid of the prism
        public Quaternion Rotation = Quaternion.identity;
        public Vector3 Size;              // box size, or the local bounding size of a prism
        public Mesh PrismMesh;            // null for boxes
        public float Volume;              // m^3
        public ShardBox[] PrismShards;    // fracture pieces of a prism (boxes are split on the fly)
        public uint UserValue;            // brightness variation + pattern seed for the shader

        public bool IsBox => PrismMesh == null;
        public float Mass => Volume * BlockMaterials.Density(Material);
    }

    public sealed class StructureLayout
    {
        public string Name;
        public PieceDef[] Pieces;
    }

    public static class LayoutHash
    {
        /// <summary>Deterministic per-piece shader value: bits 0-7 brightness (-6%..+6%), bits 8-25 pattern seed.</summary>
        public static uint UserValue(int structure, int index)
        {
            uint h = Hash((uint)(structure * 7919 + index * 104729 + 17));
            uint brightness = h & 255u;
            uint seed = (Hash(h) >> 8) & 0x3FFFFu;
            return brightness | (seed << 8);
        }

        public static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352dU;
            x ^= x >> 15; x *= 0x846ca68bU;
            x ^= x >> 16;
            return x;
        }
    }

    /// <summary>Box splitting shared by blocks and shards (D-11: 2x2x2 sub-boxes, D-12: size limit).</summary>
    public static class BoxSplit
    {
        public static bool CanSubdivide(Vector3 size)
        {
            float limit = GameConfig.Fracture.MinSubdivideSize;
            // D-12: pieces smaller than the limit in EVERY dimension are final.
            return !(size.x < limit && size.y < limit && size.z < limit);
        }

        /// <summary>Fills 'result' (length 8) with the octants of a box of the given size.</summary>
        public static void Octants(Vector3 size, ShardBox[] result)
        {
            Vector3 half = size * 0.5f;
            Vector3 quarter = size * 0.25f;
            float volume = half.x * half.y * half.z;
            int i = 0;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        result[i++] = new ShardBox
                        {
                            LocalCenter = new Vector3(quarter.x * x, quarter.y * y, quarter.z * z),
                            Size = half,
                            Volume = volume
                        };
        }
    }
}
