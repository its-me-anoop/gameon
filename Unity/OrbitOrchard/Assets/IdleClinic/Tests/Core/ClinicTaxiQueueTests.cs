using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using IdleClinic.Core;

namespace IdleClinic.Tests
{
    public sealed class ClinicTaxiQueueTests
    {
        [Test] public void TransportBookingsDoNotBlockOrJumpThePhysicalReceptionQueue()
        {
            var game = DoctorsProgressionFixture.MaxDoctors();
            bool overtookOffscreenBooking = false, checkedReturningTransport = false;
            long lastPayment = game.State.Tick, longestPaymentGap = 0;
            for (int tick = 0; tick < 36000; tick++)
            {
                var pending = game.State.Patients.Where(p => PendingTransport(p.Phase)).Select(p => p.Id).ToArray();
                var physical = game.State.Patients.Where(p => p.Phase == ClinicPatientPhase.Arriving || p.Phase == ClinicPatientPhase.ReceptionQueue)
                    .Select(p => p.Id).ToArray();
                var report = game.Advance(.1);
                foreach (var payment in report.Events.Where(e => e.Kind == ClinicEventKind.PaymentReceived))
                {
                    longestPaymentGap = Math.Max(longestPaymentGap, payment.Tick - lastPayment); lastPayment = payment.Tick;
                    overtookOffscreenBooking |= pending.Any(id => id < payment.PatientId && game.State.Patients.Any(p => p.Id == id && PendingTransport(p.Phase)));
                }
                foreach (var joined in game.State.Patients.Where(p => pending.Contains(p.Id) && p.Phase == ClinicPatientPhase.Arriving))
                {
                    var alreadyHere = game.State.Patients.Where(p => physical.Contains(p.Id) && p.QueueIndex >= 0).ToArray();
                    if (alreadyHere.Length == 0) continue;
                    Assert.That(alreadyHere.All(p => p.QueueIndex < joined.QueueIndex), Is.True, "An old booking must join behind visitors already here.");
                    checkedReturningTransport = true;
                }
                if (tick % 600 == 0) Assert.That(ClinicSimulation.IsValidState(game.State), Is.True);
            }
            longestPaymentGap = Math.Max(longestPaymentGap, game.State.Tick - lastPayment);
            Assert.That(overtookOffscreenBooking && checkedReturningTransport, Is.True);
            Assert.That(longestPaymentGap, Is.LessThan(1200), "An offscreen transport booking previously stopped all income for over ten minutes.");
            TestContext.WriteLine("Longest full-clinic payment gap: " + longestPaymentGap / 10d + "s.");
        }

        [Test] public void TaxiWaitingPlacesAndCalledBoardingHaveSavedExclusiveOwnership()
        {
            var game = DoctorsProgressionFixture.MaxDoctors();
            var requestTicks = new Dictionary<long,long>();
            bool waiting = false, walkingToBoard = false, testedRestore = false;
            int completedPickups = 0;
            for (int tick = 0; tick < 36000; tick++)
            {
                game.Advance(.1, false);
                foreach (var ride in game.State.TaxiRides)
                {
                    if (requestTicks.TryGetValue(ride.Id, out long requested)) Assert.That(ride.RoadRequestedTick, Is.EqualTo(requested));
                    else requestTicks.Add(ride.Id, ride.RoadRequestedTick);
                }
                var reserved = game.State.Patients.Where(p => p.TaxiWaitingReserved).ToArray();
                Assert.That(reserved.Length, Is.LessThanOrEqualTo(ClinicRules.TaxiWaitingCapacity));
                Assert.That(game.State.Patients.Count(p => p.UsesTaxi), Is.LessThanOrEqualTo(ClinicRules.TaxiBookingCapacity));
                Assert.That(reserved.Select(p => p.TaxiWaitingSlot).Distinct().Count(), Is.EqualTo(reserved.Length));
                Assert.That(game.State.Patients.Count(p => p.Phase == ClinicPatientPhase.WalkingToTaxiBoarding), Is.LessThanOrEqualTo(1));
                Assert.That(game.State.Patients.Count(p => p.Phase == ClinicPatientPhase.WalkingToTaxi), Is.LessThanOrEqualTo(1));
                foreach (var patient in reserved.Where(p => p.Phase == ClinicPatientPhase.WaitingForTaxi))
                { waiting = true; Assert.That(patient.ToAnchor, Is.EqualTo(ClinicRules.TaxiWaitingAnchor(patient.TaxiWaitingSlot))); }
                foreach (var patient in game.State.Patients.Where(p => p.Phase == ClinicPatientPhase.WalkingToTaxiBoarding))
                {
                    walkingToBoard = true;
                    var ride = game.State.TaxiRides.Single(r => r.PatientId == patient.Id);
                    Assert.That(ride.Phase, Is.EqualTo(ClinicTaxiPhase.WaitingForPassenger));
                    Assert.That(ride.PhaseEndsTick, Is.Zero, "The three-second boarding timer begins only after the walk.");
                    Assert.That(patient.PhaseEndsTick - patient.PhaseStartedTick,
                        Is.EqualTo(ClinicDoctorsNavigation.WalkTicks(patient.FromAnchor, patient.ToAnchor)));
                    if (!testedRestore)
                    {
                        var fine = new ClinicSimulation(ClinicDoctorsTests.Clone(game.State));
                        var coarse = new ClinicSimulation(ClinicDoctorsTests.Clone(game.State));
                        var offline = new ClinicSimulation(ClinicDoctorsTests.Clone(game.State));
                        for (int i = 0; i < 1200; i++) fine.Advance(.1, false);
                        coarse.Advance(120, false); offline.AdvanceOffline(120);
                        Assert.That(ClinicDoctorsTests.Fingerprint(fine.State), Is.EqualTo(ClinicDoctorsTests.Fingerprint(coarse.State)));
                        Assert.That(ClinicDoctorsTests.Fingerprint(fine.State), Is.EqualTo(ClinicDoctorsTests.Fingerprint(offline.State)));
                        testedRestore = true;
                    }
                }
                completedPickups += game.State.TaxiRides.Count(r => r.Pickup && r.Phase == ClinicTaxiPhase.Boarding && r.PhaseStartedTick == game.State.Tick);
                if (tick % 600 == 0) Assert.That(ClinicSimulation.IsValidState(game.State), Is.True);
            }
            Assert.That(waiting && walkingToBoard && testedRestore, Is.True);
            Assert.That(completedPickups, Is.GreaterThan(5));
        }

