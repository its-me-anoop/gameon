using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleClinic.Core
{
    /// <summary>Deterministic clinic rules. Money, reservations and timers belong to this saved state, never to animations.</summary>
    public sealed partial class ClinicSimulation
    {
        private const long MaximumTick = 1000000000000000L;
        private const long MaximumMoney = ClinicRules.MaximumCurrency;
        private static readonly HashSet<string> PatientAnchors = KnownPatientAnchors();
        private readonly List<ClinicEvent> pendingEvents = new List<ClinicEvent>();
        private bool captureEvents = true;
        public ClinicState State { get; }
        public double ElapsedSeconds => (State.Tick + State.SubTick) / ClinicRules.TicksPerSecond;
        public int NurseCount => State.Staff.Count(s => s.Role == ClinicStaffRole.Nurse);
        public int ReceptionistCount => State.ReceptionDesks.Count;
        public long TillCash => State.ReceptionDesks.Sum(d => d.Till) + State.Amenity(ClinicAmenity.Vending).Till;
        public int PaidWaitingCount => State.Patients.Count(IsPaidWaiting);
        public int AdmissionCapacity => ClinicRules.WaitingCapacity(State) + Math.Max(1, NurseCount);

        public ClinicSimulation(ClinicState state)
        {
            if (!IsValidState(state)) throw new ArgumentException("The clinic save is incomplete or invalid.", nameof(state));
            State = state;
        }

        public static ClinicSimulation CreateNew(ulong seed = 42)
        {
            var state = new ClinicState { Seed = seed, NextPatientId = 1 };
            state.Rooms.Add(new ClinicRoomState { Kind = ClinicRoom.Reception, Built = true, StationCount = 1 });
            state.Rooms.Add(new ClinicRoomState { Kind = ClinicRoom.FirstAid, Built = true, StationCount = 1 });
            state.Rooms.Add(new ClinicRoomState { Kind = ClinicRoom.Waiting });
            state.ReceptionDesks.Add(new ReceptionDeskState { Id = 0 });
            state.TreatmentStations.Add(new TreatmentStationState { Id = 0 });
            foreach (ClinicAmenity kind in Enum.GetValues(typeof(ClinicAmenity))) state.Amenities.Add(new ClinicAmenityState { Kind = kind });
            state.Staff.Add(new ClinicStaffState
            {
                Id = 0, Role = ClinicStaffRole.Receptionist, StationId = 0,
                FromAnchor = ClinicRules.DeskStaffAnchor(0), ToAnchor = ClinicRules.DeskStaffAnchor(0)
            });
            state.Patients.Add(new ClinicPatientState
            {
                Id = 0, AppearanceId = ClinicRules.PatientAppearance(seed, 0), Phase = ClinicPatientPhase.Arriving, QueueIndex = 0,
                FromAnchor = "entrance", ToAnchor = ClinicRules.QueueAnchor(0), PhaseEndsTick = 30
            });
            return new ClinicSimulation(state);
        }

        /// <summary>Returned events are drained once, including undrained commands since the previous call.</summary>
        public ClinicAdvanceReport Advance(double seconds, bool emitEvents = true)
        {
            var report = new ClinicAdvanceReport();
            if (!FinitePositive(seconds)) return report;
            seconds = Math.Min(seconds, ClinicRules.MaximumOfflineSeconds);
            var previousCapture = captureEvents;
            captureEvents = emitEvents;
            var before = ElapsedSeconds;
            var payments = State.TotalPayments;
            var earned = State.TotalEarned;
            var completed = State.TotalTreatments;
            var ticks = seconds * ClinicRules.TicksPerSecond + State.SubTick;
            var wholeTicks = (long)Math.Floor(ticks + 1e-8);
            var remainder = Math.Max(0, ticks - wholeTicks);
            var target = Math.Min(MaximumTick, State.Tick + wholeTicks);
            ProcessCurrentTick();
            while (State.Tick < target)
            {
                var next = NextEventTick();
                if (next > target) { State.Tick = target; break; }
                if (next <= State.Tick) throw new InvalidOperationException("A clinic event did not advance.");
                State.Tick = next;
                ProcessCurrentTick();
            }
            State.SubTick = State.Tick == MaximumTick ? 0 : remainder;
            report.Seconds = ElapsedSeconds - before;
            report.EarningsSeconds = report.Seconds;
            report.ConstructionSeconds = report.Seconds;
            report.PaymentsReceived = State.TotalPayments - payments;
            report.TillEarned = State.TotalEarned - earned;
            report.TreatmentsCompleted = State.TotalTreatments - completed;
            if (emitEvents) report.Events = DrainEvents();
            captureEvents = previousCapture;
            return report;
        }

        /// <summary>Up to eight hours of operations; all remaining wall time advances construction only.</summary>
        public ClinicAdvanceReport AdvanceOffline(double elapsedSeconds)
        {
            if (!FinitePositive(elapsedSeconds)) return new ClinicAdvanceReport();
            var capped = Math.Min(ClinicRules.MaximumOfflineSeconds, elapsedSeconds);
            var report = Advance(capped, false);
            report.WasCapped = elapsedSeconds > ClinicRules.MaximumOfflineSeconds;
            var skippedSeconds = elapsedSeconds - capped;
            var available = (MaximumTick - State.Tick) / (double)ClinicRules.TicksPerSecond;
            var skippedTicks = (long)Math.Floor(Math.Min(skippedSeconds, available) * ClinicRules.TicksPerSecond);
            if (skippedTicks > 0)
            {
                // Freeze operational progress beyond the earnings window. Shifting both ends keeps animation progress stable.
                foreach (var patient in State.Patients)
                {
                    patient.ArrivalTick += skippedTicks;
                    patient.PhaseStartedTick += skippedTicks;
                    if (IsTimed(patient.Phase)) patient.PhaseEndsTick += skippedTicks;
                }
                foreach (var staff in State.Staff)
                { staff.MoveStartedTick += skippedTicks; staff.MoveEndsTick += skippedTicks; }
                foreach (var desk in State.ReceptionDesks)
                    if (desk.LastStartedTick >= 0) desk.LastStartedTick += skippedTicks;
                if (State.NextArrivalTick >= 0) State.NextArrivalTick += skippedTicks;
                State.Tick += skippedTicks;
                var previousCapture = captureEvents;
                captureEvents = false;
                FinishConstruction();
                captureEvents = previousCapture;
            }
            report.Seconds = report.EarningsSeconds + skippedTicks / (double)ClinicRules.TicksPerSecond;
            report.ConstructionSeconds = report.Seconds;
            return report;
        }

        public List<ClinicEvent> DrainEvents()
        {
            var result = new List<ClinicEvent>(pendingEvents);
            pendingEvents.Clear();
            return result;
        }

        public ClinicCommandResult Collect(int deskId)
        {
            var desk = State.ReceptionDesks.Find(d => d.Id == deskId);
            if (desk == null) return No("Choose an open reception desk.");
            if (desk.Till == 0) return No("Payments collect here after check-in.");
            var amount = desk.Till;
            if (State.Wallet > MaximumMoney - amount) return No("The clinic wallet is full.");
            State.Wallet += amount;
            State.TotalCollected += amount;
            desk.Till = 0;
            Emit(ClinicEventKind.CashCollected, ClinicRoom.Reception, deskId: desk.Id,
                amount: amount, source: ClinicRules.DeskCashAnchor(desk.Id));
            if (State.Tutorial == ClinicTutorialStep.CollectFirstPayment) Tutorial(ClinicTutorialStep.HireFirstNurse);
            return Yes("Cash collected.", amount: amount);
        }

        public ClinicCommandResult HireNurse()
        {
            var count = NurseCount;
            if (count >= ClinicRules.MaximumNurses) return No("Both nurses are already hired.");
            if (State.Tutorial != ClinicTutorialStep.Complete && State.Tutorial != ClinicTutorialStep.HireFirstNurse)
                return No("Collect the first payment to hire your nurse.");
            if (count >= State.Room(ClinicRoom.FirstAid).StationCount) return No("Add a treatment station first.");
            var cost = ClinicRules.HireNurseCost(count);
            if (!CanSpend(cost)) return No("Save " + cost + " coins to hire this nurse.");
            Spend(cost);
            var staff = NewStaff(100 + count, ClinicStaffRole.Nurse, count);
            State.Staff.Add(staff);
            Emit(ClinicEventKind.NurseHired, ClinicRoom.FirstAid, staffId: staff.Id, amount: cost, source: "entrance");
            if (count == 0) Tutorial(ClinicTutorialStep.FirstTreatment);
            return Yes("Nurse hired.", cost);
        }

        public ClinicCommandResult HireReceptionist()
        {
            if (!TutorialComplete()) return No("Finish the first treatment before expanding.");
            if (ReceptionistCount >= ClinicRules.MaximumReceptionists) return No("Both reception desks are staffed.");
            var cost = ClinicRules.ReceptionistCost;
            if (!CanSpend(cost)) return No("Save " + cost + " coins for a receptionist and desk.");
            var id = ReceptionistCount;
            Spend(cost);
            State.ReceptionDesks.Add(new ReceptionDeskState { Id = id });
            State.Room(ClinicRoom.Reception).StationCount++;
            var staff = NewStaff(id, ClinicStaffRole.Receptionist, id);
            State.Staff.Add(staff);
            Emit(ClinicEventKind.ReceptionistHired, ClinicRoom.Reception, staffId: id, deskId: id,
                amount: cost, source: "entrance");
            return Yes("Receptionist and desk added.", cost);
        }

        public ClinicCommandResult BuildWaitingRoom()
        {
            if (!TutorialComplete() || !State.WaitingRoomUnlocked) return No("A waiting room unlocks when two paid patients are waiting.");
            if (State.Room(ClinicRoom.Waiting).Built || IsUnderConstruction(ClinicRoom.Waiting)) return No("The waiting room is already built or being prepared.");
            if (State.NextConstructionId == int.MaxValue) return No("This clinic cannot start another construction job.");
            if (!CanSpend(ClinicRules.WaitingRoomCost)) return No("Save 160 coins for the waiting room.");
            StartConstruction(ClinicRoom.Waiting, ClinicConstructionKind.WaitingRoom, 1,
                ClinicRules.WaitingRoomCost, ClinicRules.WaitingRoomBuildSeconds);
            return Yes("Waiting room construction started.", ClinicRules.WaitingRoomCost);
        }

        public ClinicCommandResult AddTreatmentStation()
        {
            var room = State.Room(ClinicRoom.FirstAid);
            if (!TutorialComplete() || room.Tier < 2) return No("Upgrade the first-aid room to tier 2 first.");
            if (room.StationCount >= 2) return No("Both treatment stations are installed.");
            if (!CanSpend(ClinicRules.TreatmentStationCost)) return No("Save 180 coins for the second treatment station.");
            Spend(ClinicRules.TreatmentStationCost);
            State.TreatmentStations.Add(new TreatmentStationState { Id = room.StationCount++ });
            Emit(ClinicEventKind.StationAdded, ClinicRoom.FirstAid, amount: ClinicRules.TreatmentStationCost,
                source: ClinicRules.TreatmentPatientAnchor(1));
            return Yes("Second station ready. Hire its nurse to open it.", ClinicRules.TreatmentStationCost);
        }

        public ClinicCommandResult Upgrade(ClinicRoom kind, UpgradeTrack track)
        {
            if (!TutorialComplete() || !Defined(kind) || !Defined(track)) return No("Finish the first treatment before upgrading.");
            var room = State.Room(kind);
            if (!room.Built) return No("Build this room first.");
            if (room.Level(track) >= ClinicRules.TrackCap(room.Tier)) return No("Upgrade the room tier to unlock more improvements.");
            var cost = ClinicRules.UpgradeCost(room, track);
            if (!CanSpend(cost)) return No("Save " + cost + " coins for this improvement.");
            Spend(cost);
            if (track == UpgradeTrack.Equipment) room.EquipmentLevel++;
            else if (track == UpgradeTrack.Facilities) room.FacilitiesLevel++;
            else room.DecorationLevel++;
            Emit(ClinicEventKind.EquipmentUpgraded, kind, amount: cost, source: RoomAnchor(kind));
            return Yes("Room improved.", cost);
        }

        public ClinicCommandResult Renovate(ClinicRoom kind)
        {
            if (!TutorialComplete() || !Defined(kind)) return No("Finish the first treatment before renovating.");
            var room = State.Room(kind);
            if (!room.Built) return No("Build this room first.");
            if (room.Tier >= ClinicRules.MaximumRoomTier) return No("This room has reached its largest size.");
            if (IsUnderConstruction(kind)) return No("This room is already being upgraded.");
            if (State.NextConstructionId == int.MaxValue) return No("This clinic cannot start another construction job.");
            var cost = ClinicRules.RenovationCost(room);
            if (!CanSpend(cost)) return No("Save " + cost + " coins for this room upgrade.");
            StartConstruction(kind, ClinicConstructionKind.RoomRenovation, room.Tier + 1,
                cost, ClinicRules.RenovationSeconds(room.Tier));
            return Yes("Room upgrade started. Care continues while it is prepared.", cost);
        }

        public bool IsUnderConstruction(ClinicRoom room) => State.Construction.Any(c => c.Room == room);

        private void StartConstruction(ClinicRoom room, ClinicConstructionKind kind, int tier, long cost, int seconds)
        {
            Spend(cost);
            State.Construction.Add(new ClinicConstructionState
            {
                Id = State.NextConstructionId++, Room = room, Kind = kind, TargetTier = tier,
                StartedTick = State.Tick, EndsTick = State.Tick + seconds * 10L, PaidCost = cost
            });
            Emit(ClinicEventKind.ConstructionStarted, room, amount: cost, source: RoomAnchor(room));
        }

        private ClinicStaffState NewStaff(int id, ClinicStaffRole role, int station)
            => new ClinicStaffState
            {
                Id = id, Role = role, StationId = station, FromAnchor = "entrance",
                ToAnchor = role == ClinicStaffRole.Nurse ? ClinicRules.TreatmentStaffAnchor(station) : ClinicRules.DeskStaffAnchor(station),
                MoveStartedTick = State.Tick, MoveEndsTick = State.Tick + 40
            };

        private long NextEventTick()
        {
            var next = State.NextArrivalTick < 0 ? long.MaxValue : State.NextArrivalTick;
            foreach (var patient in State.Patients)
                if (IsTimed(patient.Phase)) next = Math.Min(next, patient.PhaseEndsTick);
            foreach (var staff in State.Staff)
                if (staff.MoveEndsTick > State.Tick) next = Math.Min(next, staff.MoveEndsTick);
            foreach (var job in State.Construction) next = Math.Min(next, job.EndsTick);
            return next;
        }

        private void ProcessCurrentTick()
        {
            // Stable ordering makes simultaneous payments, arrivals and treatments independent of frame size.
            foreach (var patient in State.Patients.OrderBy(p => p.Id).ToArray())
            {
                if (!IsTimed(patient.Phase) || patient.PhaseEndsTick > State.Tick) continue;
                switch (patient.Phase)
                {
                    case ClinicPatientPhase.Arriving:
                        Rest(patient, ClinicPatientPhase.ReceptionQueue);
                        break;
                    case ClinicPatientPhase.WalkingToReception:
                        Phase(patient, ClinicPatientPhase.CheckingIn, patient.ToAnchor, patient.ToAnchor, ClinicRules.ReceptionTicks(State, patient.DeskId));
                        Emit(ClinicEventKind.CheckInStarted, ClinicRoom.Reception, patient.Id, deskId: patient.DeskId,
                            source: patient.ToAnchor);
                        break;
                    case ClinicPatientPhase.CheckingIn:
                        FinishPayment(patient);
                        break;
                    case ClinicPatientPhase.WalkingToWaiting:
                        Rest(patient, ClinicPatientPhase.Seated);
                        break;
                    case ClinicPatientPhase.WalkingToTreatment:
                        Phase(patient, ClinicPatientPhase.Treating, patient.ToAnchor, patient.ToAnchor, ClinicRules.TreatmentTicks(State, patient.TreatmentStationId));
                        Emit(ClinicEventKind.TreatmentStarted, ClinicRoom.FirstAid, patient.Id,
                            staffId: 100 + patient.TreatmentStationId, source: patient.ToAnchor);
                        break;
                    case ClinicPatientPhase.Treating:
                        FinishTreatment(patient);
                        break;
                    case ClinicPatientPhase.WalkingToAmenity:
                        Phase(patient, ClinicPatientPhase.UsingAmenity, patient.ToAnchor, patient.ToAnchor, ClinicRules.AmenityUseTicks(State, patient.VisitingAmenity));
                        break;
                    case ClinicPatientPhase.UsingAmenity:
                        FinishAmenityVisit(patient);
                        break;
                    case ClinicPatientPhase.ReturningFromAmenity:
                        Rest(patient, ClinicPatientPhase.Seated);
                        break;
                    case ClinicPatientPhase.Leaving:
                        State.Patients.Remove(patient);
                        break;
                }
            }
            FinishConstruction();
            if (State.NextArrivalTick >= 0 && State.NextArrivalTick <= State.Tick) AdmitPatient();
            DispatchNurses();
            CheckWaitingUnlock();
            PlaceWaitingPatients();
            DispatchAmenities();
            DispatchReception();
            ReindexQueue();
        }

        private void AdmitPatient()
        {
            State.NextArrivalTick = State.Tick + ClinicRules.ArrivalIntervalTicks;
            if (State.Patients.Count >= ClinicRules.MaximumPatients || State.NextPatientId == int.MaxValue
                || State.Patients.Count(IsUnpaidQueue) >= ClinicRules.UnpaidQueueCapacity(State)) return;
            var index = State.Patients.Count(IsUnpaidQueue);
            var patient = new ClinicPatientState { Id = State.NextPatientId++, ArrivalTick = State.Tick, QueueIndex = index };
            patient.AppearanceId = ClinicRules.PatientAppearance(State.Seed, patient.Id);
            if (patient.Id % 3 == 0)
            {
                for (var bay = 0; bay < ClinicRules.ParkingCapacity(State); bay++)
                {
                    if (State.Patients.Any(p => p.ParkingBayId == bay)) continue;
                    patient.ParkingBayId = bay;
                    break;
                }
            }
            var origin = patient.ParkingBayId >= 0 ? ClinicRules.ParkingPatientAnchor(patient.ParkingBayId) : "entrance";
            Phase(patient, ClinicPatientPhase.Arriving, origin, ClinicRules.QueueAnchor(index), patient.ParkingBayId >= 0 ? 100 : 30);
            State.Patients.Add(patient);
            Emit(ClinicEventKind.PatientArrived, ClinicRoom.Reception, patient.Id, source: origin);
        }

        private void DispatchReception()
        {
            // Longest-idle first prevents the first desk monopolising admission slots when treatment is the bottleneck.
            foreach (var desk in State.ReceptionDesks.OrderBy(d => d.LastStartedTick).ThenBy(d => d.Id))
            {
                var staff = State.Staff.Find(s => s.Id == desk.Id);
                if (desk.PatientId >= 0 || staff.MoveEndsTick > State.Tick) continue;
                if (State.Patients.Count(p => p.HasAdmissionReservation) >= AdmissionCapacity) break;
                var patient = State.Patients.Where(p => p.Phase == ClinicPatientPhase.ReceptionQueue).OrderBy(p => p.Id).FirstOrDefault();
                if (patient == null) break;
                patient.HasAdmissionReservation = true;
                patient.DeskId = desk.Id;
                patient.Payment = ClinicRules.VisitFee(State) + (patient.ParkingBayId >= 0 ? 5L * State.Amenity(ClinicAmenity.Parking).Level : 0);
                patient.QueueIndex = -1;
                desk.PatientId = patient.Id;
                desk.LastStartedTick = State.Tick;
                staff.PatientId = patient.Id;
                Phase(patient, ClinicPatientPhase.WalkingToReception, patient.ToAnchor, ClinicRules.DeskPatientAnchor(desk.Id), 30);
            }
        }

        private void FinishPayment(ClinicPatientState patient)
        {
            var desk = State.ReceptionDesks.Find(d => d.Id == patient.DeskId);
            desk.Till = checked(desk.Till + patient.Payment);
            State.TotalPayments++;
            State.TotalEarned = checked(State.TotalEarned + patient.Payment);
            patient.Paid = true;
            desk.PatientId = -1;
            State.Staff.Find(s => s.Id == desk.Id).PatientId = -1;
            Emit(ClinicEventKind.PaymentReceived, ClinicRoom.Reception, patient.Id, deskId: desk.Id,
                amount: patient.Payment, source: ClinicRules.DeskCashAnchor(desk.Id));
            Rest(patient, ClinicPatientPhase.WaitingForTreatment);
            if (patient.Id == 0 && State.Tutorial == ClinicTutorialStep.FirstArrival)
                Tutorial(ClinicTutorialStep.CollectFirstPayment);
        }

        private void DispatchNurses()
        {
            foreach (var staff in State.Staff.Where(s => s.Role == ClinicStaffRole.Nurse).OrderBy(s => s.Id))
            {
                if (staff.PatientId >= 0 || staff.MoveEndsTick > State.Tick) continue;
                // A seated patient finishes walking to their seat before being called elsewhere.
                var patient = State.Patients.Where(p => p.Phase == ClinicPatientPhase.WaitingForTreatment || p.Phase == ClinicPatientPhase.Seated)
                    .OrderBy(p => p.Id).FirstOrDefault();
                if (patient == null) break;
                staff.PatientId = patient.Id;
                patient.TreatmentStationId = staff.StationId;
                patient.SeatId = -1;
                var call = State.Room(ClinicRoom.Waiting).Built && patient.ToAnchor.StartsWith("waiting.seat.", StringComparison.Ordinal)
                    ? ClinicRules.WaitingCallTicks(State) : 20;
                Phase(patient, ClinicPatientPhase.WalkingToTreatment, patient.ToAnchor,
                    ClinicRules.TreatmentPatientAnchor(staff.StationId), 30 + call);
            }
        }

        private void PlaceWaitingPatients()
        {
            var waitingBuilt = State.Room(ClinicRoom.Waiting).Built;
            foreach (var patient in State.Patients.Where(IsPaidWaiting).OrderBy(p => p.Id))
            {
                if (patient.Phase == ClinicPatientPhase.WalkingToWaiting || IsVisitingAmenity(patient)) continue;
                if (patient.SeatId >= 0 && patient.ToAnchor == ClinicRules.WaitingAnchor(waitingBuilt, patient.SeatId)) continue;
                var occupied = new HashSet<int>(State.Patients.Where(p => p.Id != patient.Id && IsPaidWaiting(p) && p.SeatId >= 0).Select(p => p.SeatId));
                var seat = 0;
                while (occupied.Contains(seat)) seat++;
                if (seat >= ClinicRules.WaitingCapacity(State)) continue;
                patient.SeatId = seat;
                Phase(patient, ClinicPatientPhase.WalkingToWaiting, patient.ToAnchor, ClinicRules.WaitingAnchor(waitingBuilt, seat), 20);
            }
        }

        private void FinishTreatment(ClinicPatientState patient)
        {
            State.Staff.Find(s => s.Role == ClinicStaffRole.Nurse && s.StationId == patient.TreatmentStationId).PatientId = -1;
            patient.HasAdmissionReservation = false;
            State.TotalTreatments++;
            Emit(ClinicEventKind.TreatmentCompleted, ClinicRoom.FirstAid, patient.Id,
                staffId: 100 + patient.TreatmentStationId, source: patient.ToAnchor);
            patient.TreatmentStationId = -1;
            Phase(patient, ClinicPatientPhase.Leaving, patient.ToAnchor, patient.ParkingBayId >= 0 ? ClinicRules.ParkingPatientAnchor(patient.ParkingBayId) : "exit", patient.ParkingBayId >= 0 ? 100 : 30);
            if (State.Tutorial == ClinicTutorialStep.FirstTreatment)
            {
                Tutorial(ClinicTutorialStep.Complete);
                State.NextArrivalTick = State.Tick + ClinicRules.ArrivalIntervalTicks;
            }
        }

        private void FinishConstruction()
        {
            foreach (var job in State.Construction.Where(c => c.EndsTick <= State.Tick).OrderBy(c => c.Id).ToArray())
            {
                var room = State.Room(job.Room);
                room.Built = true;
                room.Tier = job.TargetTier;
                State.Construction.Remove(job);
                Emit(ClinicEventKind.ConstructionCompleted, job.Room, source: RoomAnchor(job.Room));
            }
        }

        private void CheckWaitingUnlock()
        {
            if (State.WaitingRoomUnlocked || !TutorialComplete() || PaidWaitingCount < 2) return;
            State.WaitingRoomUnlocked = true;
            Emit(ClinicEventKind.WaitingRoomUnlocked, ClinicRoom.Waiting, source: "waiting.plot");
        }

        private void ReindexQueue()
        {
            var index = 0;
            foreach (var patient in State.Patients.Where(IsUnpaidQueue).OrderBy(p => p.Id))
            {
                patient.QueueIndex = index++;
                patient.ToAnchor = ClinicRules.QueueAnchor(patient.QueueIndex);
                if (patient.Phase == ClinicPatientPhase.ReceptionQueue) patient.FromAnchor = patient.ToAnchor;
            }
        }

        private void Phase(ClinicPatientState patient, ClinicPatientPhase phase, string from, string to, int ticks)
        {
            patient.Phase = phase;
            patient.FromAnchor = from;
            patient.ToAnchor = to;
            patient.PhaseStartedTick = State.Tick;
            patient.PhaseEndsTick = State.Tick + ticks;
        }
        private void Rest(ClinicPatientState patient, ClinicPatientPhase phase)
        {
            patient.Phase = phase;
            patient.FromAnchor = patient.ToAnchor;
            patient.PhaseStartedTick = State.Tick;
            patient.PhaseEndsTick = 0;
        }
        private void Tutorial(ClinicTutorialStep step)
        {
            State.Tutorial = step;
            Emit(ClinicEventKind.TutorialAdvanced, ClinicRoom.FirstAid);
        }
        private void Emit(ClinicEventKind kind, ClinicRoom room, int patientId = -1, int staffId = -1,
            int deskId = -1, long amount = 0, string source = "", ClinicAmenity amenity = ClinicAmenity.Parking)
        {
            var id = State.NextEventId++;
            if (!captureEvents) return;
            pendingEvents.Add(new ClinicEvent { Id = id, Tick = State.Tick, Kind = kind, Room = room,
                PatientId = patientId, StaffId = staffId, DeskId = deskId, Amount = amount, SourceAnchor = source, Amenity = amenity });
        }
        private bool CanSpend(long amount) => amount > 0 && State.Wallet >= amount;
        private void Spend(long amount) { State.Wallet -= amount; State.TotalSpent += amount; }
        private bool TutorialComplete() => State.Tutorial == ClinicTutorialStep.Complete;
        private static bool IsUnpaidQueue(ClinicPatientState patient) => patient.Phase == ClinicPatientPhase.Arriving || patient.Phase == ClinicPatientPhase.ReceptionQueue;
        private static bool IsPaidWaiting(ClinicPatientState patient) => patient.Paid && (patient.Phase == ClinicPatientPhase.WaitingForTreatment
            || patient.Phase == ClinicPatientPhase.WalkingToWaiting || patient.Phase == ClinicPatientPhase.Seated || IsVisitingAmenity(patient));
        private static bool IsVisitingAmenity(ClinicPatientState patient) => patient.Phase == ClinicPatientPhase.WalkingToAmenity
            || patient.Phase == ClinicPatientPhase.UsingAmenity || patient.Phase == ClinicPatientPhase.ReturningFromAmenity;
        private static bool IsTimed(ClinicPatientPhase phase) => phase != ClinicPatientPhase.ReceptionQueue
            && phase != ClinicPatientPhase.WaitingForTreatment && phase != ClinicPatientPhase.Seated;
        private static bool FinitePositive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
        private static HashSet<string> KnownPatientAnchors()
        {
            var anchors = new HashSet<string>(StringComparer.Ordinal) { "entrance", "exit" };
            for (var id = 0; id < 2; id++)
            {
                anchors.Add(ClinicRules.DeskPatientAnchor(id));
                anchors.Add(ClinicRules.TreatmentPatientAnchor(id));
                anchors.Add(ClinicRules.WaitingAnchor(false, id));
            }
            for (var id = 0; id < 11; id++) anchors.Add(ClinicRules.QueueAnchor(id));
            for (var id = 0; id < 14; id++) anchors.Add(ClinicRules.WaitingAnchor(true, id));
            for (var id = 0; id < 6; id++) anchors.Add(ClinicRules.ParkingPatientAnchor(id));
            anchors.Add(ClinicRules.AmenityPatientAnchor(ClinicAmenity.Toilet));
            anchors.Add(ClinicRules.AmenityPatientAnchor(ClinicAmenity.Vending));
            return anchors;
        }
        private static bool IsLegacyPatientAnchor(string anchor) => !anchor.StartsWith("parking.", StringComparison.Ordinal)
            && anchor != ClinicRules.AmenityPatientAnchor(ClinicAmenity.Toilet) && anchor != ClinicRules.AmenityPatientAnchor(ClinicAmenity.Vending);
        private static int MaximumPhaseTicks(ClinicPatientPhase phase)
            => phase == ClinicPatientPhase.Treating ? 180 : phase == ClinicPatientPhase.CheckingIn ? 140
                : phase == ClinicPatientPhase.WalkingToTreatment ? 50 : phase == ClinicPatientPhase.WalkingToWaiting ? 20
                : phase == ClinicPatientPhase.Arriving || phase == ClinicPatientPhase.Leaving ? 100
                : phase == ClinicPatientPhase.UsingAmenity ? 60 : 30;
        private static bool Defined<T>(T value) where T : struct => Enum.IsDefined(typeof(T), value);
        private static string RoomAnchor(ClinicRoom room) => room == ClinicRoom.Reception ? "reception.plot" : room == ClinicRoom.FirstAid ? "firstaid.plot" : "waiting.plot";
        private static ClinicCommandResult No(string message) => new ClinicCommandResult(false, message);
        private static ClinicCommandResult Yes(string message, long cost = 0, long amount = 0) => new ClinicCommandResult(true, message, cost, amount);

        public static bool IsValidState(ClinicState state) => IsValidState(state, false);
        internal static bool IsValidLegacyState(ClinicState state) => IsValidState(state, true);

        private static bool IsValidState(ClinicState state, bool legacy)
        {
            if (state == null || state.SchemaVersion != (legacy ? 1 : 2) || state.RulesVersion != (legacy ? 1 : 2) || !Defined(state.Tutorial)
                || state.Tick < 0 || state.Tick >= MaximumTick || double.IsNaN(state.SubTick) || state.SubTick < 0 || state.SubTick >= 1
                || state.Wallet < 0 || state.Wallet > MaximumMoney || state.NextEventId < 1 || state.NextEventId > MaximumMoney
                || state.NextPatientId < 1 || state.NextConstructionId < 0 || state.TotalEarned < 0 || state.TotalEarned > MaximumMoney
                || state.TotalCollected < 0 || state.TotalCollected > state.TotalEarned || state.TotalSpent < 0 || state.TotalSpent > state.TotalCollected
                || state.TotalTreatments < 0 || state.TotalPayments < state.TotalTreatments || state.TotalPayments > state.NextPatientId
                || state.TotalEarned < 50 * state.TotalPayments + (legacy ? 0 : state.TotalTips)
                || state.TotalEarned > (legacy ? 125 : 140) * state.TotalPayments + (legacy ? 0 : state.TotalTips)) return false;
            if (state.Rooms == null || state.Rooms.Count != 3 || state.ReceptionDesks == null || state.ReceptionDesks.Count < 1 || state.ReceptionDesks.Count > 2
                || state.Staff == null || state.Patients == null || state.Patients.Count > ClinicRules.MaximumPatients || state.Construction == null || state.Construction.Count > 3) return false;
            var roomKinds = new HashSet<ClinicRoom>();
            foreach (var room in state.Rooms)
                if (room == null || !Defined(room.Kind) || !roomKinds.Add(room.Kind) || room.Tier < 1 || room.Tier > 3
                    || room.EquipmentLevel < 1 || room.EquipmentLevel > ClinicRules.TrackCap(room.Tier)
                    || room.FacilitiesLevel < 1 || room.FacilitiesLevel > ClinicRules.TrackCap(room.Tier)
                    || room.DecorationLevel < 1 || room.DecorationLevel > ClinicRules.TrackCap(room.Tier)
                    || room.StationCount < 0 || room.StationCount > 2) return false;
            var reception = state.Room(ClinicRoom.Reception);
            var firstAid = state.Room(ClinicRoom.FirstAid);
            var waiting = state.Room(ClinicRoom.Waiting);
            if (!reception.Built || !firstAid.Built || reception.StationCount != state.ReceptionDesks.Count
                || firstAid.StationCount < 1 || (firstAid.StationCount == 2 && firstAid.Tier < 2) || waiting.StationCount != 0
                || (!waiting.Built && (waiting.Tier != 1 || waiting.EquipmentLevel != 1 || waiting.FacilitiesLevel != 1 || waiting.DecorationLevel != 1))
                || (waiting.Built && !state.WaitingRoomUnlocked)) return false;
            if (!legacy && !IsValidExpansion(state)) return false;
            var deskIds = new HashSet<int>();
            long till = legacy ? 0 : state.Amenity(ClinicAmenity.Vending).Till;
            foreach (var desk in state.ReceptionDesks)
            {
                if (desk == null || desk.Id < 0 || desk.Id >= state.ReceptionDesks.Count || !deskIds.Add(desk.Id)
                    || desk.Till < 0 || desk.Till > MaximumMoney || desk.PatientId < -1
                    || desk.LastStartedTick < -1 || desk.LastStartedTick > state.Tick
                    || (!legacy && (desk.EquipmentLevel < 1 || desk.EquipmentLevel > ClinicRules.TrackCap(reception.Tier)))) return false;
                till += desk.Till;
            }
            if (till != state.TotalEarned - state.TotalCollected || state.Wallet != state.TotalCollected - state.TotalSpent) return false;
            var staffIds = new HashSet<int>();
            var taskIds = new HashSet<int>();
            var nurseStations = new HashSet<int>();
            var receptionistStations = new HashSet<int>();
            foreach (var staff in state.Staff)
            {
                if (staff == null || !Defined(staff.Role) || !staffIds.Add(staff.Id) || staff.PatientId < -1
                    || (!legacy && (staff.TrainingLevel < 1 || staff.TrainingLevel > ClinicRules.TrackCap(state.Room(staff.Role == ClinicStaffRole.Nurse ? ClinicRoom.FirstAid : ClinicRoom.Reception).Tier)))
                    || string.IsNullOrEmpty(staff.FromAnchor) || string.IsNullOrEmpty(staff.ToAnchor)
                    || staff.MoveStartedTick < 0 || staff.MoveStartedTick > state.Tick || staff.MoveEndsTick < staff.MoveStartedTick
                    || staff.MoveEndsTick - staff.MoveStartedTick > 40
                    || (staff.PatientId >= 0 && (staff.MoveEndsTick > state.Tick || !taskIds.Add(staff.PatientId)))) return false;
                if (staff.Role == ClinicStaffRole.Nurse)
                {
                    if (staff.Id != 100 + staff.StationId || staff.StationId < 0 || staff.StationId >= firstAid.StationCount
                        || !nurseStations.Add(staff.StationId) || staff.ToAnchor != ClinicRules.TreatmentStaffAnchor(staff.StationId)) return false;
                }
                else if (staff.Id != staff.StationId || !deskIds.Contains(staff.StationId) || !receptionistStations.Add(staff.StationId)
                    || staff.ToAnchor != ClinicRules.DeskStaffAnchor(staff.StationId)) return false;
                if (staff.FromAnchor != "entrance" && staff.FromAnchor != staff.ToAnchor) return false;
            }
            if (nurseStations.Count > 2 || receptionistStations.Count != deskIds.Count
                || (nurseStations.Count >= 1 && !nurseStations.Contains(0))) return false;
            var patientIds = new HashSet<int>();
            var occupiedStations = new HashSet<int>();
            var occupiedSeats = new HashSet<int>();
            var queueSlots = new HashSet<int>();
            var reserved = 0;
            var currentPaid = 0;
            foreach (var patient in state.Patients)
            {
                if (patient == null || patient.Id < 0 || patient.Id >= state.NextPatientId || !patientIds.Add(patient.Id) || !Defined(patient.Phase)
                    || patient.ArrivalTick < 0 || patient.ArrivalTick > state.Tick || patient.PhaseStartedTick < 0 || patient.PhaseStartedTick > state.Tick
                    || !PatientAnchors.Contains(patient.FromAnchor) || !PatientAnchors.Contains(patient.ToAnchor)
                    || (IsTimed(patient.Phase) ? patient.PhaseEndsTick <= state.Tick : patient.PhaseEndsTick != 0)
                    || (IsTimed(patient.Phase) && patient.PhaseEndsTick - patient.PhaseStartedTick > MaximumPhaseTicks(patient.Phase))
                    || (legacy && ((int)patient.Phase > (int)ClinicPatientPhase.Leaving
                        || !IsLegacyPatientAnchor(patient.FromAnchor) || !IsLegacyPatientAnchor(patient.ToAnchor)
                        || (patient.Phase == ClinicPatientPhase.Arriving || patient.Phase == ClinicPatientPhase.Leaving) && patient.PhaseEndsTick - patient.PhaseStartedTick > 30))
                    || patient.Payment < 0 || patient.Payment > (legacy ? 125 : 140) || patient.DeskId < -1 || patient.DeskId >= deskIds.Count
                    || patient.SeatId < -1 || patient.SeatId >= ClinicRules.WaitingCapacity(state)) return false;
                var atReception = patient.Phase == ClinicPatientPhase.WalkingToReception || patient.Phase == ClinicPatientPhase.CheckingIn;
                var atTreatment = patient.Phase == ClinicPatientPhase.WalkingToTreatment || patient.Phase == ClinicPatientPhase.Treating;
                if (patient.HasAdmissionReservation) reserved++;
                if (patient.Paid) currentPaid++;
                if (patient.Paid != (!IsUnpaidQueue(patient) && !atReception)) return false;
                if (patient.HasAdmissionReservation != (atReception || patient.Paid && patient.Phase != ClinicPatientPhase.Leaving)) return false;
                if (patient.Paid && (patient.Payment < 50 || patient.DeskId < 0)) return false;
                if (IsUnpaidQueue(patient))
                {
                    if (patient.QueueIndex < 0 || patient.QueueIndex >= ClinicRules.UnpaidQueueCapacity(state)
                        || !queueSlots.Add(patient.QueueIndex) || patient.Payment != 0 || patient.DeskId != -1) return false;
                }
                else if (patient.QueueIndex != -1) return false;
                if (atReception)
                {
                    if (patient.DeskId < 0 || patient.Payment < 50 || state.ReceptionDesks.Find(d => d.Id == patient.DeskId).PatientId != patient.Id
                        || state.Staff.Find(s => s.Id == patient.DeskId).PatientId != patient.Id) return false;
                }
                if (atTreatment)
                {
                    if (!nurseStations.Contains(patient.TreatmentStationId) || !occupiedStations.Add(patient.TreatmentStationId)
                        || state.Staff.Find(s => s.Id == 100 + patient.TreatmentStationId).PatientId != patient.Id || patient.SeatId != -1) return false;
                }
                else if (patient.TreatmentStationId != -1) return false;
                if (patient.SeatId >= 0 && (!IsPaidWaiting(patient) || !occupiedSeats.Add(patient.SeatId))) return false;
                if ((patient.Phase == ClinicPatientPhase.WalkingToWaiting || patient.Phase == ClinicPatientPhase.Seated) && patient.SeatId < 0) return false;
            }
            if (reserved > ClinicRules.WaitingCapacity(state) + Math.Max(1, nurseStations.Count) || currentPaid > state.TotalPayments
                || state.Patients.Count(IsUnpaidQueue) > ClinicRules.UnpaidQueueCapacity(state)) return false;
            foreach (var desk in state.ReceptionDesks)
                if (desk.PatientId >= 0 && !state.Patients.Any(p => p.Id == desk.PatientId
                    && p.DeskId == desk.Id && (p.Phase == ClinicPatientPhase.WalkingToReception || p.Phase == ClinicPatientPhase.CheckingIn))) return false;
            foreach (var staff in state.Staff)
            {
                if (staff.PatientId < 0) continue;
                var task = state.Patients.Find(p => p.Id == staff.PatientId);
                if (task == null) return false;
                if (staff.Role == ClinicStaffRole.Nurse)
                {
                    if (task.TreatmentStationId != staff.StationId || (task.Phase != ClinicPatientPhase.WalkingToTreatment && task.Phase != ClinicPatientPhase.Treating)) return false;
                }
                else if (task.DeskId != staff.StationId || (task.Phase != ClinicPatientPhase.WalkingToReception && task.Phase != ClinicPatientPhase.CheckingIn)) return false;
            }
            var jobRooms = new HashSet<ClinicRoom>();
            var jobIds = new HashSet<int>();
            foreach (var job in state.Construction)
            {
                if (job == null || job.Id < 0 || job.Id >= state.NextConstructionId || !jobIds.Add(job.Id) || !Defined(job.Room) || !Defined(job.Kind)
                    || !jobRooms.Add(job.Room) || job.StartedTick < 0 || job.StartedTick > state.Tick || job.EndsTick <= state.Tick || job.PaidCost <= 0) return false;
                var room = state.Room(job.Room);
                if (job.Kind == ClinicConstructionKind.WaitingRoom)
                {
                    if (job.Room != ClinicRoom.Waiting || room.Built || !state.WaitingRoomUnlocked || job.TargetTier != 1
                        || job.PaidCost != ClinicRules.WaitingRoomCost || job.EndsTick - job.StartedTick != 200) return false;
                }
                else if (!room.Built || room.Tier >= 3 || job.TargetTier != room.Tier + 1
                    || job.PaidCost != ClinicRules.RenovationCost(room)
                    || job.EndsTick - job.StartedTick != ClinicRules.RenovationSeconds(room.Tier) * 10L) return false;
            }
            if (state.Tutorial == ClinicTutorialStep.Complete)
            {
                if (state.TotalTreatments < 1 || nurseStations.Count < 1 || state.NextArrivalTick <= state.Tick) return false;
            }
            else
            {
                if (state.NextArrivalTick != -1 || state.NextPatientId != 1 || state.TotalTreatments != 0 || state.Patients.Count != 1
                    || state.Patients[0].Id != 0 || state.TotalPayments > 1 || state.WaitingRoomUnlocked || state.Construction.Count != 0) return false;
                if (state.Tutorial == ClinicTutorialStep.FirstArrival && (state.TotalPayments != 0 || nurseStations.Count != 0)) return false;
                if (state.Tutorial == ClinicTutorialStep.CollectFirstPayment && (state.TotalPayments != 1 || till != 50 || state.Wallet != 0 || nurseStations.Count != 0)) return false;
                if (state.Tutorial == ClinicTutorialStep.HireFirstNurse && (state.TotalPayments != 1 || till != 0 || state.Wallet != 50 || nurseStations.Count != 0)) return false;
                if (state.Tutorial == ClinicTutorialStep.FirstTreatment && (state.TotalPayments != 1 || nurseStations.Count != 1 || state.TotalSpent != 50)) return false;
            }
            return true;
        }
    }
}
