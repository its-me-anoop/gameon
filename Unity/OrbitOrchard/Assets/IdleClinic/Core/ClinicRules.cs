using System;
using System.Linq;

namespace IdleClinic.Core
{
    public static partial class ClinicRules
    {
        public const int TicksPerSecond = 10;
        public const long MaximumCurrency = 1000000000000000L;
        public const int MaximumNurses = 2;
        public const int MaximumReceptionists = 2;
        public const int MaximumRoomTier = 3;
        public const int MaximumPatients = 40;
        public const double MaximumOfflineSeconds = 8 * 60 * 60;
        public const int ArrivalIntervalTicks = 70;
        public const long WaitingRoomCost = 160;
        public const long TreatmentStationCost = 180;
        public const long ReceptionistCost = 300;
        public const int WaitingRoomBuildSeconds = 20;
        public const int StreetCrossingCycleTicks = 400;
        public const int StreetCrossingStartsTick = 110;
        public const int StreetCrossingEndsTick = 180;
        public const int ParkingEntryTicks = 180;
        public const int ParkingReverseTicks = 40;
        public const int ParkingGearChangeTicks = 8;
        public const int ParkingExitTravelTicks = 160;
        public const int ParkingExitTicks = ParkingReverseTicks + ParkingGearChangeTicks + ParkingExitTravelTicks;
        public const int FastestTreatmentTicks = 64;
        public const int EarliestCalledPatientCompletionTicks = 106;

