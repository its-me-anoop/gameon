using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace OrbitOrchard.App.Tests
{
    public sealed class OrchardProfileTests
    {
        private string directory;
        private string path;
        private ProfileStore store;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "orbit-profile-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "orbit-orchard-unity-v1.json");
            store = new ProfileStore(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void FirstLaunchHasUsableDefaultsAndNoSaveError()
        {
            var profile = store.Load();
            Assert.That(profile.theme, Is.EqualTo("orchard"));
            Assert.That(profile.sound, Is.True);
            Assert.That(profile.haptics, Is.True);
            Assert.That(profile.daily, Is.Empty);
            Assert.That(profile.recent, Is.Empty);
            Assert.That(store.Error, Is.Null);
        }

        [Test]
        public void PreferencesAndRecordsRoundTripAcrossStoreInstances()
        {
            var profile = new OrchardProfile { theme = "cherry", sound = false, haptics = false, reducedMotion = true };
            profile.Record(Run(100, 5, "2026-09-11"), false);
            profile.Record(Run(250, 8, "2026-09-11"), true);
            Assert.That(store.Save(profile), Is.True);
            var restored = new ProfileStore(directory).Load();
            Assert.That(restored.best, Is.EqualTo(100));
            Assert.That(restored.classicRuns, Is.EqualTo(1));
            Assert.That(restored.DailyBest("2026-09-11"), Is.EqualTo(250));
            Assert.That(restored.totalSeeds, Is.EqualTo(13));
            Assert.That(restored.theme, Is.EqualTo("cherry"));
            Assert.That(restored.sound, Is.False);
            Assert.That(restored.haptics, Is.False);
            Assert.That(restored.reducedMotion, Is.True);
            Assert.That(restored.recent.Count, Is.EqualTo(2));
        }

        [Test]
        public void DailyAttemptsNeverChangeClassicBestOrRunCount()
        {
            var profile = new OrchardProfile();
            profile.Record(Run(100, 2, "2026-09-11"), false);
            profile.Record(Run(600, 3, "2026-09-11"), true);
            profile.Record(Run(400, 4, "2026-09-11"), true);
            Assert.That(profile.best, Is.EqualTo(100));
            Assert.That(profile.classicRuns, Is.EqualTo(1));
            Assert.That(profile.DailyBest("2026-09-11"), Is.EqualTo(600));
            Assert.That(profile.DailyBest("2026-09-12"), Is.Zero);
            Assert.That(profile.totalSeeds, Is.EqualTo(9), "Each completed attempt contributes its own banked seeds.");
        }

        [Test]
        public void HistoryIsCappedAndKeepsTheMostRecentUTCDays()
        {
            var profile = new OrchardProfile();
            var first = new DateTime(2026, 1, 1);
            for (var index = 0; index < 95; index++)
                profile.Record(Run(index + 1, 1, first.AddDays(index).ToString("yyyy-MM-dd")), true);
            Assert.That(profile.daily.Count, Is.EqualTo(90));
            Assert.That(profile.recent.Count, Is.EqualTo(20));
            Assert.That(profile.daily[0].score, Is.EqualTo(95));
            Assert.That(profile.DailyBest("2026-01-01"), Is.Zero);
        }

        [Test]
        public void PartialSchemaOneSavePreservesDefaultsForMissingFields()
        {
            File.WriteAllText(path, "{\"schema\":1,\"best\":120}");
            var profile = store.Load();
            Assert.That(profile.best, Is.EqualTo(120));
            Assert.That(profile.sound, Is.True);
            Assert.That(profile.haptics, Is.True);
            Assert.That(profile.theme, Is.EqualTo("orchard"));
            Assert.That(profile.daily, Is.Empty);
            Assert.That(profile.recent, Is.Empty);
        }

        [Test]
        public void MalformedNestedRecordsAreSanitizedWithoutLosingGoodRecords()
        {
            File.WriteAllText(path, "{\"schema\":1,\"theme\":\"unknown\",\"best\":-1,\"totalSeeds\":-10,\"daily\":[null,{\"day\":\"2026-09-11\",\"score\":40},{\"day\":\"2026-09-11\",\"score\":90},{\"day\":\"not-a-day\",\"score\":700}],\"recent\":[null,{\"score\":5,\"seeds\":1,\"seconds\":10,\"day\":\"2026-09-11\"}]}");
            var profile = store.Load();
            Assert.That(profile.best, Is.Zero);
            Assert.That(profile.totalSeeds, Is.Zero);
            Assert.That(profile.theme, Is.EqualTo("orchard"));
            Assert.That(profile.daily.Count, Is.EqualTo(1));
            Assert.That(profile.DailyBest("2026-09-11"), Is.EqualTo(90));
            Assert.That(profile.recent.Count, Is.EqualTo(1));
            Assert.That(profile.recent[0].score, Is.EqualTo(5));
            Assert.That(profile.recent[0].day, Is.EqualTo("2026-09-11"));
        }

        [Test]
        public void UndatedRecordsAreDiscardedWhileValidZeroScoreRunsRemain()
        {
            var profile = new OrchardProfile();
            // Unity's inline serializer can materialize a null list entry as a
            // default object, so a non-null reference alone is not sufficient.
            profile.recent.Add(new HarvestRecord());
            profile.recent.Add(Run(0, 0, "2026-09-10"));
            profile.Record(Run(100, 3, "2026-09-11"), false);
            Assert.That(profile.recent.Count, Is.EqualTo(2));
            Assert.That(profile.recent[1].score, Is.Zero);
            Assert.That(profile.recent[1].day, Is.EqualTo("2026-09-10"));
            profile.Record(new HarvestRecord(), false);
            profile.Record(Run(10, 1, "not-a-day"), false);
            Assert.That(profile.classicRuns, Is.EqualTo(1));
            Assert.That(profile.recent.Count, Is.EqualTo(2));
        }

        [Test]
        public void LastGoodBackupIsRecoveredWhenThePrimaryIsCorrupt()
        {
            Assert.That(store.Save(new OrchardProfile { best = 100 }), Is.True);
            Assert.That(store.Save(new OrchardProfile { best = 200 }), Is.True);
            File.WriteAllText(path, "corrupt primary");
            var recovered = store.Load();
            Assert.That(recovered.best, Is.EqualTo(100));
            Assert.That(store.Error, Does.Contain("backup"));
            Assert.That(Directory.GetFiles(directory, "*.unreadable-*").Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(File.ReadAllText(path), Is.EqualTo("corrupt primary"));
        }

        [Test]
        public void SavingAfterRecoveryDoesNotReplaceTheGoodBackupWithCorruption()
        {
            Assert.That(store.Save(new OrchardProfile { best = 100 }), Is.True);
            Assert.That(store.Save(new OrchardProfile { best = 200 }), Is.True);
            File.WriteAllText(path, "broken");
            var recovered = store.Load();
            recovered.best = 300;
            Assert.That(store.Save(recovered), Is.True);
            Assert.That(new ProfileStore(directory).Load().best, Is.EqualTo(300));
            Assert.That(JsonUtility.FromJson<OrchardProfile>(File.ReadAllText(path + ".backup")).best, Is.EqualTo(100));
        }

        [Test]
        public void BackupCanRecoverAMissingPrimaryFile()
        {
            File.WriteAllText(path + ".backup", JsonUtility.ToJson(new OrchardProfile { best = 800 }));
            Assert.That(store.Load().best, Is.EqualTo(800));
            Assert.That(store.Error, Does.Contain("backup"));
        }

        [Test]
        public void UnknownSchemaAndCorruptBackupArePreservedAndDoNotCrash()
        {
            File.WriteAllText(path, "{\"schema\":99,\"best\":800}");
            File.WriteAllText(path + ".backup", "broken backup");
            var profile = store.Load();
            Assert.That(profile.best, Is.Zero);
            Assert.That(store.Error, Is.Not.Null);
            Assert.That(File.ReadAllText(path), Does.Contain("\"schema\":99"));
            Assert.That(File.ReadAllText(path + ".backup"), Is.EqualTo("broken backup"));
        }

        [Test]
        public void FailedReplacementKeepsTheLastGoodPrimary()
        {
            Assert.That(store.Save(new OrchardProfile { best = 120 }), Is.True);
            Directory.CreateDirectory(path + ".tmp");
            Assert.That(store.Save(new OrchardProfile { best = 999 }), Is.False);
            Assert.That(new ProfileStore(directory).Load().best, Is.EqualTo(120));
            Assert.That(store.Error, Is.Not.Null);
        }

        [Test]
        public void AFailedSaveCanBeRetriedAndClearsTheError()
        {
            Directory.CreateDirectory(path + ".tmp");
            Assert.That(store.Save(new OrchardProfile { best = 99 }), Is.False);
            Directory.Delete(path + ".tmp");
            Assert.That(store.Save(new OrchardProfile { best = 99 }), Is.True);
            Assert.That(store.Error, Is.Null);
            Assert.That(store.Load().best, Is.EqualTo(99));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void CallerCannotMutateTheRecordedHistoryAfterTheFact()
        {
            var profile = new OrchardProfile();
            var run = Run(100, 3, "2026-09-11");
            profile.Record(run, false);
            run.score = 999;
            Assert.That(profile.recent[0].score, Is.EqualTo(100));
        }

        [Test]
        public void InvalidRunsAreIgnoredAndTotalsCannotWrapNegative()
        {
            var profile = new OrchardProfile { totalSeeds = int.MaxValue - 1, classicRuns = int.MaxValue };
            profile.Record(null, false);
            profile.Record(Run(-2, 0, "2026-09-11"), false);
            profile.Record(Run(100, 3, "invalid"), true);
            Assert.That(profile.best, Is.Zero);
            Assert.That(profile.recent, Is.Empty);
            profile.Record(Run(100, 3, "2026-09-11"), false);
            Assert.That(profile.classicRuns, Is.EqualTo(int.MaxValue));
            Assert.That(profile.totalSeeds, Is.EqualTo(int.MaxValue));
        }

        private static HarvestRecord Run(int score, int seeds, string day) =>
            new HarvestRecord { score = score, seeds = seeds, seconds = 90, day = day };
    }
}
