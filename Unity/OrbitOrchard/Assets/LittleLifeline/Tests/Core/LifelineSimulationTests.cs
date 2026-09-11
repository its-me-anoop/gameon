using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace LittleLifeline.Core.Tests
{
    public sealed class LifelineSimulationTests
    {
        [Test]
        public void FirstResidentFinishesAndFundsARealChoiceWithinThirtySeconds()
        {
            var game = LifelineSimulation.CreateCampaign();
            Assert.That(game.State.Carriages.Count, Is.EqualTo(1));
            Assert.That(game.State.Crew.Count, Is.EqualTo(3));
            var report = game.Advance(30);
            Assert.That(report.Completed, Is.GreaterThan(0));
            Assert.That(game.State.Coins, Is.GreaterThanOrEqualTo(LifelineRules.BuildCost(RoomKind.Diagnostics)));
            Assert.That(game.Build(RoomKind.Diagnostics, 1).Success, Is.True);
            Assert.That(game.Assign(1, game.State.Carriages.Single(r => r.Slot == 1).Id).Success, Is.True);
        }

        [Test]
        public void FrameChunkingDoesNotChangeAnySerializedSimulationField()
        {
            var single = Equipped();
            var frames = Equipped();
            single.Advance(420.075);
            for (var i = 0; i < 25200; i++) frames.Advance(1.0 / 60);
            frames.Advance(.075);
            AssertStateEqual(single.State, frames.State);
        }

        [Test]
        public void FullDayOfflineMatchesTwentyFourHourlyAdvancesAndStaysBounded()
        {
            var whole = Equipped();
            var hours = Equipped();
            var report = whole.Advance(86400);
            for (var i = 0; i < 24; i++) hours.Advance(3600);
            AssertStateEqual(whole.State, hours.State);
            Assert.That(report.Completed, Is.GreaterThan(500));
            Assert.That(whole.State.Patients.Count, Is.LessThanOrEqualTo(LifelineRules.MaxPatients));
            Assert.That(whole.State.Coins, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void SerializedPublicFieldsAloneResumeWalkingAndTreatmentExactly()
        {
            var uninterrupted = Equipped();
            uninterrupted.Advance(37.15);
            var restored = new LifelineSimulation((SimulationState)CopyFields(uninterrupted.State));
            uninterrupted.Advance(233.85);
            restored.Advance(233.85);
            AssertStateEqual(uninterrupted.State, restored.State);
        }

        [Test]
        public void CostsAndFourSlotsPreventFreeConstructionOrOverdraft()
        {
            var game = LifelineSimulation.CreateCampaign();
            var before = Digest(game.State);
            Assert.That(game.Build(RoomKind.Diagnostics, 1).Success, Is.False);
            Assert.That(game.Build(RoomKind.Consultation, 0).Success, Is.False);
            Assert.That(game.Build(RoomKind.Consultation, 4).Success, Is.False);
            Assert.That(Digest(game.State), Is.EqualTo(before));
            game.State.Coins = 10000;
            game.State.Reputation = 100;
            for (var slot = 1; slot < 4; slot++) Assert.That(game.Build(RoomKind.Consultation, slot).Success, Is.True);
            Assert.That(game.Build(RoomKind.Consultation, 4).Success, Is.False);
            Assert.That(game.State.Carriages.Count, Is.EqualTo(4));
        }

        [Test]
        public void DiagnosticAndRecoveryVisitsCompleteEveryRequiredStage()
        {
            foreach (var path in new[] { CarePath.Diagnostics, CarePath.Recovery })
            {
                var game = Equipped();
                game.State.NextArrivalTick = 10000;
                game.State.Patients.Add(Visitor(path));
                game.Advance(9);
                Assert.That(game.State.TotalCompleted, Is.Zero);
                game.Advance(60);
                Assert.That(game.State.TotalCompleted, Is.EqualTo(1));
                Assert.That(game.State.Patients.Count, Is.Zero);
            }
        }

        [Test]
        public void CrewSpecialismChangesTreatmentTime()
        {
            var doctor = SingleVisitor(CarePath.Consultation);
            var technician = SingleVisitor(CarePath.Consultation);
            technician.State.Crew[0].Role = CrewRole.Technician;
            doctor.Advance(9);
            technician.Advance(9);
            Assert.That(doctor.State.TotalCompleted, Is.EqualTo(1));
            Assert.That(technician.State.TotalCompleted, Is.Zero);
            technician.Advance(5);
            Assert.That(technician.State.TotalCompleted, Is.EqualTo(1));
        }

        [Test]
        public void AdjacentSpecialistRoomsReduceTotalWaitingTime()
        {
            var near = Equipped();
            var far = Equipped();
            near.State.NextArrivalTick = far.State.NextArrivalTick = 10000;
            near.State.Patients.Add(Visitor(CarePath.Diagnostics));
            far.State.Patients.Add(Visitor(CarePath.Diagnostics));
            Assert.That(far.Reorder(1, 3).Success, Is.True);
            near.Advance(60);
            far.Advance(60);
            Assert.That(near.State.TotalCompleted, Is.EqualTo(far.State.TotalCompleted));
            Assert.That(near.State.TotalWaitingTicks, Is.LessThan(far.State.TotalWaitingTicks));
        }

        [Test]
        public void TransferPreviewMatchesTheResidentWalkBeforeCareStarts()
        {
            for (var from = -1; from < LifelineRules.CarriageSlots; from++)
            for (var to = 0; to < LifelineRules.CarriageSlots; to++)
            {
                var game = LifelineSimulation.CreateCampaign();
                game.State.Coins = 500;
                game.State.Reputation = 1;
                game.State.NextArrivalTick = 10000;
                Assert.That(game.Reorder(0, to).Success, Is.True);
                var visitor = Visitor(CarePath.Consultation);
                if (from >= 0)
                {
                    if (from != to) Assert.That(game.Build(RoomKind.Diagnostics, from).Success, Is.True);
                    visitor.LastRoomId = game.State.Carriages.Single(r => r.Slot == from).Id;
                    visitor.FromSlot = visitor.ToSlot = from;
                }
                game.State.Patients.Add(visitor);
                game.State.NextPatientId = visitor.Id + 1;
                // A ready crew member isolates the transfer the layout preview promises.
                game.State.Crew[0].PositionSlot = game.State.Crew[0].ToSlot = to;
                game.Advance(.1);
                var preview = LifelineRules.TransferSeconds(from, to);
                Assert.That(preview, Is.EqualTo(.6 + .8 * Math.Abs(from - to)).Within(1e-9));
                Assert.That(visitor.Phase, Is.EqualTo(PatientPhase.Walking));
                Assert.That((visitor.PhaseEndsTick - visitor.PhaseStartedTick) / (double)LifelineRules.TicksPerSecond,
                    Is.EqualTo(preview), $"Transfer from slot {from} to {to}");
            }
        }

        [Test]
        public void AssignmentChangeLetsTheCurrentPatientFinishBeforeMovingCrew()
        {
            var game = Equipped();
            game.State.NextArrivalTick = 10000;
            game.State.Patients.Add(Visitor(CarePath.Consultation));
            game.Advance(2);
            Assert.That(game.State.Crew[0].TaskPatientId, Is.EqualTo(100));
            Assert.That(game.Assign(0, 1).Success, Is.True);
            Assert.That(game.State.Crew[0].TaskPatientId, Is.EqualTo(100));
            game.Advance(30);
            Assert.That(game.State.TotalCompleted, Is.EqualTo(1));
            Assert.That(game.State.Crew[0].PrimaryRoomId, Is.EqualTo(1));
        }

        [Test]
        public void RearrangingDuringCareIsDeferredWithoutDroppingPatients()
        {
            var game = Equipped();
            game.State.NextArrivalTick = 10000;
            game.State.Patients.Add(Visitor(CarePath.Diagnostics));
            game.Advance(2);
            Assert.That(game.Reorder(0, 3).Success, Is.True);
            Assert.That(game.State.Carriages.Single(r => r.Id == 0).Slot, Is.Zero);
            Assert.That(game.State.PendingMoveRoomId, Is.EqualTo(0));
            game.Advance(80);
            Assert.That(game.State.Carriages.Single(r => r.Id == 0).Slot, Is.EqualTo(3));
            Assert.That(game.State.TotalCompleted, Is.EqualTo(1));
            Assert.That(game.State.PendingMoveRoomId, Is.EqualTo(-1));
        }

        [Test]
        public void ShortVisitPolicyAndProjectPolicyChooseDifferentResidents()
        {
            var shortFirst = Equipped();
            shortFirst.State.NextArrivalTick = 10000;
            shortFirst.State.Patients.Add(Visitor(CarePath.Diagnostics, 100));
            shortFirst.State.Patients.Add(Visitor(CarePath.Consultation, 101));
            shortFirst.State.NextPatientId = 102;
            var projectFirst = new LifelineSimulation((SimulationState)CopyFields(shortFirst.State));
            projectFirst.ChooseProject(1);
            shortFirst.SetPolicy(ServicePolicy.ShortVisitsFirst);
            projectFirst.SetPolicy(ServicePolicy.ProjectFirst);
            shortFirst.Advance(.1);
            projectFirst.Advance(.1);
            Assert.That(shortFirst.State.Crew[0].TaskPatientId, Is.EqualTo(101));
            Assert.That(projectFirst.State.Crew[0].TaskPatientId, Is.EqualTo(100));
        }

        [Test]
        public void MissingDepartmentHasABoundedVisibleQueueAndCanBeFixed()
        {
            var game = LifelineSimulation.CreateCampaign(72);
            game.Advance(86400);
            Assert.That(game.State.Patients.Count, Is.LessThanOrEqualTo(LifelineRules.MaxPatients));
            Assert.That(game.QueueFor(RoomKind.Diagnostics) + game.QueueFor(RoomKind.Recovery), Is.GreaterThan(0));
            Assert.That(game.Build(RoomKind.Diagnostics, 1).Success, Is.True);
            Assert.That(game.Build(RoomKind.Recovery, 2).Success, Is.True);
            game.Assign(1, 1);
            game.Assign(2, 2);
            Assert.That(game.Advance(120).Completed, Is.GreaterThan(0));
        }

        [Test]
        public void UnservedSpecialistsCannotLockOutBasicCareAndConstructionIncome()
        {
            var game = LifelineSimulation.CreateCampaign(72);
            game.State.Coins = 0;
            var report = game.Advance(86400);
            Assert.That(report.Completed, Is.GreaterThan(100));
            Assert.That(report.CoinsEarned, Is.GreaterThan(1000));
            Assert.That(game.State.Patients.Count, Is.LessThanOrEqualTo(LifelineRules.MaxPatients));
            Assert.That(game.QueueFor(RoomKind.Diagnostics) + game.QueueFor(RoomKind.Recovery), Is.GreaterThan(0));
        }

        [Test]
        public void EqualConstructionBudgetsFavorDifferentRoomMixesAtDifferentTowns()
        {
            var copperConsult = BudgetTrain(TownId.Copperhill, RoomKind.Consultation);
            var copperRecovery = BudgetTrain(TownId.Copperhill, RoomKind.Recovery);
            var seaConsult = BudgetTrain(TownId.Seabrook, RoomKind.Consultation);
            var seaRecovery = BudgetTrain(TownId.Seabrook, RoomKind.Recovery);
            foreach (var game in new[] { copperConsult, copperRecovery, seaConsult, seaRecovery }) game.Advance(600);
            Assert.That(copperConsult.State.TotalCompleted, Is.GreaterThan(copperRecovery.State.TotalCompleted));
            Assert.That(seaRecovery.State.TotalCompleted, Is.GreaterThan(seaConsult.State.TotalCompleted));
            Assert.That(seaRecovery.State.TotalWaitingTicks, Is.LessThan(seaConsult.State.TotalWaitingTicks));
        }

        [Test]
        public void TownTravelPreservesRoomsCrewAndOldTownCareCredit()
        {
            var game = Equipped();
            game.State.NextArrivalTick = 10000;
            game.State.Patients.Add(Visitor(CarePath.Diagnostics));
            game.Advance(2);
            Assert.That(game.Travel(TownId.Copperhill).Success, Is.True);
            game.State.NextArrivalTick = 10000;
            game.Advance(60);
            Assert.That(game.State.Towns.Single(t => t.Town == TownId.Willowbank).Completed, Is.EqualTo(1));
            Assert.That(game.State.Towns.Single(t => t.Town == TownId.Copperhill).Completed, Is.Zero);
            Assert.That(game.State.Carriages.Count, Is.EqualTo(3));
            Assert.That(game.State.Crew.Count, Is.EqualTo(3));
        }

        [Test]
        public void ProjectsRewardOnceAndRetainProgressAcrossProjectChoices()
        {
            var game = Equipped();
            game.Advance(600);
            var town = game.State.Towns.Single(t => t.Town == TownId.Willowbank);
            Assert.That(town.CompletedProjects, Is.GreaterThanOrEqualTo(1));
            var first = town.ProjectProgress;
            Assert.That(game.ChooseProject(1).Success, Is.True);
            Assert.That(game.ChooseProject(0).Success, Is.True);
            Assert.That(town.ProjectProgress, Is.EqualTo(first));
            var coin = game.State.Coins;
            game.Advance(0);
            Assert.That(game.State.Coins, Is.EqualTo(coin));
        }

        [Test]
        public void CrewHiringRequiresEarnedReputationAndStopsAtSix()
        {
            var game = LifelineSimulation.CreateCampaign();
            Assert.That(game.Hire().Success, Is.False);
            game.State.Reputation = 1000;
            game.State.Coins = 10000;
            for (var i = 0; i < 3; i++) Assert.That(game.Hire().Success, Is.True);
            Assert.That(game.Hire().Success, Is.False);
            Assert.That(game.State.Crew.Select(c => c.Name).Distinct().Count(), Is.EqualTo(6));
            Assert.That(game.State.Crew[3].Name, Is.EqualTo(LifelineRules.NextHireName(3)));
            Assert.That(game.State.Crew[3].Role, Is.EqualTo(LifelineRules.NextHireRole(3)));
        }

        [Test]
        public void RoomAndCrewTrainingCostsArePaidAndImproveFutureTreatmentsOnly()
        {
            var game = SingleVisitor(CarePath.Consultation);
            game.State.Coins = 2000;
            game.Advance(2);
            var patient = game.State.Patients.Single();
            Assert.That(patient.Phase, Is.EqualTo(PatientPhase.Treating));
            var ends = patient.PhaseEndsTick;
            var before = LifelineRules.TreatmentSeconds(game.State.Carriages[0], game.State.Crew[0]);
            var cost = LifelineRules.UpgradeCost(game.State.Carriages[0]) + LifelineRules.TrainCost(game.State.Crew[0]);
            Assert.That(game.Upgrade(0).Success, Is.True);
            Assert.That(game.Train(0).Success, Is.True);
            Assert.That(game.State.Coins, Is.EqualTo(2000 - cost));
            Assert.That(patient.PhaseEndsTick, Is.EqualTo(ends));
            Assert.That(LifelineRules.TreatmentSeconds(game.State.Carriages[0], game.State.Crew[0]), Is.LessThan(before));
            Assert.That(game.Upgrade(0).Success, Is.True);
            Assert.That(game.Train(0).Success, Is.True);
            var coins = game.State.Coins;
            Assert.That(game.Upgrade(0).Success, Is.False);
            Assert.That(game.Train(0).Success, Is.False);
            Assert.That(game.State.Coins, Is.EqualTo(coins));
        }

        [Test]
        public void BusyRoomRefitFinishesItsPatientBeforeChangingService()
        {
            var game = Equipped();
            Assert.That(game.Refit(0, RoomKind.Diagnostics).Success, Is.False);
            game.State.NextArrivalTick = 10000;
            game.State.Patients.Add(Visitor(CarePath.Diagnostics));
            game.State.NextPatientId = 101;
            game.Advance(12);
            Assert.That(game.State.Patients.Single().RoomId, Is.EqualTo(1));
            var before = game.State.Coins;
            Assert.That(game.Refit(1, RoomKind.Recovery).Success, Is.True);
            Assert.That(game.State.Carriages.Single(r => r.Id == 1).Kind, Is.EqualTo(RoomKind.Diagnostics));
            Assert.That(game.State.Coins, Is.EqualTo(before - LifelineRules.RefitCost(RoomKind.Recovery)));
            Assert.That(game.State.HasPendingRefit, Is.True);
            var queued = Visitor(CarePath.Diagnostics, 101);
            queued.Stage = 1;
            queued.LastRoomId = 0;
            queued.FromSlot = queued.ToSlot = 0;
            queued.ArrivalTick = queued.WaitingSinceTick = game.State.Tick;
            game.State.NextPatientId = 102;
            game.State.Patients.Add(queued);
            game.Advance(40);
            Assert.That(game.State.TotalCompleted, Is.EqualTo(1));
            Assert.That(game.State.Carriages.Single(r => r.Id == 1).Kind, Is.EqualTo(RoomKind.Recovery));
            Assert.That(game.State.HasPendingRefit, Is.False);
            Assert.That(queued.Phase, Is.EqualTo(PatientPhase.Waiting), "The room must not refill with another old-service patient before the refit.");
        }

        [Test]
        public void PendingRefitSurvivesSavingWithoutChargingTwice()
        {
            var game = Equipped();
            game.State.NextArrivalTick = 10000;
            game.State.Patients.Add(Visitor(CarePath.Diagnostics));
            game.State.NextPatientId = 101;
            game.Advance(12);
            var before = game.State.Coins;
            Assert.That(game.Refit(1, RoomKind.Recovery).Success, Is.True);
            var reserved = game.State.Coins;
            Assert.That(game.Refit(1, RoomKind.Recovery).Success, Is.False);
            Assert.That(game.Upgrade(1).Success, Is.False);
            Assert.That(game.State.Coins, Is.EqualTo(reserved));
            var restored = new LifelineSimulation((SimulationState)CopyFields(game.State));
            game.Advance(90);
            restored.Advance(90);
            AssertStateEqual(game.State, restored.State);
            Assert.That(restored.State.Coins, Is.EqualTo(before - LifelineRules.RefitCost(RoomKind.Recovery) + 36));
            Assert.That(restored.State.HasPendingRefit, Is.False);
        }

        [Test]
        public void UnaffordableRefitsAndRemovingTheLastClinicLeaveStateUntouched()
        {
            var game = Equipped();
            game.State.Coins = 0;
            var before = Digest(game.State);
            Assert.That(game.Refit(1, RoomKind.Recovery).Success, Is.False);
            Assert.That(game.Refit(0, RoomKind.Diagnostics).Success, Is.False);
            Assert.That(Digest(game.State), Is.EqualTo(before));
        }

        [Test]
        public void SchemaOneSavesWithoutPendingRefitDataRemainValid()
        {
            var game = LifelineSimulation.CreateCampaign();
            Assert.That(game.State.SchemaVersion, Is.EqualTo(1));
            Assert.That(game.State.HasPendingRefit, Is.False);
            Assert.That(game.State.PendingRefitRoomId, Is.Zero);
            Assert.That(game.State.PendingRefitCost, Is.Zero);
            Assert.That(LifelineSimulation.IsValidState(game.State), Is.True);
            game.State.HasPendingRefit = true;
            game.State.PendingRefitKind = RoomKind.Diagnostics;
            game.State.PendingRefitCost = LifelineRules.RefitCost(RoomKind.Diagnostics);
            Assert.That(LifelineSimulation.IsValidState(game.State), Is.False, "An invalid pending request cannot remove the only consultation room.");
        }

        [Test]
        public void DeferredCarriageMoveSurvivesAnOfflineSaveBoundary()
        {
            var game = Equipped();
            game.Advance(4);
            game.Reorder(0, 3);
            Assert.That(game.State.PendingMoveRoomId, Is.EqualTo(0));
            var restored = new LifelineSimulation((SimulationState)CopyFields(game.State));
            game.Advance(180);
            restored.Advance(180);
            AssertStateEqual(game.State, restored.State);
            Assert.That(restored.State.PendingMoveRoomId, Is.EqualTo(-1));
        }

        [Test]
        public void WeeklyStartsEqualWithinTheUTCWeekAndCannotUseCampaignProgress()
        {
            var first = LifelineSimulation.CreateWeekly(new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero));
            var replay = LifelineSimulation.CreateWeekly(new DateTimeOffset(2026, 9, 13, 23, 59, 0, TimeSpan.Zero));
            AssertStateEqual(first.State, replay.State);
            Assert.That(first.State.WeeklyId, Is.EqualTo("2026-09-07"));
            Assert.That(first.Hire().Success, Is.False);
            Assert.That(first.Travel(TownId.Seabrook).Success, Is.False);
            first.Advance(1000);
            Assert.That(first.State.Tick, Is.EqualTo(2400));
            Assert.That(first.State.IsFinished, Is.True);
            var frozen = Digest(first.State);
            first.Advance(1000);
            Assert.That(Digest(first.State), Is.EqualTo(frozen));
            Assert.That(first.Upgrade(0).Success, Is.False);
        }

        [Test]
        public void WeeklyRefitStillFinishingAtTheDeadlineRemainsValidAndFrozen()
        {
            var game = LifelineSimulation.CreateWeekly(new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero));
            game.State.NextArrivalTick = 10000;
            game.Advance(239);
            var visitor = Visitor(CarePath.Diagnostics);
            visitor.Stage = 1;
            visitor.LastRoomId = 0;
            visitor.FromSlot = visitor.ToSlot = 0;
            visitor.ArrivalTick = visitor.WaitingSinceTick = visitor.PhaseStartedTick = game.State.Tick;
            game.State.Patients.Add(visitor);
            game.State.NextPatientId = visitor.Id + 1;
            game.Advance(.1);
            Assert.That(game.Refit(1, RoomKind.Recovery).Success, Is.True);
            var coins = game.State.Coins;
            var score = game.LeaderboardScore;
            game.Advance(10);
            Assert.That(game.State.IsFinished, Is.True);
            Assert.That(game.State.HasPendingRefit, Is.True);
            Assert.That(game.State.Coins, Is.EqualTo(coins));
            Assert.That(game.LeaderboardScore, Is.EqualTo(score));
            Assert.That(LifelineSimulation.IsValidState(game.State), Is.True);
            var restored = new LifelineSimulation((SimulationState)CopyFields(game.State));
            restored.Advance(86400);
            AssertStateEqual(game.State, restored.State);
        }

        [Test]
        public void WeeklyRankingPrioritizesCompletionsBeforeWaitingTime()
        {
            var game = LifelineSimulation.CreateWeekly(DateTimeOffset.UtcNow);
            game.State.TotalCompleted = 10;
            game.State.TotalWaitingTicks = 100;
            var quick = game.LeaderboardScore;
            game.State.TotalWaitingTicks = 1000;
            Assert.That(game.LeaderboardScore, Is.LessThan(quick));
            game.State.TotalCompleted = 11;
            game.State.TotalWaitingTicks = 100000;
            Assert.That(game.LeaderboardScore, Is.GreaterThan(quick));
        }

        [Test]
        public void InvalidTimeAndCorruptStateAreRejectedWithoutReward()
        {
            var game = LifelineSimulation.CreateCampaign();
            var original = Digest(game.State);
            foreach (var seconds in new[] { -1.0, double.NaN, double.PositiveInfinity, 0 }) game.Advance(seconds);
            Assert.That(Digest(game.State), Is.EqualTo(original));
            game.State.Carriages.Add(new CarriageState { Id = 8, Slot = 0, Kind = RoomKind.Consultation, Level = 1 });
            Assert.That(LifelineSimulation.IsValidState(game.State), Is.False);
            Assert.Throws<ArgumentException>(() => new LifelineSimulation(game.State));
        }

        [Test]
        public void StaleResidentIdsAndSparseCrewIdsCannotBeLoaded()
        {
            var game = LifelineSimulation.CreateCampaign();
            game.Advance(2);
            game.State.NextPatientId = 0;
            Assert.That(LifelineSimulation.IsValidState(game.State), Is.False, "A stale counter would reuse a resident ID on the next arrival.");
            game = LifelineSimulation.CreateCampaign();
            game.State.Crew[2].Id = 5;
            Assert.That(LifelineSimulation.IsValidState(game.State), Is.False, "Hiring must not eventually reuse an existing roster ID.");
        }

        [Test]
        public void CompletedProjectFlagsMustMatchEarnedProjectProgress()
        {
            var game = LifelineSimulation.CreateCampaign();
            game.State.Towns[0].ProjectCompletionMask = 1;
            game.State.Towns[0].CompletedProjects = 1;
            Assert.That(LifelineSimulation.IsValidState(game.State), Is.False);
        }

        [Test]
        public void NoPatientOrCrewCanBeReservedByTwoRooms()
        {
            var game = Equipped();
            game.State.Coins = 10000;
            game.Hire();
            game.Assign(3, 0, 1);
            for (var i = 0; i < 2400; i++)
            {
                game.Advance(.1);
                var busy = game.State.Crew.Where(c => c.TaskPatientId >= 0).Select(c => c.TaskPatientId).ToArray();
                Assert.That(busy.Distinct().Count(), Is.EqualTo(busy.Length));
                Assert.That(LifelineSimulation.IsValidState(game.State), Is.True);
            }
        }

        private static LifelineSimulation Equipped()
        {
            var game = LifelineSimulation.CreateCampaign(42);
            game.State.Coins = 1000;
            game.State.Reputation = 100;
            game.Build(RoomKind.Diagnostics, 1);
            game.Build(RoomKind.Recovery, 2);
            game.Assign(1, 1);
            game.Assign(2, 2);
            return game;
        }

        private static LifelineSimulation BudgetTrain(TownId town, RoomKind extraRoom)
        {
            var game = LifelineSimulation.CreateCampaign(42);
            game.State.Coins = 600;
            game.State.Reputation = 100;
            game.State.Town = town;
            game.State.ArrivalCount = 3;
            Assert.That(game.Build(RoomKind.Diagnostics, 1).Success, Is.True);
            Assert.That(game.Build(RoomKind.Recovery, 2).Success, Is.True);
            Assert.That(game.Build(extraRoom, 3).Success, Is.True);
            Assert.That(game.Hire().Success, Is.True);
            game.Assign(1, 1); game.Assign(2, 2); game.Assign(3, 3);
            return game;
        }

        private static LifelineSimulation SingleVisitor(CarePath path)
        {
            var game = LifelineSimulation.CreateCampaign();
            game.State.NextArrivalTick = 10000;
            game.State.Patients.Add(Visitor(path));
            return game;
        }

        private static PatientState Visitor(CarePath path, int id = 100) => new PatientState
        {
            Id = id, Name = "Resident", Path = path, Town = TownId.Willowbank,
            Phase = PatientPhase.Waiting, ArrivalTick = 0, WaitingSinceTick = 0,
            FromSlot = -1, ToSlot = -1, RoomId = -1, CrewId = -1, LastRoomId = -1
        };

        private static void AssertStateEqual(SimulationState a, SimulationState b)
        {
            Assert.That(a.SubTick, Is.EqualTo(b.SubTick).Within(1e-7));
            var copy = (SimulationState)CopyFields(b);
            copy.SubTick = a.SubTick;
            Assert.That(Digest(copy), Is.EqualTo(Digest(a)));
        }

        private static object CopyFields(object value)
        {
            if (value == null || value is string || value.GetType().IsValueType) return value;
            if (value is IList list)
            {
                var result = (IList)Activator.CreateInstance(value.GetType());
                foreach (var item in list) result.Add(CopyFields(item));
                return result;
            }
            var clone = Activator.CreateInstance(value.GetType());
            foreach (var field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                field.SetValue(clone, CopyFields(field.GetValue(value)));
            return clone;
        }

        private static string Digest(object value)
        {
            if (value == null) return "null";
            if (value is string || value.GetType().IsValueType) return Convert.ToString(value, CultureInfo.InvariantCulture);
            if (value is IEnumerable list) return "[" + string.Join(";", list.Cast<object>().Select(Digest)) + "]";
            return "{" + string.Join(";", value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(f => f.Name).Select(f => f.Name + ":" + Digest(f.GetValue(value)))) + "}";
        }
    }
}
