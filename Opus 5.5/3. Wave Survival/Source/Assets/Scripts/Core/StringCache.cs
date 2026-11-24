namespace WaveSurvival
{
    /// <summary>Caches integer strings so HUD updates do not allocate every time a number changes.</summary>
    public static class StringCache
    {
        const int Size = 10000;
        static readonly string[] cache = new string[Size];

        public static string Int(int value)
        {
            if (value < 0 || value >= Size) return value.ToString();
            return cache[value] ??= value.ToString();
        }
    }
}
