using System;
using NUnit.Framework;

namespace OrbitOrchard.Core.Tests
{
    public sealed class OrchardRandomTests
    {
        [Test]
        public void SplitMixMatchesTheSwiftUInt64GoldenFixture()
        {
            var random = new SplitMix64(42);
            var expected = new ulong[] { 13679457532755275413ul, 2949826092126892291ul, 5139283748462763858ul, 6349198060258255764ul, 701532786141963250ul };
            foreach (var value in expected) Assert.That(random.NextUInt64(), Is.EqualTo(value));
        }

        [Test]
        public void UnitDoubleAlwaysStaysBelowOne()
        {
            var random = new SplitMix64(ulong.MaxValue);
            for (var index = 0; index < 1000; index++) Assert.That(random.UnitDouble(), Is.InRange(0.0, 0.9999999999999999));
        }

        [Test]
        public void DailySeedUsesTheUTCDayAndMatchesSwift()
        {
            var morning = new DateTimeOffset(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);
            var evening = morning.AddDays(1).AddTicks(-1);
            Assert.That(DailySeed.Identifier(morning), Is.EqualTo("2026-09-11"));
            Assert.That(DailySeed.ForDate(morning), Is.EqualTo(7980448824916712818ul));
            Assert.That(DailySeed.ForDate(evening), Is.EqualTo(DailySeed.ForDate(morning)));
            Assert.That(DailySeed.ForDate(morning.AddDays(1)), Is.Not.EqualTo(DailySeed.ForDate(morning)));
            Assert.That(DailySeed.ForDate(morning.ToOffset(TimeSpan.FromHours(-8))), Is.EqualTo(DailySeed.ForDate(morning)));
        }
    }
}
