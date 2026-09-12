using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using IdleClinic.Core;
using NUnit.Framework;

namespace IdleClinic.Tests
{
    public sealed class ClinicSimulationTests
    {
        [Test]
        public void TutorialPaysAtReceptionThenRequiresCollectionAndNurseBeforeCare()
        {
            var game = ClinicSimulation.CreateNew();
            Assert.That(game.NurseCount, Is.Zero);
            Assert.That(game.ReceptionistCount, Is.EqualTo(1));
            Assert.That(game.State.Wallet, Is.Zero);
            Assert.That(game.HireNurse().Success, Is.False);
            game.Advance(25);
            Assert.That(game.State.TotalPayments, Is.EqualTo(1));
            Assert.That(game.State.TotalTreatments, Is.Zero);
            Assert.That(game.State.Wallet, Is.Zero);
            Assert.That(game.TillCash, Is.EqualTo(50));
            Assert.That(game.State.Tutorial, Is.EqualTo(ClinicTutorialStep.CollectFirstPayment));
            game.Advance(3600);
            Assert.That(game.State.NextPatientId, Is.EqualTo(1));
            Assert.That(game.TillCash, Is.EqualTo(50));
            Assert.That(game.Collect(0).Amount, Is.EqualTo(50));
            Assert.That(game.State.Tutorial, Is.EqualTo(ClinicTutorialStep.HireFirstNurse));
            Assert.That(game.Upgrade(ClinicRoom.Reception, UpgradeTrack.Decoration).Success, Is.False);
            Assert.That(game.HireReceptionist().Success, Is.False);
            Assert.That(game.HireNurse().Success, Is.True);
            Assert.That(game.State.Wallet, Is.Zero);
            Assert.That(game.State.Tutorial, Is.EqualTo(ClinicTutorialStep.FirstTreatment));
            game.Advance(30);
            Assert.That(game.State.TotalTreatments, Is.EqualTo(1));
            Assert.That(game.State.Tutorial, Is.EqualTo(ClinicTutorialStep.Complete));
            Assert.That(game.State.NextArrivalTick, Is.GreaterThan(game.State.Tick));
            Valid(game);
        }

        [Test]
        public void NewNurseWalksFromEntranceAndCannotTreatBeforeArriving()
        {
            var game = ClinicSimulation.CreateNew();
            game.Advance(25); game.Collect(0); game.HireNurse();
            var nurse = game.State.Staff.Single(s => s.Role == ClinicStaffRole.Nurse);
            Assert.That(nurse.FromAnchor, Is.EqualTo("entrance"));
            Assert.That(nurse.ToAnchor, Is.EqualTo("firstaid.station.0.staff"));
            Assert.That(nurse.MoveEndsTick - nurse.MoveStartedTick, Is.EqualTo(40));
            game.Advance(3.9);
            Assert.That(nurse.PatientId, Is.EqualTo(-1));
            Assert.That(game.State.Patients[0].Phase, Is.Not.EqualTo(ClinicPatientPhase.Treating));
            game.Advance(.1);
            Assert.That(nurse.PatientId, Is.EqualTo(0));
            Assert.That(game.State.Patients[0].Phase, Is.EqualTo(ClinicPatientPhase.WalkingToTreatment));
            Valid(game);
        }

        [Test]
        public void FrameChunkingAndPublicFieldRoundTripKeepTheSameSimulation()
        {
            var once = Ready();
            var frames = new ClinicSimulation(Clone(once.State));
            once.Advance(600, false);
            for (var i = 0; i < 6000; i++) frames.Advance(.1, false);
            Assert.That(Snapshot(frames.State), Is.EqualTo(Snapshot(once.State)));
            Valid(once); Valid(frames);
        }

        [Test]
        public void SavedReceptionPaymentAndTreatmentReservationsResumeExactly()
        {
            var game = Ready();
            game.Advance(13);
            var resumed = new ClinicSimulation(Clone(game.State));
            game.Advance(117);
            resumed.Advance(117);
            Assert.That(Snapshot(resumed.State), Is.EqualTo(Snapshot(game.State)));
            Valid(resumed);
        }

