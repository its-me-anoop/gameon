using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace OrbitOrchard.App
{
    [Serializable] public sealed class DailyRecord { public string day; public int score; }
    [Serializable] public sealed class HarvestRecord { public int score; public int seeds; public double seconds; public string day; }

    [Serializable]
    public sealed class OrchardProfile
    {
        public int schema = 1;
        public int best;
        public int classicRuns;
        public int totalSeeds;
        public string theme = "orchard";
        public bool sound = true;
        public bool haptics = true;
        public bool reducedMotion;
        public List<DailyRecord> daily = new List<DailyRecord>();
        public List<HarvestRecord> recent = new List<HarvestRecord>();

        public int DailyBest(string day)
        {
            if (daily == null || string.IsNullOrEmpty(day)) return 0;
            var result = 0;
            foreach (var entry in daily)
                if (entry != null && entry.day == day) result = Math.Max(result, entry.score);
            return result;
        }

        /// <summary>The caller records only finished competitive runs; practice never enters history.</summary>
        public void Record(HarvestRecord run, bool isDaily)
        {
            if (!IsValid(run)) return;
            Normalize();
            totalSeeds = AddWithoutOverflow(totalSeeds, run.seeds);
            if (isDaily)
            {
                var record = daily.Find(entry => entry.day == run.day);
                if (record == null) daily.Add(new DailyRecord { day = run.day, score = run.score });
                else record.score = Math.Max(record.score, run.score);
                SortAndLimitDays();
            }
            else
            {
                classicRuns = AddWithoutOverflow(classicRuns, 1);
                best = Math.Max(best, run.score);
            }
            // Store a snapshot: a caller changing its run object cannot rewrite history.
            recent.Insert(0, Copy(run));
            if (recent.Count > 20) recent.RemoveRange(20, recent.Count - 20);
        }

        internal void Normalize()
        {
            best = Math.Max(0, best);
            classicRuns = Math.Max(0, classicRuns);
            totalSeeds = Math.Max(0, totalSeeds);
            if (theme != "orchard" && theme != "dusk" && theme != "porcelain" && theme != "cherry") theme = "orchard";

            var days = new Dictionary<string, int>(StringComparer.Ordinal);
            if (daily != null)
            {
                foreach (var entry in daily)
                {
                    if (entry == null || !IsDay(entry.day) || entry.score < 0) continue;
                    days.TryGetValue(entry.day, out var oldBest);
                    days[entry.day] = Math.Max(oldBest, entry.score);
                }
            }
            daily = new List<DailyRecord>();
            foreach (var entry in days) daily.Add(new DailyRecord { day = entry.Key, score = entry.Value });
            SortAndLimitDays();

            var history = new List<HarvestRecord>();
            if (recent != null)
            {
                foreach (var run in recent)
                {
                    if (!IsValid(run)) continue;
                    history.Add(Copy(run));
                    if (history.Count == 20) break;
                }
            }
            recent = history;
        }

        private void SortAndLimitDays()
        {
            daily.Sort((left, right) => string.CompareOrdinal(right.day, left.day));
            if (daily.Count > 90) daily.RemoveRange(90, daily.Count - 90);
        }

        private static bool IsDay(string value) => DateTime.TryParseExact(value, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

        private static bool IsValid(HarvestRecord run) => run != null && IsDay(run.day) && run.score >= 0 && run.seeds >= 0
            && run.seconds >= 0 && !double.IsNaN(run.seconds) && !double.IsInfinity(run.seconds);

        private static HarvestRecord Copy(HarvestRecord run) => new HarvestRecord
            { score = run.score, seeds = run.seeds, seconds = run.seconds, day = run.day ?? "" };

        private static int AddWithoutOverflow(int left, int right) => (int)Math.Min(int.MaxValue, (long)left + right);
    }

    /// <summary>Atomic local saves with a recoverable prior version and preserved unreadable originals.</summary>
    public sealed class ProfileStore
    {
        [Serializable] private sealed class SchemaHeader { public int schema; }
        private readonly string path;
        private bool primaryWasUnreadable;
        public string Error { get; private set; }

        public ProfileStore(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("A save directory is required.", nameof(directory));
            path = Path.Combine(directory, "orbit-orchard-unity-v1.json");
        }

        public OrchardProfile Load()
        {
            Error = null;
            primaryWasUnreadable = false;
            if (TryRead(path, out var profile)) return profile;
            if (File.Exists(path))
            {
                primaryWasUnreadable = true;
                PreserveUnreadable();
            }
            if (TryRead(path + ".backup", out profile))
            {
                Error = "Your garden was recovered from its last good backup. The unreadable original, if present, is preserved.";
                return profile;
            }
            if (primaryWasUnreadable || File.Exists(path + ".backup"))
                Error = "Your saved garden couldn't be read. A fresh garden is ready; the original files are preserved.";
            return new OrchardProfile();
        }

        public bool Save(OrchardProfile profile)
        {
            try
            {
                if (profile == null || profile.schema != 1) throw new InvalidDataException("Unknown garden format.");
                profile.Normalize();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(profile));
                var temporary = path + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path))
                {
                    // Do not replace a known-good backup with the corrupt primary
                    // that Load just recovered from. Both originals remain available.
                    var keepBackup = primaryWasUnreadable || !TryRead(path, out _);
                    var previous = keepBackup ? UnreadablePath() : path + ".backup";
                    File.Replace(temporary, path, previous);
                }
                else File.Move(temporary, path);
                primaryWasUnreadable = false;
                Error = null;
                return true;
            }
            catch (Exception)
            {
                Error = "Your garden couldn't be saved. Keep the app open and free a little storage.";
                return false;
            }
        }

        private static bool TryRead(string file, out OrchardProfile profile)
        {
            profile = null;
            if (!File.Exists(file)) return false;
            try
            {
                var json = File.ReadAllText(file);
                var header = JsonUtility.FromJson<SchemaHeader>(json);
                if (header == null || header.schema != 1) return false;
                // Overwrite an initialized model so missing fields keep defaults.
                profile = new OrchardProfile();
                JsonUtility.FromJsonOverwrite(json, profile);
                profile.Normalize();
                return true;
            }
            catch (Exception)
            {
                profile = null;
                return false;
            }
        }

        private void PreserveUnreadable()
        {
            try { File.Copy(path, UnreadablePath(), false); }
            catch (Exception) { /* The primary remains untouched when making a recovery copy is impossible. */ }
        }

        private string UnreadablePath() => path + ".unreadable-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
            + "-" + Guid.NewGuid().ToString("N");
    }
}
