using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using IdleClinic.Core;
using NUnit.Framework;

namespace IdleClinic.Tests
{
    public sealed class ClinicDoctorsTests
    {
        [Test] public void ZeroWalletOpeningEarnsThroughConsultationFirstAidAndPharmacyInOrder()
        {
            var game = ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic);
            Assert.That(game.State.Wallet, Is.Zero);
            Assert.That(game.State.Staff.Select(s => s.Role), Is.EquivalentTo(Enum.GetValues(typeof(ClinicStaffRole))));
            var steps = new List<ClinicEventKind>();
            for (int i = 0; i < 3600; i++)
            {
                foreach (var e in game.Advance(.1).Events.Where(e => e.PatientId == 0))
                    if (e.Kind == ClinicEventKind.PaymentReceived || e.Kind == ClinicEventKind.ConsultationCompleted || e.Kind == ClinicEventKind.TreatmentCompleted || e.Kind == ClinicEventKind.DispensingCompleted) steps.Add(e.Kind);
                Valid(game);
            }
            Assert.That(steps, Is.EqualTo(new[] { ClinicEventKind.PaymentReceived, ClinicEventKind.ConsultationCompleted, ClinicEventKind.TreatmentCompleted, ClinicEventKind.DispensingCompleted }));
            Assert.That(game.State.Patients.Any(p => p.Id == 0), Is.False);
            Assert.That(game.Collect(0).Amount, Is.GreaterThanOrEqualTo(100));
            Assert.That(game.Collect(0).Success, Is.False);
        }
        [Test] public void CorrespondingCostsTimesIncomeDoubleAndEveryNewFacilityHasABenefit()
        {
            var starter = ClinicSimulation.CreateNew().State;
            var doctors = ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic).State;
            Assert.That(ClinicRules.VisitFee(doctors), Is.EqualTo(2 * ClinicRules.VisitFee(starter)));
            Assert.That(ClinicRules.ReceptionTicks(doctors), Is.EqualTo(280));
            Assert.That(ClinicRules.TreatmentTicks(doctors), Is.EqualTo(360));
            Assert.That(ClinicRules.StationServiceTicks(doctors, ClinicStaffRole.Doctor, 0), Is.EqualTo(480));
            Assert.That(ClinicRules.StationServiceTicks(doctors, ClinicStaffRole.Pharmacist, 0), Is.EqualTo(240));
            foreach (var room in starter.Rooms)
            {
                foreach (UpgradeTrack track in Enum.GetValues(typeof(UpgradeTrack)))
                    Assert.That(ClinicRules.UpgradeCost(doctors, room.Kind, track), Is.EqualTo(2 * ClinicRules.UpgradeCost(starter, room.Kind, track)));
                Assert.That(ClinicRules.RenovationCost(doctors, room.Kind), Is.EqualTo(2 * ClinicRules.RenovationCost(starter, room.Kind)));
                Assert.That(ClinicRules.RenovationSeconds(doctors, room.Kind), Is.EqualTo(120));
            }
            var durations = new List<int>();
            for (int tier = 1; tier <= 5; tier++) { doctors.Room(ClinicRoom.Reception).Tier = tier; durations.Add(ClinicRules.RenovationSeconds(doctors, ClinicRoom.Reception)); }
            Assert.That(durations, Is.EqualTo(new[] { 120, 360, 1080, 3240, 9720 }));
            doctors.Room(ClinicRoom.Consultation).FacilitiesLevel++;
            Assert.That(ClinicRules.VisitFee(doctors), Is.EqualTo(110));
            doctors.Room(ClinicRoom.Pharmacy).FacilitiesLevel++;
            Assert.That(ClinicRules.VisitFee(doctors), Is.EqualTo(120));
        }
        [Test] public void EveryStarterPrerequisiteIndependentlyBlocksUnlockAndExactly100000IsChargedOnce()
        {
            var maxed = DoctorsProgressionFixture.MaxStarter();
            Assert.That(ClinicRules.StarterCompletion(maxed.State), Is.Empty);
            var variants = new List<Action<ClinicState>>();
            foreach (var room in maxed.State.Rooms)
            {
                var kind = room.Kind;
                variants.Add(s => s.Room(kind).Tier--);
                variants.Add(s => s.Room(kind).EquipmentLevel--);
                variants.Add(s => s.Room(kind).FacilitiesLevel--);
                variants.Add(s => s.Room(kind).DecorationLevel--);
            }
            variants.Add(s => s.Room(ClinicRoom.Waiting).Built = false);
            foreach (var staff in maxed.State.Staff)
            {
                var id = staff.Id; var role = staff.Role; var station = staff.StationId;
                variants.Add(s => s.Staff.Find(x => x.Id == id).TrainingLevel--);
                variants.Add(s => { if (role == ClinicStaffRole.Receptionist) s.ReceptionDesks[station].EquipmentLevel--; else s.TreatmentStations[station].EquipmentLevel--; });
                variants.Add(s => s.Staff.RemoveAll(x => x.Id == id));
            }
            foreach (var amenity in maxed.State.Amenities) { var kind = amenity.Kind; variants.Add(s => s.Amenity(kind).Level--); }
            variants.Add(s => { s.TreatmentStations.RemoveAt(1); s.Room(ClinicRoom.FirstAid).StationCount--; });
            variants.Add(s => { s.ReceptionDesks.RemoveAt(1); s.Room(ClinicRoom.Reception).StationCount--; });
            variants.Add(s => s.Construction.Add(new ClinicConstructionState { Room = ClinicRoom.Waiting }));
            foreach (var mutate in variants)
            {
                var game = new ClinicSimulation(Clone(maxed.State));
                mutate(game.State);
                var before = Fingerprint(game.State);
                Assert.That(game.UnlockDoctorsClinic().Success, Is.False);
                Assert.That(Fingerprint(game.State), Is.EqualTo(before));
                Assert.That(ClinicRules.StarterCompletion(game.State), Is.Not.Empty);
            }
            // Drain the legitimately earned surplus to the other location through the real transfer command,
            // then return exactly 100,000. The boundary is a unit-test ledger fixture, not gameplay currency.
            var boundary = Clone(maxed.State);
            var surplus = boundary.Wallet - 100000;
            boundary.TotalTransferredOut += surplus; boundary.Wallet = 100000;
            var exact = new ClinicSimulation(boundary);
            var paid = exact.State.Patients.Where(p => p.Paid).Select(p => p.Id).ToArray();
            Assert.That(exact.UnlockDoctorsClinic().Cost, Is.EqualTo(100000));
            Assert.That(exact.State.Wallet, Is.Zero); Valid(exact);
            var unchanged = Fingerprint(exact.State);
            Assert.That(exact.UnlockDoctorsClinic().Success, Is.False);
            Assert.That(Fingerprint(exact.State), Is.EqualTo(unchanged));
            Assert.That(exact.State.Patients.Where(p => p.Paid).Select(p => p.Id), Is.EqualTo(paid));
            var low = Clone(boundary); low.DoctorsClinicUnlocked = false; low.TotalSpent -= 100000; low.Wallet = 99999; low.TotalTransferredOut++;
            var shortGame = new ClinicSimulation(low);
            Assert.That(shortGame.UnlockDoctorsClinic().Success, Is.False);
        }
        [Test] public void BothLocationTransfersConserveMoneyAndRejectOverflowsWithoutMutation()
        {
            var a = DoctorsProgressionFixture.MaxStarter();
            Assert.That(a.UnlockDoctorsClinic().Success, Is.True);
            var b = ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic);
            long sum = a.State.Wallet + b.State.Wallet;
            Assert.That(a.TransferWalletTo(b).Success, Is.True);
            Assert.That(a.State.Wallet, Is.Zero);
            Assert.That(a.State.Wallet + b.State.Wallet, Is.EqualTo(sum)); Valid(a); Valid(b);
            b.Advance(600); b.Collect(0); sum = a.State.Wallet + b.State.Wallet;
            Assert.That(b.TransferWalletTo(a).Success, Is.True);
            Assert.That(b.State.Wallet, Is.Zero); Assert.That(a.State.Wallet, Is.EqualTo(sum));
            Assert.That(a.State.TotalTransferredIn, Is.EqualTo(b.State.TotalTransferredOut));
            Assert.That(a.State.TotalTransferredOut, Is.EqualTo(b.State.TotalTransferredIn)); Valid(a); Valid(b);
            var before = Fingerprint(a.State);
            Assert.That(a.TransferWalletTo(a).Success, Is.False);
            Assert.That(Fingerprint(a.State), Is.EqualTo(before));
            var destination = ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic);
            destination.State.TotalTransferredIn = ClinicRules.MaximumCurrency; destination.State.Wallet = ClinicRules.MaximumCurrency;
            Assert.That(a.TransferWalletTo(destination).Success, Is.False);
            Assert.That(Fingerprint(a.State), Is.EqualTo(before));
        }
        [Test] public void AllDoctorsStaffStationsRoomsAndAmenitiesReachTheirRequestedCapsThroughCommands()
        {
            var game = DoctorsProgressionFixture.MaxDoctors();
            foreach (ClinicStaffRole role in Enum.GetValues(typeof(ClinicStaffRole)))
            {
                int cap = role == ClinicStaffRole.Pharmacist ? 2 : 4;
                Assert.That(game.State.Staff.Count(s => s.Role == role), Is.EqualTo(cap));
                Assert.That(ClinicRules.StationCount(game.State, role), Is.EqualTo(cap));
                Assert.That(game.HireStaff(role).Success, Is.False); Assert.That(game.AddStation(role).Success, Is.False);
                foreach (var staff in game.State.Staff.Where(s => s.Role == role))
                {
                    Assert.That(staff.TrainingLevel, Is.EqualTo(12));
                    Assert.That(ClinicRules.StationLevel(game.State, role, staff.StationId), Is.EqualTo(12));
                    Assert.That(game.TrainStaff(staff.Id).Success, Is.False);
                    Assert.That(game.UpgradeStation(role, staff.StationId).Success, Is.False);
                }
            }
            foreach (var room in game.State.Rooms) { Assert.That(room.Tier, Is.EqualTo(6)); Assert.That(room.EquipmentLevel, Is.EqualTo(12)); Assert.That(room.FacilitiesLevel, Is.EqualTo(12)); Assert.That(room.DecorationLevel, Is.EqualTo(12)); }
            Assert.That(ClinicRules.ParkingCapacity(game.State), Is.EqualTo(12));
            Assert.That(ClinicRules.ToiletCubicleCount(game.State), Is.EqualTo(2));
            Assert.That(ClinicRules.UnpaidQueueCapacity(game.State), Is.EqualTo(23));
            Assert.That(ClinicRules.WaitingCapacity(game.State), Is.EqualTo(30));
            Assert.That(game.State.Amenities.All(a => a.Level == 6), Is.True);
            Valid(game);
        }
        [Test] public void TaxiDropoffAndPickupAreRealSeparateBoundedJourneysAndRestoreBetweenEveryPhase()
        {
            var game = ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic);
            var taxiStages = new HashSet<string>(); var completed = new HashSet<int>();
            for (int i = 0; i < 12000; i++)
            {
                foreach (var e in game.Advance(.1).Events) if (e.Kind == ClinicEventKind.TaxiDeparted) completed.Add(e.PatientId);
                foreach (var r in game.State.TaxiRides)
                {
                    var key = r.Pickup + ":" + r.Phase;
                    if (taxiStages.Add(key))
                    {
                        var restored = new ClinicSimulation(Clone(game.State));
                        restored.Advance(120, false); var direct = new ClinicSimulation(Clone(game.State)); direct.Advance(120, false);
                        Assert.That(Fingerprint(restored.State), Is.EqualTo(Fingerprint(direct.State)));
                    }
                }
                Valid(game);
            }
            Assert.That(taxiStages, Is.SupersetOf(new[] { "False:Approaching", "False:Boarding", "False:Departing", "True:Approaching", "True:Boarding", "True:Departing" }));
            Assert.That(completed, Does.Contain(2)); Assert.That(game.State.Patients.Any(p => p.Id == 2), Is.False);
        }
        [Test] public void CoarseFineAndOfflineOperationsRemainIdenticalAcrossClinicalAndTrafficEvents()
        {
            var a = ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic);
            var b = new ClinicSimulation(Clone(a.State)); var c = new ClinicSimulation(Clone(a.State));
            a.Advance(1800, false); c.AdvanceOffline(1800);
            for (int i = 0; i < 18000; i++) b.Advance(.1, false);
            Assert.That(Fingerprint(a.State), Is.EqualTo(Fingerprint(b.State)));
            Assert.That(Fingerprint(a.State), Is.EqualTo(Fingerprint(c.State))); Valid(a); Valid(b); Valid(c);
            var shifted = new ClinicSimulation(Clone(a.State)); var capped = new ClinicSimulation(Clone(a.State));
            shifted.AdvanceOffline(30000); capped.AdvanceOffline(28800);
            Assert.That(shifted.State.TotalEarned, Is.EqualTo(capped.State.TotalEarned));
            Assert.That(shifted.State.PausedTrafficTicks, Is.EqualTo(12000)); Valid(shifted);
            shifted.Advance(300, false); capped.Advance(300, false);
            Assert.That(shifted.State.TotalEarned, Is.EqualTo(capped.State.TotalEarned));
        }
        [Test] public void DoctorSaveRejectsWrongStagesDuplicateRolesSeatsBaysCubiclesAndTaxiReservations()
        {
            var game = ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic); game.Advance(180);
            var mutations = new Action<ClinicState>[] {
                s => s.Wallet++, s => s.Location = (ClinicLocation)50, s => s.Room(ClinicRoom.Pharmacy).Tier = 7,
                s => s.Room(ClinicRoom.Consultation).EquipmentLevel = 3, s => s.Staff[0].StationId = 10,
                s => s.Patients[0].ToAnchor = "unknown.anchor", s => s.Patients[0].NextService = ClinicStaffRole.Receptionist,
                s => s.Patients[0].PharmacyComplete = true, s => s.Amenity(ClinicAmenity.Taxi).Level = 7,
                s => s.TotalTransferredOut = -1, s => s.TaxiRides.Add(new ClinicTaxiState { PatientId = 99999, DockId = 0 })
            };
            foreach (var mutate in mutations) { var copy = Clone(game.State); mutate(copy); Assert.That(ClinicSimulation.IsValidState(copy), Is.False); }
        }
        [Test] public void V2MigrationRejectsInvalidLegacyBeforeChangingSchemaAndKeepsPaidCareAndCash()
        {
            var legacy = DoctorsProgressionFixture.MaxStarter().State; legacy.SchemaVersion = legacy.RulesVersion = 2;
            long wallet = legacy.Wallet, earned = legacy.TotalEarned;
            Assert.That(ClinicStateMigration.TryMigrateV2(legacy), Is.True); Assert.That(legacy.SchemaVersion, Is.EqualTo(3));
            Assert.That(legacy.Wallet, Is.EqualTo(wallet)); Assert.That(legacy.TotalEarned, Is.EqualTo(earned));
            Assert.That(legacy.Location, Is.EqualTo(ClinicLocation.StarterClinic));
            var invalid = Clone(legacy); invalid.SchemaVersion = invalid.RulesVersion = 2; invalid.Wallet++;
            var before = Fingerprint(invalid); Assert.That(ClinicStateMigration.TryMigrateV2(invalid), Is.False); Assert.That(Fingerprint(invalid), Is.EqualTo(before));
        }
        [Test] public void TwoToiletCubiclesKeepDifferentPatientsAndTheirNextServiceReserved()
        {
            var game = ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic);
            DoctorsProgressionFixture.Earn(game, 10000);
            while (game.ReceptionistCount < 4) Assert.That(game.HireReceptionist().Success, Is.True);
            // A valid scheduling fixture puts two already paid waiting patients before their
            // first toilet visit. It keeps their real quotes, service stages and seats unchanged.
            ClinicPatientState[] eligible = null;
            for (int i = 0; i < 18000; i++)
            {
                game.Advance(.1, false);
                eligible = game.State.Patients.Where(p => p.Phase == ClinicPatientPhase.Seated && p.Id % 3 == 1).Take(2).ToArray();
                if (eligible.Length == 2 && game.State.Staff.Where(s => s.Role != ClinicStaffRole.Receptionist).All(s => s.PatientId >= 0)) break;
            }
            Assert.That(eligible.Length, Is.EqualTo(2));
            foreach (var patient in eligible) patient.UsedToilet = false;
            Valid(game);
            game.Advance(.1, false);
            var visitors = game.State.Patients.Where(p => eligible.Any(e => e.Id == p.Id)).ToArray();
            Assert.That(visitors.All(p => p.Phase == ClinicPatientPhase.WalkingToAmenity), Is.True);
            Assert.That(visitors.Select(p => p.ToiletCubicleId), Is.EquivalentTo(new[] { 0, 1 }));
            Assert.That(visitors.All(p => p.HasAdmissionReservation && p.SeatId >= 0), Is.True);
            var corrupted = Clone(game.State);
            corrupted.Patients.Find(p => p.Id == visitors[1].Id).ToiletCubicleId = visitors[0].ToiletCubicleId;
            Assert.That(ClinicSimulation.IsValidState(corrupted), Is.False);
            for (int i = 0; i < 600; i++) { game.Advance(.1, false); Valid(game); }
            Assert.That(visitors.All(p => p.UsedToilet), Is.True);
        }

        [Test] public void PaidPatientsFinishAllCareThroughRenovationsAndTaxisKeepTheirDockAfterReload()
        {
            var game = ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic);
            DoctorsProgressionFixture.Earn(game, 3000);
            var paid = game.State.Patients.Where(p => p.Paid && p.HasAdmissionReservation).Select(p => p.Id).ToArray();
            foreach (var room in game.State.Rooms) Assert.That(game.Renovate(room.Kind).Success, Is.True);
            var reload = new ClinicSimulation(Clone(game.State));
            game.AdvanceOffline(1800); reload.Advance(1800, false);
            Assert.That(Fingerprint(game.State), Is.EqualTo(Fingerprint(reload.State)));
            Assert.That(game.State.Patients.Any(p => paid.Contains(p.Id)), Is.False);
            Assert.That(game.State.Construction, Is.Empty); Valid(game);
        }
        [Test] public void SelectedWorkstationHireAndTrainingOnlyChangeTheChosenDoctorOrNurse()
        {
            var game = ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic);
            DoctorsProgressionFixture.Earn(game, 20000);
            foreach (var kind in new[] { ClinicRoom.Consultation, ClinicRoom.FirstAid })
                while (game.State.Room(kind).Tier < 4) { Assert.That(game.Renovate(kind).Success, Is.True); game.Advance(ClinicRules.RenovationSeconds(game.State, kind)); }
            foreach (var role in new[] { ClinicStaffRole.Doctor, ClinicStaffRole.Nurse })
            {
                while (ClinicRules.StationCount(game.State, role) < 4) { DoctorsProgressionFixture.Earn(game, ClinicRules.AddStationCost(game.State, role)); Assert.That(game.AddStation(role).Success, Is.True); }
                DoctorsProgressionFixture.Earn(game, ClinicRules.HireCost(game.State, role));
                Assert.That(game.HireStaff(role, 3).Success, Is.True);
                Assert.That(game.State.Staff.Any(s => s.Role == role && s.StationId == 3), Is.True);
                DoctorsProgressionFixture.Earn(game, ClinicRules.HireCost(game.State, role));
                Assert.That(game.HireStaff(role).Success, Is.True); // first free station, not a duplicate of selected station three
                Assert.That(game.State.Staff.Any(s => s.Role == role && s.StationId == 1), Is.True);
                int neighbour = ClinicRules.StationServiceTicks(game.State, role, 0);
                DoctorsProgressionFixture.Earn(game, ClinicRules.StationUpgradeCost(game.State, role, 3) + ClinicRules.StaffTrainingCost(game.State, game.State.Staff.Find(staff => staff.Id == ClinicRules.StaffId(role, 3))));
                Assert.That(game.UpgradeStation(role, 3).Success, Is.True);
                Assert.That(game.TrainStaff(ClinicRules.StaffId(role, 3)).Success, Is.True);
                Assert.That(ClinicRules.StationServiceTicks(game.State, role, 0), Is.EqualTo(neighbour));
                Assert.That(ClinicRules.StationServiceTicks(game.State, role, 3), Is.LessThan(neighbour));
            }
            Valid(game);
        }
        private static void Valid(ClinicSimulation g) => Assert.That(ClinicSimulation.IsValidState(g.State), Is.True, "Invalid state at " + g.State.Tick);
        internal static T Clone<T>(T value) => (T)Copy(value);
        private static object Copy(object value)
        {
            if (value == null || value is string || value.GetType().IsValueType) return value;
            if (value is IList items) { var list = (IList)Activator.CreateInstance(value.GetType()); foreach (var item in items) list.Add(Copy(item)); return list; }
            var result = Activator.CreateInstance(value.GetType()); foreach (var field in value.GetType().GetFields()) field.SetValue(result, Copy(field.GetValue(value))); return result;
        }
        internal static string Fingerprint(object value)
        {
            if (value == null) return "null"; if (value is string || value.GetType().IsValueType) return value.ToString();
            if (value is IEnumerable items) return string.Join("|", items.Cast<object>().Select(Fingerprint));
            return string.Join(";", value.GetType().GetFields().Select(f => f.Name + "=" + Fingerprint(f.GetValue(value))));
        }
    }
    public static class DoctorsProgressionFixture
    {
        private static ClinicState cachedStarter, cachedDoctors;
        public static ClinicSimulation MaxStarter()
        {
            if (cachedStarter != null) return new ClinicSimulation(ClinicDoctorsTests.Clone(cachedStarter));
            var g = ClinicSimulation.CreateNew(); g.Advance(25); g.Collect(0); g.HireNurse(); g.Advance(30);
            Earn(g, 160); Assert.That(g.BuildWaitingRoom().Success, Is.True); g.Advance(20);
            Maximise(g); Earn(g, 100000);
            cachedStarter = ClinicDoctorsTests.Clone(g.State); return g;
        }
        public static ClinicSimulation MaxDoctors()
        {
            if (cachedDoctors != null) return new ClinicSimulation(ClinicDoctorsTests.Clone(cachedDoctors));
            var g = ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic); Maximise(g);
            cachedDoctors = ClinicDoctorsTests.Clone(g.State); return g;
        }
        private static void Maximise(ClinicSimulation g)
        {
            // Improve the working bottlenecks first; the fixture earns every coin through ordinary simulation.
            foreach (var room in g.State.Rooms)
                while (room.Tier < ClinicRules.MaximumTier(g.State))
                { Earn(g, ClinicRules.RenovationCost(g.State, room.Kind)); Assert.That(g.Renovate(room.Kind).Success, Is.True); g.Advance(ClinicRules.RenovationSeconds(g.State, room.Kind)); }
            foreach (ClinicStaffRole role in Enum.GetValues(typeof(ClinicStaffRole)))
            {
                int max = ClinicRules.MaximumStaff(g.State, role);
                if (max == 0) continue;
                while (ClinicRules.StationCount(g.State, role) < max)
                {
                    if (role == ClinicStaffRole.Receptionist) { Earn(g, ClinicRules.HireCost(g.State, role)); Assert.That(g.HireStaff(role).Success, Is.True); }
                    else { Earn(g, ClinicRules.AddStationCost(g.State, role)); Assert.That(g.AddStation(role).Success, Is.True); }
                }
                while (g.State.Staff.Count(s => s.Role == role) < max) { Earn(g, ClinicRules.HireCost(g.State, role)); Assert.That(g.HireStaff(role).Success, Is.True); }
            }
            foreach (var room in g.State.Rooms)
                foreach (UpgradeTrack track in Enum.GetValues(typeof(UpgradeTrack)))
                    while (room.Level(track) < ClinicRules.ComponentCap(g.State, room.Kind)) { Earn(g, ClinicRules.UpgradeCost(g.State, room.Kind, track)); Assert.That(g.Upgrade(room.Kind, track).Success, Is.True); }
            foreach (var staff in g.State.Staff)
            {
                while (staff.TrainingLevel < ClinicRules.ComponentCap(g.State, ClinicRules.RoomForRole(staff.Role))) { Earn(g, ClinicRules.StaffTrainingCost(g.State, staff)); Assert.That(g.TrainStaff(staff.Id).Success, Is.True); }
                while (ClinicRules.StationLevel(g.State, staff.Role, staff.StationId) < ClinicRules.ComponentCap(g.State, ClinicRules.RoomForRole(staff.Role))) { Earn(g, ClinicRules.StationUpgradeCost(g.State, staff.Role, staff.StationId)); Assert.That(g.UpgradeStation(staff.Role, staff.StationId).Success, Is.True); }
            }
            foreach (var amenity in g.State.Amenities)
                while (amenity.Level < ClinicRules.MaximumAmenityLevel(g.State, amenity.Kind)) { Earn(g, ClinicRules.AmenityUpgradeCost(g.State, amenity.Kind)); Assert.That(g.UpgradeAmenity(amenity.Kind).Success, Is.True); }
            Assert.That(ClinicSimulation.IsValidState(g.State), Is.True);
        }
        public static void Earn(ClinicSimulation game, long amount)
        {
            for (int i = 0; i < 5000 && game.State.Wallet < amount; i++)
            { game.Advance(300, false); foreach (var desk in game.State.ReceptionDesks) game.Collect(desk.Id); game.CollectVendingTips(); }
            Assert.That(game.State.Wallet, Is.GreaterThanOrEqualTo(amount));
        }
    }
}
