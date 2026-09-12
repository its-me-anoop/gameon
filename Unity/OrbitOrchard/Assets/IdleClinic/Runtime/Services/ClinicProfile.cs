using System;
using IdleClinic.Core;

namespace IdleClinic.Services
{
    [Serializable]
    public sealed class ClinicPreferences
    {
        public bool sound = true;
        public bool haptics = true;
        public bool reducedMotion;
    }

    [Serializable]
    public sealed class ClinicProfile
    {
        public int schemaVersion = 1;
        public long revision;
        public ClinicState state;
        public ClinicPreferences preferences = new ClinicPreferences();
        public long lastAccountedUtcTicks;

        internal void Normalize()
        {
            if (preferences == null) preferences = new ClinicPreferences();
        }
    }

    /// <summary>Already committed results; there is no second claim action.</summary>
    public sealed class ClinicOfflineReport
    {
        public double elapsedSeconds;
        public double earningsSeconds;
        public double constructionSeconds;
        public long paymentsReceived;
        public long tillEarned;
        public long treatmentsCompleted;
        public bool wasCapped;
        public bool applied;
    }
}