        [Test] public void TaxiRoadRequestAgeSurvivesTheOfflineEarningsCapAndInvalidFutureRequestsAreRejected()
        {
            var game = DoctorsProgressionFixture.MaxDoctors();
            var active = new ClinicSimulation(ClinicDoctorsTests.Clone(game.State));
            var capped = new ClinicSimulation(ClinicDoctorsTests.Clone(game.State));
            active.Advance(ClinicRules.MaximumOfflineSeconds,false);
            capped.AdvanceOffline(ClinicRules.MaximumOfflineSeconds + 60);
            Assert.That(active.State.TaxiRides.Count, Is.GreaterThan(0));
            foreach (var ride in active.State.TaxiRides)
            {
                var restored = capped.State.TaxiRides.Single(r => r.Id == ride.Id);
                Assert.That(restored.RoadRequestedTick, Is.EqualTo(ride.RoadRequestedTick + 600));
                Assert.That(capped.State.Tick - restored.RoadRequestedTick, Is.EqualTo(active.State.Tick - ride.RoadRequestedTick));
            }
            Assert.That(ClinicSimulation.IsValidState(capped.State), Is.True);
            var corrupt = ClinicDoctorsTests.Clone(active.State);
            corrupt.TaxiRides[0].RoadRequestedTick = corrupt.TaxiRides[0].PhaseStartedTick + 1;
            Assert.That(ClinicSimulation.IsValidState(corrupt), Is.False);
        }

