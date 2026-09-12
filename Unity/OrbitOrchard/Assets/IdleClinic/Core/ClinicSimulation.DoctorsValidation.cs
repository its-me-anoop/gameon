using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleClinic.Core
{
    public sealed partial class ClinicSimulation
    {
        private static bool IsValidDoctorsState(ClinicState state)
        {
            if (state == null || state.SchemaVersion != 3 || state.RulesVersion != 3 || state.Location != ClinicLocation.DoctorsClinic
                || state.DoctorsClinicUnlocked || state.Tutorial != ClinicTutorialStep.Complete || !state.WaitingRoomUnlocked
                || state.Tick < 0 || state.Tick >= MaximumTick || state.PausedTrafficTicks < 0 || state.PausedTrafficTicks > state.Tick
                || double.IsNaN(state.SubTick) || state.SubTick < 0 || state.SubTick >= 1
                || state.NextArrivalTick <= state.Tick || state.NextArrivalTick - state.Tick > ClinicRules.ArrivalIntervalTicks
                || state.NextPatientId < 1 || state.NextEventId < 1 || state.NextEventId > MaximumMoney || state.NextConstructionId < 0
                || !MoneyRange(state.Wallet) || !MoneyRange(state.TotalEarned) || !MoneyRange(state.TotalCollected) || !MoneyRange(state.TotalSpent)
                || !MoneyRange(state.TotalTransferredIn) || !MoneyRange(state.TotalTransferredOut)
                || state.TotalPayments < 0 || state.TotalPayments > state.NextPatientId || state.TotalTreatments < 0 || state.TotalTreatments > state.TotalPayments
                || state.TotalCollected > state.TotalEarned || state.TotalTips < 0 || state.TotalTips > 84L * state.TotalPayments
                || state.TotalEarned < 100L * state.TotalPayments + state.TotalTips || state.TotalEarned > 820L * state.TotalPayments + state.TotalTips
                || (decimal)state.Wallet != state.TotalCollected - (decimal)state.TotalSpent + state.TotalTransferredIn - state.TotalTransferredOut
                || state.Rooms == null || state.Rooms.Count != 5 || state.ReceptionDesks == null || state.TreatmentStations == null
                || state.ConsultationStations == null || state.PharmacyStations == null || state.Amenities == null || state.Amenities.Count != 4
                || state.Staff == null || state.Patients == null || state.Patients.Count > ClinicRules.MaximumPatientCount(state)
                || state.Construction == null || state.Construction.Count > 5 || state.TaxiRides == null || state.TaxiRides.Count > 2) return false;
            var roomKinds = new HashSet<ClinicRoom>();
            foreach (var room in state.Rooms)
            {
                if (room == null || !Defined(room.Kind) || !roomKinds.Add(room.Kind) || !room.Built || room.Tier < 1 || room.Tier > 6
                    || room.EquipmentLevel < 1 || room.EquipmentLevel > ClinicRules.TrackCap(room.Tier)
                    || room.FacilitiesLevel < 1 || room.FacilitiesLevel > ClinicRules.TrackCap(room.Tier)
                    || room.DecorationLevel < 1 || room.DecorationLevel > ClinicRules.TrackCap(room.Tier)) return false;
                var role = room.Kind == ClinicRoom.Reception ? ClinicStaffRole.Receptionist : room.Kind == ClinicRoom.FirstAid ? ClinicStaffRole.Nurse
                    : room.Kind == ClinicRoom.Consultation ? ClinicStaffRole.Doctor : ClinicStaffRole.Pharmacist;
                if (room.Kind == ClinicRoom.Waiting ? room.StationCount != 0 : room.StationCount < 1 || room.StationCount > ClinicRules.MaximumStaff(state, role)) return false;
            }
            var amenityKinds = new HashSet<ClinicAmenity>();
            foreach (var amenity in state.Amenities)
                if (amenity == null || !Defined(amenity.Kind) || !amenityKinds.Add(amenity.Kind) || amenity.Level < 1
                    || amenity.Level > ClinicRules.AmenityCap(state, amenity.Kind) || !MoneyRange(amenity.Till)
                    || amenity.Kind != ClinicAmenity.Vending && amenity.Till != 0) return false;
            long till = state.Amenity(ClinicAmenity.Vending).Till;
            foreach (ClinicStaffRole role in Enum.GetValues(typeof(ClinicStaffRole)))
            {
                var count = ClinicRules.StationCount(state, role);
                var room = state.Room(ClinicRules.RoomForRole(role));
                if (count != room.StationCount || count < 1 || count > ClinicRules.StationCap(state, role)) return false;
                for (var id = 0; id < count; id++)
                {
                    if (role == ClinicStaffRole.Receptionist)
                    {
                        var desk = state.ReceptionDesks[id];
                        if (desk == null || desk.Id != id || !MoneyRange(desk.Till) || desk.PatientId < -1 || desk.LastStartedTick < -1 || desk.LastStartedTick > state.Tick) return false;
                        till += desk.Till;
                    }
                    else if (ClinicRules.Stations(state, role)[id] == null || ClinicRules.Stations(state, role)[id].Id != id) return false;
                    if (ClinicRules.StationLevel(state, role, id) < 1 || ClinicRules.StationLevel(state, role, id) > ClinicRules.TrackCap(room.Tier)) return false;
                }
                var staffCount = state.Staff.Count(s => s != null && s.Role == role);
                if (staffCount < 1 || staffCount > count || role == ClinicStaffRole.Receptionist && staffCount != count) return false;
            }
            if (till != state.TotalEarned - state.TotalCollected) return false;
            var staffIds = new HashSet<int>();
            var tasks = new HashSet<int>();
            foreach (var staff in state.Staff)
                if (staff == null || !Defined(staff.Role) || !staffIds.Add(staff.Id) || staff.Id != ClinicRules.StaffId(staff.Role, staff.StationId)
                    || staff.StationId < 0 || staff.StationId >= ClinicRules.StationCount(state, staff.Role)
                    || staff.TrainingLevel < 1 || staff.TrainingLevel > ClinicRules.ComponentCap(state, ClinicRules.RoomForRole(staff.Role))
                    || staff.PatientId < -1 || staff.ToAnchor != ClinicRules.StationStaffAnchor(staff.Role, staff.StationId)
                    || staff.FromAnchor != "entrance" && staff.FromAnchor != staff.ToAnchor
                    || staff.MoveStartedTick < 0 || staff.MoveStartedTick > state.Tick || staff.MoveEndsTick < staff.MoveStartedTick || staff.MoveEndsTick - staff.MoveStartedTick > ClinicDoctorsNavigation.WalkTicks("entrance", staff.ToAnchor, true)
                    || staff.PatientId >= 0 && (staff.MoveEndsTick > state.Tick || !tasks.Add(staff.PatientId))) return false;
            var anchors = DoctorsPatientAnchors(state);
            var patientIds = new HashSet<int>();
            var seats = new HashSet<int>();
            var queues = new HashSet<int>();
            var bays = new HashSet<int>();
            var cubicles = new HashSet<int>();
            var taxiWaiting = new HashSet<int>();
            var workstations = new HashSet<int>();
            int vendingVisitors = 0, movingCars = 0, reserved = 0, currentPaid = 0;
            long currentTips = 0;
            foreach (var patient in state.Patients)
            {
                if (patient == null || patient.Id < 0 || patient.Id >= state.NextPatientId || !patientIds.Add(patient.Id) || !Defined(patient.Phase)
                    || !Defined(patient.NextService) || patient.NextService == ClinicStaffRole.Receptionist
                    || patient.AppearanceId != ClinicRules.PatientAppearance(state.Seed, patient.Id)
                    || patient.ArrivalTick < 0 || patient.ArrivalTick > state.Tick || patient.PhaseStartedTick < 0 || patient.PhaseStartedTick > state.Tick
                    || !anchors.Contains(patient.FromAnchor) || !anchors.Contains(patient.ToAnchor)
                    || (IsTimed(patient.Phase) ? patient.PhaseEndsTick <= state.Tick || patient.PhaseEndsTick - patient.PhaseStartedTick > DoctorsMaximumPhaseTicks(patient.Phase) : patient.PhaseEndsTick != 0)
                    || patient.Payment < 0 || patient.Payment > 820 || patient.DeskId < -1 || patient.DeskId >= state.ReceptionDesks.Count
                    || patient.SeatId < -1 || patient.SeatId >= ClinicRules.WaitingCapacity(state) || !Defined(patient.VisitingAmenity)
                    || patient.ParkingBayId < -1 || patient.ParkingBayId >= ClinicRules.ParkingCapacity(state)
                    || patient.ParkingBayId >= 0 && (patient.Id == 0 || patient.Id % 3 != 0 || patient.UsesTaxi || !bays.Add(patient.ParkingBayId))
                    || patient.UsesTaxi != (patient.TaxiDockId >= 0) || patient.TaxiDockId < -1 || patient.TaxiDockId >= ClinicRules.TaxiDockCount(state)
                    || patient.UsesTaxi && patient.Id % 4 != 2
                    || patient.TaxiWaitingSlot < 0 || patient.TaxiWaitingSlot >= ClinicRules.TaxiWaitingCapacity
                    || patient.TaxiWaitingReserved && (!patient.UsesTaxi || !patient.FirstAidComplete || !taxiWaiting.Add(patient.TaxiWaitingSlot))
                    || patient.TipPaid < 0 || patient.TipPaid > 84 || !patient.UsedVending && patient.TipPaid != 0
                    || patient.UsedVending && !patient.Paid || patient.UsedToilet && (!patient.Paid || patient.Id % 3 != 1)) return false;
                var atReception = patient.Phase == ClinicPatientPhase.WalkingToReception || patient.Phase == ClinicPatientPhase.CheckingIn;
                var role = ServiceRole(patient.Phase);
                if (patient.Paid != (!IsUnpaidQueue(patient) && !atReception)
                    || patient.HasAdmissionReservation != (atReception || patient.Paid && !HasCompletedCare(patient.Phase))) return false;
                if (patient.HasAdmissionReservation) reserved++;
                if (patient.Paid) currentPaid++;
                currentTips += patient.TipPaid;
                if (IsUnpaidQueue(patient))
                {
                    if (patient.QueueIndex < 0 || patient.QueueIndex >= ClinicRules.UnpaidQueueCapacity(state) || !queues.Add(patient.QueueIndex)
                        || patient.Payment != 0 || patient.DeskId != -1) return false;
                }
                else if (patient.QueueIndex != -1) return false;
                if ((patient.Paid || atReception) && (patient.Payment < 100 || patient.DeskId < 0)) return false;
                if (atReception && (state.ReceptionDesks[patient.DeskId].PatientId != patient.Id || state.Staff.Find(s => s.Id == patient.DeskId)?.PatientId != patient.Id
                    || patient.ToAnchor != ClinicRules.DeskPatientAnchor(patient.DeskId))) return false;
                if (patient.ConsultationComplete && !patient.Paid || patient.FirstAidComplete && !patient.ConsultationComplete || patient.PharmacyComplete && !patient.FirstAidComplete) return false;
                if (!patient.Paid && (patient.ConsultationComplete || patient.FirstAidComplete || patient.PharmacyComplete)) return false;
                if (patient.Paid && patient.NextService != (!patient.ConsultationComplete ? ClinicStaffRole.Doctor : !patient.FirstAidComplete ? ClinicStaffRole.Nurse : ClinicStaffRole.Pharmacist)) return false;
                if (patient.Paid && HasCompletedCare(patient.Phase) != patient.PharmacyComplete) return false;
                if (role.HasValue)
                {
                    var station = PatientStation(patient, role.Value);
                    var staffId = ClinicRules.StaffId(role.Value, station);
                    if (station < 0 || !workstations.Add(staffId) || state.Staff.Find(s => s.Id == staffId)?.PatientId != patient.Id
                        || patient.NextService != role.Value || patient.SeatId != -1 || patient.ToAnchor != ClinicRules.StationPatientAnchor(role.Value, station)) return false;
                }
                if ((role != ClinicStaffRole.Nurse && patient.TreatmentStationId != -1) || (role != ClinicStaffRole.Doctor && patient.ConsultationStationId != -1)
                    || (role != ClinicStaffRole.Pharmacist && patient.PharmacyStationId != -1)) return false;
                if (patient.SeatId >= 0 && (!IsPaidWaiting(patient) || !seats.Add(patient.SeatId))) return false;
                if ((patient.Phase == ClinicPatientPhase.WalkingToWaiting || patient.Phase == ClinicPatientPhase.Seated) && patient.SeatId < 0) return false;
                if (patient.Phase == ClinicPatientPhase.Seated && (patient.FromAnchor != ClinicRules.WaitingAnchor(true, patient.SeatId) || patient.ToAnchor != patient.FromAnchor)) return false;
                if (IsMovingVehicle(patient.Phase) && ++movingCars > 1) return false;
                if (!ValidateDoctorsCar(patient) || !ValidateDoctorsTaxiPassenger(patient) || !ValidateArrivalPath(patient) || !ValidateQueueMove(state, patient)) return false;
                if (IsVisitingAmenity(patient))
                {
                    if (!patient.Paid || !patient.HasAdmissionReservation || patient.SeatId < 0 || role.HasValue
                        || (patient.VisitingAmenity != ClinicAmenity.Toilet && patient.VisitingAmenity != ClinicAmenity.Vending)) return false;
                    bool toilet = patient.VisitingAmenity == ClinicAmenity.Toilet;
                    if (toilet ? patient.ToiletCubicleId < 0 || patient.ToiletCubicleId >= 2 || !cubicles.Add(patient.ToiletCubicleId) || patient.Id % 3 != 1 : ++vendingVisitors > 1 || patient.ToiletCubicleId != -1) return false;
                    var anchor = toilet ? ClinicRules.ToiletPatientAnchor(state, patient.ToiletCubicleId) : ClinicRules.AmenityPatientAnchor(ClinicAmenity.Vending);
                    if (patient.Phase == ClinicPatientPhase.ReturningFromAmenity)
                    {
                        if (patient.FromAnchor != anchor || patient.ToAnchor != ClinicRules.WaitingAnchor(true, patient.SeatId) || (toilet ? !patient.UsedToilet : !patient.UsedVending)) return false;
                    }
                    else if (patient.ToAnchor != anchor || patient.Phase == ClinicPatientPhase.UsingAmenity && patient.FromAnchor != anchor || (toilet ? patient.UsedToilet : patient.UsedVending)) return false;
                }
                else if (patient.ToiletCubicleId != -1) return false;
            }
            if (reserved > ClinicRules.WaitingCapacity(state) + state.Staff.Count(s => s.Role != ClinicStaffRole.Receptionist)
                || currentPaid > state.TotalPayments || currentTips > state.TotalTips || state.Patients.Count(IsUnpaidQueue) > ClinicRules.UnpaidQueueCapacity(state)) return false;
            foreach (var staff in state.Staff.Where(s => s.PatientId >= 0))
            {
                var patient = state.Patients.Find(p => p.Id == staff.PatientId);
                if (patient == null) return false;
                if (staff.Role == ClinicStaffRole.Receptionist)
                {
                    if (patient.DeskId != staff.StationId || (patient.Phase != ClinicPatientPhase.WalkingToReception && patient.Phase != ClinicPatientPhase.CheckingIn)) return false;
                }
                else if (ServiceRole(patient.Phase) != staff.Role || PatientStation(patient, staff.Role) != staff.StationId) return false;
            }
            foreach (var desk in state.ReceptionDesks)
                if (desk.PatientId >= 0 && !state.Patients.Any(p => p.Id == desk.PatientId && p.DeskId == desk.Id
                    && (p.Phase == ClinicPatientPhase.WalkingToReception || p.Phase == ClinicPatientPhase.CheckingIn))) return false;
            if (!ValidateTaxiRides(state, movingCars)) return false;
            var jobIds = new HashSet<int>(); var jobRooms = new HashSet<ClinicRoom>();
            foreach (var job in state.Construction)
                if (job == null || job.Id < 0 || job.Id >= state.NextConstructionId || !jobIds.Add(job.Id) || !Defined(job.Room) || !jobRooms.Add(job.Room)
                    || job.Kind != ClinicConstructionKind.RoomRenovation || job.StartedTick < 0 || job.StartedTick > state.Tick || job.EndsTick <= state.Tick
                    || state.Room(job.Room).Tier >= 6 || job.TargetTier != state.Room(job.Room).Tier + 1
                    || job.PaidCost != ClinicRules.RenovationCost(state, job.Room)
                    || job.EndsTick - job.StartedTick != 10L * ClinicRules.RenovationSeconds(state, job.Room)) return false;
            return true;
        }
        private static bool MoneyRange(long amount) => amount >= 0 && amount <= MaximumMoney;
        private static ClinicStaffRole? ServiceRole(ClinicPatientPhase phase) => phase == ClinicPatientPhase.WalkingToConsultation || phase == ClinicPatientPhase.Consulting ? ClinicStaffRole.Doctor
            : phase == ClinicPatientPhase.WalkingToTreatment || phase == ClinicPatientPhase.Treating ? ClinicStaffRole.Nurse
            : phase == ClinicPatientPhase.WalkingToPharmacy || phase == ClinicPatientPhase.Dispensing ? ClinicStaffRole.Pharmacist : (ClinicStaffRole?)null;
        private static int PatientStation(ClinicPatientState patient, ClinicStaffRole role) => role == ClinicStaffRole.Nurse ? patient.TreatmentStationId : role == ClinicStaffRole.Doctor ? patient.ConsultationStationId : patient.PharmacyStationId;
        private static int DoctorsMaximumPhaseTicks(ClinicPatientPhase phase) => IsWalkingPhase(phase) ? 600 : phase == ClinicPatientPhase.Consulting ? 480 : phase == ClinicPatientPhase.Dispensing ? 240
            : phase == ClinicPatientPhase.WalkingToConsultation || phase == ClinicPatientPhase.WalkingToPharmacy ? 100
            : phase == ClinicPatientPhase.Treating ? 360 : phase == ClinicPatientPhase.CheckingIn ? 280
            : phase == ClinicPatientPhase.WalkingToTreatment ? 70 : phase == ClinicPatientPhase.WalkingToTaxi ? 80
            : phase == ClinicPatientPhase.UsingAmenity ? 120 : MaximumPhaseTicks(phase);
        private static HashSet<string> DoctorsPatientAnchors(ClinicState state)
        {
            var anchors = new HashSet<string>(StringComparer.Ordinal) { "entrance", "exit" };
            foreach (ClinicStaffRole role in Enum.GetValues(typeof(ClinicStaffRole)))
                for (int i = 0; i < ClinicRules.StationCount(state, role); i++) anchors.Add(ClinicRules.StationPatientAnchor(role, i));
            for (int i = 0; i < ClinicRules.UnpaidQueueCapacity(state); i++) anchors.Add(ClinicRules.QueueAnchor(i));
            for (int i = 0; i < ClinicRules.WaitingCapacity(state); i++) anchors.Add(ClinicRules.WaitingAnchor(true, i));
            for (int i = 0; i < ClinicRules.ParkingCapacity(state); i++) anchors.Add(ClinicRules.ParkingPatientAnchor(i));
            for (int i = 0; i < 2; i++) { anchors.Add(ClinicRules.ToiletPatientAnchor(state, i)); anchors.Add(ClinicRules.TaxiPatientAnchor(i)); }
            for (int i = 0; i < ClinicRules.TaxiWaitingCapacity; i++) anchors.Add(ClinicRules.TaxiWaitingAnchor(i));
            anchors.Add(ClinicRules.AmenityPatientAnchor(ClinicAmenity.Vending));
            return anchors;
        }
        private static bool ValidateArrivalPath(ClinicPatientState patient)
        {
            if (patient.ArrivalPath == null) return false;
            if (patient.Phase != ClinicPatientPhase.Arriving) return patient.ArrivalPath.Count == 0;
            if (patient.ArrivalPath.Count < 2 || patient.ArrivalPath.Count > 32) return false;
            foreach (var point in patient.ArrivalPath)
                if (point == null || float.IsNaN(point.X) || float.IsNaN(point.Z) || float.IsInfinity(point.X) || float.IsInfinity(point.Z)
                    || point.X < -25 || point.X > 25 || point.Z < -12 || point.Z > 10) return false;
            var end = ClinicDoctorsNavigation.Anchor(patient.ToAnchor);
            var last = patient.ArrivalPath[patient.ArrivalPath.Count - 1];
            return Math.Abs(last.X - end.x) < .002 && Math.Abs(last.Z - end.z) < .002
                && patient.PhaseEndsTick - patient.PhaseStartedTick == ClinicDoctorsNavigation.WalkTicks(patient.ArrivalPath);
        }
        private static bool ValidateQueueMove(ClinicState state, ClinicPatientState patient)
        {
            if (patient.QueueMovePath == null || patient.QueueMovePath.Count == 0)
                return patient.QueueMoveStartedTick == 0 && patient.QueueMoveEndsTick == 0;
            if (patient.Phase != ClinicPatientPhase.ReceptionQueue || patient.QueueMovePath.Count < 2 || patient.QueueMovePath.Count > 32
                || patient.QueueMoveStartedTick < 0 || patient.QueueMoveStartedTick > state.Tick || patient.QueueMoveEndsTick <= state.Tick) return false;
            foreach (var point in patient.QueueMovePath)
                if (point == null || float.IsNaN(point.X) || float.IsNaN(point.Z) || float.IsInfinity(point.X) || float.IsInfinity(point.Z)
                    || point.X < -11.802f || point.X > -2.698f || point.Z < -9.652f || point.Z > -8.248f) return false;
            var end = ClinicDoctorsNavigation.Anchor(patient.ToAnchor);
            var last = patient.QueueMovePath[patient.QueueMovePath.Count - 1];
            return Math.Abs(last.X - end.x) < .002 && Math.Abs(last.Z - end.z) < .002
                && patient.QueueMoveEndsTick - patient.QueueMoveStartedTick == ClinicDoctorsNavigation.QueueMoveTicks(patient.QueueMovePath);
        }
        private static bool ValidateDoctorsCar(ClinicPatientState patient)
        {
            var bay = patient.ParkingBayId >= 0 ? ClinicRules.ParkingPatientAnchor(patient.ParkingBayId) : null;
            if (patient.FromAnchor.StartsWith("parking.", StringComparison.Ordinal) && patient.FromAnchor != bay || patient.ToAnchor.StartsWith("parking.", StringComparison.Ordinal) && patient.ToAnchor != bay) return false;
            if (patient.ParkingBayId >= 0 && (patient.Phase == ClinicPatientPhase.Arriving && patient.FromAnchor != bay || patient.Phase == ClinicPatientPhase.Leaving && patient.ToAnchor != bay)) return false;
            if (!IsVehiclePhase(patient.Phase)) return true;
            if (patient.ParkingBayId < 0) return false;
            bool entry = patient.Phase == ClinicPatientPhase.WaitingToPark || patient.Phase == ClinicPatientPhase.DrivingToParking;
            if (entry ? patient.FromAnchor != bay || patient.ToAnchor != ClinicRules.QueueAnchor(patient.QueueIndex) : patient.FromAnchor != bay || patient.ToAnchor != bay) return false;
            return patient.Phase != ClinicPatientPhase.DrivingToParking && patient.Phase != ClinicPatientPhase.DrivingFromParking
                || patient.PhaseEndsTick - patient.PhaseStartedTick == (entry ? ClinicRules.ParkingEntryTicks : ClinicRules.ParkingExitTicks);
        }
        private static bool ValidateDoctorsTaxiPassenger(ClinicPatientState patient)
        {
            bool phase = patient.Phase == ClinicPatientPhase.TaxiArriving || patient.Phase == ClinicPatientPhase.TaxiDroppingOff || patient.Phase == ClinicPatientPhase.WalkingToTaxi
                || patient.Phase == ClinicPatientPhase.WaitingForTaxi || patient.Phase == ClinicPatientPhase.TaxiPickingUp || patient.Phase == ClinicPatientPhase.TaxiDeparting
                || patient.Phase == ClinicPatientPhase.WalkingToTaxiBoarding;
            if (phase && !patient.UsesTaxi) return false;
            var dock = patient.UsesTaxi ? ClinicRules.TaxiPatientAnchor(patient.TaxiDockId) : null;
            var waiting = ClinicRules.TaxiWaitingAnchor(patient.TaxiWaitingSlot);
            if (patient.FromAnchor.StartsWith("taxi.", StringComparison.Ordinal) && patient.FromAnchor != dock && patient.FromAnchor != waiting
                || patient.ToAnchor.StartsWith("taxi.", StringComparison.Ordinal) && patient.ToAnchor != dock && patient.ToAnchor != waiting) return false;
            if (!phase) return true;
            bool entry = patient.Phase == ClinicPatientPhase.TaxiArriving || patient.Phase == ClinicPatientPhase.TaxiDroppingOff;
            if (entry) return !patient.Paid && patient.FromAnchor == dock && patient.ToAnchor == ClinicRules.QueueAnchor(patient.QueueIndex);
            if (!patient.PharmacyComplete) return false;
            if (patient.Phase == ClinicPatientPhase.WalkingToTaxi || patient.Phase == ClinicPatientPhase.WaitingForTaxi)
                return patient.ToAnchor == (patient.TaxiWaitingReserved ? waiting : dock); // legacy schema-3 input
            if (patient.Phase == ClinicPatientPhase.WalkingToTaxiBoarding)
                return patient.TaxiWaitingReserved && patient.FromAnchor == waiting && patient.ToAnchor == dock;
            return !patient.TaxiWaitingReserved && patient.ToAnchor == dock;
        }
        private static bool ValidateTaxiRides(ClinicState state, int movingCars)
        {
            var ids = new HashSet<long>(); var docks = new HashSet<int>(); var owners = new HashSet<int>();
            foreach (var ride in state.TaxiRides)
            {
                if (ride == null || !Defined(ride.Phase) || !ids.Add(ride.Id) || !docks.Add(ride.DockId) || !owners.Add(ride.PatientId)
                    || ride.Id != ride.PatientId * 2L + (ride.Pickup ? 1 : 0) || ride.DockId < 0 || ride.DockId >= 2
                    || ride.RoadRequestedTick < 0 || ride.RoadRequestedTick > ride.PhaseStartedTick
                    || ride.PhaseStartedTick < 0 || ride.PhaseStartedTick > state.Tick
                    || (ride.Phase == ClinicTaxiPhase.WaitingToDepart || ride.Phase == ClinicTaxiPhase.WaitingForPassenger ? ride.PhaseEndsTick != 0 : ride.PhaseEndsTick <= state.Tick)) return false;
                var patient = state.Patients.Find(p => p.Id == ride.PatientId);
                if (patient == null || !patient.UsesTaxi || patient.TaxiDockId != ride.DockId) return false;
                if ((ride.Phase == ClinicTaxiPhase.Approaching || ride.Phase == ClinicTaxiPhase.Departing) && ++movingCars > 1) return false;
                var duration = ride.Phase == ClinicTaxiPhase.Approaching ? ClinicRules.TaxiArrivalTicks : ride.Phase == ClinicTaxiPhase.Boarding ? ClinicRules.TaxiPickupTicks : ClinicRules.TaxiDepartureTicks;
                long savedDuration = ride.PhaseEndsTick - ride.PhaseStartedTick;
                if (ride.Phase == ClinicTaxiPhase.Approaching)
                {
                    // A paid upgrade never retimes a ride already moving; its captured duration
                    // must match one of the levels that could have existed when it started.
                    bool matches = false;
                    for (int level = 1; level <= state.Amenity(ClinicAmenity.Taxi).Level; level++)
                        if (savedDuration == (16000 + 100 + 15 * (level - 1) - 1) / (100 + 15 * (level - 1))) matches = true;
                    if (!matches) return false;
                }
                else if (ride.Phase != ClinicTaxiPhase.WaitingToDepart && ride.Phase != ClinicTaxiPhase.WaitingForPassenger && savedDuration != duration) return false;
                if (ride.Phase == ClinicTaxiPhase.WaitingForPassenger)
                {
                    if (!ride.Pickup || !patient.TaxiWaitingReserved || (patient.Phase != ClinicPatientPhase.WaitingForTaxi
                        && patient.Phase != ClinicPatientPhase.WalkingToTaxi && patient.Phase != ClinicPatientPhase.WalkingToTaxiBoarding)) return false;
                    continue;
                }
                var expected = ride.Phase == ClinicTaxiPhase.Approaching ? (ride.Pickup ? ClinicPatientPhase.WaitingForTaxi : ClinicPatientPhase.TaxiArriving)
                    : ride.Phase == ClinicTaxiPhase.Boarding ? (ride.Pickup ? ClinicPatientPhase.TaxiPickingUp : ClinicPatientPhase.TaxiDroppingOff) : ClinicPatientPhase.TaxiDeparting;
                if ((ride.Pickup || ride.Phase == ClinicTaxiPhase.Approaching || ride.Phase == ClinicTaxiPhase.Boarding) && patient.Phase != expected
                    && !(ride.Pickup && ride.Phase == ClinicTaxiPhase.Approaching && patient.Phase == ClinicPatientPhase.WalkingToTaxi && patient.TaxiWaitingReserved)) return false;
            }
            foreach (var patient in state.Patients.Where(p => p.Phase == ClinicPatientPhase.TaxiPickingUp || p.Phase == ClinicPatientPhase.TaxiDroppingOff || p.Phase == ClinicPatientPhase.TaxiDeparting || p.Phase == ClinicPatientPhase.WalkingToTaxiBoarding))
                if (!owners.Contains(patient.Id)) return false;
            return true;
        }
    }
}
