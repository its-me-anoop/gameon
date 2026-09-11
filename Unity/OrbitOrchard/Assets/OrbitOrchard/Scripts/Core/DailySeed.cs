using System;
using System.Globalization;
using System.Text;

namespace OrbitOrchard.Core
{
    /// <summary>Versioned daily routes shared with the native Swift game.</summary>
    public static class DailySeed
    {
        public static string Identifier(DateTimeOffset date) => date.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static ulong ForDate(DateTimeOffset date)
        {
            var bytes = Encoding.UTF8.GetBytes("orbit-orchard-v1:" + Identifier(date));
            unchecked
            {
                var hash = 0xCBF29CE484222325ul;
                foreach (var value in bytes)
                {
                    hash ^= value;
                    hash *= 0x00000100000001B3ul;
                }
                return hash;
            }
        }
    }
}