        [Test]
        public void EveryCollectionMovesOnlyItsCounterCashAndEmitsOneFlight()
        {
            var game = Ready();
            Earn(game, 300); game.HireReceptionist(); game.Advance(120); game.DrainEvents();
            var first = game.State.ReceptionDesks[0].Till;
            var second = game.State.ReceptionDesks[1].Till;
            var wallet = game.State.Wallet;
            Assert.That(first, Is.GreaterThan(0));
            Assert.That(second, Is.GreaterThan(0));
            var collected = game.Collect(1);
            Assert.That(collected.Amount, Is.EqualTo(second));
            Assert.That(game.State.Wallet, Is.EqualTo(wallet + second));
            Assert.That(game.State.ReceptionDesks[0].Till, Is.EqualTo(first));
            Assert.That(game.State.ReceptionDesks[1].Till, Is.Zero);
            Assert.That(game.Collect(1).Success, Is.False);
            Assert.That(game.Collect(7).Success, Is.False);
            var events = game.DrainEvents();
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Kind, Is.EqualTo(ClinicEventKind.CashCollected));
            Assert.That(events[0].SourceAnchor, Is.EqualTo("reception.desk.1.cash"));
            Assert.That(events[0].Amount, Is.EqualTo(second));
            Assert.That(game.DrainEvents(), Is.Empty);
            Valid(game);
        }

        [Test]
        public void EventIdsStayUniqueAndAdvanceReportsDoNotReplayDrainedCommands()
        {
            var game = Ready();
            game.DrainEvents();
            var ids = new HashSet<long>();
            for (var i = 0; i < 100; i++)
            {
                foreach (var e in game.Advance(1).Events) Assert.That(ids.Add(e.Id), Is.True);
                game.Collect(0);
                foreach (var e in game.DrainEvents()) Assert.That(ids.Add(e.Id), Is.True);
            }
            Assert.That(ids.Count, Is.GreaterThan(10));
        }

        [Test]
        public void NoPaymentIsAcceptedWithoutAReservedDownstreamPlace()
        {
            var game = Ready();
            for (var i = 0; i < 600; i++)
            {
                game.Advance(1, false);
                Assert.That(game.State.Patients.Count(p => p.HasAdmissionReservation), Is.LessThanOrEqualTo(3));
                Assert.That(game.PaidWaitingCount, Is.LessThanOrEqualTo(2));
                Assert.That(game.State.Patients.Count(p => p.Phase == ClinicPatientPhase.Arriving || p.Phase == ClinicPatientPhase.ReceptionQueue), Is.LessThanOrEqualTo(6));
                Valid(game);
            }
            Assert.That(game.State.TotalPayments, Is.GreaterThan(10));
            Assert.That(game.State.TotalPayments - game.State.TotalTreatments, Is.LessThanOrEqualTo(3));
        }

        [Test]
        public void WaitingUnlockIsEarnedByPaidWaitingAndSurvivesTheQueueClearing()
        {
            var game = Ready();
            Assert.That(game.State.WaitingRoomUnlocked, Is.False);
            Assert.That(game.BuildWaitingRoom().Success, Is.False);
            for (var i = 0; i < 300 && !game.State.WaitingRoomUnlocked; i++) game.Advance(1);
            Assert.That(game.State.WaitingRoomUnlocked, Is.True);
            Assert.That(game.PaidWaitingCount, Is.GreaterThanOrEqualTo(2));
            Earn(game, 160);
            var wallet = game.State.Wallet;
            Assert.That(game.BuildWaitingRoom().Success, Is.True);
            Assert.That(game.State.Wallet, Is.EqualTo(wallet - 160));
            Assert.That(game.BuildWaitingRoom().Success, Is.False);
            Assert.That(game.State.Room(ClinicRoom.Waiting).Built, Is.False);
            game.Advance(19.9);
            Assert.That(game.State.Room(ClinicRoom.Waiting).Built, Is.False);
            game.Advance(.1);
            Assert.That(game.State.Room(ClinicRoom.Waiting).Built, Is.True);
            Assert.That(ClinicRules.WaitingCapacity(game.State), Is.EqualTo(4));
            game.Advance(60);
            Assert.That(game.State.WaitingRoomUnlocked, Is.True);
            Valid(game);
        }