        [Test] public void NewTaxiBookingsReachDropOffAndPickupWithinTenMinutesWithoutStarvingParkedDrivers()
        {
            var game = DoctorsProgressionFixture.MaxDoctors();
            long start = game.State.Tick;
            int firstNewId = game.State.NextPatientId;
            var firstHourBookings = new HashSet<int>();
            int dropoffs = 0, pickups = 0;
            long longestIncoming = 0, longestPickup = 0, longestCarExit = 0;
            for (int second = 0; second < 7200; second++)
            {
                var before = game.State.Patients.Where(p => p.UsesTaxi && p.Id >= firstNewId)
                    .ToDictionary(p => p.Id, p => new { p.Phase, p.PhaseStartedTick });
                foreach (var patient in game.State.Patients.Where(p => p.Phase == ClinicPatientPhase.WaitingToExit))
                {
                    longestCarExit = Math.Max(longestCarExit, game.State.Tick - patient.PhaseStartedTick);
                    Assert.That(game.State.Tick - patient.PhaseStartedTick, Is.LessThanOrEqualTo(6000));
                }
                foreach (var patient in game.State.Patients.Where(p => p.UsesTaxi && p.Id >= firstNewId))
                {
                    if (patient.ArrivalTick < start + 36000) firstHourBookings.Add(patient.Id);
                    if (patient.Phase == ClinicPatientPhase.TaxiArriving || patient.Phase == ClinicPatientPhase.WaitingForTaxi)
                        Assert.That(game.State.Tick - patient.PhaseStartedTick, Is.LessThanOrEqualTo(6000), "Fresh booking " + patient.Id + " stalled in " + patient.Phase);
                }
                game.Advance(1,false);
                foreach (var patient in game.State.Patients.Where(p => p.UsesTaxi && p.Id >= firstNewId))
                {
                    if (!before.TryGetValue(patient.Id,out var previous)) continue;
                    if (previous.Phase == ClinicPatientPhase.TaxiArriving && patient.Phase == ClinicPatientPhase.TaxiDroppingOff)
                    {
                        long elapsed = patient.PhaseStartedTick - patient.ArrivalTick;
                        longestIncoming = Math.Max(longestIncoming,elapsed); dropoffs++;
                        Assert.That(elapsed, Is.LessThanOrEqualTo(6000));
                    }
                    if (previous.Phase == ClinicPatientPhase.WaitingForTaxi && patient.Phase == ClinicPatientPhase.WalkingToTaxiBoarding)
                    {
                        long elapsed = patient.PhaseStartedTick - previous.PhaseStartedTick;
                        longestPickup = Math.Max(longestPickup,elapsed); pickups++;
                        Assert.That(elapsed, Is.LessThanOrEqualTo(6000));
                    }
                }
                if (second % 60 == 0) Assert.That(ClinicSimulation.IsValidState(game.State), Is.True);
            }
            Assert.That(firstHourBookings.Count, Is.GreaterThan(10));
            Assert.That(firstHourBookings.All(id => !game.State.Patients.Any(p => p.Id == id)), Is.True);
            Assert.That(dropoffs, Is.GreaterThan(10)); Assert.That(pickups, Is.GreaterThan(10));
            TestContext.WriteLine("Fresh taxi incoming " + longestIncoming / 10d + "s; pickup " + longestPickup / 10d + "s; paid-car exit " + longestCarExit / 10d + "s; " + firstHourBookings.Count + " first-hour bookings completed.");
        }

        [Test] public void FullTaxiWaitingAreaKeepsTheNextPaidPatientOnAnIndoorSeatUntilASlotIsReleased()
        {
            var state = ClinicDoctorsTests.Clone(DoctorsProgressionFixture.MaxDoctors().State);
            state.Patients.Clear(); state.TaxiRides.Clear();
            foreach (var staff in state.Staff) staff.PatientId = -1;
            foreach (var desk in state.ReceptionDesks) desk.PatientId = -1;
            for (int i = 0; i <= ClinicRules.TaxiWaitingCapacity; i++)
            {
                int id = 2 + i * 4; bool indoors = i == ClinicRules.TaxiWaitingCapacity;
                var anchor = indoors ? ClinicRules.WaitingAnchor(true, 0) : ClinicRules.TaxiWaitingAnchor(i);
                state.Patients.Add(new ClinicPatientState { Id = id, AppearanceId = ClinicRules.PatientAppearance(state.Seed,id),
                    UsesTaxi = true, TaxiDockId = id / 4 % 2, TaxiWaitingReserved = !indoors, TaxiWaitingSlot = indoors ? 0 : i,
                    Paid = true, Payment = 100, DeskId = 0, ConsultationComplete = true, FirstAidComplete = true, PharmacyComplete = !indoors,
                    NextService = ClinicStaffRole.Pharmacist, HasAdmissionReservation = indoors, SeatId = indoors ? 0 : -1,
                    Phase = indoors ? ClinicPatientPhase.Seated : ClinicPatientPhase.WaitingForTaxi,
                    FromAnchor = anchor, ToAnchor = anchor, PhaseStartedTick = state.Tick });
            }
            Assert.That(ClinicSimulation.IsValidState(state), Is.True, "An explicit saturated but financially valid queue isolates the backpressure rule.");
            var game = new ClinicSimulation(state); var patient = state.Patients.Last();
            game.Advance(.1,false);
            Assert.That(patient.Phase, Is.Not.EqualTo(ClinicPatientPhase.WalkingToPharmacy), "Waiting patients can still visit the indoor amenities.");
            Assert.That(patient.HasAdmissionReservation && patient.SeatId == 0 && !patient.TaxiWaitingReserved, Is.True);
            for (int i = 0; i < 3000 && !patient.TaxiWaitingReserved; i++) game.Advance(.1,false);
            Assert.That(patient.TaxiWaitingReserved, Is.True, "A called and boarded visitor must release its place for the next paid patient.");
            Assert.That(ClinicSimulation.IsValidState(state), Is.True);
        }

