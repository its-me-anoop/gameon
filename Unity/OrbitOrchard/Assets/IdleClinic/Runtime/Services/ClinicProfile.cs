using System;
using System.Collections.Generic;
using IdleClinic.Core;

namespace IdleClinic.Services
{
    [Serializable]
    public sealed class ClinicPreferences
    {
        public bool sound = true;
        public bool music = true;
        public bool haptics = true;
        public bool reducedMotion;
    }

    [Serializable]
    public sealed class ClinicProfile
    {
        public int schemaVersion = 3;
        public long revision;
        public ClinicState state;
        // Unity's inline JSON serializer materializes null custom objects. An empty list
        // explicitly represents a locked location without synthesizing an invalid clinic.
        public List<ClinicState> additionalClinics = new List<ClinicState>();
        public ClinicState doctorsState
        {
            get => additionalClinics != null && additionalClinics.Count == 1 ? additionalClinics[0] : null;
            set { additionalClinics = value == null ? new List<ClinicState>() : new List<ClinicState> { value }; }
        }
        public ClinicLocation activeLocation;
        public ClinicState ActiveState => activeLocation == ClinicLocation.DoctorsClinic ? doctorsState : state;
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
