using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using IdleClinic.Core;
using IdleClinic.Services;
using NUnit.Framework;
using UnityEngine;

namespace IdleClinic.Tests
{
    public sealed class ClinicMigrationTests
    {
        private string directory;
        private string path;
        private string originalEnvelope;
        private ClinicProfile legacy;
        private DateTimeOffset savedAt;
        [Serializable] private sealed class Envelope { public int schemaVersion = 1; public string payload; public string checksum; }

        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "clinic-migration-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, ClinicProfileStore.FileName);
            // Genuine installed 3.1(15) simulator save, captured before the 3.2 update. No generated replacement state.
            originalEnvelope = File.ReadAllText(Path.Combine(Application.dataPath, "IdleClinic/Tests/Profile/Fixtures/clinic-3.1.json"));
            File.WriteAllText(path, originalEnvelope);
            var envelope = JsonUtility.FromJson<Envelope>(originalEnvelope);
            Assert.That(Digest(envelope.payload), Is.EqualTo(envelope.checksum));
            legacy = JsonUtility.FromJson<ClinicProfile>(envelope.payload);
            savedAt = new DateTimeOffset(legacy.lastAccountedUtcTicks, TimeSpan.Zero);
        }
        [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        [Test] public void GenuineVersionOneSnapshotMigratesAtomicallyWithoutChangingMoneyServicesOrPreferences()
        {
            var store = new ClinicProfileStore(directory);
            var loaded = store.LoadClinic(savedAt);
            Assert.That(loaded.schemaVersion, Is.EqualTo(3));
            Assert.That(loaded.state.SchemaVersion, Is.EqualTo(3));
            Assert.That(loaded.state.RulesVersion, Is.EqualTo(3));
            Assert.That(loaded.revision, Is.EqualTo(legacy.revision + 1));
            Assert.That(loaded.lastAccountedUtcTicks, Is.EqualTo(legacy.lastAccountedUtcTicks));
            Assert.That(loaded.state.Wallet, Is.EqualTo(3807));
            Assert.That(loaded.state.ReceptionDesks.Sum(d => d.Till), Is.EqualTo(11700));
            Assert.That(loaded.state.TotalEarned, Is.EqualTo(legacy.state.TotalEarned));
            Assert.That(loaded.state.TotalSpent, Is.EqualTo(legacy.state.TotalSpent));
            Assert.That(loaded.state.TotalCollected, Is.EqualTo(legacy.state.TotalCollected));
            Assert.That(loaded.state.Tick, Is.EqualTo(legacy.state.Tick));
            Assert.That(loaded.state.SubTick, Is.EqualTo(legacy.state.SubTick));
            Assert.That(loaded.state.Tutorial, Is.EqualTo(legacy.state.Tutorial));
            Assert.That(loaded.preferences.sound, Is.EqualTo(legacy.preferences.sound));
            Assert.That(loaded.preferences.music, Is.EqualTo(legacy.preferences.sound));
            Assert.That(loaded.preferences.haptics, Is.EqualTo(legacy.preferences.haptics));
            Assert.That(loaded.preferences.reducedMotion, Is.EqualTo(legacy.preferences.reducedMotion));
            Assert.That(loaded.state.Amenities.All(a => a.Level == 0 && a.Till == 0), Is.True);
            Assert.That(loaded.state.Staff.All(s => s.TrainingLevel == 1), Is.True);
            Assert.That(loaded.state.ReceptionDesks.All(d => d.EquipmentLevel == 1), Is.True);
            Assert.That(loaded.state.TreatmentStations.Count, Is.EqualTo(2));
            foreach (var patient in legacy.state.Patients)
            {
                var restored = loaded.state.Patients.Single(p => p.Id == patient.Id);
                Assert.That(restored.Phase, Is.EqualTo(patient.Phase));
                Assert.That(restored.FromAnchor, Is.EqualTo(patient.FromAnchor));
                Assert.That(restored.ToAnchor, Is.EqualTo(patient.ToAnchor));
                Assert.That(restored.PhaseStartedTick, Is.EqualTo(patient.PhaseStartedTick));
                Assert.That(restored.PhaseEndsTick, Is.EqualTo(patient.PhaseEndsTick));
                Assert.That(restored.Payment, Is.EqualTo(patient.Payment));
                Assert.That(restored.HasAdmissionReservation, Is.EqualTo(patient.HasAdmissionReservation));
                Assert.That(restored.SeatId, Is.EqualTo(patient.SeatId));
                Assert.That(restored.ParkingBayId, Is.EqualTo(-1));
            }
            Assert.That(File.ReadAllText(path + ".backup"), Is.EqualTo(originalEnvelope));
            Assert.That(store.LastOfflineReport.applied, Is.False);
            Assert.That(ClinicSimulation.IsValidState(loaded.state), Is.True);
            Assert.That(JsonUtility.ToJson(new ClinicProfileStore(directory).LoadClinic(savedAt)), Is.EqualTo(JsonUtility.ToJson(loaded)));
        }

        [Test] public void MigrationCommitsFirstThenAdvancesAnAbsenceOnce()
        {
            var expected = JsonUtility.FromJson<ClinicState>(JsonUtility.ToJson(legacy.state));
            Assert.That(ClinicStateMigration.TryMigrateV1(expected), Is.True);
            new ClinicSimulation(expected).AdvanceOffline(600);
            var store = new ClinicProfileStore(directory);
            var loaded = store.LoadClinic(savedAt.AddMinutes(10));
            Assert.That(loaded.revision, Is.EqualTo(legacy.revision + 2));
            Assert.That(JsonUtility.ToJson(loaded.state), Is.EqualTo(JsonUtility.ToJson(expected)));
            Assert.That(store.LastOfflineReport.applied, Is.True);
            Assert.That(File.ReadAllText(path + ".v1-before-migration-" + legacy.revision), Is.EqualTo(originalEnvelope));
            var reopened = new ClinicProfileStore(directory);
            reopened.LoadClinic(savedAt.AddMinutes(10));
            Assert.That(reopened.LastOfflineReport.applied, Is.False);
            Assert.That(JsonUtility.ToJson(reopened.Profile.state), Is.EqualTo(JsonUtility.ToJson(expected)));
        }

        [Test] public void FailedMigrationCommitPreservesVersionOneAndBlocksOfflineUntilAtomicRetry()
        {
            Directory.CreateDirectory(path + ".tmp");
            var store = new ClinicProfileStore(directory);
            var loaded = store.LoadClinic(savedAt.AddMinutes(10));
            Assert.That(store.HasPendingOfflineProgress, Is.True);
            Assert.That(store.LastOfflineReport.applied, Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(originalEnvelope));
            Assert.That(loaded.state.Tick, Is.EqualTo(legacy.state.Tick));
            Assert.That(loaded.revision, Is.EqualTo(legacy.revision));
            Assert.That(store.Save(loaded, savedAt.AddMinutes(10)), Is.False);
            Directory.Delete(path + ".tmp");
            Assert.That(store.ApplyOffline(savedAt.AddMinutes(5)).applied, Is.True);
            Assert.That(store.Profile.revision, Is.EqualTo(legacy.revision + 2));
            Assert.That(store.Profile.lastAccountedUtcTicks, Is.EqualTo(savedAt.AddMinutes(10).UtcDateTime.Ticks));
            Assert.That(store.ApplyOffline(savedAt.AddMinutes(10)).applied, Is.False);
        }

        [Test] public void VersionOnePayloadCannotMigrateBeforeItsEnvelopeChecksumIsVerified()
        {
            var changed = originalEnvelope.Replace("\\\"Wallet\\\":3807", "\\\"Wallet\\\":999999");
            Assert.That(changed, Is.Not.EqualTo(originalEnvelope));
            File.WriteAllText(path, changed);
            File.WriteAllText(path + ".backup", originalEnvelope);
            var store = new ClinicProfileStore(directory);
            var loaded = store.LoadClinic(savedAt);
            Assert.That(loaded.state.Wallet, Is.EqualTo(3807));
            Assert.That(loaded.schemaVersion, Is.EqualTo(3));
            Assert.That(store.Error, Does.Contain("backup"));
            Assert.That(Directory.GetFiles(directory, "*.unreadable-*").Any(f => File.ReadAllText(f) == changed), Is.True);
        }

        [Test] public void ChecksumValidButNonsensicalVersionOneCannotBeUpgraded()
        {
            var envelope = JsonUtility.FromJson<Envelope>(originalEnvelope);
            envelope.payload = envelope.payload.Replace("\"Wallet\":3807", "\"Wallet\":3808");
            envelope.checksum = Digest(envelope.payload);
            var invalid = JsonUtility.ToJson(envelope);
            File.WriteAllText(path, invalid);
            File.WriteAllText(path + ".backup", originalEnvelope);
            var store = new ClinicProfileStore(directory);
            Assert.That(store.LoadClinic(savedAt).state.Wallet, Is.EqualTo(3807));
            Assert.That(store.Error, Does.Contain("backup"));
            Assert.That(Directory.GetFiles(directory, "*.unreadable-*").Any(f => File.ReadAllText(f) == invalid), Is.True);
        }

        [Test] public void NewExpansionProgressAndAnInFlightVisitSurviveRelaunchWithoutDuplicatingTips()
        {
            var store = new ClinicProfileStore(directory); var profile = store.LoadClinic(savedAt);
            var sim = new ClinicSimulation(profile.state);
            Assert.That(sim.UpgradeAmenity(ClinicAmenity.Parking).Success, Is.True);
            Assert.That(sim.UpgradeAmenity(ClinicAmenity.Toilet).Success, Is.True);
            Assert.That(sim.UpgradeAmenity(ClinicAmenity.Vending).Success, Is.True);
            Assert.That(sim.UpgradeStation(ClinicStaffRole.Receptionist, 1).Success, Is.True);
            Assert.That(sim.TrainStaff(101).Success, Is.True);
            for (var i = 0; i < 2400 && !sim.State.Patients.Any(p => p.Phase == ClinicPatientPhase.UsingAmenity); i++) sim.Advance(.1);
            Assert.That(sim.State.Patients.Any(p => p.Phase == ClinicPatientPhase.UsingAmenity), Is.True);
            var now = savedAt.AddSeconds((sim.State.Tick - legacy.state.Tick) / 10d);
            Assert.That(store.Save(profile, now), Is.True);
            var expected = JsonUtility.FromJson<ClinicState>(JsonUtility.ToJson(sim.State));
            new ClinicSimulation(expected).AdvanceOffline(120);
            var reopened = new ClinicProfileStore(directory); var restored = reopened.LoadClinic(now.AddSeconds(120));
            Assert.That(JsonUtility.ToJson(restored.state), Is.EqualTo(JsonUtility.ToJson(expected)));
            var resumed = new ClinicSimulation(restored.state); var tips = resumed.State.Amenity(ClinicAmenity.Vending).Till;
            if (tips > 0) { Assert.That(resumed.CollectVendingTips().Amount, Is.EqualTo(tips)); Assert.That(resumed.CollectVendingTips().Success, Is.False); }
            Assert.That(reopened.Save(restored, now.AddSeconds(120)), Is.True);
            Assert.That(new ClinicSimulation(new ClinicProfileStore(directory).LoadClinic(now.AddSeconds(120)).state).CollectVendingTips().Success, Is.False);
        }

        private static string Digest(string payload) { using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))); }
    }
}
