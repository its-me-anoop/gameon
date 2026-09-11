using System;
using System.Collections.Generic;
using System.Globalization;
using LittleLifeline.Core;

namespace LittleLifeline.Services
{
    [Serializable]
    public sealed class LifelinePreferences
    {
        public bool sound = true;
        public bool haptics = true;
        public bool reducedMotion;
        public string livery = "base";

        internal void Normalize()
        {
            if (livery != "base" && livery != "sunrise" && livery != "coastal" && livery != "heritage") livery = "base";
        }
    }

    [Serializable]
    public sealed class LifelineWeeklyRecord
    {
        public string weekKey;
        public int score;
        public long completedUtcTicks;
    }

    [Serializable]
    public sealed class LifelineProfile
    {
        public int schemaVersion = 1;
        public long revision;
        public SimulationState state;
        public LifelinePreferences preferences = new LifelinePreferences();
        public long lastAccountedUtcTicks;
        public List<LifelineWeeklyRecord> weeklyRecords = new List<LifelineWeeklyRecord>();

        public int WeeklyBest(string weekKey)
        {
            var best = 0;
            if (weeklyRecords == null || !LifelineWeek.TryStart(weekKey, out _)) return best;
            foreach (var entry in weeklyRecords)
                if (entry != null && entry.weekKey == weekKey) best = Math.Max(best, entry.score);
            return best;
        }

        public void AddWeeklyRecord(string weekKey, long score, DateTimeOffset completedAt)
        {
            if (score < 0 || score > int.MaxValue) return;
            AddWeeklyRecord(weekKey, (int)score, completedAt);
        }

        public void AddWeeklyRecord(string weekKey, int score, DateTimeOffset completedAt)
        {
            if (score < 0 || !LifelineWeek.TryStart(weekKey, out var start) || completedAt < start) return;
            Normalize();
            var previous = weeklyRecords.Find(entry => entry.weekKey == weekKey);
            if (previous == null)
                weeklyRecords.Add(new LifelineWeeklyRecord { weekKey = weekKey, score = score, completedUtcTicks = completedAt.UtcDateTime.Ticks });
            else if (score > previous.score)
            {
                previous.score = score;
                previous.completedUtcTicks = completedAt.UtcDateTime.Ticks;
            }
            TrimRecords();
        }

        internal void Normalize()
        {
            if (preferences == null) preferences = new LifelinePreferences();
            preferences.Normalize();
            var best = new Dictionary<string, LifelineWeeklyRecord>(StringComparer.Ordinal);
            if (weeklyRecords != null)
                foreach (var entry in weeklyRecords)
                {
                    if (entry == null || entry.score < 0 || !LifelineWeek.TryStart(entry.weekKey, out var start)
                        || entry.completedUtcTicks < start.UtcDateTime.Ticks || entry.completedUtcTicks > DateTime.MaxValue.Ticks) continue;
                    if (!best.TryGetValue(entry.weekKey, out var previous) || entry.score > previous.score)
                        best[entry.weekKey] = new LifelineWeeklyRecord
                        { weekKey = entry.weekKey, score = entry.score, completedUtcTicks = entry.completedUtcTicks };
                }
            weeklyRecords = new List<LifelineWeeklyRecord>(best.Values);
            TrimRecords();
        }

        private void TrimRecords()
        {
            weeklyRecords.Sort((left, right) => string.CompareOrdinal(right.weekKey, left.weekKey));
            if (weeklyRecords.Count > 13) weeklyRecords.RemoveRange(13, weeklyRecords.Count - 13);
        }
    }

    /// <summary>UTC Monday identity; expired results stay local and never enter another occurrence.</summary>
    public static class LifelineWeek
    {
        public static string Key(DateTimeOffset date)
        {
            var utc = date.UtcDateTime.Date;
            var daysSinceMonday = ((int)utc.DayOfWeek + 6) % 7;
            return utc.AddDays(-daysSinceMonday).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static bool TryStart(string key, out DateTimeOffset start)
        {
            start = default;
            if (!DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
                || date.DayOfWeek != DayOfWeek.Monday || date > DateTime.MaxValue.AddDays(-7)) return false;
            start = new DateTimeOffset(date);
            return true;
        }

        public static bool Contains(string key, DateTimeOffset now)
            => TryStart(key, out var start) && now >= start && now < start.AddDays(7);
    }

    public sealed class LifelineOfflineReport
    {
        public double elapsedSeconds;
        public int completed;
        public int coinsEarned;
        public string limitingDepartment = "";
        public bool wasCapped;
        public bool applied;
    }
}
