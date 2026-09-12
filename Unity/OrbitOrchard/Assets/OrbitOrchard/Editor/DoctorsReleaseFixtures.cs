using System;
using System.IO;
using System.Linq;
using System.Reflection;
using IdleClinic.Core;
using IdleClinic.Services;
using UnityEditor;
using UnityEngine;

namespace OrbitOrchard.Editor
{
    /// <summary>Explicit, local QA campaigns earned through the same commands as gameplay.</summary>
    public static class DoctorsReleaseFixtures
    {
        public static void CaptureAndWrite()
        {
            Write();
            ClinicWorldPreview.Capture();
        }

        public static void Write()
        {
            var output = Environment.GetEnvironmentVariable("CLINIC_QA_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("CLINIC_QA_OUTPUT must name a dedicated QA directory.");
            output = Path.GetFullPath(output);
            var build = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../build")) + Path.DirectorySeparatorChar;
            if (!output.StartsWith(build, StringComparison.Ordinal)) throw new InvalidOperationException("QA fixtures must stay inside this checkout's build directory.");
            var fixture = Type.GetType("IdleClinic.Tests.DoctorsProgressionFixture, IdleClinic.Core.Tests", true);
            var starter = (ClinicSimulation)fixture.GetMethod("MaxStarter").Invoke(null, null);
            fixture.GetMethod("Earn").Invoke(null, new object[] { starter, 130000L });
            Persist(Path.Combine(output, "maxed-starter"), starter, null, ClinicLocation.StarterClinic);

            var doctors = (ClinicSimulation)fixture.GetMethod("MaxDoctors").Invoke(null, null);
            var unlock = starter.UnlockDoctorsClinic();
            if (!unlock.Success) throw new InvalidOperationException(unlock.Message);
            var transfer = starter.TransferWalletTo(doctors);
            if (!transfer.Success) throw new InvalidOperationException(transfer.Message);
            Persist(Path.Combine(output, "maxed-doctors"), starter, doctors, ClinicLocation.DoctorsClinic);
            Debug.Log("Doctors clinic QA fixtures generated through earned income and authoritative commands: " + output);
        }

        private static void Persist(string directory, ClinicSimulation starter, ClinicSimulation doctors, ClinicLocation active)
        {
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
                throw new InvalidOperationException("Use a fresh QA output directory; existing fixtures are preserved.");
            var store = new ClinicProfileStore(directory);
            var now = DateTimeOffset.UtcNow;
            var profile = store.LoadClinic(now);
            profile.state = starter.State;
            profile.doctorsState = doctors?.State;
            profile.activeLocation = active;
            profile.preferences = new ClinicPreferences { music = true, sound = true, haptics = true, reducedMotion = false };
            if (!store.Save(profile, now)) throw new InvalidOperationException(store.Error);
            var reloaded = new ClinicProfileStore(directory).LoadClinic(now);
            if (JsonUtility.ToJson(reloaded) != JsonUtility.ToJson(profile)) throw new InvalidOperationException("QA campaign did not round-trip exactly.");
            File.WriteAllText(Path.Combine(directory, "fixture-provenance.json"), JsonUtility.ToJson(new Receipt
            {
                generatedUtc = now.ToString("O"), location = active.ToString(),
                schemaVersion = profile.schemaVersion, wallet = profile.ActiveState.Wallet,
                starterEarned = starter.State.TotalEarned, starterSpent = starter.State.TotalSpent,
                doctorsEarned = doctors?.State.TotalEarned ?? 0, doctorsSpent = doctors?.State.TotalSpent ?? 0,
                staff = profile.ActiveState.Staff.Count, rooms = profile.ActiveState.Rooms.Count,
                basis = "DoctorsProgressionFixture: deterministic patient income, collection and production commands; no wallet injection."
            }, true));
        }
        [Serializable] private sealed class Receipt
        {
            public string generatedUtc, location, basis;
            public int schemaVersion, staff, rooms;
            public long wallet, starterEarned, starterSpent, doctorsEarned, doctorsSpent;
        }
    }
}
