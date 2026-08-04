using System;
using System.Security.Cryptography;

namespace DecisionDisc
{
    public static class DecisionEngine
    {
        public static bool Decide(float strength, DecisionMode mode, float baseYesProbability = 0.5f)
        {
            strength = UnityEngine.Mathf.Clamp01(strength);
            if (mode == DecisionMode.StrengthInfluences)
            {
                double yesProbability = EffectiveYesProbability(strength, mode, baseYesProbability);
                return NextUnit() < yesProbability;
            }

            // Fair mode is a direct cryptographic 50/50 draw. Strength is deliberately
            // ignored here so it cannot accidentally introduce a hidden bias.
            return NextUnit() < 0.5;
        }

        public static float EffectiveYesProbability(float strength, DecisionMode mode, float baseYesProbability)
        {
            if (mode == DecisionMode.Fair5050) return 0.5f;
            float baseline = UnityEngine.Mathf.Clamp01(baseYesProbability);
            if (baseline <= 0f || baseline >= 1f) return baseline;
            // Press strength belongs to presentation only: height, duration and
            // rotation speed. It must never alter the random source or probability.
            return baseline;
        }

        private static double NextUnit()
        {
            byte[] bytes = new byte[8];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            ulong value = BitConverter.ToUInt64(bytes, 0) >> 11;
            return value / (double)(1UL << 53);
        }
    }
}
