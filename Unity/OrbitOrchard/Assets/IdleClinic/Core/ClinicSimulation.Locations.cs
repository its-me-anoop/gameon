using System;
using System.Linq;

namespace IdleClinic.Core
{
    public sealed partial class ClinicSimulation
    {
        public static ClinicSimulation CreateForLocation(ClinicLocation location, ulong seed = 42)
        {
            if (location == ClinicLocation.StarterClinic) return CreateNew(seed);
            if (location != ClinicLocation.DoctorsClinic) throw new ArgumentOutOfRangeException(nameof(location));
            var state = new ClinicState { Location = location, Seed = seed, Tutorial = ClinicTutorialStep.Complete,
                WaitingRoomUnlocked = true, NextPatientId = 1, NextArrivalTick = ClinicRules.ArrivalIntervalTicks };
            foreach (ClinicRoom kind in Enum.GetValues(typeof(ClinicRoom)))
                state.Rooms.Add(new ClinicRoomState { Kind = kind, Built = true, StationCount = kind == ClinicRoom.Waiting ? 0 : 1 });
            state.ReceptionDesks.Add(new ReceptionDeskState { Id = 0 });
            state.TreatmentStations.Add(new TreatmentStationState { Id = 0 });
            state.ConsultationStations.Add(new TreatmentStationState { Id = 0 });
            state.PharmacyStations.Add(new TreatmentStationState { Id = 0 });
            foreach (ClinicAmenity kind in Enum.GetValues(typeof(ClinicAmenity)))
                state.Amenities.Add(new ClinicAmenityState { Kind = kind, Level = 1 });
            foreach (ClinicStaffRole role in Enum.GetValues(typeof(ClinicStaffRole)))
                state.Staff.Add(new ClinicStaffState { Id = ClinicRules.StaffId(role, 0), Role = role, StationId = 0,
                    FromAnchor = ClinicRules.StationStaffAnchor(role, 0), ToAnchor = ClinicRules.StationStaffAnchor(role, 0) });
            // The first visitor is ordinary and pays the doubled quote. No starting money is invented.
            state.Patients.Add(new ClinicPatientState { Id = 0, AppearanceId = ClinicRules.PatientAppearance(seed, 0),
                Phase = ClinicPatientPhase.Arriving, QueueIndex = 0, NextService = ClinicStaffRole.Doctor,
                ArrivalPath = ClinicDoctorsNavigation.ArrivalPath("entrance", ClinicRules.QueueAnchor(0)),
                FromAnchor = "entrance", ToAnchor = ClinicRules.QueueAnchor(0), PhaseEndsTick = ClinicDoctorsNavigation.WalkTicks("entrance", ClinicRules.QueueAnchor(0)) });
            return new ClinicSimulation(state);
        }

        public ClinicCommandResult HireStaff(ClinicStaffRole role)
        {
            if (role == ClinicStaffRole.Receptionist) return HireReceptionist();
            if (role == ClinicStaffRole.Nurse) return HireNurse();
            if (!Defined(role) || !ClinicRules.IsDoctors(State)) return No("This role is available at the doctors clinic.");
            var count = State.Staff.Count(s => s.Role == role);
            if (count >= ClinicRules.MaximumStaff(State, role)) return No("All staff for this role are hired.");
            var station = Enumerable.Range(0, ClinicRules.StationCount(State, role)).Where(id => !State.Staff.Any(s => s.Role == role && s.StationId == id)).DefaultIfEmpty(-1).First();
            if (station < 0) return No("Build another workstation first.");
            var cost = ClinicRules.HireCost(State, role);
            if (!CanSpend(cost)) return No("Save " + cost + " coins to hire this staff member.");
            Spend(cost);
            var staff = NewStaff(ClinicRules.StaffId(role, station), role, station);
            State.Staff.Add(staff);
            Emit(role == ClinicStaffRole.Doctor ? ClinicEventKind.DoctorHired : ClinicEventKind.PharmacistHired,
                ClinicRules.RoomForRole(role), staffId: staff.Id, amount: cost, source: "entrance");
            return Yes("Staff hired.", cost);
        }

