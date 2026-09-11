using System;
using System.Globalization;
using System.Text;

namespace LittleLifeline.Core
{
    public static class LifelineRules
    {
        private static readonly string[] CrewNames = { "Mara", "Ivo", "Nell", "Jun", "Ada", "Otis" };
        private static readonly CrewRole[] CrewRoles = { CrewRole.Doctor, CrewRole.Technician, CrewRole.Nurse, CrewRole.Nurse, CrewRole.Doctor, CrewRole.Technician };
        public const int TicksPerSecond = 10;
        public const int CarriageSlots = 4;
        public const int MaxCrew = 6;
        public const int MaxPatients = 24;
        public const int MaxWaiting = 18;
        public const int WeeklySeconds = 240;
        public const int MaxRoomLevel = 3;
        public const int MaxCrewLevel = 3;
        public const int WeeklyScoreUnit = 1000000;

        public static string TownName(TownId town) => town == TownId.Copperhill ? "Copperhill" : town == TownId.Seabrook ? "Seabrook" : "Willowbank";
        public static string RoomName(RoomKind kind) => kind == RoomKind.Diagnostics ? "Diagnostics" : kind == RoomKind.Recovery ? "Recovery" : "Consultation";
        public static string RoleName(CrewRole role) => role == CrewRole.Technician ? "Technician" : role == CrewRole.Nurse ? "Nurse" : "Doctor";
        public static string PolicyName(ServicePolicy policy) => policy == ServicePolicy.ShortVisitsFirst ? "Short visits first" : policy == ServicePolicy.ProjectFirst ? "Town project first" : "Longest waiting first";
        public static string TownDescription(TownId town) => town == TownId.Copperhill ? "A workshop town with a testing backlog." : town == TownId.Seabrook ? "A seaside community that needs room to rest." : "A riverside stop with plenty of quick consultations.";
        public static string DemandDescription(TownId town) => town == TownId.Copperhill ? "30% consultations · 50% tests · 20% recovery" : town == TownId.Seabrook ? "30% consultations · 20% tests · 50% recovery" : "60% consultations · 25% tests · 15% recovery";
        public static int TownUnlockReputation(TownId town) => town == TownId.Copperhill ? 60 : town == TownId.Seabrook ? 180 : 0;
        public static int BuildCost(RoomKind kind) => kind == RoomKind.Consultation ? 100 : 120;
        public static int UpgradeCost(CarriageState room) => room == null ? 0 : 70 * room.Level;
        public static int HireCost(int crewCount) => 180 + Math.Max(0, crewCount - 3) * 120;
        public static int HireReputation(int crewCount) => 15 + Math.Max(0, crewCount - 3) * 20;
        public static string NextHireName(int crewCount) => crewCount >= 0 && crewCount < MaxCrew ? CrewNames[crewCount] : "Full crew";
        public static CrewRole NextHireRole(int crewCount) => CrewRoles[Math.Max(0, Math.Min(MaxCrew - 1, crewCount))];
        public static int TrainCost(CrewState crew) => crew == null ? 0 : 90 * crew.Level;
        public static int RefitCost(RoomKind kind) => BuildCost(kind) / 2;
        public static int ProjectGoal(TownId town, int projectIndex) => (town == TownId.Willowbank ? 84 : town == TownId.Copperhill ? 140 : 180) + (projectIndex == 1 ? 36 : 0);
        public static int ProjectReward(TownId town, int projectIndex) => 160 + (int)town * 60 + projectIndex * 40;
        public static CarePath ProjectPath(TownId town, int projectIndex) => projectIndex == 1 ? CarePath.Diagnostics : town == TownId.Seabrook ? CarePath.Recovery : CarePath.Consultation;
        public static string ProjectName(TownId town, int projectIndex)
        {
            if (town == TownId.Copperhill) return projectIndex == 0 ? "Reopen the workshop" : "Restore the clock tower";
            if (town == TownId.Seabrook) return projectIndex == 0 ? "Restore the seaside clinic" : "Repair the lighthouse path";
            return projectIndex == 0 ? "Grow the station garden" : "Reopen the village school";
        }
        public static RoomKind NeededRoom(PatientState patient) => patient.Stage == 0 ? RoomKind.Consultation : (RoomKind)patient.Path;
        public static int BaseTreatmentTicks(RoomKind kind) => kind == RoomKind.Consultation ? 100 : kind == RoomKind.Diagnostics ? 240 : 260;
        public static CrewRole SpecialistFor(RoomKind kind) => kind == RoomKind.Consultation ? CrewRole.Doctor : kind == RoomKind.Diagnostics ? CrewRole.Technician : CrewRole.Nurse;
        /// <summary>Resident transfer time between carriage slots; -1 is the station. Crew or queue waiting is separate.</summary>
        public static double TransferSeconds(int fromSlot, int toSlot)
            => TransferTicks(fromSlot, toSlot) / (double)TicksPerSecond;
        internal static long TransferTicks(int fromSlot, int toSlot)
            => 6 + Math.Abs((long)fromSlot - toSlot) * 8;
        public static double TreatmentSeconds(CarriageState room, CrewState crew)
            => TreatmentTicks(room, crew) / (double)TicksPerSecond;
        internal static int TreatmentTicks(CarriageState room, CrewState crew)
        {
            var rolePercent = SpecialistFor(room.Kind) == crew.Role ? 75 : 100;
            return Math.Max(10, BaseTreatmentTicks(room.Kind) * rolePercent /
                (100 + (room.Level - 1) * 20 + (crew.Level - 1) * 10));
        }
        public static string WeeklyIdentifier(DateTimeOffset date)
        {
            var utc = date.UtcDateTime.Date;
            var sinceMonday = ((int)utc.DayOfWeek + 6) % 7;
            return utc.AddDays(-sinceMonday).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        public static ulong WeeklySeed(DateTimeOffset date)
        {
            unchecked
            {
                var hash = 0xCBF29CE484222325ul;
                foreach (var value in Encoding.UTF8.GetBytes("little-lifeline-weekly-v1:" + WeeklyIdentifier(date)))
                    hash = (hash ^ value) * 0x100000001B3ul;
                return hash;
            }
        }
    }
}
