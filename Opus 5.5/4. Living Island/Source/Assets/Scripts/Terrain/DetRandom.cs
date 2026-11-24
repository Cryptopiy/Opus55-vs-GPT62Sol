namespace LivingIsland
{
    /// <summary>
    /// Deterministic SplitMix64 random stream. System.Random / UnityEngine.Random are avoided so the
    /// island and every placement are bit-identical for a given seed on every machine and run.
    /// </summary>
    public struct DetRandom
    {
        ulong state;

        public DetRandom(int seed, int stream)
        {
            state = ((ulong)(uint)seed * 0x9E3779B97F4A7C15UL) ^ ((ulong)(uint)(stream + 1) * 0xD1B54A32D192ED03UL);
            NextULong();
        }

        public ulong NextULong()
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float Value => (NextULong() >> 40) * (1f / 16777216f);

        public float Range(float min, float max) => min + (max - min) * Value;

        /// <summary>Uniform int in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)((NextULong() >> 33) % (ulong)(maxExclusive - min));
        }

        /// <summary>Stateless hash of integer coordinates to [0,1) (used for stable per-cell jitter).</summary>
        public static float Hash01(int x, int y, int seed)
        {
            uint h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ (uint)seed * 0xcb1ab31fu;
            h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12; h *= 0x297a2d39u; h ^= h >> 15;
            return (h >> 8) * (1f / 16777216f);
        }
    }
}
