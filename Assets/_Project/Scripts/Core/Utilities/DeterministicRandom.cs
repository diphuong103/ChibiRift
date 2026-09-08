using System;

namespace ChibiRift.Core
{
    /// <summary>
    /// Seeded RNG used by every gameplay random draw. Deliberately independent of
    /// <c>UnityEngine.Random</c>'s global state so a fixed seed reproduces a run exactly
    /// (RNG-003) and so upgrade rolls can be asserted in unit tests (NFR-008, RNG-004).
    /// </summary>
    /// <remarks>Implements xorshift128; identical sequence on every platform and Unity version.</remarks>
    public sealed class DeterministicRandom
    {
        private uint _x;
        private uint _y;
        private uint _z;
        private uint _w;

        /// <summary>The seed this instance was created with, so a run can be replayed (RNG-003).</summary>
        public int Seed { get; }

        /// <param name="seed">Any value. The same seed always yields the same sequence.</param>
        public DeterministicRandom(int seed)
        {
            Seed = seed;

            // Zero would make xorshift degenerate, so fold the seed into a non-zero state.
            unchecked
            {
                _x = (uint)seed ^ 0x9E3779B9u;
                _y = (uint)seed * 0x85EBCA6Bu + 0x165667B1u;
                _z = (uint)seed * 0xC2B2AE35u + 0x27D4EB2Fu;
                _w = (uint)seed ^ 0xD3A2646Cu;
                if ((_x | _y | _z | _w) == 0u) _x = 0x1u;
            }
        }

        /// <summary>Next raw 32-bit value.</summary>
        public uint NextUInt()
        {
            unchecked
            {
                uint t = _x ^ (_x << 11);
                _x = _y;
                _y = _z;
                _z = _w;
                _w = _w ^ (_w >> 19) ^ t ^ (t >> 8);
                return _w;
            }
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Uniform float in [minInclusive, maxExclusive).</summary>
        public float NextFloat(float minInclusive, float maxExclusive)
            => minInclusive + NextFloat() * (maxExclusive - minInclusive);

        /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
        /// <exception cref="ArgumentOutOfRangeException">Range is empty or inverted.</exception>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must exceed minInclusive.");

            uint range = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % range);
        }

        /// <summary>True with probability <paramref name="chance"/>. Used for the crit roll (COM-006).</summary>
        public bool NextBool(float chance) => NextFloat() < chance;
    }
}
