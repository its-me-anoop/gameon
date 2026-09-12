using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleClinic.Core;
using NUnit.Framework;

namespace IdleClinic.Tests
{
    public sealed class ClinicExpansionTests
    {
        [Test] public void NewClinicKeepsOpeningSpeedsAndAddsUnbuiltAmenities()
        {
            var game = ClinicSimulation.CreateNew();
            Assert.That(game.State.SchemaVersion, Is.EqualTo(3));
            Assert.That(game.State.RulesVersion, Is.EqualTo(3));
            Assert.That(game.State.Amenities.Count, Is.EqualTo(3));
            Assert.That(game.State.Amenities.All(a => a.Level == 0 && a.Till == 0), Is.True);
            Assert.That(ClinicRules.ReceptionTicks(game.State, 0), Is.EqualTo(140));
            Assert.That(ClinicRules.TreatmentTicks(game.State, 0), Is.EqualTo(180));
            Assert.That(game.State.TreatmentStations.Single().EquipmentLevel, Is.EqualTo(1));
            Assert.That(game.UpgradeAmenity(ClinicAmenity.Parking).Success, Is.False);
            Assert.That(game.TrainStaff(0).Success, Is.False);
            Assert.That(game.UpgradeStation(ClinicStaffRole.Receptionist, 0).Success, Is.False);
        }

        [Test] public void UpgradePreviewsMatchTheAuthoritativeRoundedServiceTicks()
        {
            var game = Ready(); Earn(game, 500);
            var expected = ClinicRules.StationServiceTicks(game.State, ClinicStaffRole.Receptionist, 0, equipmentLevelsAdded: 1);
            Assert.That(game.UpgradeStation(ClinicStaffRole.Receptionist, 0).Success, Is.True);
            Assert.That(ClinicRules.ReceptionTicks(game.State), Is.EqualTo(expected));
            var waiting = ClinicRules.WaitingCallTicks(game.State, equipmentLevelsAdded: 1);
            Assert.That(waiting, Is.EqualTo(18));
            game.BuildWaitingRoom(); game.Advance(20); game.Upgrade(ClinicRoom.Waiting, UpgradeTrack.Equipment);
            Assert.That(ClinicRules.WaitingCallTicks(game.State), Is.EqualTo(waiting));
        }

        [Test] public void IndividualDesksAndTrainingUseAdditiveSpeedsWithoutChangingOtherDesks()
        {
            var game = Ready(); Earn(game, 1000); game.HireReceptionist();
            Assert.That(game.UpgradeStation(ClinicStaffRole.Receptionist, 0).Success, Is.True);
            Assert.That(ClinicRules.ReceptionTicks(game.State, 0), Is.EqualTo(128));
            Assert.That(ClinicRules.ReceptionTicks(game.State, 1), Is.EqualTo(140));
            Assert.That(game.TrainStaff(0).Success, Is.True);
            Assert.That(ClinicRules.ReceptionTicks(game.State, 0), Is.EqualTo(115));
            Assert.That(game.Upgrade(ClinicRoom.Reception, UpgradeTrack.Equipment).Success, Is.True);
            Assert.That(ClinicRules.ReceptionTicks(game.State, 0), Is.EqualTo(103));
            Assert.That(ClinicRules.ReceptionTicks(game.State, 1), Is.EqualTo(122));
            Assert.That(game.UpgradeStation(ClinicStaffRole.Receptionist, 0).Success, Is.False);
            Assert.That(game.TrainStaff(0).Success, Is.False);
            Valid(game);
        }

        [Test] public void NursingStationUpgradeDoesNotUpgradeItsNeighbourOrAlterCurrentCareTimer()
        {
            var game = Ready(); Earn(game, 1400); game.Renovate(ClinicRoom.FirstAid); game.Advance(60);
            game.AddTreatmentStation(); game.HireNurse();
            var care = WaitFor(game, p => p.Phase == ClinicPatientPhase.Treating && p.TreatmentStationId == 0);
            var end = care.PhaseEndsTick;
            Assert.That(game.UpgradeStation(ClinicStaffRole.Nurse, 0).Success, Is.True);
            Assert.That(game.TrainStaff(100).Success, Is.True);
            Assert.That(care.PhaseEndsTick, Is.EqualTo(end));
            Assert.That(ClinicRules.TreatmentTicks(game.State, 0), Is.EqualTo(148));
            Assert.That(ClinicRules.TreatmentTicks(game.State, 1), Is.EqualTo(180));
            Assert.That(game.State.TreatmentStations[1].EquipmentLevel, Is.EqualTo(1));
            Valid(game);
        }