        [Test]
        public void RenovationsKeepCareRunningAndUnlockCapsOnlyAtCompletion()
        {
            var game = Ready();
            Earn(game, 500);
            Assert.That(game.Upgrade(ClinicRoom.FirstAid, UpgradeTrack.Equipment).Success, Is.True);
            Assert.That(game.State.Room(ClinicRoom.FirstAid).EquipmentLevel, Is.EqualTo(2));
            Assert.That(game.Upgrade(ClinicRoom.FirstAid, UpgradeTrack.Equipment).Success, Is.False);
            var wallet = game.State.Wallet;
            var completed = game.State.TotalTreatments;
            Assert.That(game.Renovate(ClinicRoom.FirstAid).Success, Is.True);
            Assert.That(game.State.Wallet, Is.EqualTo(wallet - 250));
            Assert.That(game.Renovate(ClinicRoom.FirstAid).Success, Is.False);
            game.Advance(59.9);
            Assert.That(game.State.Room(ClinicRoom.FirstAid).Tier, Is.EqualTo(1));
            Assert.That(game.State.TotalTreatments, Is.GreaterThan(completed));
            Assert.That(game.Upgrade(ClinicRoom.FirstAid, UpgradeTrack.Equipment).Success, Is.False);
            var resumed = new ClinicSimulation(Clone(game.State));
            resumed.Advance(.1);
            Assert.That(resumed.State.Room(ClinicRoom.FirstAid).Tier, Is.EqualTo(2));
            Assert.That(resumed.State.Construction, Is.Empty);
            Assert.That(resumed.State.Wallet, Is.EqualTo(game.State.Wallet));
            Valid(resumed);
        }

        [Test]
        public void TreatmentTimeIsCapturedAtServiceStartAndPaymentAtCheckIn()
        {
            var game = Ready();
            Earn(game, 500);
            ClinicPatientState treating = null;
            for (var i = 0; i < 60 && treating == null; i++)
            { game.Advance(.5); treating = game.State.Patients.FirstOrDefault(p => p.Phase == ClinicPatientPhase.Treating); }
            Assert.That(treating, Is.Not.Null);
            var finish = treating.PhaseEndsTick;
            Assert.That(game.Upgrade(ClinicRoom.FirstAid, UpgradeTrack.Equipment).Success, Is.True);
            Assert.That(treating.PhaseEndsTick, Is.EqualTo(finish));
            ClinicPatientState checking = null;
            for (var i = 0; i < 100 && checking == null; i++)
            { game.Advance(.5); checking = game.State.Patients.FirstOrDefault(p => p.Phase == ClinicPatientPhase.CheckingIn); }
            Assert.That(checking, Is.Not.Null);
            var quote = checking.Payment;
            Assert.That(game.Upgrade(ClinicRoom.FirstAid, UpgradeTrack.Facilities).Success, Is.True);
            Assert.That(checking.Payment, Is.EqualTo(quote));
            Assert.That(ClinicRules.VisitFee(game.State), Is.GreaterThan(quote));
            game.Advance(5);
            Valid(game);
        }

        [Test]
        public void TwoNursesHaveSeparateStationsAndStaffCapsAreHard()
        {
            var game = Ready();
            Earn(game, 1000);
            Assert.That(game.AddTreatmentStation().Success, Is.False);
            Assert.That(game.HireNurse().Success, Is.False);
            game.Renovate(ClinicRoom.FirstAid); game.Advance(60); game.Collect(0);
            Assert.That(game.AddTreatmentStation().Success, Is.True);
            Assert.That(game.HireNurse().Success, Is.True);
            Earn(game, 300); Assert.That(game.HireReceptionist().Success, Is.True);
            Assert.That(game.HireNurse().Success, Is.False);
            Assert.That(game.HireReceptionist().Success, Is.False);
            Assert.That(game.AddTreatmentStation().Success, Is.False);
            var sawTwo = false;
            for (var i = 0; i < 600; i++)
            {
                game.Advance(.5, false);
                var busy = game.State.Patients.Where(p => p.TreatmentStationId >= 0).ToArray();
                sawTwo |= busy.Length == 2;
                Assert.That(busy.Select(p => p.TreatmentStationId).Distinct().Count(), Is.EqualTo(busy.Length));
                Assert.That(game.State.Staff.Where(s => s.PatientId >= 0).Select(s => s.PatientId).Distinct().Count(),
                    Is.EqualTo(game.State.Staff.Count(s => s.PatientId >= 0)));
                Valid(game);
            }
            Assert.That(sawTwo, Is.True);
        }