        public static long TrafficTick(ClinicState state) => state.Tick - state.PausedTrafficTicks;
        public static int TrackCap(int tier) => Math.Max(1, Math.Min(6, tier)) * 2;
        public static int ComponentCap(int tier) => TrackCap(tier);
        public static long HireNurseCost(int nurses) => nurses == 0 ? 50 : nurses == 1 ? 450 : 0;
        public static long HireReceptionistCost(int receptionists) => receptionists == 1 ? ReceptionistCost : 0;
        public static long UpgradeBase(ClinicRoom room, UpgradeTrack track)
        {
            if (track == UpgradeTrack.Decoration) return 40;
            if (room == ClinicRoom.Reception) return track == UpgradeTrack.Equipment ? 60 : 50;
            if (room == ClinicRoom.FirstAid) return track == UpgradeTrack.Equipment ? 80 : 60;
            return track == UpgradeTrack.Equipment ? 35 : 45;
        }
        public static long UpgradeCost(ClinicRoomState room, UpgradeTrack track)
            => room == null ? 0 : ScaleCost(UpgradeBase(room.Kind, track), 8, 5, room.Level(track) - 1);
        public static long RenovationCost(ClinicRoomState room)
            => room == null ? 0 : ScaleCost(room.Kind == ClinicRoom.Reception ? 180 : room.Kind == ClinicRoom.FirstAid ? 250 : 120, 5, 2, room.Tier - 1);
        public static int RenovationSeconds(int currentTier) => currentTier == 1 ? 60 : currentTier == 2 ? 180 : 0;
        public static int UnpaidQueueCapacity(ClinicState state) => (IsDoctors(state) ? 12 : 6) + state.Room(ClinicRoom.Reception).FacilitiesLevel - 1;
        public static int WaitingCapacity(ClinicState state) => state.Room(ClinicRoom.Waiting).Built
            ? (IsDoctors(state) ? 8 : 4) + 2 * (state.Room(ClinicRoom.Waiting).FacilitiesLevel - 1) : 2;
        public static long VisitFee(ClinicState state)
        {
            var percent = 100 + 15 * (state.Room(ClinicRoom.FirstAid).FacilitiesLevel - 1)
                + 5 * state.Rooms.Where(r => r.Built).Sum(r => r.DecorationLevel - 1);
            if (IsDoctors(state)) percent += 10 * (state.Room(ClinicRoom.Consultation).FacilitiesLevel - 1) + 10 * (state.Room(ClinicRoom.Pharmacy).FacilitiesLevel - 1);
            return 50L * LocationMultiplier(state) * percent / 100;
        }
        public static double ReceptionSeconds(ClinicState state) => ReceptionTicks(state) / 10d;
        public static double TreatmentSeconds(ClinicState state) => TreatmentTicks(state) / 10d;
        public static double WaitingCallSeconds(ClinicState state) => WaitingCallTicks(state) / 10d;
        public static int ReceptionTicks(ClinicState state) => ReceptionTicks(state, 0);
        public static int TreatmentTicks(ClinicState state) => TreatmentTicks(state, 0);
        public static int WaitingCallTicks(ClinicState state, int equipmentLevelsAdded = 0) => SpeedTicks(20 * LocationMultiplier(state), state.Room(ClinicRoom.Waiting).EquipmentLevel + equipmentLevelsAdded);
        public static int PatientAppearance(ulong seed, int patientId) => (int)((seed % 12 + (ulong)patientId * 7) % 12);
        public static int ParkingCapacity(ClinicState state) => 2 * state.Amenity(ClinicAmenity.Parking).Level;
        public static long VendingTip(ClinicState state) => 5L * LocationMultiplier(state) * state.Amenity(ClinicAmenity.Vending).Level;
        public static long VendingTipForPatient(ClinicState state, ClinicPatientState patient)
        {
            if (patient.UsedVending || state.TotalEarned < 0 || state.TotalEarned > MaximumCurrency) return 0;
            var amount = VendingTip(state) + (patient.UsedToilet ? 2L * LocationMultiplier(state) * state.Amenity(ClinicAmenity.Toilet).Level : 0);
            if (amount <= 0 || state.TotalTips < 0 || state.TotalTips > MaximumCurrency - amount
                || state.Amenity(ClinicAmenity.Vending).Till < 0 || state.Amenity(ClinicAmenity.Vending).Till > MaximumCurrency - amount) return 0;
            var headroom = MaximumCurrency - state.TotalEarned;
            // Reception has already quoted these admissions. Optional tips cannot consume the
            // space needed to honour their pending payments.
            foreach (var admission in state.Patients.Where(p => !p.Paid && p.HasAdmissionReservation))
            {
                if (admission.Payment < 0 || admission.Payment > headroom) return 0;
                headroom -= admission.Payment;
            }
            return amount <= headroom ? amount : 0;
        }
        public static int AmenityUseTicks(ClinicState state, ClinicAmenity kind) => kind == ClinicAmenity.Toilet
            ? (6000 * LocationMultiplier(state) + 100 + 25 * (state.Amenity(kind).Level - 1) - 1) / (100 + 25 * (state.Amenity(kind).Level - 1)) : 30 * LocationMultiplier(state);
        public static int StationLevel(ClinicState state, ClinicStaffRole role, int stationId)
            => role == ClinicStaffRole.Receptionist ? state.ReceptionDesks.Find(d => d.Id == stationId)?.EquipmentLevel ?? 0
                : Stations(state, role)?.Find(s => s.Id == stationId)?.EquipmentLevel ?? 0;
        public static long StationUpgradeCost(ClinicState state, ClinicStaffRole role, int stationId)
        {
            var level = StationLevel(state, role, stationId);
            return level < 1 ? 0 : ScaleCost((role == ClinicStaffRole.Receptionist ? 90 : role == ClinicStaffRole.Nurse ? 110 : role == ClinicStaffRole.Doctor ? 150 : 120) * LocationMultiplier(state), 9, 5, level - 1);
        }
        public static long StaffTrainingCost(ClinicStaffState staff) => staff == null ? 0
            : ScaleCost(staff.Role == ClinicStaffRole.Receptionist ? 80 : 100, 9, 5, staff.TrainingLevel - 1);
        public static long AmenityUpgradeCost(ClinicAmenity kind, int currentLevel) => !Enum.IsDefined(typeof(ClinicAmenity), kind)
            || currentLevel < 0 || currentLevel >= 3 ? 0 : ScaleCost(kind == ClinicAmenity.Parking ? 220 : kind == ClinicAmenity.Toilet ? 140 : 180, 2, 1, currentLevel);
        public static int ReceptionTicks(ClinicState state, int deskId) => StationServiceTicks(state, ClinicStaffRole.Receptionist, deskId);
        public static int TreatmentTicks(ClinicState state, int stationId) => StationServiceTicks(state, ClinicStaffRole.Nurse, stationId);
        public static int StationServiceTicks(ClinicState state, ClinicStaffRole role, int stationId,
            int equipmentLevelsAdded = 0, int trainingLevelsAdded = 0, int roomEquipmentLevelsAdded = 0)
        {
            var room = state.Room(RoomForRole(role));
            var staff = state.Staff.Find(s => s.Role == role && s.StationId == stationId);
            var speed = 100 + 15 * (room.EquipmentLevel - 1 + roomEquipmentLevelsAdded)
                + 10 * (Math.Max(1, StationLevel(state, role, stationId)) - 1 + equipmentLevelsAdded)
                + 12 * ((staff?.TrainingLevel ?? 1) - 1 + trainingLevelsAdded);
            return ((role == ClinicStaffRole.Nurse ? 180 : role == ClinicStaffRole.Receptionist ? 140 : role == ClinicStaffRole.Doctor ? 240 : 120) * LocationMultiplier(state) * 100 + speed - 1) / speed;
        }
        public static string ParkingPatientAnchor(int id) => "parking.bay." + id + ".patient";
        public static string AmenityPatientAnchor(ClinicAmenity kind) => kind == ClinicAmenity.Toilet ? "waiting.toilet.patient" : "waiting.vending.patient";
        private static int SpeedTicks(int baseTicks, int level) => (baseTicks * 100 + (100 + 15 * (level - 1)) - 1) / (100 + 15 * (level - 1));
        private static long ScaleCost(long basis, long numerator, long denominator, int exponent)
        {
            if (basis <= 0 || numerator <= 0 || denominator <= 0 || exponent < 0) return 0;
            decimal value = basis;
            for (var i = 0; i < exponent; i++)
            {
                value = value * numerator / denominator;
                if (value >= MaximumCurrency) return MaximumCurrency;
            }
            return (long)Math.Ceiling(value);
        }
        public static string DeskPatientAnchor(int id) => "reception.desk." + id + ".patient";
        public static string DeskStaffAnchor(int id) => "reception.desk." + id + ".staff";
        public static string DeskCashAnchor(int id) => "reception.desk." + id + ".cash";
        public static string TreatmentPatientAnchor(int id) => "firstaid.station." + id + ".patient";
        public static string TreatmentStaffAnchor(int id) => "firstaid.station." + id + ".staff";
        public static string QueueAnchor(int id) => "reception.queue." + id;
        public static string WaitingAnchor(bool built, int id) => (built ? "waiting.seat." : "firstaid.standing.") + id;
    }
}