        [Test] public void ExpansionPricesGrowAndRoomTiersCapAmenitiesStationsAndTraining()
        {
            var game = Ready(); Earn(game, 5000);
            Assert.That(ClinicRules.StationUpgradeCost(game.State, ClinicStaffRole.Receptionist, 0), Is.EqualTo(90));
            Assert.That(ClinicRules.StationUpgradeCost(game.State, ClinicStaffRole.Nurse, 0), Is.EqualTo(110));
            Assert.That(ClinicRules.StaffTrainingCost(game.State.Staff[0]), Is.EqualTo(80));
            Assert.That(ClinicRules.AmenityUpgradeCost(ClinicAmenity.Parking, 0), Is.EqualTo(220));
            Assert.That(ClinicRules.AmenityUpgradeCost(ClinicAmenity.Parking, 1), Is.EqualTo(440));
            Assert.That(ClinicRules.AmenityUpgradeCost(ClinicAmenity.Parking, 2), Is.EqualTo(880));
            Assert.That(game.UpgradeAmenity(ClinicAmenity.Toilet).Success, Is.False);
            Assert.That(game.UpgradeAmenity(ClinicAmenity.Vending).Success, Is.False);
            game.BuildWaitingRoom(); game.Advance(20);
            Assert.That(game.UpgradeAmenity(ClinicAmenity.Toilet).Cost, Is.EqualTo(140));
            Assert.That(game.UpgradeAmenity(ClinicAmenity.Vending).Cost, Is.EqualTo(180));
            Assert.That(game.UpgradeAmenity(ClinicAmenity.Toilet).Success, Is.False);
            Assert.That(game.UpgradeAmenity(ClinicAmenity.Vending).Success, Is.False);
            for (var level = 1; level <= 3; level++)
            { Assert.That(game.UpgradeAmenity(ClinicAmenity.Parking).Success, Is.True); Assert.That(ClinicRules.ParkingCapacity(game.State), Is.EqualTo(level * 2)); }
            Assert.That(game.UpgradeAmenity(ClinicAmenity.Parking).Success, Is.False);
            Assert.That(game.UpgradeStation((ClinicStaffRole)9, 0).Success, Is.False);
            Assert.That(game.UpgradeStation(ClinicStaffRole.Nurse, 99).Success, Is.False);
            Assert.That(game.TrainStaff(99).Success, Is.False);
            Assert.That(game.UpgradeAmenity((ClinicAmenity)9).Success, Is.False);
            Valid(game);
        }

        [Test] public void ParkingReservationsAreUniqueHeldThroughDepartureAndReleased()
        {
            var game = Ready(); Earn(game, 600); game.UpgradeAmenity(ClinicAmenity.Parking);
            var arrived = WaitFor(game, p => p.ParkingBayId >= 0 && p.Phase == ClinicPatientPhase.Arriving);
            Assert.That(arrived.Id % 3, Is.Zero);
            Assert.That(arrived.FromAnchor, Is.EqualTo(ClinicRules.ParkingPatientAnchor(arrived.ParkingBayId)));
            Assert.That(arrived.PhaseEndsTick - arrived.PhaseStartedTick, Is.EqualTo(100));
            var id = arrived.Id; var bay = arrived.ParkingBayId;
            var leaving = WaitFor(game, p => p.Id == id && p.Phase == ClinicPatientPhase.Leaving);
            Assert.That(leaving.ParkingBayId, Is.EqualTo(bay));
            Assert.That(leaving.ToAnchor, Is.EqualTo(ClinicRules.ParkingPatientAnchor(bay)));
            Assert.That(leaving.Payment, Is.EqualTo(55));
            game.Advance(10);
            Assert.That(game.State.Patients.Any(p => p.Id == id && p.ParkingBayId == bay), Is.True);
            var driving = WaitFor(game, p => p.Id == id && p.Phase == ClinicPatientPhase.DrivingFromParking);
            game.Advance((driving.PhaseEndsTick-game.State.Tick)/10d);
            Assert.That(game.State.Patients.Any(p => p.Id == id), Is.False);
            for (var i = 0; i < 600; i++)
            {
                game.Advance(.5, false);
                var parked = game.State.Patients.Where(p => p.ParkingBayId >= 0).ToArray();
                Assert.That(parked.Select(p => p.ParkingBayId).Distinct().Count(), Is.EqualTo(parked.Length));
                Assert.That(parked.Length, Is.LessThanOrEqualTo(2)); Valid(game);
            }
        }

