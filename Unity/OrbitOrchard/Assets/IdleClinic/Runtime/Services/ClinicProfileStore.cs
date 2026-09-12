using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using IdleClinic.Core;
using UnityEngine;

namespace IdleClinic.Services
{
    /// <summary>Progress and accounted time commit together before offline results are exposed.</summary>
    public sealed class ClinicProfileStore
    {
        public const double MaximumOfflineSeconds = ClinicRules.MaximumOfflineSeconds;
        public const string FileName = "idle-clinic-profile-v1.json";
        private const string LegacyFileName = "little-lifeline-profile-v1.json";
        [Serializable] private sealed class Envelope { public int schemaVersion = 1; public string payload; public string checksum; }
        [Serializable] private sealed class LegacyPreferencesProfile { public int schemaVersion = 0; public ClinicPreferences preferences = new ClinicPreferences(); }
        private readonly string directory;
        private readonly string path;
        private bool primaryWasUnreadable;
        private bool migrationPending;
        private string migrationSourcePath;
        private long pendingOfflineUtcTicks;
        public ClinicProfile Profile { get; private set; }
        public ClinicOfflineReport LastOfflineReport { get; private set; } = new ClinicOfflineReport();
        public string Error { get; private set; }
        public bool HasPendingOfflineProgress { get; private set; }

        public ClinicProfileStore(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("A save directory is required.", nameof(directory));
            this.directory = directory;
            path = Path.Combine(directory, FileName);
        }

        public ClinicProfile Load(DateTimeOffset now) => LoadClinic(now);

        public ClinicProfile LoadClinic(DateTimeOffset now)
        {
            Error = null;
            LastOfflineReport = new ClinicOfflineReport();
            HasPendingOfflineProgress = false;
            primaryWasUnreadable = false;
            migrationPending = false;
            migrationSourcePath = null;
            pendingOfflineUtcTicks = 0;
            string recovery = null;
            if (TryRead(path, out var saved, out var migrated))
            {
                Profile = saved;
                if (migrated) migrationSourcePath = path;
            }
            else
            {
                primaryWasUnreadable = File.Exists(path);
                if (primaryWasUnreadable) PreserveUnreadable(path);
                if (TryRead(path + ".backup", out saved, out migrated))
                {
                    Profile = saved;
                    if (migrated) migrationSourcePath = path + ".backup";
                    recovery = "Your clinic was recovered from its last good backup. The unreadable save is preserved.";
                }
                else
                {
                    if (File.Exists(path + ".backup")) PreserveUnreadable(path + ".backup");
                    if (primaryWasUnreadable || File.Exists(path + ".backup"))
                        recovery = "Your clinic save could not be read. A fresh clinic is ready; the original files are preserved.";
                    Profile = new ClinicProfile
                    {
                        state = ClinicSimulation.CreateNew().State,
                        preferences = ReadLegacyPreferences(),
                        lastAccountedUtcTicks = now.UtcDateTime.Ticks
                    };
                    Save(Profile, now);
                }
            }
            if (migrated)
            {
                migrationPending = true;
                HasPendingOfflineProgress = true;
                pendingOfflineUtcTicks = now.UtcDateTime.Ticks;
            }
            ApplyOffline(now);
            if (Error == null) Error = recovery;
            return Profile;
        }

        /// <summary>Call after active ticks/actions and before backgrounding. A stale snapshot cannot replace a resumed clinic.</summary>
        public bool Save(ClinicProfile profile, DateTimeOffset now)
        {
            if (HasPendingOfflineProgress) return false;
            if (!IsValid(profile) || (Profile != null && !ReferenceEquals(profile, Profile)))
            {
                Error = "This clinic state could not be saved. The last good save is unchanged.";
                return false;
            }
            // Active ticks have already happened even if storage fails. Pair their in-memory
            // watermark with that state; the disk snapshot retains its own previous time.
            profile.lastAccountedUtcTicks = Math.Max(profile.lastAccountedUtcTicks, now.UtcDateTime.Ticks);
            profile.Normalize();
            Profile = profile;
            var candidate = Copy(profile);
            candidate.revision = NextRevision(profile.revision);
            if (!WriteSnapshot(candidate)) return false;
            profile.revision = candidate.revision;
            return true;
        }

