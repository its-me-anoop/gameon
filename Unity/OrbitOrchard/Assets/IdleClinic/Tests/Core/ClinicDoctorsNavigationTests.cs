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
