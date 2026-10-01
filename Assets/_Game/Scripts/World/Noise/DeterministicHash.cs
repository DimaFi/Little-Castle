namespace LittleCastle.World
{
    /// <summary>
    /// Stable hashing utilities for IDs and deterministic pseudo-random choices.
    /// Never use string.GetHashCode() for generated world identity.
    /// </summary>
    public static class DeterministicHash
    {
        public static int Hash32(int a, int b, int c, int d)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = Mix(h, (uint)a);
                h = Mix(h, (uint)b);
                h = Mix(h, (uint)c);
                h = Mix(h, (uint)d);
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (int)h;
            }
        }

        public static int String32(string value)
        {
            unchecked
            {
                uint h = 2166136261u;

                if (value != null)
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        char ch = value[i];
                        h = Mix(h, (byte)(ch & 0xFF));
                        h = Mix(h, (byte)(ch >> 8));
                    }
                }

                return (int)h;
            }
        }

        public static float Hash01(int a, int b, int c, int d)
        {
            unchecked
            {
                uint h = (uint)Hash32(a, b, c, d);
                return (h & 0x00FFFFFFu) / 16777215f;
            }
        }

        public static long StableId(int seed, int x, int z, int salt)
        {
            unchecked
            {
                uint hi = (uint)Hash32(seed, x, z, salt);
                uint lo = (uint)Hash32(seed ^ 0x6D2B79F5, z, x, salt ^ 0x1B873593);
                return (long)(((ulong)hi << 32) | lo);
            }
        }

        private static uint Mix(uint h, uint value)
        {
            return (h ^ value) * 16777619u;
        }
    }
}
