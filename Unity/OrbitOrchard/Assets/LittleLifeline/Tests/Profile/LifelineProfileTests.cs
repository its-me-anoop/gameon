using System;
using System.IO;
using LittleLifeline.Core;
using LittleLifeline.Services;
using NUnit.Framework;
using UnityEngine;

namespace LittleLifeline.Tests
{
    public sealed class LifelineProfileTests
    {
        private string directory;
        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
        private string SavePath => Path.Combine(directory, LifelineProfileStore.FileName);
        [SetUp] public void SetUp() { directory = Path.Combine(Path.GetTempPath(), "lifeline-tests-" + Guid.NewGuid().ToString("N")); }
        [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        private LifelineProfileStore OpenTrain()
        {
            var store = new LifelineProfileStore(directory);
            var profile = store.LoadCampaign(Start);
            new LifelineSimulation(profile.state).Advance(.1);
            Assert.That(store.Save(profile, Start), Is.True, store.Error);
            return store;
        }

        [Test] public void UnopenedTrainDoesNotEarnOrSkipItsIntroduction()
        {
            var store = new LifelineProfileStore(directory);
            store.LoadCampaign(Start);
            var reopened = new LifelineProfileStore(directory);
            var profile = reopened.LoadCampaign(Start.AddDays(2));
            Assert.That(profile.state.Tick, Is.Zero);
            Assert.That(profile.state.TotalCompleted, Is.Zero);
            Assert.That(reopened.LastOfflineReport.applied, Is.False);
        }

        [Test] public void FullStatePreferencesAndWeeklyBestSurviveRoundTrip()
        {
            var store = OpenTrain();
            var profile = store.Profile;
            new LifelineSimulation(profile.state).Advance(120);
            profile.preferences.sound = false;
            profile.preferences.haptics = false;
            profile.preferences.reducedMotion = true;
            profile.preferences.livery = "heritage";
            profile.AddWeeklyRecord("2026-09-14", 9000999L, Start.AddMinutes(2));
            Assert.That(store.Save(profile, Start.AddMinutes(2)), Is.True);
            var expected = JsonUtility.ToJson(profile.state);
            var loaded = new LifelineProfileStore(directory).LoadCampaign(Start.AddMinutes(2));
            Assert.That(JsonUtility.ToJson(loaded.state), Is.EqualTo(expected));
            Assert.That(loaded.preferences.sound, Is.False);
            Assert.That(loaded.preferences.haptics, Is.False);
            Assert.That(loaded.preferences.reducedMotion, Is.True);
            Assert.That(loaded.preferences.livery, Is.EqualTo("heritage"));
            Assert.That(loaded.WeeklyBest("2026-09-14"), Is.EqualTo(9000999));
        }

        [Test] public void OfflineUsesExactlyTheActiveSimulationAndCommitsBeforeExposingRewards()
        {
            var store = OpenTrain();
            var expected = JsonUtility.FromJson<SimulationState>(JsonUtility.ToJson(store.Profile.state));
            var advance = new LifelineSimulation(expected).Advance(3600);
            var report = store.ApplyOffline(Start.AddHours(1));
            Assert.That(report.applied, Is.True, store.Error);
            Assert.That(JsonUtility.ToJson(store.Profile.state), Is.EqualTo(JsonUtility.ToJson(expected)));
            Assert.That(report.coinsEarned, Is.EqualTo(advance.CoinsEarned));
            Assert.That(report.completed, Is.EqualTo(advance.Completed));
            Assert.That(report.elapsedSeconds, Is.EqualTo(3600));
            var reloaded = new LifelineProfileStore(directory);
            reloaded.LoadCampaign(Start.AddHours(1));
            Assert.That(reloaded.LastOfflineReport.applied, Is.False);
            Assert.That(JsonUtility.ToJson(reloaded.Profile.state), Is.EqualTo(JsonUtility.ToJson(expected)));
        }

        [Test] public void TwentyFourHourCapConsumesTheWholeWallClockGapOnce()
        {
            var store = OpenTrain();
            var expected = JsonUtility.FromJson<SimulationState>(JsonUtility.ToJson(store.Profile.state));
            new LifelineSimulation(expected).Advance(86400);
            var report = store.ApplyOffline(Start.AddDays(3));
            Assert.That(report.wasCapped, Is.True);
            Assert.That(report.elapsedSeconds, Is.EqualTo(86400));
            Assert.That(JsonUtility.ToJson(store.Profile.state), Is.EqualTo(JsonUtility.ToJson(expected)));
            Assert.That(store.Profile.lastAccountedUtcTicks, Is.EqualTo(Start.AddDays(3).UtcDateTime.Ticks));
            Assert.That(store.ApplyOffline(Start.AddDays(3)).applied, Is.False);
        }

        [Test] public void BackwardClockDoesNotRegressWatermarkOrCreditAgain()
        {
            var store = OpenTrain();
            store.ApplyOffline(Start.AddHours(2));
            var before = JsonUtility.ToJson(store.Profile.state);
            Assert.That(store.Save(store.Profile, Start.AddHours(-1)), Is.True);
            Assert.That(store.ApplyOffline(Start.AddHours(1)).applied, Is.False);
            Assert.That(store.Profile.lastAccountedUtcTicks, Is.EqualTo(Start.AddHours(2).UtcDateTime.Ticks));
            Assert.That(JsonUtility.ToJson(store.Profile.state), Is.EqualTo(before));
        }

        [Test] public void SavingActivePlayPreventsItBeingCreditedAgainAsOffline()
        {
            var store = OpenTrain();
            new LifelineSimulation(store.Profile.state).Advance(300);
            var before = JsonUtility.ToJson(store.Profile.state);
            Assert.That(store.Save(store.Profile, Start.AddMinutes(5)), Is.True);
            Assert.That(store.ApplyOffline(Start.AddMinutes(5)).applied, Is.False);
            Assert.That(JsonUtility.ToJson(store.Profile.state), Is.EqualTo(before));
        }

        [Test] public void FailedOfflineWriteLeavesCreditUnappliedAndCanRetryExactlyOnce()
        {
            var store = OpenTrain();
            var before = JsonUtility.ToJson(store.Profile);
            Directory.CreateDirectory(SavePath + ".tmp");
            Assert.That(store.ApplyOffline(Start.AddMinutes(5)).applied, Is.False);
            Assert.That(store.Error, Is.Not.Null);
            Assert.That(store.HasPendingOfflineProgress, Is.True);
            Assert.That(store.Save(store.Profile, Start.AddMinutes(5)), Is.False);
            Assert.That(JsonUtility.ToJson(store.Profile), Is.EqualTo(before));
            Directory.Delete(SavePath + ".tmp");
            Assert.That(store.ApplyOffline(Start.AddMinutes(5)).applied, Is.True);
            Assert.That(store.HasPendingOfflineProgress, Is.False);
            Assert.That(store.ApplyOffline(Start.AddMinutes(5)).applied, Is.False);
        }

        [Test] public void FailedActiveSaveStillPairsLiveStateWithItsAccountedTime()
        {
            var store = OpenTrain();
            new LifelineSimulation(store.Profile.state).Advance(300);
            var before = JsonUtility.ToJson(store.Profile.state);
            Directory.CreateDirectory(SavePath + ".tmp");
            Assert.That(store.Save(store.Profile, Start.AddMinutes(5)), Is.False);
            Assert.That(store.ApplyOffline(Start.AddMinutes(5)).applied, Is.False);
            Assert.That(JsonUtility.ToJson(store.Profile.state), Is.EqualTo(before));
        }

        [Test] public void CorruptPrimaryRecoversBackupAndPreservesUnreadableOriginal()
        {
            var store = OpenTrain();
            var backup = File.ReadAllText(SavePath);
            new LifelineSimulation(store.Profile.state).Advance(30);
            Assert.That(store.Save(store.Profile, Start), Is.True);
            File.WriteAllText(SavePath, "interrupted or corrupt bytes");
            var reopened = new LifelineProfileStore(directory);
            var recovered = reopened.LoadCampaign(Start);
            Assert.That(recovered.state.Tick, Is.EqualTo(1));
            Assert.That(reopened.Error, Does.Contain("backup"));
            Assert.That(Directory.GetFiles(directory, "*.unreadable-*").Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(File.ReadAllText(SavePath + ".backup"), Is.EqualTo(backup));
        }

        [Test] public void ModifiedPayloadWithUnchangedChecksumIsRejected()
        {
            OpenTrain();
            File.WriteAllText(SavePath, File.ReadAllText(SavePath).Replace("\\\"Coins\\\":100", "\\\"Coins\\\":999999"));
            var store = new LifelineProfileStore(directory);
            var loaded = store.LoadCampaign(Start);
            Assert.That(loaded.state.Coins, Is.EqualTo(100));
            Assert.That(store.Error, Is.Not.Null);
        }

        [Test] public void UncommittedTemporarySaveIsIgnoredAfterProcessStops()
        {
            var store = OpenTrain();
            var expected = JsonUtility.ToJson(store.Profile.state);
            File.WriteAllText(SavePath + ".tmp", "partial new snapshot");
            var loaded = new LifelineProfileStore(directory).LoadCampaign(Start);
            Assert.That(JsonUtility.ToJson(loaded.state), Is.EqualTo(expected));
        }

        [Test] public void WeeklyStateCannotOverwriteCampaignSave()
        {
            var store = OpenTrain();
            store.Profile.state.Mode = LittleLifeline.Core.SimulationMode.Weekly;
            Assert.That(store.Save(store.Profile, Start), Is.False);
            var loaded = new LifelineProfileStore(directory).LoadCampaign(Start);
            Assert.That(loaded.state.Mode, Is.EqualTo(LittleLifeline.Core.SimulationMode.Campaign));
        }

        [Test] public void BestWeeklyResultsAreBoundedAndCannotAddCampaignMoney()
        {
            var store = OpenTrain();
            var coins = store.Profile.state.Coins;
            for (var index = 0; index < 15; index++)
            {
                var date = Start.AddDays(7 * index);
                var key = LifelineWeek.Key(date);
                store.Profile.AddWeeklyRecord(key, 20, date);
                store.Profile.AddWeeklyRecord(key, 10, date);
                Assert.That(store.Profile.WeeklyBest(key), Is.EqualTo(20));
            }
            Assert.That(store.Profile.weeklyRecords.Count, Is.EqualTo(13));
            Assert.That(store.Profile.state.Coins, Is.EqualTo(coins));
            Assert.That(store.Profile.WeeklyBest("2026-09-14"), Is.Zero);
        }

        [TestCase("2026-09-20T23:59:59Z", "2026-09-14")]
        [TestCase("2026-09-21T00:00:00Z", "2026-09-21")]
        [TestCase("2026-10-26T00:30:00+02:00", "2026-10-19")]
        [TestCase("2026-03-30T00:30:00+01:00", "2026-03-23")]
        public void WeeklyIdentityUsesMondayUtcDespiteLocalOffsets(string instant, string expected)
        {
            var date = DateTimeOffset.Parse(instant);
            Assert.That(LifelineWeek.Key(date), Is.EqualTo(expected));
            Assert.That(LifelineWeek.Key(date), Is.EqualTo(LifelineRules.WeeklyIdentifier(date)));
        }

        [Test] public void WeeklyOccurrenceRejectsInvalidDatesAndEndsExclusively()
        {
            Assert.That(LifelineWeek.TryStart("2026-09-15", out _), Is.False);
            Assert.That(LifelineWeek.TryStart("2026-02-30", out _), Is.False);
            Assert.That(LifelineWeek.TryStart("2026-9-14", out _), Is.False);
            Assert.That(LifelineWeek.Contains("2026-09-14", Start.AddHours(-12)), Is.True);
            Assert.That(LifelineWeek.Contains("2026-09-14", Start.AddDays(7).AddHours(-12)), Is.False);
        }
    }
}