        [Test] public void ParkingFeeIsFixedAtCheckInAndAppearanceIsDeterministicAndVaried()
        {
            var game = Ready(); Earn(game, 1000); game.UpgradeAmenity(ClinicAmenity.Parking);
            var patient = WaitFor(game, p => p.ParkingBayId >= 0 && p.Phase == ClinicPatientPhase.CheckingIn);
            var quote = patient.Payment; Assert.That(quote, Is.EqualTo(55));
            game.UpgradeAmenity(ClinicAmenity.Parking);
            Assert.That(patient.Payment, Is.EqualTo(quote));
            var appearances = new HashSet<int>();
            for (var i = 0; i < 300; i++)
            { game.Advance(1); foreach (var p in game.State.Patients) { appearances.Add(p.AppearanceId); Assert.That(p.AppearanceId, Is.EqualTo(ClinicRules.PatientAppearance(game.State.Seed, p.Id))); } }
            Assert.That(appearances.Count, Is.GreaterThanOrEqualTo(6)); Valid(game);
        }

        [Test] public void ToiletAndVendingVisitsReserveSeatsAndAwardTipsOnlyOnceOnUse()
        {
            var game = Amenities();
            var seen = new HashSet<ClinicAmenity>(); var tipped = new HashSet<int>();
            for (var i = 0; i < 2400; i++)
            {
                var report = game.Advance(.5);
                foreach (var e in report.Events.Where(e => e.Kind == ClinicEventKind.TipReceived))
                { Assert.That(tipped.Add(e.PatientId), Is.True); Assert.That(e.Amount, Is.InRange(5, 7)); Assert.That(e.SourceAnchor, Is.EqualTo("waiting.vending.cash")); }
                var visitors = game.State.Patients.Where(IsVisiting).ToArray();
                foreach (var p in visitors) { seen.Add(p.VisitingAmenity); Assert.That(p.Paid && p.HasAdmissionReservation, Is.True); Assert.That(p.SeatId, Is.GreaterThanOrEqualTo(0)); }
                Assert.That(visitors.GroupBy(p => p.VisitingAmenity).All(g => g.Count() <= 1), Is.True);
                Valid(game);
            }
            Assert.That(seen, Is.EquivalentTo(new[] { ClinicAmenity.Toilet, ClinicAmenity.Vending }));
            Assert.That(tipped.Count, Is.GreaterThan(1));
            Assert.That(game.State.TotalTips, Is.EqualTo(game.State.Amenity(ClinicAmenity.Vending).Till));
            Assert.That(game.State.TotalTreatments, Is.GreaterThan(40));
        }

        [Test] public void VendingTipReservesPendingPaymentsAndCannotOverflowTheCurrencyLimit()
        {
            var state = ClinicSimulation.CreateNew().State;
            state.Amenity(ClinicAmenity.Vending).Level = 1;
            var visitor = new ClinicPatientState { Paid = true };
            // Exercise the arithmetic boundary directly: current lifetime arrival limits keep a
            // real campaign below this ceiling, but a later rules version must remain overflow-safe.
            state.TotalEarned = ClinicRules.MaximumCurrency - 5;
            Assert.That(ClinicRules.VendingTipForPatient(state, visitor), Is.EqualTo(5));
            state.TotalEarned++;
            Assert.That(ClinicRules.VendingTipForPatient(state, visitor), Is.Zero);
            state.TotalEarned = ClinicRules.MaximumCurrency - 55;
            state.Patients.Add(new ClinicPatientState { Payment = 50, HasAdmissionReservation = true });
            Assert.That(ClinicRules.VendingTipForPatient(state, visitor), Is.EqualTo(5));
            state.Patients[1].Payment = 51;
            Assert.That(ClinicRules.VendingTipForPatient(state, visitor), Is.Zero);
            state.TotalEarned = long.MaxValue;
            Assert.That(ClinicRules.VendingTipForPatient(state, visitor), Is.Zero);
        }

        [Test] public void VendingCollectionTransfersAuthoritativeTillOnceAndUsesOneFlight()
        {
            var game = Amenities(); game.Advance(600); game.DrainEvents();
            var amount = game.State.Amenity(ClinicAmenity.Vending).Till; var wallet = game.State.Wallet;
            Assert.That(amount, Is.GreaterThan(0));
            Assert.That(game.CollectVendingTips().Amount, Is.EqualTo(amount));
            Assert.That(game.State.Wallet, Is.EqualTo(wallet + amount));
            Assert.That(game.CollectVendingTips().Success, Is.False);
            var events = game.DrainEvents(); Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Kind, Is.EqualTo(ClinicEventKind.CashCollected));
            Assert.That(events[0].Amenity, Is.EqualTo(ClinicAmenity.Vending));
            Assert.That(events[0].SourceAnchor, Is.EqualTo("waiting.vending.cash"));
            Assert.That(new ClinicSimulation(Clone(game.State)).CollectVendingTips().Success, Is.False);
            Valid(game);
        }

