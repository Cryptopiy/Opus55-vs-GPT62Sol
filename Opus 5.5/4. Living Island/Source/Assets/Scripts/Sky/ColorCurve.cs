using UnityEngine;

namespace LivingIsland
{
    /// <summary>Allocation-free piecewise-linear colour (or scalar) curve over a float key.</summary>
    public sealed class ColorCurve
    {
        readonly float[] keys;
        readonly Color[] values;

        public ColorCurve(float[] keys, Color[] values)
        {
            this.keys = keys;
            this.values = values;
        }

        public Color Evaluate(float t)
        {
            if (t <= keys[0]) return values[0];
            int last = keys.Length - 1;
            if (t >= keys[last]) return values[last];
            for (int i = 0; i < last; i++)
            {
                if (t <= keys[i + 1])
                {
                    float f = (t - keys[i]) / (keys[i + 1] - keys[i]);
                    f = f * f * (3f - 2f * f); // smooth between keys: no visible "kinks" in colour over time
                    return Color.LerpUnclamped(values[i], values[i + 1], f);
                }
            }
            return values[last];
        }
    }
}