        /// <summary>Core caps earnings at eight hours and independently completes construction for the full gap.
        /// Rebind the simulation to Profile.state after success. Pause active ticks/actions while a commit is pending.</summary>
        public ClinicOfflineReport ApplyOffline(DateTimeOffset now)
        {
            var result = new ClinicOfflineReport();
            if (!IsValid(Profile)) return result;
            if (migrationPending && !CommitMigration()) return result;
            var targetTicks = Math.Max(now.UtcDateTime.Ticks, pendingOfflineUtcTicks);
            var elapsed = (targetTicks - Profile.lastAccountedUtcTicks) / (double)TimeSpan.TicksPerSecond;
            if (elapsed <= 0)
            {
                HasPendingOfflineProgress = false;
                pendingOfflineUtcTicks = 0;
                return result;
            }
            var candidate = Copy(Profile);
            var advanced = new ClinicSimulation(candidate.state).AdvanceOffline(elapsed);
            candidate.lastAccountedUtcTicks = Math.Max(candidate.lastAccountedUtcTicks, targetTicks);
            candidate.revision = NextRevision(candidate.revision);
            if (!WriteSnapshot(candidate))
            {
                HasPendingOfflineProgress = true;
                pendingOfflineUtcTicks = targetTicks;
                return result;
            }
            HasPendingOfflineProgress = false;
            pendingOfflineUtcTicks = 0;
            Profile = candidate;
            result.elapsedSeconds = elapsed;
            result.earningsSeconds = advanced.EarningsSeconds;
            result.constructionSeconds = advanced.ConstructionSeconds;
            result.paymentsReceived = advanced.PaymentsReceived;
            result.tillEarned = advanced.TillEarned;
            result.treatmentsCompleted = advanced.TreatmentsCompleted;
            result.wasCapped = advanced.WasCapped;
            result.applied = true;
            LastOfflineReport = result;
            return result;
        }

        private bool CommitMigration()
        {
            // Do not advance the old watermark while upgrading the schema. Offline operations
            // get their own subsequent revision, so a failed write can never replay either step.
            if (!PreserveLegacyMigration()) return false;
            var candidate = Copy(Profile);
            candidate.revision = NextRevision(candidate.revision);
            if (!WriteSnapshot(candidate)) return false;
            Profile = candidate;
            migrationPending = false;
            migrationSourcePath = null;
            HasPendingOfflineProgress = false;
            return true;
        }

        private bool PreserveLegacyMigration()
        {
            try
            {
                if (!TryRead(migrationSourcePath, out var source, out var isLegacy) || !isLegacy
                    || source.revision != Profile.revision || JsonUtility.ToJson(source) != JsonUtility.ToJson(Profile))
                    throw new InvalidDataException("The legacy snapshot changed before migration.");
                var original = File.ReadAllBytes(migrationSourcePath);
                var archive = path + ".v1-before-migration-" + Profile.revision;
                if (File.Exists(archive))
                {
                    if (Convert.ToBase64String(File.ReadAllBytes(archive)) != Convert.ToBase64String(original))
                        throw new InvalidDataException("The preserved legacy snapshot differs.");
                    return true;
                }
                var temporary = archive + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(original, 0, original.Length); stream.Flush(true); }
                File.Move(temporary, archive);
                return true;
            }
            catch (Exception)
            {
                Error = "Your existing clinic is safe. Free a little storage so its upgrade can be saved.";
                return false;
            }
        }