        [Test] public void InterruptedAmenityUseAndOfflineAdvanceMatchLiveAndConserveMoney()
        {
            var active = Amenities();
            WaitFor(active, p => p.Phase == ClinicPatientPhase.UsingAmenity && p.VisitingAmenity == ClinicAmenity.Vending);
            var resumed = new ClinicSimulation(Clone(active.State));
            active.Advance(600, false); resumed.AdvanceOffline(600);
            Assert.That(resumed.State.TotalTips, Is.EqualTo(active.State.TotalTips));
            Assert.That(resumed.State.TotalTreatments, Is.EqualTo(active.State.TotalTreatments));
            Assert.That(resumed.State.TotalEarned, Is.EqualTo(active.State.TotalEarned));
            var capped = new ClinicSimulation(Clone(active.State)); var week = new ClinicSimulation(Clone(active.State));
            capped.AdvanceOffline(28800); week.AdvanceOffline(604800);
            Assert.That(week.State.TotalEarned, Is.EqualTo(capped.State.TotalEarned));
            Assert.That(week.State.TotalTips, Is.EqualTo(capped.State.TotalTips));
            Assert.That(week.State.TotalEarned, Is.EqualTo(week.State.TotalSpent + week.State.Wallet + week.TillCash));
            Valid(active); Valid(resumed); Valid(capped); Valid(week);
        }

        [Test] public void CorruptedExpansionLevelsReservationsAndTipAccountingAreRejected()
        {
            var game = Amenities(); game.Advance(100);
            var bad = Clone(game.State); bad.Staff[0].TrainingLevel = 6; Assert.That(ClinicSimulation.IsValidState(bad), Is.False);
            bad = Clone(game.State); bad.ReceptionDesks[0].EquipmentLevel = 6; Assert.That(ClinicSimulation.IsValidState(bad), Is.False);
            bad = Clone(game.State); bad.TreatmentStations.Add(new TreatmentStationState { Id = 0 }); Assert.That(ClinicSimulation.IsValidState(bad), Is.False);
            bad = Clone(game.State); bad.Amenity(ClinicAmenity.Toilet).Till = 1; Assert.That(ClinicSimulation.IsValidState(bad), Is.False);
            bad = Clone(game.State); bad.TotalTips = bad.TotalEarned + 1; Assert.That(ClinicSimulation.IsValidState(bad), Is.False);
            bad = Clone(game.State); bad.Patients[0].AppearanceId = 99; Assert.That(ClinicSimulation.IsValidState(bad), Is.False);
            bad = Clone(game.State); bad.Patients[0].ParkingBayId = 5; Assert.That(ClinicSimulation.IsValidState(bad), Is.False);
        }

        [Test] public void UnreservedParkingAnchorsAndLegacyAmenityAnchorsAreRejected()
        {
            var game = ClinicSimulation.CreateNew();
            game.State.Patients[0].FromAnchor = ClinicRules.ParkingPatientAnchor(0);
            Assert.That(ClinicSimulation.IsValidState(game.State), Is.False);
            var legacy = ClinicSimulation.CreateNew().State; legacy.SchemaVersion = 1; legacy.RulesVersion = 1;
            legacy.Patients[0].FromAnchor = ClinicRules.AmenityPatientAnchor(ClinicAmenity.Toilet);
            Assert.That(ClinicStateMigration.TryMigrateV1(legacy), Is.False);
        }

        [Test] public void PaidPatientsFinishThroughFullQueuesAmenityVisitsAndRenovation()
        {
            var game = Amenities();
            WaitFor(game, IsVisiting);
            var paid = game.State.Patients.Where(p => p.Paid).Select(p => p.Id).ToArray();
            Earn(game, 600);
            Assert.That(game.Renovate(ClinicRoom.Waiting).Success, Is.True);
            Assert.That(game.Renovate(ClinicRoom.FirstAid).Success, Is.True);
            for (var i = 0; i < 1200; i++) { game.Advance(.5, false); Valid(game); }
            Assert.That(game.State.Patients.Any(p => paid.Contains(p.Id)), Is.False);
            Assert.That(game.State.Construction, Is.Empty);
            Assert.That(game.State.TotalTreatments, Is.GreaterThan(20));
            Assert.That(game.State.TotalEarned, Is.EqualTo(game.State.TotalSpent + game.State.Wallet + game.TillCash));
        }