        [Test]
        public void ExponentialPricesTiersAndVisibleEffectsMatchLaunchRules()
        {
            var game = ClinicSimulation.CreateNew();
            var room = game.State.Room(ClinicRoom.FirstAid);
            Assert.That(ClinicRules.HireNurseCost(0), Is.EqualTo(50));
            Assert.That(ClinicRules.HireNurseCost(1), Is.EqualTo(450));
            Assert.That(ClinicRules.UpgradeCost(room, UpgradeTrack.Equipment), Is.EqualTo(80));
            room.EquipmentLevel = 2;
            Assert.That(ClinicRules.UpgradeCost(room, UpgradeTrack.Equipment), Is.EqualTo(128));
            room.EquipmentLevel = 3;
            Assert.That(ClinicRules.UpgradeCost(room, UpgradeTrack.Equipment), Is.EqualTo(205));
            Assert.That(ClinicRules.TrackCap(1), Is.EqualTo(2));
            Assert.That(ClinicRules.TrackCap(2), Is.EqualTo(4));
            Assert.That(ClinicRules.TrackCap(3), Is.EqualTo(6));
            Assert.That(ClinicRules.RenovationCost(room), Is.EqualTo(250));
            Assert.That(ClinicRules.RenovationSeconds(1), Is.EqualTo(60));
            room.Tier = 2;
            Assert.That(ClinicRules.RenovationCost(room), Is.EqualTo(625));
            Assert.That(ClinicRules.RenovationSeconds(2), Is.EqualTo(180));
            room.FacilitiesLevel = 2;
            Assert.That(ClinicRules.VisitFee(game.State), Is.EqualTo(57));
            room.DecorationLevel = 2;
            Assert.That(ClinicRules.VisitFee(game.State), Is.EqualTo(60));
        }

        [Test]
        public void EightHourOfflineEarningsStayInTheTillAndExtraTimeDoesNotPay()
        {
            var eight = Ready();
            var week = new ClinicSimulation(Clone(eight.State));
            var wallet = eight.State.Wallet;
            var a = eight.AdvanceOffline(8 * 3600);
            var b = week.AdvanceOffline(7 * 24 * 3600);
            Assert.That(a.WasCapped, Is.False);
            Assert.That(b.WasCapped, Is.True);
            Assert.That(b.EarningsSeconds, Is.EqualTo(28800));
            Assert.That(b.ConstructionSeconds, Is.EqualTo(604800));
            Assert.That(week.State.TotalEarned, Is.EqualTo(eight.State.TotalEarned));
            Assert.That(week.State.TotalTreatments, Is.EqualTo(eight.State.TotalTreatments));
            Assert.That(week.TillCash, Is.GreaterThan(1000));
            Assert.That(week.State.Wallet, Is.EqualTo(wallet));
            Assert.That(b.Events, Is.Empty);
            var aPatient = eight.State.Patients.First(p => p.PhaseEndsTick > eight.State.Tick);
            var bPatient = week.State.Patients.Single(p => p.Id == aPatient.Id);
            Assert.That(bPatient.PhaseEndsTick - week.State.Tick, Is.EqualTo(aPatient.PhaseEndsTick - eight.State.Tick));
            week.Advance(1);
            Valid(eight); Valid(week);
        }

        [Test]
        public void OfflineUsesSameOperationsAndCompletesPaidConstructionOnce()
        {
            var active = Ready();
            Earn(active, 250); active.Renovate(ClinicRoom.FirstAid);
            var offline = new ClinicSimulation(Clone(active.State));
            var spent = active.State.TotalSpent;
            active.Advance(1800, false);
            offline.AdvanceOffline(1800);
            Assert.That(Snapshot(offline.State), Is.EqualTo(Snapshot(active.State)));
            Assert.That(offline.State.Room(ClinicRoom.FirstAid).Tier, Is.EqualTo(2));
            Assert.That(offline.State.TotalSpent, Is.EqualTo(spent));
            Assert.That(offline.State.Construction, Is.Empty);
            Valid(offline);
        }

