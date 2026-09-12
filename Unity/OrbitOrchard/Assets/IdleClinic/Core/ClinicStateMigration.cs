using System.Collections.Generic;

namespace IdleClinic.Core
{
    /// <summary>Upgrade an already checksum-verified v1 snapshot without replaying money, services or tutorial events.</summary>
    public static class ClinicStateMigration
    {
        public static bool TryMigrateV1(ClinicState state)
        {
            if (!ClinicSimulation.IsValidLegacyState(state)) return false;
            state.TreatmentStations = new List<TreatmentStationState>();
            for (var id = 0; id < state.Room(ClinicRoom.FirstAid).StationCount; id++)
                state.TreatmentStations.Add(new TreatmentStationState { Id = id });
            state.Amenities = new List<ClinicAmenityState>
            {
                new ClinicAmenityState { Kind = ClinicAmenity.Parking },
                new ClinicAmenityState { Kind = ClinicAmenity.Toilet },
                new ClinicAmenityState { Kind = ClinicAmenity.Vending }
            };
            foreach (var desk in state.ReceptionDesks) desk.EquipmentLevel = 1;
            foreach (var staff in state.Staff) staff.TrainingLevel = 1;
            foreach (var patient in state.Patients)
            {
                patient.AppearanceId = ClinicRules.PatientAppearance(state.Seed, patient.Id);
                patient.ParkingBayId = -1;
                patient.VisitingAmenity = ClinicAmenity.Parking;
                patient.UsedToilet = false;
                patient.UsedVending = false;
                patient.TipPaid = 0;
            }
            state.TotalTips = 0;
            state.SchemaVersion = 2;
            state.RulesVersion = 2;
            return ClinicSimulation.IsValidState(state);
        }
    }
}
