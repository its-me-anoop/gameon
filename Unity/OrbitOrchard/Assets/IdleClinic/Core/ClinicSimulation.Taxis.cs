using System;
using System.Linq;

namespace IdleClinic.Core
{
    public sealed partial class ClinicSimulation
    {
        private bool TaxiRoadBusy => State.TaxiRides.Any(r => r.Phase == ClinicTaxiPhase.Approaching || r.Phase == ClinicTaxiPhase.Departing);
        private int OldestDoctorRoadRequestPatientId()
        {
            long oldestTick = long.MaxValue;
            int oldestId = int.MaxValue;
            bool parkingEntrySafe = State.Patients.Any(p => p.Phase == ClinicPatientPhase.WaitingToPark) && ParkingMovementHasSafeWindow(true);
            bool parkingExitSafe = State.Patients.Any(p => p.Phase == ClinicPatientPhase.WaitingToExit) && ParkingMovementHasSafeWindow(false);
            void Consider(int patientId, long requestedTick)
            {
                if (requestedTick < oldestTick || requestedTick == oldestTick && patientId < oldestId)
                { oldestTick = requestedTick; oldestId = patientId; }
            }
            foreach (var patient in State.Patients)
            {
                if (patient.Phase == ClinicPatientPhase.WaitingToPark || patient.Phase == ClinicPatientPhase.WaitingToExit)
                {
                    if (patient.Phase == ClinicPatientPhase.WaitingToPark ? parkingEntrySafe : parkingExitSafe) Consider(patient.Id, patient.PhaseStartedTick);
                }
                else if ((patient.Phase == ClinicPatientPhase.TaxiArriving || patient.Phase == ClinicPatientPhase.WaitingForTaxi)
                    && RoadWindowAvailable(ClinicRules.TaxiTravelTicks(State))
                    && (patient.Phase != ClinicPatientPhase.TaxiArriving || !TaxiOutboundApproachBusy())
                    && !State.TaxiRides.Any(r => r.PatientId == patient.Id || r.DockId == patient.TaxiDockId))
                    Consider(patient.Id, patient.PhaseStartedTick);
            }
            foreach (var ride in State.TaxiRides)
                if (ride.Phase == ClinicTaxiPhase.WaitingToDepart && RoadWindowAvailable(ClinicRules.TaxiDepartureTicks)
                    && (ride.Pickup || State.Tick - ride.PhaseStartedTick >= 20
                        || !State.Patients.Any(p => p.Id == ride.PatientId && p.Phase == ClinicPatientPhase.Arriving)))
                    Consider(ride.PatientId, ride.RoadRequestedTick > 0 ? ride.RoadRequestedTick : ride.PhaseStartedTick);
            // Age wins when a full crossing-free window opens, including over taxis
            // already waiting at the curb. A shorter taxi leg may use the tail of the
            // current window, but must finish before the crossing and cannot consume
            // the next complete window ahead of an older 208-tick car departure.
            return oldestId == int.MaxValue ? -1 : oldestId;
        }
        private static void HoldTaxiPassenger(ClinicPatientState patient, ClinicPatientPhase phase, long tick)
        { patient.Phase = phase; patient.PhaseStartedTick = tick; patient.PhaseEndsTick = 0; }
        private int FreeTaxiWaitingPlace() => Enumerable.Range(0, ClinicRules.TaxiWaitingCapacity)
            .Where(slot => !State.Patients.Any(p => p.TaxiWaitingReserved && p.TaxiWaitingSlot == slot)).DefaultIfEmpty(-1).First();
        private bool CanReserveTaxiWaitingPlace(ClinicPatientState patient)
        {
            if (!patient.UsesTaxi) return true;
            if (FreeTaxiWaitingPlace() < 0) return false;
            if (TaxiDropOffCurbBusy() || State.TaxiRides.Any(r => !r.Pickup &&
                (r.Phase == ClinicTaxiPhase.Approaching || r.Phase == ClinicTaxiPhase.Boarding))) return false;
            // One outbound taxi visitor uses the north approach at a time. Other
            // pharmacy visitors remain eligible and paid taxi visitors keep their seat.
            return !State.Patients.Any(p => p.UsesTaxi && p.Id != patient.Id &&
                (p.Phase == ClinicPatientPhase.WalkingToPharmacy || p.Phase == ClinicPatientPhase.Dispensing || p.Phase == ClinicPatientPhase.WalkingToTaxi));
        }
        private bool TaxiOutboundApproachBusy() => State.Patients.Any(p => p.UsesTaxi &&
            (p.Phase == ClinicPatientPhase.WalkingToPharmacy || p.Phase == ClinicPatientPhase.Dispensing
                || p.Phase == ClinicPatientPhase.WalkingToTaxi || p.Phase == ClinicPatientPhase.WalkingToTaxiBoarding));
        private bool TaxiDropOffCurbBusy()
            => State.Patients.Any(p => TaxiCurbClearTick(p) > State.Tick);
        private static long TaxiCurbClearTick(ClinicPatientState patient)
        {
            if (!patient.UsesTaxi || patient.Phase != ClinicPatientPhase.Arriving || patient.ArrivalPath.Count < 2) return 0;
            var path = patient.ArrivalPath;
            if (path[0].X < 14.45f) return patient.PhaseStartedTick;
            double distance = 0, total = ClinicDoctorsNavigation.PathLength(path);
            for (int i = 1; i < path.Count; i++)
            {
                double dx = path[i].X - path[i-1].X, dz = path[i].Z - path[i-1].Z;
                double segment = Math.Sqrt(dx*dx + dz*dz);
                if (path[i].X < 14.45f)
                {
                    distance += segment * (path[i-1].X - 14.45) / (path[i-1].X - path[i].X);
                    return patient.PhaseStartedTick + (long)Math.Ceiling((patient.PhaseEndsTick - patient.PhaseStartedTick) * distance / total);
                }
                distance += segment;
            }
            return patient.PhaseEndsTick;
        }
        private void ReserveTaxiWaitingPlace(ClinicPatientState patient)
        {
            if (patient.TaxiWaitingReserved) return;
            int slot = FreeTaxiWaitingPlace();
            if (slot < 0) throw new InvalidOperationException("No taxi waiting place is available.");
            patient.TaxiWaitingSlot = slot;
            patient.TaxiWaitingReserved = true;
        }
        private void RestoreLegacyTaxiWaitingReservations()
        {
            // Only unpublished schema-3 saves can contain outgoing passengers at the
            // shared dock. Existing money, care and ride ownership stay authoritative.
            foreach (var patient in State.Patients.Where(p => p.UsesTaxi && !p.TaxiWaitingReserved &&
                (p.Phase == ClinicPatientPhase.WalkingToPharmacy || p.Phase == ClinicPatientPhase.Dispensing
                    || p.Phase == ClinicPatientPhase.WalkingToTaxi || p.Phase == ClinicPatientPhase.WaitingForTaxi)).OrderBy(p => p.Id))
            {
                ReserveTaxiWaitingPlace(patient);
                if (patient.Phase == ClinicPatientPhase.WaitingForTaxi || patient.Phase == ClinicPatientPhase.WalkingToTaxi)
                    Phase(patient, ClinicPatientPhase.WalkingToTaxi, patient.Phase == ClinicPatientPhase.WaitingForTaxi ? patient.ToAnchor : patient.FromAnchor,
                        ClinicRules.TaxiWaitingAnchor(patient.TaxiWaitingSlot), 80);
            }
        }
        private void CallTaxiPassenger()
        {
            if (!ClinicRules.IsDoctors(State) || TaxiDropOffCurbBusy()
                || State.TaxiRides.Any(r => !r.Pickup && (r.Phase == ClinicTaxiPhase.Approaching || r.Phase == ClinicTaxiPhase.Boarding))
                || State.Patients.Any(p => p.Phase == ClinicPatientPhase.WalkingToTaxiBoarding)) return;
            foreach (var ride in State.TaxiRides.Where(r => r.Pickup && r.Phase == ClinicTaxiPhase.WaitingForPassenger)
                .OrderBy(r => r.PhaseStartedTick).ThenBy(r => r.Id))
            {
                var patient = State.Patients.Find(p => p.Id == ride.PatientId);
                if (patient.Phase != ClinicPatientPhase.WaitingForTaxi) continue;
                Phase(patient, ClinicPatientPhase.WalkingToTaxiBoarding, patient.ToAnchor, ClinicRules.TaxiPatientAnchor(ride.DockId), 1);
                return;
            }
        }
        private void BeginTaxiBoarding(ClinicPatientState patient)
        {
            var ride = State.TaxiRides.Find(r => r.PatientId == patient.Id && r.Pickup);
            ride.Phase = ClinicTaxiPhase.Boarding;
            ride.PhaseStartedTick = State.Tick;
            ride.PhaseEndsTick = State.Tick + ClinicRules.TaxiPickupTicks;
            patient.TaxiWaitingReserved = false;
            Rest(patient, ClinicPatientPhase.TaxiPickingUp);
        }
        private bool RoadWindowAvailable(int duration)
        {
            var cycle = ClinicRules.TrafficTick(State) % ClinicRules.StreetCrossingCycleTicks;
            var next = cycle < ClinicRules.StreetCrossingStartsTick ? ClinicRules.StreetCrossingStartsTick - cycle
                : ClinicRules.StreetCrossingCycleTicks + ClinicRules.StreetCrossingStartsTick - cycle;
            return !(cycle >= ClinicRules.StreetCrossingStartsTick && cycle < ClinicRules.StreetCrossingEndsTick) && duration < next;
        }
        private bool TryAdmitTaxiPatient(ClinicPatientState patient, int queueIndex)
        {
            if (!ClinicRules.IsDoctors(State) || patient.ParkingBayId >= 0 || patient.Id % 4 != 2 || ClinicRules.TaxiDockCount(State) == 0) return false;
            // Book transport only while it has capacity. Other visitors use the
            // normal walking entrance instead of occupying every care reservation.
            if (State.Patients.Count(p => p.UsesTaxi) >= ClinicRules.TaxiBookingCapacity) return false;
            patient.UsesTaxi = true;
            patient.TaxiDockId = patient.Id / 4 % ClinicRules.TaxiDockCount(State);
            patient.FromAnchor = ClinicRules.TaxiPatientAnchor(patient.TaxiDockId);
            patient.ToAnchor = ClinicRules.QueueAnchor(queueIndex);
            HoldTaxiPassenger(patient, ClinicPatientPhase.TaxiArriving, State.Tick);
            State.Patients.Add(patient);
            Emit(ClinicEventKind.PatientArrived, ClinicRoom.Reception, patient.Id, source: patient.FromAnchor);
            return true;
        }
        private void ProcessTaxiRides()
        {
            foreach (var ride in State.TaxiRides.Where(r => r.PhaseEndsTick > 0 && r.PhaseEndsTick <= State.Tick).OrderBy(r => r.Id).ToArray())
            {
                var patient = State.Patients.Find(p => p.Id == ride.PatientId);
                if (ride.Phase == ClinicTaxiPhase.Approaching)
                {
                    ride.Phase = ride.Pickup ? ClinicTaxiPhase.WaitingForPassenger : ClinicTaxiPhase.Boarding;
                    ride.PhaseStartedTick = State.Tick;
                    ride.PhaseEndsTick = ride.Pickup ? 0 : State.Tick + ClinicRules.TaxiDropOffTicks;
                    if (!ride.Pickup) HoldTaxiPassenger(patient, ClinicPatientPhase.TaxiDroppingOff, State.Tick);
                    Emit(ClinicEventKind.TaxiArrived, ClinicRoom.Reception, patient.Id, source: ClinicRules.TaxiPatientAnchor(ride.DockId), amenity: ClinicAmenity.Taxi);
                }
                else if (ride.Phase == ClinicTaxiPhase.Boarding)
                {
                    ride.Phase = ClinicTaxiPhase.WaitingToDepart;
                    ride.PhaseStartedTick = State.Tick;
                    ride.PhaseEndsTick = 0;
                    if (ride.Pickup) HoldTaxiPassenger(patient, ClinicPatientPhase.TaxiDeparting, State.Tick);
                    else JoinReceptionFromTransport(patient, ClinicRules.TaxiPatientAnchor(ride.DockId));
                }
                else if (ride.Phase == ClinicTaxiPhase.Departing)
                {
                    State.TaxiRides.Remove(ride);
                    if (ride.Pickup) State.Patients.Remove(patient);
                    Emit(ClinicEventKind.TaxiDeparted, ClinicRoom.Reception, ride.PatientId, source: ClinicRules.TaxiPatientAnchor(ride.DockId), amenity: ClinicAmenity.Taxi);
                }
            }
        }
        private void DispatchTaxis()
        {
            if (!ClinicRules.IsDoctors(State) || TaxiRoadBusy || State.Patients.Any(p => IsMovingVehicle(p.Phase))) return;
            int roadOwner = OldestDoctorRoadRequestPatientId();
            var departure = State.TaxiRides.FirstOrDefault(r => r.Phase == ClinicTaxiPhase.WaitingToDepart && r.PatientId == roadOwner);
            if (departure != null && departure.PatientId == roadOwner && RoadWindowAvailable(ClinicRules.TaxiDepartureTicks))
            {
                // A drop-off passenger first leaves the curb, before its vehicle pulls away.
                if (!departure.Pickup && State.Patients.Any(p => p.Id == departure.PatientId && p.Phase == ClinicPatientPhase.Arriving && State.Tick - departure.PhaseStartedTick < 20)) return;
                departure.Phase = ClinicTaxiPhase.Departing;
                departure.PhaseStartedTick = State.Tick;
                departure.PhaseEndsTick = State.Tick + ClinicRules.TaxiDepartureTicks;
                return;
            }
            if (!RoadWindowAvailable(ClinicRules.TaxiTravelTicks(State))) return;
            foreach (var patient in State.Patients.Where(p => p.Phase == ClinicPatientPhase.TaxiArriving || p.Phase == ClinicPatientPhase.WaitingForTaxi).OrderBy(p => p.PhaseStartedTick).ThenBy(p => p.Id))
            {
                if (patient.Id != roadOwner) continue;
                if (State.TaxiRides.Any(r => r.PatientId == patient.Id || r.DockId == patient.TaxiDockId)) continue;
                bool pickup = patient.Phase == ClinicPatientPhase.WaitingForTaxi;
                State.TaxiRides.Add(new ClinicTaxiState { Id = patient.Id * 2L + (pickup ? 1 : 0), PatientId = patient.Id,
                    RoadRequestedTick = patient.PhaseStartedTick, DockId = patient.TaxiDockId, Pickup = pickup, Phase = ClinicTaxiPhase.Approaching,
                    PhaseStartedTick = State.Tick, PhaseEndsTick = State.Tick + ClinicRules.TaxiTravelTicks(State) });
                return;
            }
        }
        private long NextTaxiEligibilityTick()
        {
            if (!ClinicRules.IsDoctors(State) || !(State.TaxiRides.Any(r => r.Phase == ClinicTaxiPhase.WaitingToDepart)
                || State.Patients.Any(p => p.Phase == ClinicPatientPhase.TaxiArriving || p.Phase == ClinicPatientPhase.WaitingForTaxi))) return long.MaxValue;
            var delay = ClinicRules.StreetCrossingEndsTick - ClinicRules.TrafficTick(State) % ClinicRules.StreetCrossingCycleTicks;
            if (delay <= 0) delay += ClinicRules.StreetCrossingCycleTicks;
            long next = State.Tick + delay;
            foreach (var ride in State.TaxiRides.Where(r => !r.Pickup && r.Phase == ClinicTaxiPhase.WaitingToDepart))
            {
                var passenger = State.Patients.Find(p => p.Id == ride.PatientId);
                if (passenger != null && passenger.Phase == ClinicPatientPhase.Arriving && ride.PhaseStartedTick + 20 > State.Tick)
                    next = Math.Min(next, ride.PhaseStartedTick + 20);
            }
            return next;
        }
    }
}
