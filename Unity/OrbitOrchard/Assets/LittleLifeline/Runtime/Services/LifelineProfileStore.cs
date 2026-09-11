using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LittleLifeline.Core;
using UnityEngine;

namespace LittleLifeline.Services
{
    /// <summary>One atomic snapshot owns both progress and its accounted time; credit has no separate claim step.</summary>
    public sealed class LifelineProfileStore
    {
        public const double MaximumOfflineSeconds = 24 * 60 * 60;
        public const string FileName = "little-lifeline-profile-v1.json";
        [Serializable] private sealed class Envelope { public int schemaVersion = 1; public string payload; public string checksum; }
        private readonly string path;
        private bool primaryWasUnreadable;
        public LifelineProfile Profile { get; private set; }
        public LifelineOfflineReport LastOfflineReport { get; private set; } = new LifelineOfflineReport();
        public string Error { get; private set; }
        public bool HasPendingOfflineProgress { get; private set; }

        public LifelineProfileStore(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("A save directory is required.", nameof(directory));
            path = Path.Combine(directory, FileName);
        }

        public LifelineProfile LoadCampaign(DateTimeOffset now)
        {
            Error = null;
            HasPendingOfflineProgress = false;
            primaryWasUnreadable = false;
            string recovery = null;
            if (TryRead(path, out var saved)) Profile = saved;
            else
            {
                primaryWasUnreadable = File.Exists(path);
                if (primaryWasUnreadable) PreserveUnreadable(path);
                if (TryRead(path + ".backup", out saved))
                {
                    Profile = saved;
                    recovery = "Your train was recovered from its last good backup. The unreadable save is preserved.";
                }
                else
                {
                    if (File.Exists(path + ".backup")) PreserveUnreadable(path + ".backup");
                    if (primaryWasUnreadable || File.Exists(path + ".backup"))
                        recovery = "Your train save could not be read. A fresh train is ready; the original files are preserved.";
                    Profile = new LifelineProfile
                    {
                        state = LifelineSimulation.CreateCampaign().State,
                        lastAccountedUtcTicks = now.UtcDateTime.Ticks
                    };
                    Save(Profile, now);
                }
            }
            ApplyOffline(now);
            if (Error == null) Error = recovery;
            return Profile;
        }

        /// <summary>Save after active simulation or a player action and before backgrounding.</summary>
        public bool Save(LifelineProfile profile, DateTimeOffset now)
        {
            // The caller pauses campaign ticks until ApplyOffline can commit. Never consume
            // that owed interval through an unrelated preference/autosave timestamp.
            if (HasPendingOfflineProgress) return false;
            if (!IsValid(profile))
            {
                Error = "This train state could not be saved. The last good save is unchanged.";
                return false;
            }
            // Live state has already advanced even if storage is unavailable. Keep its in-memory
            // time paired with it; the old on-disk state keeps its own earlier timestamp.
            profile.lastAccountedUtcTicks = Math.Max(profile.lastAccountedUtcTicks, now.UtcDateTime.Ticks);
            profile.Normalize();
            Profile = profile;
            var candidate = Copy(profile);
            candidate.revision = profile.revision == long.MaxValue ? long.MaxValue : profile.revision + 1;
            if (!WriteSnapshot(candidate)) return false;
            profile.revision = candidate.revision;
            return true;
        }

        /// <summary>Applies to a copy, then commits. Rebind the simulation to Profile.state after this call.</summary>
        public LifelineOfflineReport ApplyOffline(DateTimeOffset now)
        {
            var result = new LifelineOfflineReport();
            LastOfflineReport = result;
            if (!IsValid(Profile) || Profile.state.Tick == 0) return result;
            var elapsed = (now.UtcDateTime.Ticks - Profile.lastAccountedUtcTicks) / (double)TimeSpan.TicksPerSecond;
            if (elapsed <= 0) return result;
            var candidate = Copy(Profile);
            var seconds = Math.Min(MaximumOfflineSeconds, elapsed);
            var simulation = new LifelineSimulation(candidate.state);
            var advanced = simulation.Advance(seconds);
            candidate.lastAccountedUtcTicks = Math.Max(candidate.lastAccountedUtcTicks, now.UtcDateTime.Ticks);
            candidate.revision = candidate.revision == long.MaxValue ? long.MaxValue : candidate.revision + 1;
            if (!WriteSnapshot(candidate))
            {
                HasPendingOfflineProgress = true;
                return result;
            }
            HasPendingOfflineProgress = false;
            Profile = candidate;
            result.elapsedSeconds = advanced.Seconds;
            result.completed = advanced.Completed;
            result.coinsEarned = advanced.CoinsEarned;
            result.limitingDepartment = advanced.Bottleneck.HasValue ? LifelineRules.RoomName(advanced.Bottleneck.Value) : "";
            result.wasCapped = elapsed > MaximumOfflineSeconds;
            result.applied = true;
            return result;
        }

        private bool WriteSnapshot(LifelineProfile profile)
        {
            try
            {
                if (!IsValid(profile)) throw new InvalidDataException("Invalid campaign state.");
                profile.Normalize();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
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
                Error = "Your train could not be saved. Keep the app open and free a little storage; offline rewards have no separate claim to lose.";
                return false;
            }
        }

        private static bool TryRead(string file, out LifelineProfile profile)
        {
            profile = null;
            if (!File.Exists(file)) return false;
            try
            {
                var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(file));
                if (envelope == null || envelope.schemaVersion != 1 || string.IsNullOrEmpty(envelope.payload)
                    || string.IsNullOrEmpty(envelope.checksum) || Digest(envelope.payload) != envelope.checksum) return false;
                var candidate = new LifelineProfile();
                JsonUtility.FromJsonOverwrite(envelope.payload, candidate);
                if (!IsValid(candidate)) return false;
                candidate.Normalize();
                profile = candidate;
                return true;
            }
            catch (Exception) { return false; }
        }

        private static bool IsValid(LifelineProfile profile)
            => profile != null && profile.schemaVersion == 1 && profile.revision >= 0
                && profile.lastAccountedUtcTicks > 0 && profile.lastAccountedUtcTicks <= DateTime.MaxValue.Ticks
                && profile.state != null && profile.state.Mode == LittleLifeline.Core.SimulationMode.Campaign
                && LifelineSimulation.IsValidState(profile.state);

        private static LifelineProfile Copy(LifelineProfile profile)
        {
            var copy = new LifelineProfile();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(profile), copy);
            return copy;
        }

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
            catch (Exception) { /* The original file remains untouched if its recovery copy cannot be written. */ }
        }
    }
}