        public ClinicCommandResult HireStaff(ClinicStaffRole role, int stationId)
        {
            if (role == ClinicStaffRole.Receptionist)
                return stationId == ReceptionistCount ? HireReceptionist() : No("This desk is already staffed.");
            if (!ClinicRules.IsDoctors(State))
                return role == ClinicStaffRole.Nurse && stationId == NurseCount ? HireNurse() : No("Choose an available workstation.");
            if (!Defined(role) || stationId < 0 || stationId >= ClinicRules.StationCount(State, role)
                || State.Staff.Any(s => s.Role == role && s.StationId == stationId)) return No("Choose an available workstation.");
            var cost = ClinicRules.HireCost(State, role);
            if (!CanSpend(cost)) return No("Save " + cost + " coins to hire this staff member.");
            Spend(cost);
            var staff = NewStaff(ClinicRules.StaffId(role, stationId), role, stationId);
            State.Staff.Add(staff);
            Emit(role == ClinicStaffRole.Nurse ? ClinicEventKind.NurseHired : role == ClinicStaffRole.Doctor ? ClinicEventKind.DoctorHired : ClinicEventKind.PharmacistHired,
                ClinicRules.RoomForRole(role), staffId: staff.Id, amount: cost, source: "entrance");
            return Yes("Staff hired.", cost);
        }

        public ClinicCommandResult AddStation(ClinicStaffRole role)
        {
            if (!TutorialComplete() || !Defined(role) || ClinicRules.MaximumStaff(State, role) == 0) return No("This workstation is unavailable.");
            if (role == ClinicStaffRole.Receptionist) return No("Hiring a receptionist includes the desk.");
            var count = ClinicRules.StationCount(State, role);
            if (count >= ClinicRules.StationCap(State, role)) return No(count >= ClinicRules.MaximumStaff(State, role)
                ? "All workstations are installed." : "Renovate this room to add a workstation.");
            var cost = ClinicRules.AddStationCost(State, role);
            if (!CanSpend(cost)) return No("Save " + cost + " coins for this workstation.");
            Spend(cost);
            ClinicRules.Stations(State, role).Add(new TreatmentStationState { Id = count });
            State.Room(ClinicRules.RoomForRole(role)).StationCount++;
            Emit(ClinicEventKind.StationAdded, ClinicRules.RoomForRole(role), amount: cost, source: ClinicRules.StationPatientAnchor(role, count));
            return Yes("Workstation ready. Hire its staff to open it.", cost);
        }

        public ClinicCommandResult UnlockDoctorsClinic()
        {
            if (State.Location != ClinicLocation.StarterClinic) return No("Open the doctors clinic from the starter clinic.");
            if (State.DoctorsClinicUnlocked) return No("The doctors clinic is already open.");
            if (!IsValidState(State)) return No("The clinic state is invalid.");
            var missing = ClinicRules.StarterCompletion(State);
            if (missing.Count != 0) return No(missing[0]);
            if (!CanSpend(ClinicRules.DoctorsClinicUnlockCost)) return No("Collect 100,000 coins to open the doctors clinic.");
            Spend(ClinicRules.DoctorsClinicUnlockCost);
            State.DoctorsClinicUnlocked = true;
            Emit(ClinicEventKind.DoctorsClinicUnlocked, ClinicRoom.Reception, amount: ClinicRules.DoctorsClinicUnlockCost, source: "entrance");
            return Yes("Doctors clinic unlocked.", ClinicRules.DoctorsClinicUnlockCost);
        }

        public ClinicCommandResult TransferWalletTo(ClinicSimulation destination)
        {
            if (destination == null || ReferenceEquals(this, destination) || destination.State.Location == State.Location)
                return No("Choose the other clinic.");
            if (!IsValidState(State) || !IsValidState(destination.State)) return No("The clinic state is invalid.");
            var amount = State.Wallet;
            if (destination.State.Wallet > MaximumMoney - amount || destination.State.TotalTransferredIn > MaximumMoney - amount
                || State.TotalTransferredOut > MaximumMoney - amount) return No("The clinic transfer limit is reached.");
            State.Wallet = 0;
            State.TotalTransferredOut += amount;
            destination.State.Wallet += amount;
            destination.State.TotalTransferredIn += amount;
            return Yes("Wallet transferred.", amount: amount);
        }

