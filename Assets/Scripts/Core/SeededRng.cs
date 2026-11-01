namespace ArmyRoguelike.Core
{
    /// <summary>
    /// A small seeded generator (SplitMix64) for a battle's random picks. Unlike System.Random its whole state is one
    /// number, so a copied battle can go on rolling exactly the same results.
    /// </summary>
    public sealed class SeededRng
    {
        private ulong _state;

        public SeededRng(int seed)
        {
            _state = (ulong)(uint)seed;
        }

        private SeededRng(ulong state)
        {
            _state = state;
        }

        /// <summary>A generator that rolls the same numbers from here on.</summary>
        public SeededRng Clone() => new SeededRng(_state);

        /// <summary>A number from 0 up to, but not including, <paramref name="max"/>; 0 when max is 0 or less.</summary>
        public int Next(int max)
        {
            if (max <= 0)
                return 0;
            _state += 0x9E3779B97F4A7C15UL;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (int)(((z >> 32) * (ulong)max) >> 32);
        }
    }
}
