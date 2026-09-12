using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using IdleClinic.Core;
using IdleClinic.Services;
using NUnit.Framework;
using UnityEngine;

namespace IdleClinic.Tests
{
    public sealed class ClinicProfileTests
    {
        private string directory;
        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);
        private string SavePath => Path.Combine(directory, ClinicProfileStore.FileName);
        [Serializable] private sealed class TestEnvelope { public int schemaVersion = 1; public string payload; public string checksum; }
        [SetUp] public void SetUp() { directory = Path.Combine(Path.GetTempPath(), "clinic-profile-tests-" + Guid.NewGuid().ToString("N")); }
        [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        private ClinicProfileStore OpenClinic(out DateTimeOffset now)
        {
            var store = new ClinicProfileStore(directory);
            var profile = store.LoadClinic(Start);
            var sim = new ClinicSimulation(profile.state);
            sim.Advance(25);
            Assert.That(sim.Collect(0).Success, Is.True);
            Assert.That(sim.HireNurse().Success, Is.True);
            sim.Advance(30);
            Assert.That(profile.state.Tutorial, Is.EqualTo(ClinicTutorialStep.Complete));
            now = Start.AddSeconds(55);
            Assert.That(store.Save(profile, now), Is.True, store.Error);
            return store;
        }

        private static ClinicState Clone(ClinicState state) => JsonUtility.FromJson<ClinicState>(JsonUtility.ToJson(state));

        [Test] public void FirstArrivalProgressesOfflineButCannotSkipCollectionOrHireTutorial()
        {
            var store = new ClinicProfileStore(directory);
            store.LoadClinic(Start);
            var reopened = new ClinicProfileStore(directory);
            var profile = reopened.LoadClinic(Start.AddHours(1));
            Assert.That(profile.state.Tutorial, Is.EqualTo(ClinicTutorialStep.CollectFirstPayment));
            Assert.That(profile.state.Wallet, Is.Zero);
            Assert.That(profile.state.ReceptionDesks[0].Till, Is.EqualTo(50));
            Assert.That(profile.state.TotalPayments, Is.EqualTo(1));
            Assert.That(profile.state.Staff.Count, Is.EqualTo(1));
        }

        [Test] public void TutorialCollectionAndCashSurviveWithoutReplayingFirstPayment()
        {
            var store = new ClinicProfileStore(directory);
            var profile = store.LoadClinic(Start);
            var sim = new ClinicSimulation(profile.state);
            sim.Advance(25);
            Assert.That(sim.Collect(0).Success, Is.True);
            Assert.That(store.Save(profile, Start.AddSeconds(25)), Is.True);
            var reopened = new ClinicProfileStore(directory);
            profile = reopened.LoadClinic(Start.AddSeconds(25));
            Assert.That(profile.state.Tutorial, Is.EqualTo(ClinicTutorialStep.HireFirstNurse));
            Assert.That(profile.state.Wallet, Is.EqualTo(50));
            Assert.That(profile.state.ReceptionDesks[0].Till, Is.Zero);
            Assert.That(new ClinicSimulation(profile.state).Collect(0).Success, Is.False);
            Assert.That(profile.state.Wallet, Is.EqualTo(50));
        }

        [Test] public void FullStateAndPreferencesRoundTripExactly()
        {
            var store = OpenClinic(out var now);
            var profile = store.Profile;
            new ClinicSimulation(profile.state).Advance(27.35);
            profile.preferences.sound = false; profile.preferences.haptics = false; profile.preferences.reducedMotion = true;
            now = now.AddSeconds(27.35);
            Assert.That(store.Save(profile, now), Is.True);
            var expected = JsonUtility.ToJson(profile.state);
            var loaded = new ClinicProfileStore(directory).LoadClinic(now);
            Assert.That(JsonUtility.ToJson(loaded.state), Is.EqualTo(expected));
            Assert.That(loaded.preferences.sound, Is.False);
            Assert.That(loaded.preferences.haptics, Is.False);
            Assert.That(loaded.preferences.reducedMotion, Is.True);
        }

        [Test] public void OfflineMatchesCoreAndCommitsBeforeResultsAreExposed()
        {
            var store = OpenClinic(out var now);
            var expected = Clone(store.Profile.state);
            var advanced = new ClinicSimulation(expected).AdvanceOffline(3600);
            var wallet = store.Profile.state.Wallet;
            var report = store.ApplyOffline(now.AddHours(1));
            Assert.That(report.applied, Is.True, store.Error);
            Assert.That(JsonUtility.ToJson(store.Profile.state), Is.EqualTo(JsonUtility.ToJson(expected)));
            Assert.That(report.paymentsReceived, Is.EqualTo(advanced.PaymentsReceived));
            Assert.That(report.tillEarned, Is.EqualTo(advanced.TillEarned));
            Assert.That(report.treatmentsCompleted, Is.EqualTo(advanced.TreatmentsCompleted));
            Assert.That(store.Profile.state.Wallet, Is.EqualTo(wallet), "Offline earnings remain in reception tills until collection.");
            var reopened = new ClinicProfileStore(directory);
            reopened.LoadClinic(now.AddHours(1));
            Assert.That(reopened.LastOfflineReport.applied, Is.False);
            Assert.That(JsonUtility.ToJson(reopened.Profile.state), Is.EqualTo(JsonUtility.ToJson(expected)));
        }

        [Test] public void EarningsCapConsumesWholeAbsenceAndConstructionUsesFullClock()
        {
            var store = OpenClinic(out var now);
            var sim = new ClinicSimulation(store.Profile.state);
            sim.Advance(600); Assert.That(sim.Collect(0).Success, Is.True); now = now.AddSeconds(600);
            Assert.That(sim.Renovate(ClinicRoom.FirstAid).Success, Is.True);
            Assert.That(store.Save(store.Profile, now), Is.True);
            var expected = Clone(store.Profile.state);
            new ClinicSimulation(expected).AdvanceOffline(3 * 24 * 3600);
            var beforeWallet = store.Profile.state.Wallet;
            var report = store.ApplyOffline(now.AddDays(3));
            Assert.That(report.wasCapped, Is.True);
            Assert.That(report.earningsSeconds, Is.EqualTo(8 * 3600));
            Assert.That(report.constructionSeconds, Is.EqualTo(3 * 24 * 3600));
            Assert.That(store.Profile.state.Construction, Is.Empty);
            Assert.That(store.Profile.state.Room(ClinicRoom.FirstAid).Tier, Is.EqualTo(2));
            Assert.That(store.Profile.state.Wallet, Is.EqualTo(beforeWallet));
            Assert.That(JsonUtility.ToJson(store.Profile.state), Is.EqualTo(JsonUtility.ToJson(expected)));
            Assert.That(store.ApplyOffline(now.AddDays(3)).applied, Is.False);
            Assert.That(store.Profile.lastAccountedUtcTicks, Is.EqualTo(now.AddDays(3).UtcDateTime.Ticks));
        }

        [Test] public void PartialConstructionSurvivesProcessRestartAndChargesOnce()
        {
            var store = OpenClinic(out var now);
            var sim = new ClinicSimulation(store.Profile.state);
            sim.Advance(600); Assert.That(sim.Collect(0).Success, Is.True); now = now.AddSeconds(600);
            Assert.That(sim.Renovate(ClinicRoom.FirstAid).Success, Is.True);
            sim.Advance(20); now = now.AddSeconds(20);
            var paidWallet = store.Profile.state.Wallet;
            Assert.That(store.Save(store.Profile, now), Is.True);
            var reopened = new ClinicProfileStore(directory);
            var profile = reopened.LoadClinic(now.AddSeconds(41));
            Assert.That(profile.state.Room(ClinicRoom.FirstAid).Tier, Is.EqualTo(2));
            Assert.That(profile.state.Construction, Is.Empty);
            Assert.That(profile.state.Wallet, Is.EqualTo(paidWallet));
            Assert.That(new ClinicProfileStore(directory).LoadClinic(now.AddSeconds(41)).state.Wallet, Is.EqualTo(paidWallet));
        }

        [Test] public void BackwardClockCannotRegressWatermarkOrCreditAgain()
        {
            var store = OpenClinic(out var now);
            store.ApplyOffline(now.AddHours(2));
            var before = JsonUtility.ToJson(store.Profile.state);
            Assert.That(store.Save(store.Profile, now.AddHours(-1)), Is.True);
            Assert.That(store.ApplyOffline(now.AddHours(1)).applied, Is.False);
            Assert.That(store.Profile.lastAccountedUtcTicks, Is.EqualTo(now.AddHours(2).UtcDateTime.Ticks));
            Assert.That(JsonUtility.ToJson(store.Profile.state), Is.EqualTo(before));
        }

        [Test] public void ActiveSavePairsProgressWithTimeAndAvoidsOfflineDoubleCredit()
        {
            var store = OpenClinic(out var now);
            new ClinicSimulation(store.Profile.state).Advance(300);
            var before = JsonUtility.ToJson(store.Profile.state);
            Assert.That(store.Save(store.Profile, now.AddMinutes(5)), Is.True);
            Assert.That(store.ApplyOffline(now.AddMinutes(5)).applied, Is.False);
            Assert.That(JsonUtility.ToJson(store.Profile.state), Is.EqualTo(before));
        }

        [Test] public void FailedOfflineCommitBlocksOtherSavesAndRetriesExactlyOnce()
        {
            var store = OpenClinic(out var now);
            var before = JsonUtility.ToJson(store.Profile);
            Directory.CreateDirectory(SavePath + ".tmp");
            Assert.That(store.ApplyOffline(now.AddHours(1)).applied, Is.False);
            Assert.That(store.HasPendingOfflineProgress, Is.True);
            Assert.That(store.Save(store.Profile, now.AddHours(1)), Is.False);
            Assert.That(JsonUtility.ToJson(store.Profile), Is.EqualTo(before));
            Directory.Delete(SavePath + ".tmp");
            Assert.That(store.ApplyOffline(now.AddHours(1)).applied, Is.True);
            Assert.That(store.HasPendingOfflineProgress, Is.False);
            Assert.That(store.ApplyOffline(now.AddHours(1)).applied, Is.False);
        }

        [Test] public void PendingCommitRetainsItsIntervalWhenClockMovesBackwards()
        {
            var store = OpenClinic(out var now);
            Directory.CreateDirectory(SavePath + ".tmp");
            Assert.That(store.ApplyOffline(now.AddHours(2)).applied, Is.False);
            Directory.Delete(SavePath + ".tmp");
            var retried = store.ApplyOffline(now.AddHours(1));
            Assert.That(retried.applied, Is.True);
            Assert.That(retried.elapsedSeconds, Is.EqualTo(7200));
            Assert.That(store.Profile.lastAccountedUtcTicks, Is.EqualTo(now.AddHours(2).UtcDateTime.Ticks));
        }

        [Test] public void FailedActiveSaveDoesNotRecreditUnsavedLiveTicks()
        {
            var store = OpenClinic(out var now);
            new ClinicSimulation(store.Profile.state).Advance(300);
            var before = JsonUtility.ToJson(store.Profile.state);
            Directory.CreateDirectory(SavePath + ".tmp");
            Assert.That(store.Save(store.Profile, now.AddMinutes(5)), Is.False);
            Assert.That(store.ApplyOffline(now.AddMinutes(5)).applied, Is.False);
            Assert.That(JsonUtility.ToJson(store.Profile.state), Is.EqualTo(before));
        }

        [Test] public void StalePreResumeReferenceCannotOverwriteCommittedOfflineProgress()
        {
            var store = OpenClinic(out var now); var stale = store.Profile;
            Assert.That(store.ApplyOffline(now.AddMinutes(5)).applied, Is.True);
            var before = File.ReadAllText(SavePath);
            Assert.That(store.Save(stale, now.AddMinutes(5)), Is.False);
            Assert.That(File.ReadAllText(SavePath), Is.EqualTo(before));
        }

        [Test] public void CorruptPrimaryRecoversBackupAndPreservesOriginal()
        {
            var store = OpenClinic(out var now);
            var expected = JsonUtility.ToJson(store.Profile.state);
            var backup = File.ReadAllText(SavePath);
            new ClinicSimulation(store.Profile.state).Advance(10);
            Assert.That(store.Save(store.Profile, now.AddSeconds(10)), Is.True);
            File.WriteAllText(SavePath, "interrupted clinic write");
            var reopened = new ClinicProfileStore(directory);
            var profile = reopened.LoadClinic(now);
            Assert.That(JsonUtility.ToJson(profile.state), Is.EqualTo(expected));
            Assert.That(reopened.Error, Does.Contain("backup"));
            Assert.That(File.ReadAllText(SavePath + ".backup"), Is.EqualTo(backup));
            Assert.That(Directory.GetFiles(directory, "*.unreadable-*").Length, Is.GreaterThanOrEqualTo(1));
        }

        [Test] public void ChangedPayloadWithOldChecksumIsRejected()
        {
            var store = new ClinicProfileStore(directory); store.LoadClinic(Start);
            var original = File.ReadAllText(SavePath);
            var changed = original.Replace("\\\"Wallet\\\":0", "\\\"Wallet\\\":9000");
            Assert.That(changed, Is.Not.EqualTo(original)); File.WriteAllText(SavePath, changed);
            var reopened = new ClinicProfileStore(directory); var profile = reopened.LoadClinic(Start);
            Assert.That(profile.state.Wallet, Is.Zero); Assert.That(reopened.Error, Is.Not.Null);
        }

        [Test] public void AbandonedTemporaryFileCannotBecomeAClaimedSnapshot()
        {
            var store = OpenClinic(out var now);
            var before = JsonUtility.ToJson(store.Profile.state);
            File.WriteAllText(SavePath + ".tmp", "unfinished future progress");
            var profile = new ClinicProfileStore(directory).LoadClinic(now);
            Assert.That(JsonUtility.ToJson(profile.state), Is.EqualTo(before));
        }

        [Test] public void MigrationCopiesOnlyPreferencesAndNeverMutatesPreviousCampaign()
        {
            Directory.CreateDirectory(directory);
            var legacyPath = Path.Combine(directory, "little-lifeline-profile-v1.json");
            var payload = "{\"schemaVersion\":1,\"state\":{\"Coins\":999999,\"Tick\":99999},\"preferences\":{\"sound\":false,\"haptics\":false,\"reducedMotion\":true,\"livery\":\"heritage\"},\"IsPassOwned\":true}";
            WriteEnvelope(legacyPath, payload);
            var before = File.ReadAllText(legacyPath);
            var profile = new ClinicProfileStore(directory).LoadClinic(Start);
            Assert.That(profile.preferences.sound, Is.False); Assert.That(profile.preferences.haptics, Is.False);
            Assert.That(profile.preferences.reducedMotion, Is.True);
            Assert.That(profile.state.Wallet, Is.Zero); Assert.That(profile.state.Tick, Is.Zero);
            Assert.That(profile.state.Tutorial, Is.EqualTo(ClinicTutorialStep.FirstArrival));
            Assert.That(File.ReadAllText(legacyPath), Is.EqualTo(before));
            Assert.That(File.Exists(legacyPath + ".backup"), Is.False);
            Assert.That(JsonUtility.ToJson(profile), Does.Not.Contain("IsPassOwned"));
            Assert.That(JsonUtility.ToJson(profile), Does.Not.Contain("heritage"));
        }

        [Test] public void CorruptLegacyPrimaryFallsBackToPreferenceBackupWithoutChangingEither()
        {
            Directory.CreateDirectory(directory);
            var legacyPath = Path.Combine(directory, "little-lifeline-profile-v1.json");
            File.WriteAllText(legacyPath, "corrupt old save");
            WriteEnvelope(legacyPath + ".backup", "{\"schemaVersion\":1,\"preferences\":{\"sound\":false,\"haptics\":true,\"reducedMotion\":true}}");
            var backup = File.ReadAllText(legacyPath + ".backup");
            var profile = new ClinicProfileStore(directory).LoadClinic(Start);
            Assert.That(profile.preferences.sound, Is.False); Assert.That(profile.preferences.haptics, Is.True);
            Assert.That(File.ReadAllText(legacyPath), Is.EqualTo("corrupt old save"));
            Assert.That(File.ReadAllText(legacyPath + ".backup"), Is.EqualTo(backup));
        }

        [Test] public void NewClinicPreferencesTakePrecedenceAfterFirstMigration()
        {
            Directory.CreateDirectory(directory);
            var legacyPath = Path.Combine(directory, "little-lifeline-profile-v1.json");
            WriteEnvelope(legacyPath, "{\"schemaVersion\":1,\"preferences\":{\"sound\":false,\"haptics\":false,\"reducedMotion\":true}}");
            var store = new ClinicProfileStore(directory); var profile = store.LoadClinic(Start);
            profile.preferences.sound = true; Assert.That(store.Save(profile, Start), Is.True);
            WriteEnvelope(legacyPath, "{\"schemaVersion\":1,\"preferences\":{\"sound\":false,\"haptics\":true,\"reducedMotion\":false}}");
            Assert.That(new ClinicProfileStore(directory).LoadClinic(Start).preferences.sound, Is.True);
        }

        private static void WriteEnvelope(string path, string payload)
        {
            using (var hash = SHA256.Create())
            {
                var envelope = new TestEnvelope { payload = payload, checksum = Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))) };
                File.WriteAllText(path, JsonUtility.ToJson(envelope));
            }
        }
    }
}