        private void DispatchClinicalRole(ClinicStaffRole role)
        {
            if (!ClinicRules.IsDoctors(State)) return;
            foreach (var staff in State.Staff.Where(s => s.Role == role).OrderBy(s => s.Id))
            {
                if (staff.PatientId >= 0 || staff.MoveEndsTick > State.Tick) continue;
                var patient = State.Patients.Where(p => p.NextService == role && (p.Phase == ClinicPatientPhase.WaitingForTreatment || p.Phase == ClinicPatientPhase.Seated)
                    && (role != ClinicStaffRole.Pharmacist || CanCallPatientDuringVehicleMovement(p) && CanReserveTaxiWaitingPlace(p))).OrderBy(p => p.Id).FirstOrDefault();
                if (patient == null) break;
                if (role == ClinicStaffRole.Pharmacist && patient.UsesTaxi) ReserveTaxiWaitingPlace(patient);
                staff.PatientId = patient.Id;
                patient.SeatId = -1;
                if (role == ClinicStaffRole.Doctor) patient.ConsultationStationId = staff.StationId;
                else patient.PharmacyStationId = staff.StationId;
                var call = patient.ToAnchor.StartsWith("waiting.seat.", StringComparison.Ordinal) ? ClinicRules.WaitingCallTicks(State) : 40;
                Phase(patient, role == ClinicStaffRole.Doctor ? ClinicPatientPhase.WalkingToConsultation : ClinicPatientPhase.WalkingToPharmacy,
                    patient.ToAnchor, ClinicRules.StationPatientAnchor(role, staff.StationId), 60 + call);
            }
        }
        private void StartClinicalService(ClinicPatientState patient, ClinicStaffRole role, ClinicPatientPhase phase, ClinicEventKind kind)
        {
            var station = role == ClinicStaffRole.Doctor ? patient.ConsultationStationId : patient.PharmacyStationId;
            Phase(patient, phase, patient.ToAnchor, patient.ToAnchor, ClinicRules.StationServiceTicks(State, role, station));
            Emit(kind, ClinicRules.RoomForRole(role), patient.Id, ClinicRules.StaffId(role, station), source: patient.ToAnchor);
        }
        private void FinishConsultation(ClinicPatientState patient)
        {
            State.Staff.Find(s => s.Id == ClinicRules.StaffId(ClinicStaffRole.Doctor, patient.ConsultationStationId)).PatientId = -1;
            Emit(ClinicEventKind.ConsultationCompleted, ClinicRoom.Consultation, patient.Id, ClinicRules.StaffId(ClinicStaffRole.Doctor, patient.ConsultationStationId), source: patient.ToAnchor);
            patient.ConsultationStationId = -1;
            patient.ConsultationComplete = true;
            patient.NextService = ClinicStaffRole.Nurse;
            Rest(patient, ClinicPatientPhase.WaitingForTreatment);
        }
        private void FinishDispensing(ClinicPatientState patient)
        {
            State.Staff.Find(s => s.Id == ClinicRules.StaffId(ClinicStaffRole.Pharmacist, patient.PharmacyStationId)).PatientId = -1;
            Emit(ClinicEventKind.DispensingCompleted, ClinicRoom.Pharmacy, patient.Id, ClinicRules.StaffId(ClinicStaffRole.Pharmacist, patient.PharmacyStationId), source: patient.ToAnchor);
            patient.PharmacyStationId = -1;
            patient.PharmacyComplete = true;
            patient.HasAdmissionReservation = false;
            BeginDeparture(patient);
        }
        private void RetargetArrival(ClinicPatientState patient, string target)
        {
            var duration = patient.PhaseEndsTick - patient.PhaseStartedTick;
            var progress = duration <= 0 ? 1d : Math.Max(0, Math.Min(1, (State.Tick - patient.PhaseStartedTick) / (double)duration));
            patient.ArrivalPath = ClinicDoctorsNavigation.RetargetArrivalPath(patient.ArrivalPath, progress, target);
            patient.PhaseStartedTick = State.Tick;
            patient.PhaseEndsTick = State.Tick + ClinicDoctorsNavigation.WalkTicks(patient.ArrivalPath);
        }
        private void JoinReceptionFromTransport(ClinicPatientState patient, string origin)
        {
            Phase(patient, ClinicPatientPhase.Arriving, origin, ClinicRules.QueueAnchor(patient.QueueIndex), 80);
            // Joining the physical queue happens after parking or drop-off. An older
            // booking must not jump visitors who have already walked into the clinic.
            ReindexQueue(patient);
        }

