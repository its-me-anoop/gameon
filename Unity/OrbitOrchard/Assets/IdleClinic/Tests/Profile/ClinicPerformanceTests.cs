using System;
using IdleClinic.Services;
using NUnit.Framework;

namespace IdleClinic.Tests
{
    public sealed class ClinicPerformanceTests
    {
        [Test] public void PercentilesUseRecordedFramesIncludingLongHitches()
        {
            var window = new ClinicFrameWindow(100);
            for (var i = 1; i <= 100; i++) window.AddSeconds(i / 1000d);
            var result = window.Summarize();
            Assert.That(result.frameCount, Is.EqualTo(100));
            Assert.That(result.medianFrameMs, Is.EqualTo(50.5).Within(.00001));
            Assert.That(result.p95FrameMs, Is.EqualTo(95).Within(.00001));
            Assert.That(result.maximumFrameMs, Is.EqualTo(100).Within(.00001));
            Assert.That(result.elapsedSeconds, Is.EqualTo(5.05).Within(.00001));
        }
        [Test] public void RollingWindowEvictsOnlyTheOldestFrames()
        {
            var window = new ClinicFrameWindow(3);
            window.AddSeconds(.2); window.AddSeconds(.01); window.AddSeconds(.02); window.AddSeconds(.03);
            var result = window.Summarize();
            Assert.That(result.frameCount, Is.EqualTo(3));
            Assert.That(result.medianFrameMs, Is.EqualTo(20).Within(.00001));
            Assert.That(result.maximumFrameMs, Is.EqualTo(30).Within(.00001));
            Assert.That(result.elapsedSeconds, Is.EqualTo(.06).Within(.00001));
        }
        [Test] public void InvalidSamplesDoNotPolluteResultsAndSummaryDoesNotConsumeFrames()
        {
            var window = new ClinicFrameWindow(2);
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, 0, -1 })
                Assert.That(window.AddSeconds(value), Is.False);
            Assert.That(window.Summarize().frameCount, Is.Zero);
            window.AddSeconds(.016);
            Assert.That(window.Summarize().p95FrameMs, Is.EqualTo(window.Summarize().medianFrameMs));
            Assert.That(window.Count, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new ClinicFrameWindow(3601));
        }
    }
}
