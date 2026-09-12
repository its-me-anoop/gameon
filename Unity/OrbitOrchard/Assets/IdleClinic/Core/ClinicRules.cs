using System;
using System.Linq;

namespace IdleClinic.Core
{
    public static class ClinicRules
    {
        public const int TicksPerSecond = 10;
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

        public static int TrackCap(int tier) => tier == 1 ? 2 : tier == 2 ? 4 : 6;
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
        public static int UnpaidQueueCapacity(ClinicState state) => 6 + state.Room(ClinicRoom.Reception).FacilitiesLevel - 1;
        public static int WaitingCapacity(ClinicState state) => state.Room(ClinicRoom.Waiting).Built
            ? 4 + 2 * (state.Room(ClinicRoom.Waiting).FacilitiesLevel - 1) : 2;
        public static long VisitFee(ClinicState state)
        {
            var percent = 100 + 15 * (state.Room(ClinicRoom.FirstAid).FacilitiesLevel - 1)
                + 5 * state.Rooms.Where(r => r.Built).Sum(r => r.DecorationLevel - 1);
            return 50L * percent / 100;
        }
        public static double ReceptionSeconds(ClinicState state) => ReceptionTicks(state) / 10d;
        public static double TreatmentSeconds(ClinicState state) => TreatmentTicks(state) / 10d;
        public static double WaitingCallSeconds(ClinicState state) => WaitingCallTicks(state) / 10d;
        public static int ReceptionTicks(ClinicState state) => SpeedTicks(140, state.Room(ClinicRoom.Reception).EquipmentLevel);
        public static int TreatmentTicks(ClinicState state) => SpeedTicks(180, state.Room(ClinicRoom.FirstAid).EquipmentLevel);
        public static int WaitingCallTicks(ClinicState state) => SpeedTicks(20, state.Room(ClinicRoom.Waiting).EquipmentLevel);
        private static int SpeedTicks(int baseTicks, int level) => (baseTicks * 100 + (100 + 15 * (level - 1)) - 1) / (100 + 15 * (level - 1));
        private static long ScaleCost(long basis, long numerator, long denominator, int exponent)
        {
            long top = basis, bottom = 1;
            for (var i = 0; i < exponent; i++) { top = checked(top * numerator); bottom = checked(bottom * denominator); }
            return (top + bottom - 1) / bottom;
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
