using System;
using System.Security.Cryptography;

namespace DecisionDisc
{
    public static class DecisionEngine
    {
        public static bool Decide(float strength, DecisionMode mode)
        {
            strength = UnityEngine.Mathf.Clamp01(strength);
            if (mode == DecisionMode.StrengthInfluences)
            {
                double yesProbability = 0.25d + (0.5d * strength);
                return NextUnit() < yesProbability;
            }

            // Strength participates in the entropy, but cannot bias the 50/50 threshold.
            byte[] entropy = new byte[32 + sizeof(float)];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(entropy);
            Array.Copy(BitConverter.GetBytes(strength), 0, entropy, 32, sizeof(float));
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(entropy);
                return (hash[0] & 1) == 0;
            }
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