        [Test]
        public void FirstTenMinutesFundEquipmentWaitingRoomTwoNursesAndSecondReceptionist()
        {
            var game = ClinicSimulation.CreateNew();
            var stage = 0;
            var firstUpgrade = 0;
            var waitingBuilt = 0;
            var secondNurse = 0;
            var secondReception = 0;
            for (var second = 1; second <= 600; second++)
            {
                game.Advance(1, false);
                if (second % 10 == 0) foreach (var desk in game.State.ReceptionDesks.ToArray()) game.Collect(desk.Id);
                if (game.State.Tutorial == ClinicTutorialStep.HireFirstNurse) game.HireNurse();
                if (game.State.Tutorial != ClinicTutorialStep.Complete) continue;
                if (stage == 0 && game.Upgrade(ClinicRoom.FirstAid, UpgradeTrack.Equipment).Success) { firstUpgrade = second; stage++; }
                else if (stage == 1 && game.BuildWaitingRoom().Success) { waitingBuilt = second; stage++; }
                else if (stage == 2 && game.Upgrade(ClinicRoom.FirstAid, UpgradeTrack.Facilities).Success) stage++;
                else if (stage == 3 && game.Upgrade(ClinicRoom.FirstAid, UpgradeTrack.Decoration).Success) stage++;
                else if (stage == 4 && game.Renovate(ClinicRoom.FirstAid).Success) stage++;
                else if (stage == 5 && game.AddTreatmentStation().Success) stage++;
                else if (stage == 6 && game.HireNurse().Success) { secondNurse = second; stage++; }
                else if (stage == 7 && game.HireReceptionist().Success) { secondReception = second; stage++; }
                Valid(game);
            }
            TestContext.WriteLine("First upgrade " + firstUpgrade + "s; waiting room " + waitingBuilt + "s; second nurse " + secondNurse + "s; second receptionist " + secondReception + "s.");
            Assert.That(firstUpgrade, Is.InRange(60, 120));
            Assert.That(waitingBuilt, Is.InRange(120, 240));
            Assert.That(secondNurse, Is.InRange(360, 600));
            Assert.That(secondReception, Is.InRange(420, 600));
            Assert.That(stage, Is.EqualTo(8));
        }

        [Test]
        public void DecorationsFirstAndInfrequentCollectionCannotSoftLockProgress()
        {
            var game = Ready();
            Earn(game, 40); Assert.That(game.Upgrade(ClinicRoom.Reception, UpgradeTrack.Decoration).Success, Is.True);
            for (var i = 0; i < 20; i++) { game.Advance(60, false); game.Collect(0); }
            Assert.That(game.State.TotalTreatments, Is.GreaterThan(40));
            Assert.That(game.State.Wallet, Is.GreaterThan(500));
            Assert.That(game.BuildWaitingRoom().Success, Is.True);
            Valid(game);
        }

        [Test]
        public void InvalidCommandsAndInvalidTimeDoNotChangeTheState()
        {
            var game = Ready();
            var before = Snapshot(game.State);
            Assert.That(game.Collect(-1).Success, Is.False);
            Assert.That(game.Upgrade((ClinicRoom)100, UpgradeTrack.Equipment).Success, Is.False);
            Assert.That(game.Upgrade(ClinicRoom.FirstAid, (UpgradeTrack)100).Success, Is.False);
            Assert.That(game.Renovate((ClinicRoom)100).Success, Is.False);
            game.Advance(double.NaN); game.Advance(double.PositiveInfinity); game.Advance(-1);
            game.AdvanceOffline(double.NaN); game.AdvanceOffline(-50);
            Assert.That(Snapshot(game.State), Is.EqualTo(before));
        }

        [Test]
        public void CorruptedMoneyStaffReservationsAndRoomCapsAreRejected()
        {
            var game = Ready();
            var bad = Clone(game.State); bad.Wallet++;
            Assert.That(ClinicSimulation.IsValidState(bad), Is.False);
            bad = Clone(game.State); bad.Room(ClinicRoom.FirstAid).EquipmentLevel = 6;
            Assert.That(ClinicSimulation.IsValidState(bad), Is.False);
            bad = Clone(game.State); bad.Staff.Add(Clone(bad.Staff[0]));
            Assert.That(ClinicSimulation.IsValidState(bad), Is.False);
            bad = Clone(game.State); bad.Tutorial = ClinicTutorialStep.CollectFirstPayment;
            Assert.That(ClinicSimulation.IsValidState(bad), Is.False);
            bad = Clone(game.State); bad.NextArrivalTick = bad.Tick;
            Assert.That(ClinicSimulation.IsValidState(bad), Is.False);
            Assert.Throws<ArgumentException>(() => new ClinicSimulation(bad));
        }

        [Test]
        public void InvalidAnchorsUnpaidTreatmentAndStaleStaffTasksAreRejected()
        {
            var game = Ready(); game.Advance(20);
            var bad = Clone(game.State); bad.Patients[0].ToAnchor = "train.slot.0";
            Assert.That(ClinicSimulation.IsValidState(bad), Is.False);
            bad = Clone(game.State);
            var patient = bad.Patients.First(p => !p.Paid);
            patient.Phase = ClinicPatientPhase.WaitingForTreatment;
            patient.PhaseEndsTick = 0; patient.QueueIndex = -1;
            Assert.That(ClinicSimulation.IsValidState(bad), Is.False);
            bad = Clone(game.State);
            var nurse = bad.Staff.First(s => s.Role == ClinicStaffRole.Nurse);
            nurse.PatientId = bad.Patients.First(p => !p.Paid).Id;
            Assert.That(ClinicSimulation.IsValidState(bad), Is.False);
            bad = Clone(game.State);
            patient = bad.Patients.First(p => p.PhaseEndsTick > bad.Tick);
            patient.PhaseEndsTick += 100000;
            Assert.That(ClinicSimulation.IsValidState(bad), Is.False);
        }