        [Test] public void AllStationTrainingAndAmenityLevelsReachFiniteCapsWithValidOfflineAccounting()
        {
            var game = Amenities();
            foreach (ClinicRoom room in new[] { ClinicRoom.Reception, ClinicRoom.FirstAid, ClinicRoom.Waiting })
            {
                while (game.State.Room(room).Tier < 3)
                { Earn(game, ClinicRules.RenovationCost(game.State.Room(room))); Assert.That(game.Renovate(room).Success, Is.True); game.Advance(180); }
            }
            Earn(game, 1000); game.AddTreatmentStation(); game.HireNurse(); game.HireReceptionist();
            foreach (var staff in game.State.Staff)
            {
                while (staff.TrainingLevel < 6)
                { Earn(game, ClinicRules.StaffTrainingCost(staff)); Assert.That(game.TrainStaff(staff.Id).Success, Is.True); }
                while (ClinicRules.StationLevel(game.State, staff.Role, staff.StationId) < 6)
                { Earn(game, ClinicRules.StationUpgradeCost(game.State, staff.Role, staff.StationId)); Assert.That(game.UpgradeStation(staff.Role, staff.StationId).Success, Is.True); }
                Assert.That(game.TrainStaff(staff.Id).Success, Is.False);
                Assert.That(game.UpgradeStation(staff.Role, staff.StationId).Success, Is.False);
            }
            foreach (var amenity in game.State.Amenities)
            {
                while (amenity.Level < 3)
                { Earn(game, ClinicRules.AmenityUpgradeCost(amenity.Kind, amenity.Level)); Assert.That(game.UpgradeAmenity(amenity.Kind).Success, Is.True); }
                Assert.That(game.UpgradeAmenity(amenity.Kind).Success, Is.False);
            }
            game.AdvanceOffline(28800); Valid(game);
            Assert.That(ClinicRules.ParkingCapacity(game.State), Is.EqualTo(6));
            Assert.That(ClinicRules.VendingTip(game.State), Is.EqualTo(15));
            Assert.That(game.State.TotalEarned, Is.EqualTo(game.State.TotalSpent + game.State.Wallet + game.TillCash));
        }

        [Test] public void InvalidLegacyMigrationDoesNotMutateTheOriginalSnapshot()
        {
            var state = Clone(Ready().State); state.SchemaVersion = 1; state.RulesVersion = 1; state.Wallet++;
            Assert.That(ClinicStateMigration.TryMigrateV1(state), Is.False);
            Assert.That(state.SchemaVersion, Is.EqualTo(1));
            Assert.That(state.RulesVersion, Is.EqualTo(1));
        }

        private static bool IsVisiting(ClinicPatientState p) => p.Phase == ClinicPatientPhase.WalkingToAmenity || p.Phase == ClinicPatientPhase.UsingAmenity || p.Phase == ClinicPatientPhase.ReturningFromAmenity;
        private static ClinicSimulation Ready() { var g = ClinicSimulation.CreateNew(); g.Advance(25); g.Collect(0); g.HireNurse(); g.Advance(30); return g; }
        private static ClinicSimulation Amenities() { var g = Ready(); Earn(g, 1200); g.BuildWaitingRoom(); g.Advance(20); g.UpgradeAmenity(ClinicAmenity.Toilet); g.UpgradeAmenity(ClinicAmenity.Vending); g.UpgradeAmenity(ClinicAmenity.Parking); g.DrainEvents(); Valid(g); return g; }
        private static void Earn(ClinicSimulation game, long amount) { for (var i = 0; i < 2000 && game.State.Wallet < amount; i++) { game.Advance(10, false); foreach (var d in game.State.ReceptionDesks) game.Collect(d.Id); } Assert.That(game.State.Wallet, Is.GreaterThanOrEqualTo(amount)); }
        private static ClinicPatientState WaitFor(ClinicSimulation game, Func<ClinicPatientState, bool> test) { for (var i = 0; i < 2400; i++) { game.Advance(.1); var p = game.State.Patients.FirstOrDefault(test); if (p != null) return p; } Assert.Fail("Patient transition not reached."); return null; }
        private static void Valid(ClinicSimulation game) => Assert.That(ClinicSimulation.IsValidState(game.State), Is.True, "Invalid state at tick " + game.State.Tick);
        private static T Clone<T>(T value) => (T)CloneObject(value);
        private static object CloneObject(object value)
        {
            if (value == null || value.GetType().IsValueType || value is string) return value;
            if (value is IList items) { var list = (IList)Activator.CreateInstance(value.GetType()); foreach (var item in items) list.Add(CloneObject(item)); return list; }
            var result = Activator.CreateInstance(value.GetType()); foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)) field.SetValue(result, CloneObject(field.GetValue(value))); return result;
        }
    }
}