        [Test] public void TaxiCurbClearanceIsAnExactEventAndIncomingVehiclesDelayCalledBoarding()
        {
            var game = DoctorsProgressionFixture.MaxDoctors(); bool compared = false, checkedIncoming = false;
            var method = typeof(ClinicSimulation).GetMethod("TaxiCurbClearTick", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            for (int i = 0; i < 18000; i++)
            {
                game.Advance(.1,false);
                if (game.State.TaxiRides.Any(r => !r.Pickup && (r.Phase == ClinicTaxiPhase.Approaching || r.Phase == ClinicTaxiPhase.Boarding)))
                { checkedIncoming = true; Assert.That(game.State.Patients.Any(p => p.Phase == ClinicPatientPhase.WalkingToTaxiBoarding), Is.False); }
                if (compared) continue;
                var passenger = game.State.Patients.FirstOrDefault(p => p.UsesTaxi && p.Phase == ClinicPatientPhase.Arriving && (long)method.Invoke(null,new object[]{p}) > game.State.Tick);
                if (passenger == null) continue;
                long clears = (long)method.Invoke(null,new object[]{passenger});
                var fine = new ClinicSimulation(ClinicDoctorsTests.Clone(game.State));
                var coarse = new ClinicSimulation(ClinicDoctorsTests.Clone(game.State));
                var offline = new ClinicSimulation(ClinicDoctorsTests.Clone(game.State));
                double seconds = (clears - game.State.Tick + 100) / 10d;
                for (int step = 0; step < clears - game.State.Tick + 100; step++) fine.Advance(.1,false);
                coarse.Advance(seconds,false); offline.AdvanceOffline(seconds);
                Assert.That(ClinicDoctorsTests.Fingerprint(fine.State), Is.EqualTo(ClinicDoctorsTests.Fingerprint(coarse.State)));
                Assert.That(ClinicDoctorsTests.Fingerprint(fine.State), Is.EqualTo(ClinicDoctorsTests.Fingerprint(offline.State)));
                compared = true;
            }
            Assert.That(compared && checkedIncoming, Is.True);
        }

        [Test] public void DuplicateTaxiWaitingReservationIsRejectedAndLegacyDockWaitingIsUpgraded()
        {
            var game = DoctorsProgressionFixture.MaxDoctors();
            for (int i = 0; i < 36000 && (game.State.Patients.Count(p => p.Phase == ClinicPatientPhase.WaitingForTaxi) < 2
                || game.State.Patients.Any(p => p.Phase == ClinicPatientPhase.WalkingToTaxiBoarding)
                || game.State.TaxiRides.Any(r => r.Phase == ClinicTaxiPhase.WaitingForPassenger)); i++) game.Advance(.1, false);
            var legacy = ClinicDoctorsTests.Clone(game.State);
            var waiting = legacy.Patients.Where(p => p.Phase == ClinicPatientPhase.WaitingForTaxi).ToArray();
            Assert.That(waiting.Length, Is.GreaterThanOrEqualTo(2));
            var invalid = ClinicDoctorsTests.Clone(legacy);
            invalid.Patients.Find(p => p.Id == waiting[1].Id).TaxiWaitingSlot = waiting[0].TaxiWaitingSlot;
            Assert.That(ClinicSimulation.IsValidState(invalid), Is.False);
            foreach (var patient in legacy.Patients)
            {
                if (patient.FromAnchor.StartsWith("taxi.waiting.")) patient.FromAnchor = ClinicRules.TaxiPatientAnchor(patient.TaxiDockId);
                if (patient.ToAnchor.StartsWith("taxi.waiting.")) patient.ToAnchor = ClinicRules.TaxiPatientAnchor(patient.TaxiDockId);
                if (patient.Phase == ClinicPatientPhase.WalkingToTaxi)
                    patient.PhaseEndsTick = patient.PhaseStartedTick + ClinicDoctorsNavigation.WalkTicks(patient.FromAnchor, patient.ToAnchor);
                patient.TaxiWaitingReserved = false; patient.TaxiWaitingSlot = 0;
            }
            Assert.That(ClinicSimulation.IsValidState(legacy), Is.True, "This models absent fields from the previous schema-3 save.");
            long earned = legacy.TotalEarned, collected = legacy.TotalCollected;
            var restored = new ClinicSimulation(legacy);
            Assert.That(ClinicSimulation.IsValidState(restored.State), Is.True);
            Assert.That(restored.State.TotalEarned, Is.EqualTo(earned)); Assert.That(restored.State.TotalCollected, Is.EqualTo(collected));
            foreach (var old in waiting)
            {
                var current = restored.State.Patients.Single(p => p.Id == old.Id);
                Assert.That(current.TaxiWaitingReserved && current.Paid && current.PharmacyComplete, Is.True);
                Assert.That(current.ToAnchor, Is.EqualTo(ClinicRules.TaxiWaitingAnchor(current.TaxiWaitingSlot)));
            }
        }
        private static bool PendingTransport(ClinicPatientPhase phase) => phase == ClinicPatientPhase.WaitingToPark || phase == ClinicPatientPhase.DrivingToParking
            || phase == ClinicPatientPhase.TaxiArriving || phase == ClinicPatientPhase.TaxiDroppingOff;
    }
}