        private bool WriteSnapshot(ClinicProfile profile)
        {
            try
            {
                if (!IsValid(profile)) throw new InvalidDataException("Invalid clinic state.");
                profile.Normalize();
                Directory.CreateDirectory(directory);
                var payload = JsonUtility.ToJson(profile);
                var envelope = new Envelope { payload = payload, checksum = Digest(payload) };
                var bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(envelope));
                var temporary = path + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path))
                {
                    var preserveBackup = primaryWasUnreadable || !TryRead(path, out _);
                    File.Replace(temporary, path, preserveBackup ? UnreadablePath(path) : path + ".backup");
                }
                else File.Move(temporary, path);
                primaryWasUnreadable = false;
                Error = null;
                return true;
            }
            catch (Exception)
            {
                Error = "Your clinic could not be saved. Keep the app open and free a little storage; offline progress has no separate claim to lose.";
                return false;
            }
        }

        private static bool TryRead(string file, out ClinicProfile profile) => TryRead(file, out profile, out _);

        private static bool TryRead(string file, out ClinicProfile profile, out bool migrated)
        {
            profile = null;
            migrated = false;
            if (!TryPayload(file, out var payload)) return false;
            try
            {
                var candidate = new ClinicProfile();
                JsonUtility.FromJsonOverwrite(payload, candidate);
                if (candidate.schemaVersion == 1)
                {
                    if (!IsValidHeader(candidate, 1) || !ClinicStateMigration.TryMigrateV1(candidate.state)) return false;
                    candidate.schemaVersion = 2;
                    migrated = true;
                }
                if (!IsValid(candidate)) { migrated = false; return false; }
                candidate.Normalize();
                profile = candidate;
                return true;
            }
            catch (Exception) { return false; }
        }

        // Read a small preference DTO only. Never load/advance/save the previous campaign,
        // import its cash/tutorial state, or treat any local field as a paid entitlement.
        private ClinicPreferences ReadLegacyPreferences()
        {
            foreach (var suffix in new[] { "", ".backup" })
            {
                if (!TryPayload(Path.Combine(directory, LegacyFileName + suffix), out var payload)) continue;
                try
                {
                    var legacy = JsonUtility.FromJson<LegacyPreferencesProfile>(payload);
                    if (legacy == null || legacy.schemaVersion != 1 || legacy.preferences == null) continue;
                    return new ClinicPreferences
                    {
                        sound = legacy.preferences.sound,
                        haptics = legacy.preferences.haptics,
                        reducedMotion = legacy.preferences.reducedMotion
                    };
                }
                catch (Exception) { }
            }
            return new ClinicPreferences();
        }

        private static bool TryPayload(string file, out string payload)
        {
            payload = null;
            if (!File.Exists(file)) return false;
            try
            {
                if (new FileInfo(file).Length > 4 * 1024 * 1024) return false;
                var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(file));
                if (envelope == null || envelope.schemaVersion != 1 || string.IsNullOrEmpty(envelope.payload)
                    || string.IsNullOrEmpty(envelope.checksum) || Digest(envelope.payload) != envelope.checksum) return false;
                payload = envelope.payload;
                return true;
            }
            catch (Exception) { return false; }
        }

        private static bool IsValidHeader(ClinicProfile profile, int version)
            => profile != null && profile.schemaVersion == version && profile.revision >= 0
                && profile.lastAccountedUtcTicks > 0 && profile.lastAccountedUtcTicks <= DateTime.MaxValue.Ticks && profile.state != null;

        private static bool IsValid(ClinicProfile profile)
            => IsValidHeader(profile, 2) && ClinicSimulation.IsValidState(profile.state);

        private static ClinicProfile Copy(ClinicProfile profile)
        {
            var copy = new ClinicProfile();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(profile), copy);
            return copy;
        }

        private static long NextRevision(long revision) => revision == long.MaxValue ? long.MaxValue : revision + 1;
        private static string Digest(string text)
        {
            using (var hash = SHA256.Create())
                return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }
        private static string UnreadablePath(string file) => file + ".unreadable-"
            + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
        private static void PreserveUnreadable(string file)
        {
            try { File.Copy(file, UnreadablePath(file), false); }
            catch (Exception) { /* If a recovery copy cannot be written, the original remains untouched. */ }
        }
    }
}
