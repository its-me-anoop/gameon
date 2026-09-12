using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleClinic.Core
{
    public static partial class ClinicRules
    {
        public const long DoctorsClinicUnlockCost = 100000;
        public const int TaxiArrivalTicks = 160;
        public const int TaxiDropOffTicks = 30;
        public const int TaxiPickupTicks = 30;
        public const int TaxiDepartureTicks = 160;
        public static bool IsDoctors(ClinicState state) => state != null && state.Location == ClinicLocation.DoctorsClinic;
        public static int LocationMultiplier(ClinicState state) => IsDoctors(state) ? 2 : 1;
        public static int MaximumTier(ClinicState state) => IsDoctors(state) ? 6 : 3;
        public static int MaximumStaff(ClinicState state, ClinicStaffRole role) => !Enum.IsDefined(typeof(ClinicStaffRole), role) ? 0
            : IsDoctors(state) ? role == ClinicStaffRole.Pharmacist ? 2 : 4 : (int)role < 2 ? 2 : 0;
        public static int MaximumPatientCount(ClinicState state) => IsDoctors(state) ? 80 : MaximumPatients;
        public static int ComponentCap(ClinicState state, ClinicRoom kind) => state.Room(kind) == null ? 0 : TrackCap(state.Room(kind).Tier);
        public static int MaximumAmenityLevel(ClinicState state, ClinicAmenity kind) => !Enum.IsDefined(typeof(ClinicAmenity), kind) ? 0
            : kind == ClinicAmenity.Taxi && !IsDoctors(state) ? 0 : IsDoctors(state) ? 6 : 3;
        public static int AmenityCap(ClinicState state, ClinicAmenity kind) => kind == ClinicAmenity.Parking || kind == ClinicAmenity.Taxi
            ? MaximumAmenityLevel(state, kind) : Math.Min(MaximumAmenityLevel(state, kind), state.Room(ClinicRoom.Waiting).Tier);
        public static int ToiletCubicleCount(ClinicState state) => state.Amenity(ClinicAmenity.Toilet).Level == 0 ? 0 : IsDoctors(state) ? 2 : 1;
        public static int TaxiDockCount(ClinicState state) => IsDoctors(state) && state.Amenity(ClinicAmenity.Taxi).Level > 0 ? 2 : 0;
        public static int TaxiTravelTicks(ClinicState state) => SpeedTicks(TaxiArrivalTicks, state.Amenity(ClinicAmenity.Taxi).Level);
        public static ClinicRoom RoomForRole(ClinicStaffRole role) => role == ClinicStaffRole.Nurse ? ClinicRoom.FirstAid
            : role == ClinicStaffRole.Doctor ? ClinicRoom.Consultation : role == ClinicStaffRole.Pharmacist ? ClinicRoom.Pharmacy : ClinicRoom.Reception;
        public static int StaffId(ClinicStaffRole role, int station) => (int)role * 100 + station;
        public static List<TreatmentStationState> Stations(ClinicState state, ClinicStaffRole role) => role == ClinicStaffRole.Nurse ? state.TreatmentStations
            : role == ClinicStaffRole.Doctor ? state.ConsultationStations : role == ClinicStaffRole.Pharmacist ? state.PharmacyStations : null;
        public static int StationCount(ClinicState state, ClinicStaffRole role) => role == ClinicStaffRole.Receptionist ? state.ReceptionDesks.Count : Stations(state, role)?.Count ?? 0;
        public static int StationCap(ClinicState state, ClinicStaffRole role) => role == ClinicStaffRole.Receptionist ? MaximumStaff(state, role)
            : Math.Min(MaximumStaff(state, role), state.Room(RoomForRole(role))?.Tier ?? 0);
        public static long HireCost(ClinicState state, ClinicStaffRole role)
        {
            if (MaximumStaff(state, role) == 0) return 0;
            var count = state.Staff.Count(s => s.Role == role);
            if (count >= MaximumStaff(state, role)) return 0;
            long basis = role == ClinicStaffRole.Receptionist ? 100 : role == ClinicStaffRole.Nurse ? 50 : role == ClinicStaffRole.Doctor ? 150 : 100;
            return ScaleCost(basis * LocationMultiplier(state), role == ClinicStaffRole.Receptionist ? 3 : 9, 1, count);
        }
        public static long AddStationCost(ClinicState state, ClinicStaffRole role)
        {
            var count = StationCount(state, role);
            if (role == ClinicStaffRole.Receptionist || count >= MaximumStaff(state, role)) return 0;
            return ScaleCost((role == ClinicStaffRole.Nurse ? 180 : role == ClinicStaffRole.Doctor ? 240 : 200) * LocationMultiplier(state), 5, 2, Math.Max(0, count - 1));
        }
        public static long UpgradeCost(ClinicState state, ClinicRoom kind, UpgradeTrack track)
        {
            var room = state.Room(kind);
            if (room == null) return 0;
            var basis = kind == ClinicRoom.Consultation ? (track == UpgradeTrack.Equipment ? 110 : track == UpgradeTrack.Facilities ? 90 : 40)
                : kind == ClinicRoom.Pharmacy ? (track == UpgradeTrack.Equipment ? 95 : track == UpgradeTrack.Facilities ? 75 : 40) : UpgradeBase(kind, track);
            return ScaleCost(basis * LocationMultiplier(state), 8, 5, room.Level(track) - 1);
        }
        public static long RenovationCost(ClinicState state, ClinicRoom kind)
        {
            var room = state.Room(kind);
            var basis = kind == ClinicRoom.Reception ? 180 : kind == ClinicRoom.FirstAid ? 250 : kind == ClinicRoom.Waiting ? 120 : kind == ClinicRoom.Consultation ? 300 : 220;
            return room == null ? 0 : ScaleCost(basis * LocationMultiplier(state), 5, 2, room.Tier - 1);
        }
        public static int RenovationSeconds(ClinicState state, ClinicRoom kind) => state.Room(kind) == null || state.Room(kind).Tier >= MaximumTier(state) ? 0
            : (int)ScaleCost(60 * LocationMultiplier(state), 3, 1, state.Room(kind).Tier - 1);
        public static long StaffTrainingCost(ClinicState state, ClinicStaffState staff) => staff == null ? 0
            : ScaleCost((staff.Role == ClinicStaffRole.Receptionist ? 80 : staff.Role == ClinicStaffRole.Nurse ? 100 : staff.Role == ClinicStaffRole.Doctor ? 140 : 110) * LocationMultiplier(state), 9, 5, staff.TrainingLevel - 1);
        public static long AmenityUpgradeCost(ClinicState state, ClinicAmenity kind) => state.Amenity(kind) == null || state.Amenity(kind).Level >= MaximumAmenityLevel(state, kind) ? 0
            : ScaleCost((kind == ClinicAmenity.Parking ? 220 : kind == ClinicAmenity.Toilet ? 140 : kind == ClinicAmenity.Vending ? 180 : 260) * LocationMultiplier(state), 2, 1, state.Amenity(kind).Level);
        public static string StationPatientAnchor(ClinicStaffRole role, int id) => role == ClinicStaffRole.Receptionist ? DeskPatientAnchor(id)
            : role == ClinicStaffRole.Nurse ? TreatmentPatientAnchor(id) : (role == ClinicStaffRole.Doctor ? "consultation.station." : "pharmacy.station.") + id + ".patient";
        public static string StationStaffAnchor(ClinicStaffRole role, int id) => role == ClinicStaffRole.Receptionist ? DeskStaffAnchor(id)
            : role == ClinicStaffRole.Nurse ? TreatmentStaffAnchor(id) : (role == ClinicStaffRole.Doctor ? "consultation.station." : "pharmacy.station.") + id + ".staff";
        public static string ToiletPatientAnchor(ClinicState state, int id) => IsDoctors(state) ? "waiting.toilet." + id + ".patient" : AmenityPatientAnchor(ClinicAmenity.Toilet);
        public static string TaxiPatientAnchor(int id) => "taxi.dock." + id + ".patient";
        public const int TaxiWaitingCapacity = 8;
        public const int TaxiBookingCapacity = 6;
        public static string TaxiWaitingAnchor(int id) => "taxi.waiting." + id + ".patient";
        public static string RoomPlotAnchor(ClinicRoom room) => room == ClinicRoom.FirstAid ? "firstaid.plot" : room.ToString().ToLowerInvariant() + ".plot";
        public static long MaximumVisitFee(ClinicState state) => IsDoctors(state) ? 820 : 140;
        public static List<string> StarterCompletion(ClinicState state)
        {
            var unmet = new List<string>();
            if (state == null || state.Location != ClinicLocation.StarterClinic) { unmet.Add("Complete the starter clinic."); return unmet; }
            if (state.Tutorial != ClinicTutorialStep.Complete) unmet.Add("Complete the first treatment.");
            if (state.Construction.Count != 0) unmet.Add("Finish all construction.");
            foreach (ClinicRoom kind in new[] { ClinicRoom.Reception, ClinicRoom.FirstAid, ClinicRoom.Waiting })
            {
                var room = state.Room(kind);
                if (room == null || !room.Built) { unmet.Add("Build " + kind + "."); continue; }
                if (room.Tier != 3) unmet.Add("Renovate " + kind + " to tier 3.");
                foreach (UpgradeTrack track in Enum.GetValues(typeof(UpgradeTrack)))
                    if (room.Level(track) != 6) unmet.Add("Maximise " + kind + " " + track + ".");
            }
            foreach (var role in new[] { ClinicStaffRole.Receptionist, ClinicStaffRole.Nurse })
            {
                if (StationCount(state, role) != 2) unmet.Add("Build both " + role + " stations.");
                if (state.Staff.Count(s => s.Role == role) != 2) unmet.Add("Hire both " + role + " staff.");
                for (var id = 0; id < 2; id++)
                {
                    if (StationLevel(state, role, id) != 6) unmet.Add("Maximise " + role + " workstation " + (id + 1) + ".");
                    if (state.Staff.Find(s => s.Role == role && s.StationId == id)?.TrainingLevel != 6) unmet.Add("Fully train " + role + " " + (id + 1) + ".");
                }
            }
            foreach (var kind in new[] { ClinicAmenity.Parking, ClinicAmenity.Toilet, ClinicAmenity.Vending })
                if (state.Amenity(kind)?.Level != 3) unmet.Add("Maximise " + kind + ".");
            return unmet;
        }
    }
}