        private void RetargetQueueMove(ClinicPatientState patient, string target)
        {
            ClinicMovementPoint start;
            if (patient.QueueMovePath.Count >= 2 && patient.QueueMoveEndsTick > State.Tick)
                start = ClinicDoctorsNavigation.SampleArrivalPath(patient.QueueMovePath,
                    (State.Tick - patient.QueueMoveStartedTick) / (double)(patient.QueueMoveEndsTick - patient.QueueMoveStartedTick));
            else
            {
                var anchor = ClinicDoctorsNavigation.Anchor(patient.ToAnchor);
                start = new ClinicMovementPoint(anchor.x, anchor.z);
            }
            patient.QueueMovePath = ClinicDoctorsNavigation.QueueMovePath(patient.ToAnchor, target, start);
            patient.QueueMoveStartedTick = State.Tick;
            patient.QueueMoveEndsTick = State.Tick + ClinicDoctorsNavigation.QueueMoveTicks(patient.QueueMovePath);
        }
        private static void ClearQueueMove(ClinicPatientState patient)
        {
            patient.QueueMovePath.Clear();
            patient.QueueMoveStartedTick = patient.QueueMoveEndsTick = 0;
        }
        private bool CanApproachDoctorsDesk(ClinicPatientState patient, int deskId)
        {
            // Preserve FIFO through physical arrivals and queue movement. Offscreen
            // transport bookings do not prevent visitors already here from checking in.
            if (patient.Phase != ClinicPatientPhase.ReceptionQueue || patient.QueueIndex != 0 || patient.QueueMoveEndsTick > State.Tick)
                return false;
            if (State.Patients.Any(p => p.Phase == ClinicPatientPhase.WalkingToReception
                || ClinicDoctorsNavigation.ReceptionDepartureClearTick(p) > State.Tick)) return false;
            long arrives = State.Tick + ClinicDoctorsNavigation.WalkTicks(patient.ToAnchor, ClinicRules.DeskPatientAnchor(deskId));
            // Pay-and-leave has priority. An approaching visitor must finish crossing
            // the reception aisle before an already-running check-in can finish.
            return !State.Patients.Any(p => p.Phase == ClinicPatientPhase.CheckingIn && p.PhaseEndsTick <= arrives + 1);
        }

        private static bool IsWalkingPhase(ClinicPatientPhase phase) => phase == ClinicPatientPhase.Arriving || phase == ClinicPatientPhase.WalkingToReception
            || phase == ClinicPatientPhase.WalkingToWaiting || phase == ClinicPatientPhase.WalkingToTreatment || phase == ClinicPatientPhase.Leaving
            || phase == ClinicPatientPhase.WalkingToAmenity || phase == ClinicPatientPhase.ReturningFromAmenity
            || phase == ClinicPatientPhase.WalkingToConsultation || phase == ClinicPatientPhase.WalkingToPharmacy || phase == ClinicPatientPhase.WalkingToTaxi
            || phase == ClinicPatientPhase.WalkingToTaxiBoarding;

        private void BeginDeparture(ClinicPatientState patient)
        {
            if (patient.UsesTaxi)
                Phase(patient, ClinicPatientPhase.WalkingToTaxi, patient.ToAnchor, ClinicRules.TaxiWaitingAnchor(patient.TaxiWaitingSlot), 80);
            else Phase(patient, ClinicPatientPhase.Leaving, patient.ToAnchor,
                patient.ParkingBayId >= 0 ? ClinicRules.ParkingPatientAnchor(patient.ParkingBayId) : "exit", patient.ParkingBayId >= 0 ? 100 : 30);
        }
    }
}
