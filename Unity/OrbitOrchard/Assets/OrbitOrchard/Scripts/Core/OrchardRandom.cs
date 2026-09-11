namespace OrbitOrchard.Core
{
    /// <summary>SplitMix64 matching the Swift engine's wrapping UInt64 arithmetic.</summary>
    public struct SplitMix64
    {
        private ulong state;

        public SplitMix64(ulong seed) { state = seed; }

        public ulong NextUInt64()
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15ul;
                var value = state;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9ul;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBul;
                return value ^ (value >> 31);
            }
        }

        public double UnitDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);
        public double Range(double minimum, double maximum) => minimum + UnitDouble() * (maximum - minimum);
    }
}
