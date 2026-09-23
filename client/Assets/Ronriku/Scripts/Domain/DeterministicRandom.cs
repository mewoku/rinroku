using System;

namespace Ronriku.Domain
{
    /// <summary>
    /// Cross-platform deterministic RNG shared by puzzles and figures. The TypeScript port in
    /// packages/core must match bit for bit:
    ///   state = seed == 0 ? 0x9E3779B97F4A7C15 : seed            (unsigned 64-bit)
    ///   NextInt(n): v = state; v ^= v >> 12; v ^= v << 25; v ^= v >> 27; state = v;
    ///               return (int)((v * 2685821657736338717) mod 2^64 mod n)
    /// </summary>
    public struct DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(ulong seed) => _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;

        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            ulong value = _state;
            value ^= value >> 12;
            value ^= value << 25;
            value ^= value >> 27;
            _state = value;
            return (int)(unchecked(value * 2685821657736338717UL) % (ulong)exclusiveMax);
        }
    }
}
