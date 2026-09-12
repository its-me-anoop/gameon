using System;
using System.Linq;
using IdleClinic.Core;
using NUnit.Framework;

namespace IdleClinic.Tests
{
    public sealed class ClinicDoctorsNavigationTests
    {
        [TestCase("waiting.seat.0", "waiting.toilet.0.patient")]
        [TestCase("waiting.seat.29", "consultation.station.0.patient")]
        [TestCase("consultation.station.3.patient", "firstaid.station.0.patient")]
        [TestCase("firstaid.station.3.patient", "pharmacy.station.1.patient")]
        [TestCase("parking.bay.10.patient", "reception.queue.0")]
        [TestCase("pharmacy.station.0.patient", "parking.bay.11.patient")]
        [TestCase("taxi.dock.1.patient", "reception.queue.22")]
        [TestCase("pharmacy.station.1.patient", "taxi.dock.0.patient")]
        public void LongAndShortJourneysUseTheirOwnDistanceAtAWalkingPace(string from, string to)
        {
            double distance = ClinicDoctorsNavigation.PathLength(from, to);
            int ticks = ClinicDoctorsNavigation.WalkTicks(from, to);
            Assert.That(distance, Is.GreaterThan(0));
            Assert.That(distance / (ticks / 10d), Is.LessThanOrEqualTo(1.700001));
            Assert.That(distance / ((ticks - 1) / 10d), Is.GreaterThanOrEqualTo(1.699999));
            Assert.That(ticks, Is.LessThan(600));
        }
        [Test] public void SameRowQueueAdvancesStayDirectAndLocal()
        {
            for (int id = 1; id < 23; id++)
            {
                if (id % 8 == 0) continue;
                var path = ClinicDoctorsNavigation.ArrivalPath("reception.queue." + id, "reception.queue." + (id - 1));
                Assert.That(path.Count, Is.EqualTo(2), "A one-place shuffle must not visit the corridor: " + id);
                Assert.That(ClinicDoctorsNavigation.PathLength(path), Is.EqualTo(1.1).Within(.00001));
                Assert.That(path[0].Z, Is.EqualTo(path[1].Z));
                Assert.That(path.All(p => p.X < -3.39f && p.Z <= -8.25f), Is.True);
            }
        }
        [TestCase(8, -2.70f)]
        [TestCase(16, -11.80f)]
        public void RowTurnsUseAnOutsideHairpinWithRoomForTheAdjacentPatient(int id, float outsideX)
        {
            var path = ClinicDoctorsNavigation.ArrivalPath("reception.queue." + id, "reception.queue." + (id - 1));
            Assert.That(path[0].X, Is.EqualTo(path.Last().X).Within(.00001), "Snake rows must meet at the same end.");
            Assert.That(ClinicDoctorsNavigation.PathLength(path), Is.EqualTo(2.1).Within(.00001));
            Assert.That(path.Any(p => Math.Abs(p.X - outsideX) < .00001), Is.True);
            Assert.That(path.All(p => p.X >= -11.80001f && p.X <= -2.69999f && p.Z >= -9.651f && p.Z <= -8.249f), Is.True);
            // The west inner wall is x=-12.20; the front wall is z=-10.30.
            Assert.That(path.Min(p => p.X + 12.20), Is.GreaterThan(.39));
            Assert.That(path.Min(p => p.Z + 10.30), Is.GreaterThan(.64));
        }
        [Test] public void FullQueueAdvancesTogetherWithoutCrowdingOrLongDetours()
        {
            var paths = Enumerable.Range(1, 22).Select(id =>
                ClinicDoctorsNavigation.ArrivalPath("reception.queue." + id, "reception.queue." + (id - 1))).ToArray();
            double minimumSeparation = double.MaxValue;
            for (int frame = 0; frame <= 150; frame++)
            {
                double distance = frame / 60d * 1.35;
                var people = paths.Select(p => ClinicDoctorsNavigation.SampleArrivalPath(p, distance / ClinicDoctorsNavigation.PathLength(p))).ToArray();
                for (int a = 0; a < people.Length; a++)
                {
                    Assert.That(people[a].X, Is.InRange(-11.80001f, -2.69999f));
                    Assert.That(people[a].Z, Is.InRange(-9.651f, -8.249f));
                    for (int b = a + 1; b < people.Length; b++)
                        minimumSeparation = Math.Min(minimumSeparation, Separation(people[a], people[b]));
                }
            }
            Assert.That(minimumSeparation, Is.GreaterThan(.54), "Concurrent turns need space for bodies, not only distinct endpoints.");
            Assert.That(paths.Max(p => ClinicDoctorsNavigation.PathLength(p) / 1.35), Is.LessThan(2.5));
        }
        [TestCase(8)]
        [TestCase(16)]
        public void QueueRetargetDuringEachTurnSegmentKeepsOnlyTheRemainingLocalRoute(int id)
        {
            var oldPath = ClinicDoctorsNavigation.ArrivalPath("reception.queue." + id, "reception.queue." + (id - 1));
            var next = ClinicDoctorsNavigation.Anchor("reception.queue." + (id - 2));
            // A second desk may admit someone while the previous 0.7m hairpin is
            // only partly complete. The logical source has already advanced one slot.
            foreach (double progress in new[] { .1, .3, .5, .7, .9 })
            {
                var start = ClinicDoctorsNavigation.SampleArrivalPath(oldPath, progress);
                var retargeted = new System.Collections.Generic.List<ClinicMovementPoint> { start };
                ClinicDoctorsNavigation.Build("reception.queue." + (id - 1), "reception.queue." + (id - 2), false,
                    new ClinicDoctorsNavigation.Point(start.X, start.Z), p => retargeted.Add(new ClinicMovementPoint(p.x, p.z)));
                Assert.That(ClinicDoctorsNavigation.PathLength(retargeted),
                    Is.EqualTo(ClinicDoctorsNavigation.PathLength(oldPath) * (1 - progress) + 1.1).Within(.00001));
                Assert.That(retargeted.All(p => p.Z >= start.Z - .00001f), Is.True, "Retargeting must never walk back to the old row.");
                Assert.That(retargeted.Last().X, Is.EqualTo(next.x));
                Assert.That(retargeted.Last().Z, Is.EqualTo(next.z));
            }
        }
        [TestCase(0)]
        [TestCase(3)]
        [TestCase(8)]
        [TestCase(16)]
        [TestCase(22)]
        public void QueueToDeskUsesTheClearFrontAisleWithoutReturningToTheEntrance(int queueId)
        {
            for (int desk = 0; desk < 4; desk++)
            {
                var path = ClinicDoctorsNavigation.ArrivalPath("reception.queue." + queueId, "reception.desk." + desk + ".patient");
                Assert.That(path.All(p => p.X >= -11.80001f && p.X < -2.69999f && p.Z > -9.651f), Is.True);
                Assert.That(path.Count(p => Math.Abs(p.Z + 7.5f) < .00001), Is.EqualTo(2));
                if (queueId < 8)
                {
                    Assert.That(path.Count, Is.EqualTo(4));
                    // The first step clears the line vertically before crossing other slots.
                    Assert.That(path[1].X, Is.EqualTo(path[0].X));
                    Assert.That(path[1].Z, Is.EqualTo(-7.5f));
                }
                var destination = ClinicDoctorsNavigation.Anchor("reception.desk." + desk + ".patient");
                Assert.That(path.Last().X, Is.EqualTo(destination.x));
                Assert.That(path.Last().Z, Is.EqualTo(destination.z));
            }
        }
        private static double Separation(ClinicMovementPoint a, ClinicMovementPoint b)
            => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
        [Test] public void EveryArrivalUsesTheFreeTailWithoutCrossingEarlierQueueSlots()
        {
            for (int target = 0; target < 23; target++)
                AssertClearOfEarlierSlots(ClinicDoctorsNavigation.ArrivalPath("entrance", "reception.queue." + target), target);
        }
        [TestCase(15, 7)]
        [TestCase(22, 15)]
        [TestCase(22, 0)]
        [TestCase(15, 9)]
        public void ArrivalRetargetAcrossRowsPreservesPositionAndAFreeRemainingApproach(int oldIndex, int newIndex)
        {
            foreach (var from in new[] { "entrance", "parking.bay.11.patient", "taxi.dock.1.patient" })
            {
                var path = ClinicDoctorsNavigation.ArrivalPath(from, "reception.queue." + oldIndex);
                foreach (double progress in new[] { .2, .35, .7, .9, .99 })
                {
                    var oldPosition = ClinicDoctorsNavigation.SampleArrivalPath(path, progress);
                    var remaining = ClinicDoctorsNavigation.RetargetArrivalPath(path, progress, "reception.queue." + newIndex);
                    Assert.That(Separation(oldPosition, remaining[0]), Is.LessThan(.00001));
                    AssertClearOfEarlierSlots(remaining, newIndex);
                    if (Inside(oldPosition)) Assert.That(remaining.All(Inside), Is.True, "An inside visitor must not revisit the pavement.");
                }
            }
        }
        private static void AssertClearOfEarlierSlots(System.Collections.Generic.IList<ClinicMovementPoint> path, int index)
        {
            for (int step = 0; step <= 250; step++)
            {
                var point = ClinicDoctorsNavigation.SampleArrivalPath(path, step / 250d);
                for (int earlier = 0; earlier < index; earlier++)
                {
                    var occupied = ClinicDoctorsNavigation.Anchor("reception.queue." + earlier);
                    Assert.That(Separation(point, new ClinicMovementPoint(occupied.x, occupied.z)), Is.GreaterThan(.54),
                        "Arrival to " + index + " crossed occupied slot " + earlier);
                }
            }
        }
        [Test] public void SavedQueueMovementSurvivesRelaunchAndCannotBeAdmittedBeforeReachingTheHead()
        {
            var game = DoctorsProgressionFixture.MaxDoctors();
            bool restoredMovingQueue = false;
            int approaches = 0;
            var desksUsed = new System.Collections.Generic.HashSet<int>();
            double closestReceptionPair = double.MaxValue;
            for (int tick = 0; tick < 9000; tick++)
            {
                var previous = game.State.Patients.Where(p => p.Phase == ClinicPatientPhase.ReceptionQueue).ToDictionary(p => p.Id,
                    p => new { p.ToAnchor, p.QueueMoveEndsTick, Path = p.QueueMovePath.Select(v => new ClinicMovementPoint(v.X, v.Z)).ToList() });
                game.Advance(.1, false);
                foreach (var patient in game.State.Patients.Where(p => p.Phase == ClinicPatientPhase.WalkingToReception && p.PhaseStartedTick == game.State.Tick))
                {
                    approaches++; desksUsed.Add(patient.DeskId);
                    Assert.That(patient.FromAnchor, Is.EqualTo("reception.queue.0"));
                    Assert.That(patient.QueueMovePath, Is.Empty);
                    if (previous.TryGetValue(patient.Id, out var old)) Assert.That(old.QueueMoveEndsTick, Is.LessThanOrEqualTo(patient.PhaseStartedTick));
                    Assert.That(game.State.Patients.Count(p => p.Phase == ClinicPatientPhase.WalkingToReception), Is.EqualTo(1));
                    foreach (var other in game.State.Patients.Where(p => p.Id != patient.Id))
                        Assert.That(ClinicDoctorsNavigation.ReceptionDepartureClearTick(other), Is.LessThanOrEqualTo(patient.PhaseStartedTick));
                }
                // Observe actual simultaneous check-in / departure / approach positions,
                // including calling time, rather than testing only an occupancy flag.
                var reception = game.State.Patients.Where(p => p.Phase == ClinicPatientPhase.WalkingToReception || p.Phase == ClinicPatientPhase.CheckingIn
                    || ClinicDoctorsNavigation.ReceptionDepartureClearTick(p) > game.State.Tick).Select(p =>
                    {
                        if (p.Phase == ClinicPatientPhase.CheckingIn)
                        { var a = ClinicDoctorsNavigation.Anchor(p.ToAnchor); return new ClinicMovementPoint(a.x, a.z); }
                        return ClinicDoctorsNavigation.SampleArrivalPath(ClinicDoctorsNavigation.ArrivalPath(p.FromAnchor, p.ToAnchor),
                            (game.State.Tick - p.PhaseStartedTick) / (double)(p.PhaseEndsTick - p.PhaseStartedTick));
                    }).ToArray();
                for (int a = 0; a < reception.Length; a++) for (int b = a + 1; b < reception.Length; b++)
                    closestReceptionPair = Math.Min(closestReceptionPair, Separation(reception[a], reception[b]));
                if (!restoredMovingQueue && game.State.Patients.Any(p => p.QueueMoveEndsTick > game.State.Tick))
                {
                    restoredMovingQueue = true;
                    var fine = new ClinicSimulation(ClinicDoctorsTests.Clone(game.State));
                    var coarse = new ClinicSimulation(ClinicDoctorsTests.Clone(game.State));
                    var offline = new ClinicSimulation(ClinicDoctorsTests.Clone(game.State));
                    for (int step = 0; step < 600; step++) fine.Advance(.1, false);
                    coarse.Advance(60, false); offline.AdvanceOffline(60);
                    Assert.That(ClinicDoctorsTests.Fingerprint(coarse.State), Is.EqualTo(ClinicDoctorsTests.Fingerprint(fine.State)));
                    Assert.That(ClinicDoctorsTests.Fingerprint(offline.State), Is.EqualTo(ClinicDoctorsTests.Fingerprint(fine.State)));
                    Assert.That(ClinicSimulation.IsValidState(coarse.State), Is.True);
                }
            }
            Assert.That(restoredMovingQueue, Is.True);
            Assert.That(approaches, Is.GreaterThan(20));
            Assert.That(desksUsed, Is.EquivalentTo(new[] { 0, 1, 2, 3 }), "A short trained service must not starve the more distant desks.");
            Assert.That(closestReceptionPair, Is.GreaterThan(.54), "Approaches must wait until departing visitors clear the aisle.");
            Assert.That(ClinicSimulation.IsValidState(game.State), Is.True);
        }
        [Test] public void EarlierSchemaThreeMissingQueueFieldsRestoreAsStationaryAndCorruptMovementIsRejected()
        {
            var game = DoctorsProgressionFixture.MaxDoctors();
            for (int tick = 0; tick < 20000 && !game.State.Patients.Any(p => p.QueueMoveEndsTick > game.State.Tick); tick++) game.Advance(.1, false);
            var moving = game.State.Patients.First(p => p.QueueMoveEndsTick > game.State.Tick);
            foreach (var mutation in new Action<ClinicPatientState>[]
            {
                p => p.QueueMovePath = null,
                p => p.QueueMovePath[0].X = float.NaN,
                p => p.QueueMovePath.Last().Z += 1,
                p => p.QueueMoveEndsTick++
            })
            {
                var corrupt = ClinicDoctorsTests.Clone(game.State); mutation(corrupt.Patients.Find(p => p.Id == moving.Id));
                Assert.That(ClinicSimulation.IsValidState(corrupt), Is.False);
            }
            var legacy = ClinicDoctorsTests.Clone(game.State);
            foreach (var patient in legacy.Patients)
            { patient.QueueMovePath = null; patient.QueueMoveStartedTick = patient.QueueMoveEndsTick = 0; }
            Assert.That(ClinicSimulation.IsValidState(legacy), Is.True);
            var restored = new ClinicSimulation(legacy);
            Assert.That(restored.State.Patients.All(p => p.QueueMovePath != null && p.QueueMovePath.Count == 0), Is.True);
            restored.Advance(60, false);
            Assert.That(ClinicSimulation.IsValidState(restored.State), Is.True);
        }
        [Test] public void StaffAndPatientDeadlinesCaptureTheSharedActualRoute()
        {
            var game = ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic);
            var first = game.State.Patients[0];
            Assert.That(first.PhaseEndsTick, Is.EqualTo(ClinicDoctorsNavigation.WalkTicks(first.FromAnchor, first.ToAnchor)));
            Assert.That(first.PhaseEndsTick, Is.GreaterThan(30));
            game.Advance(first.PhaseEndsTick / 10d);
            Assert.That(first.Phase, Is.EqualTo(ClinicPatientPhase.WalkingToReception));
            Assert.That(first.PhaseEndsTick - first.PhaseStartedTick, Is.EqualTo(ClinicDoctorsNavigation.WalkTicks(first.FromAnchor, first.ToAnchor)));
            var donor = DoctorsProgressionFixture.MaxStarter(); Assert.That(donor.TransferWalletTo(game).Success, Is.True);
            Assert.That(game.HireReceptionist().Success, Is.True);
            var staff = game.State.Staff.Find(s => s.Id == 1);
            Assert.That(staff.MoveEndsTick - staff.MoveStartedTick, Is.EqualTo(ClinicDoctorsNavigation.WalkTicks("entrance", staff.ToAnchor, true)));
            Assert.That(staff.MoveEndsTick - staff.MoveStartedTick, Is.GreaterThan(40));
        }
        [Test] public void QueueAdvanceTrimsActualParkingAndTaxiArrivalsAndRetimesOnlyTheRemainingWalk()
        {
            var game = ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic);
            var donor = DoctorsProgressionFixture.MaxStarter(); Assert.That(donor.TransferWalletTo(game).Success, Is.True);
            while (game.ReceptionistCount < 4) Assert.That(game.HireReceptionist().Success, Is.True);
            while (game.State.Room(ClinicRoom.Consultation).Tier < 4)
            { Assert.That(game.Renovate(ClinicRoom.Consultation).Success, Is.True); game.Advance(ClinicRules.RenovationSeconds(game.State, ClinicRoom.Consultation)); }
            while (game.State.ConsultationStations.Count < 4) Assert.That(game.AddStation(ClinicStaffRole.Doctor).Success, Is.True);
            bool parkedInside = false, taxiInside = false; int changes = 0;
            for (int i = 0; i < 60000 && !(parkedInside && taxiInside); i++)
            {
                var before = game.State.Patients.Where(p => p.Phase == ClinicPatientPhase.Arriving && (p.ParkingBayId >= 0 || p.UsesTaxi))
                    .Select(p => new { p.Id, p.ParkingBayId, p.UsesTaxi, p.ToAnchor, p.PhaseStartedTick, p.PhaseEndsTick,
                        Path = p.ArrivalPath.Select(v => new ClinicMovementPoint(v.X, v.Z)).ToList() }).ToArray();
                // Hiring a prepared doctor expands admissions through the real command. An older
                // queued visitor can then take a free desk while the driver is already inside.
                if (!parkedInside && game.State.Staff.Count(s => s.Role == ClinicStaffRole.Doctor) < 4
                    && game.State.ReceptionDesks.Any(d => d.PatientId < 0))
                {
                    var driver = before.FirstOrDefault(p => p.ParkingBayId >= 0
                        && game.State.Patients.Any(q => q.Phase == ClinicPatientPhase.ReceptionQueue && q.Id < p.Id)
                        && Inside(ClinicDoctorsNavigation.SampleArrivalPath(p.Path,
                            (game.State.Tick - p.PhaseStartedTick) / (double)(p.PhaseEndsTick - p.PhaseStartedTick))));
                    if (driver != null) Assert.That(game.HireStaff(ClinicStaffRole.Doctor).Success, Is.True);
                }
                game.Advance(.1, false);
                foreach (var old in before)
                {
                    var patient = game.State.Patients.Find(p => p.Id == old.Id);
                    if (patient == null || patient.Phase != ClinicPatientPhase.Arriving || patient.ToAnchor == old.ToAnchor) continue;
                    changes++;
                    double progress = (patient.PhaseStartedTick - old.PhaseStartedTick) / (double)(old.PhaseEndsTick - old.PhaseStartedTick);
                    var expected = ClinicDoctorsNavigation.SampleArrivalPath(old.Path, progress);
                    Assert.That(patient.ArrivalPath[0].X, Is.EqualTo(expected.X).Within(.0001));
                    Assert.That(patient.ArrivalPath[0].Z, Is.EqualTo(expected.Z).Within(.0001));
                    Assert.That(patient.PhaseStartedTick, Is.InRange(game.State.Tick - 1, game.State.Tick));
                    Assert.That(patient.PhaseEndsTick - patient.PhaseStartedTick, Is.EqualTo(ClinicDoctorsNavigation.WalkTicks(patient.ArrivalPath)));
                    if (Inside(expected))
                    {
                        Assert.That(patient.ArrivalPath.All(v => v.X > -12 && v.X < 10.3 && v.Z > -10.30f), Is.True,
                            "Retargeting an inside visitor must not repeat an outside transport leg.");
                        if (old.ParkingBayId >= 0) parkedInside = true;
                        if (old.UsesTaxi) taxiInside = true;
                        var restored = new ClinicSimulation(ClinicDoctorsTests.Clone(game.State));
                        var direct = new ClinicSimulation(ClinicDoctorsTests.Clone(game.State));
                        restored.AdvanceOffline(300); direct.Advance(300, false);
                        Assert.That(ClinicDoctorsTests.Fingerprint(restored.State), Is.EqualTo(ClinicDoctorsTests.Fingerprint(direct.State)));
                    }
                    Assert.That(ClinicSimulation.IsValidState(game.State), Is.True, "Invalid retarget at tick " + game.State.Tick + " visitor " + patient.Id);
                }
            }
            Assert.That(changes, Is.GreaterThan(0)); Assert.That(parkedInside, Is.True, "Parking visitor did not reindex inside"); Assert.That(taxiInside, Is.True, "Taxi visitor did not reindex inside");
        }
        private static bool Inside(ClinicMovementPoint point) => point.X > -12 && point.X < 10.3 && point.Z > -10.30f;
        [Test] public void SavedArrivalPathRejectsMissingEndpointsWrongQueueAndInvalidGeometry()
        {
            var original = ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic).State;
            foreach (var mutation in new Action<ClinicState>[]
            {
                s => s.Patients[0].ArrivalPath.Clear(),
                s => s.Patients[0].ArrivalPath[0].X = float.NaN,
                s => s.Patients[0].ArrivalPath[0].Z = -100,
                s => s.Patients[0].ArrivalPath.Last().X += 2,
                s => s.Patients[0].PhaseEndsTick++
            })
            {
                var copy = ClinicDoctorsTests.Clone(original); mutation(copy);
                Assert.That(ClinicSimulation.IsValidState(copy), Is.False);
            }
        }
        [Test] public void InvalidAnchorsFailRatherThanResolveToTheEntrance()
        {
            Assert.Throws<ArgumentException>(() => ClinicDoctorsNavigation.Anchor("unknown.patient"));
            Assert.Throws<ArgumentException>(() => ClinicDoctorsNavigation.Anchor("consultation.station.8.patient"));
            Assert.Throws<ArgumentException>(() => ClinicDoctorsNavigation.Anchor("pharmacy.station.no.patient"));
        }
        [Test] public void TaxiPassengersUseTheLaybyPavementClearOfTheRoad()
        {
            var points = new System.Collections.Generic.List<ClinicDoctorsNavigation.Point>();
            ClinicDoctorsNavigation.Build("taxi.dock.1.patient", "reception.queue.0", false, ClinicDoctorsNavigation.Anchor("taxi.dock.1.patient"), points.Add);
            Assert.That(points[0].z, Is.EqualTo(-9.8f));
            Assert.That(points[1].x, Is.EqualTo(14.85f));
            Assert.That(points[1].z, Is.EqualTo(-9.8f));
            Assert.That(points[2].x, Is.EqualTo(14.85f));
            Assert.That(points[2].z, Is.EqualTo(-10.9f));
            Assert.That(points.All(p => p.z >= -10.91f), Is.True);
        }
    }
}
