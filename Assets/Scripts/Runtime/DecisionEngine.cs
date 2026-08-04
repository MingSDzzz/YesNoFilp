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
            float force = UnityEngine.Mathf.Clamp01(strength);
            // Strength is a small nudge, not a hidden replacement for the badge's
            // configured probability. At a 50% baseline it can move YES only from
            // 45% to 55%; the taper keeps 0% and 100% absolute.
            float centeredBias = UnityEngine.Mathf.Lerp(-0.05f, 0.05f, force);
            float endpointTaper = 4f * baseline * (1f - baseline);
            return UnityEngine.Mathf.Clamp01(baseline + centeredBias * endpointTaper);
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