        [Test]
        public void AllThreeRoomsReachTheirFiniteCapsWithoutLosingPatientsOrReservations()
        {
            var game = Ready();
            Earn(game, 160);
            for (var i = 0; i < 300 && !game.State.WaitingRoomUnlocked; i++) game.Advance(1);
            Assert.That(game.BuildWaitingRoom().Success, Is.True); game.Advance(20);
            foreach (ClinicRoom kind in new[] { ClinicRoom.Reception, ClinicRoom.FirstAid, ClinicRoom.Waiting })
            {
                while (game.State.Room(kind).Tier < ClinicRules.MaximumRoomTier)
                {
                    Earn(game, ClinicRules.RenovationCost(game.State.Room(kind)));
                    Assert.That(game.Renovate(kind).Success, Is.True);
                    game.Advance(180);
                }
                foreach (UpgradeTrack track in Enum.GetValues(typeof(UpgradeTrack)))
                {
                    while (game.State.Room(kind).Level(track) < 6)
                    {
                        Earn(game, ClinicRules.UpgradeCost(game.State.Room(kind), track));
                        Assert.That(game.Upgrade(kind, track).Success, Is.True);
                    }
                    Assert.That(game.Upgrade(kind, track).Success, Is.False);
                }
                Assert.That(game.Renovate(kind).Success, Is.False);
            }
            Earn(game, 1000);
            Assert.That(game.AddTreatmentStation().Success, Is.True);
            Assert.That(game.HireNurse().Success, Is.True);
            Assert.That(game.HireReceptionist().Success, Is.True);
            game.AdvanceOffline(28800);
            Assert.That(ClinicRules.WaitingCapacity(game.State), Is.EqualTo(14));
            Assert.That(ClinicRules.UnpaidQueueCapacity(game.State), Is.EqualTo(11));
            Assert.That(ClinicRules.VisitFee(game.State), Is.EqualTo(125));
            Valid(game);
        }

        private static ClinicSimulation Ready()
        {
            var game = ClinicSimulation.CreateNew();
            game.Advance(25); Assert.That(game.Collect(0).Success, Is.True); Assert.That(game.HireNurse().Success, Is.True);
            game.Advance(30); Assert.That(game.State.Tutorial, Is.EqualTo(ClinicTutorialStep.Complete));
            Valid(game);
            return game;
        }
        private static void Earn(ClinicSimulation game, long amount)
        {
            for (var i = 0; i < 2000 && game.State.Wallet < amount; i++)
            { game.Advance(10, false); foreach (var desk in game.State.ReceptionDesks) game.Collect(desk.Id); }
            Assert.That(game.State.Wallet, Is.GreaterThanOrEqualTo(amount));
            Valid(game);
        }
        private static void Valid(ClinicSimulation game) => Assert.That(ClinicSimulation.IsValidState(game.State), Is.True,
            "State invalid at " + game.ElapsedSeconds.ToString(CultureInfo.InvariantCulture) + " seconds: " + Snapshot(game.State));

        private static T Clone<T>(T value) => (T)CloneObject(value);
        private static object CloneObject(object value)
        {
            if (value == null || value.GetType().IsValueType || value is string) return value;
            if (value is IList items)
            {
                var list = (IList)Activator.CreateInstance(value.GetType());
                foreach (var item in items) list.Add(CloneObject(item));
                return list;
            }
            var result = Activator.CreateInstance(value.GetType());
            foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)) field.SetValue(result, CloneObject(field.GetValue(value)));
            return result;
        }
        private static string Snapshot(object value)
        {
            if (value == null) return "null";
            if (value is string text) return "\"" + text + "\"";
            if (value.GetType().IsValueType) return Convert.ToString(value, CultureInfo.InvariantCulture);
            if (value is IEnumerable items) return "[" + string.Join(",", items.Cast<object>().Select(Snapshot)) + "]";
            var result = new StringBuilder("{");
            foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public).OrderBy(f => f.Name))
                result.Append(field.Name).Append(':').Append(Snapshot(field.GetValue(value))).Append(';');
            return result.Append('}').ToString();
        }
    }
}
