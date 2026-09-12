using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleClinic.Core;
using NUnit.Framework;

namespace IdleClinic.Tests
{
    public sealed class ClinicParkingFlowTests
    {
        [Test] public void ArrivalReservesABayBeforeDrivingAndOnlyThenUnloadsItsPatient()
        {
            var game=ParkingClinic();var car=WaitFor(game,p=>p.Phase==ClinicPatientPhase.DrivingToParking);
            int id=car.Id,bay=car.ParkingBayId;long end=car.PhaseEndsTick;
            Assert.That(bay,Is.GreaterThanOrEqualTo(0));Assert.That(car.Paid,Is.False);
            Assert.That(end-car.PhaseStartedTick,Is.EqualTo(ClinicRules.ParkingEntryTicks));
            game.Advance((end-game.State.Tick-1)/10d,false);
            Assert.That(car.Phase,Is.EqualTo(ClinicPatientPhase.DrivingToParking));
            game.Advance(.1,false);
            Assert.That(car.Phase,Is.EqualTo(ClinicPatientPhase.Arriving));Assert.That(car.FromAnchor,Is.EqualTo(ClinicRules.ParkingPatientAnchor(bay)));
            Assert.That(car.PhaseEndsTick-car.PhaseStartedTick,Is.EqualTo(100));Assert.That(game.State.Patients.Single(p=>p.Id==id).ParkingBayId,Is.EqualTo(bay));
            Valid(game);
        }

        [Test] public void BayRemainsReservedAfterPatientReturnsUntilCarHasDrivenOffTheMap()
        {
            var game=ParkingClinic();var returning=WaitFor(game,p=>p.ParkingBayId>=0&&p.Phase==ClinicPatientPhase.Leaving);
            int id=returning.Id,bay=returning.ParkingBayId;long arrival=returning.PhaseEndsTick;
            game.Advance((arrival-game.State.Tick)/10d,false);
            Assert.That(game.State.Patients.Any(p=>p.Id==id&&p.ParkingBayId==bay),Is.True);
            var car=WaitFor(game,p=>p.Id==id&&p.Phase==ClinicPatientPhase.DrivingFromParking);long exit=car.PhaseEndsTick;
            Assert.That(exit-car.PhaseStartedTick,Is.EqualTo(ClinicRules.ParkingExitTicks));
            game.Advance((exit-game.State.Tick-1)/10d,false);Assert.That(game.State.Patients.Any(p=>p.Id==id&&p.ParkingBayId==bay),Is.True);
            game.Advance(.1,false);Assert.That(game.State.Patients.Any(p=>p.Id==id),Is.False);Valid(game);
        }

        [TestCase(false)][TestCase(true)] public void ParkingSerializesManeuversAndYieldsToEveryPassengerCrossing(bool fullyUpgraded)
        {
            var game=ParkingClinic();if(fullyUpgraded)MaximizeClinic(game);bool entering=false,exiting=false;long completedBefore=game.State.TotalTreatments;
            for(int tick=0;tick<12000;tick++)
            {
                game.Advance(.1,false);var moving=game.State.Patients.Where(p=>p.Phase==ClinicPatientPhase.DrivingToParking||p.Phase==ClinicPatientPhase.DrivingFromParking).ToArray();
                Assert.That(moving.Length,Is.LessThanOrEqualTo(1));
                if(moving.Length>0)
                {
                    entering|=moving[0].Phase==ClinicPatientPhase.DrivingToParking;exiting|=moving[0].Phase==ClinicPatientPhase.DrivingFromParking;
                    if(moving[0].Phase==ClinicPatientPhase.DrivingFromParking)
                    {long cycle=ClinicRules.TrafficTick(game.State)%ClinicRules.StreetCrossingCycleTicks;Assert.That(cycle<ClinicRules.StreetCrossingStartsTick||cycle>=ClinicRules.StreetCrossingEndsTick,Is.True,"Departure must yield to the street zebra crossing");}
                    Assert.That(game.State.Patients.Any(p=>p.ParkingBayId>=0&&(p.Phase==ClinicPatientPhase.Arriving||p.Phase==ClinicPatientPhase.Leaving)),Is.False);
                }
                var bays=game.State.Patients.Where(p=>p.ParkingBayId>=0).Select(p=>p.ParkingBayId).ToArray();Assert.That(bays.Distinct().Count(),Is.EqualTo(bays.Length));
                if(tick%100==0)Valid(game);
            }
            Assert.That(entering&&exiting,Is.True);Assert.That(game.State.TotalTreatments-completedBefore,Is.GreaterThan(20));Valid(game);
        }

        [TestCase(ClinicPatientPhase.DrivingToParking)][TestCase(ClinicPatientPhase.DrivingFromParking)]
        public void RelaunchDuringVehicleMotionPreservesDeadlineBayAndSubsequentAccounting(ClinicPatientPhase phase)
        {
            var original=ParkingClinic();var car=WaitFor(original,p=>p.Phase==phase);original.Advance(.7,false);
            var restored=new ClinicSimulation(Clone(original.State));
            Assert.That(restored.State.Patients.Single(p=>p.Id==car.Id).PhaseEndsTick,Is.EqualTo(car.PhaseEndsTick));
            original.Advance(600,false);restored.AdvanceOffline(600);
            Assert.That(restored.State.Tick,Is.EqualTo(original.State.Tick));Assert.That(restored.State.TotalEarned,Is.EqualTo(original.State.TotalEarned));
            Assert.That(restored.State.TotalTreatments,Is.EqualTo(original.State.TotalTreatments));
            Assert.That(restored.State.Patients.Select(p=>p.Id+":"+p.Phase+":"+p.ParkingBayId+":"+p.PhaseEndsTick),Is.EqualTo(original.State.Patients.Select(p=>p.Id+":"+p.Phase+":"+p.ParkingBayId+":"+p.PhaseEndsTick)));
            Valid(original);Valid(restored);
        }

        [TestCase(false)][TestCase(true)]
        public void ParkingFrameChunkingAndOfflineResumePreserveEverySavedField(bool fullyUpgraded)
        {
            var once=ParkingClinic();if(fullyUpgraded)MaximizeClinic(once);
            WaitFor(once,p=>p.Phase==ClinicPatientPhase.DrivingToParking);once.DrainEvents();
            var frames=new ClinicSimulation(Clone(once.State));
            var offline=new ClinicSimulation(Clone(once.State));
            var events=once.Advance(600).Events;offline.AdvanceOffline(600);
            var frameEvents=new List<ClinicEvent>();
            for(int i=0;i<6000;i++)frameEvents.AddRange(frames.Advance(.1).Events);
            Assert.That(once.State.TotalTreatments,Is.GreaterThan(0));
            Valid(once);Valid(frames);Valid(offline);
            AssertSavedValue(once.State,frames.State,"frame-stepped save");
            AssertSavedValue(once.State,offline.State,"offline save");
            AssertSavedValue(events,frameEvents,"simulation event sequence including tick and amount");
        }

        [Test] public void CappedOfflineDepartureKeepsItsTrafficReservationAndResumesAfterReload()
        {
            var initial=ParkingClinic();MaximizeClinic(initial);ClinicSimulation capped=null;
            // Choose a real operating boundary where the eight-hour allowance ends
            // while a car is departing, rather than injecting an impossible phase.
            for(int offset=0;offset<40;offset++)
            {
                capped=new ClinicSimulation(Clone(initial.State));capped.Advance(ClinicRules.MaximumOfflineSeconds,false);
                if(capped.State.Patients.Any(p=>p.Phase==ClinicPatientPhase.DrivingFromParking))break;
                initial.Advance(1,false);
            }
            var driver=capped.State.Patients.FirstOrDefault(p=>p.Phase==ClinicPatientPhase.DrivingFromParking);
            Assert.That(driver,Is.Not.Null,"Arrange an active departure at the earnings cap.");
            long skippedTicks=(130-capped.State.Tick%ClinicRules.StreetCrossingCycleTicks+ClinicRules.StreetCrossingCycleTicks)%ClinicRules.StreetCrossingCycleTicks;
            Assert.That(skippedTicks,Is.GreaterThan(0));
            var resumed=new ClinicSimulation(Clone(initial.State));
            var report=resumed.AdvanceOffline(ClinicRules.MaximumOfflineSeconds+skippedTicks/10d);
            Assert.That(report.WasCapped,Is.True);Assert.That(report.ConstructionSeconds,Is.EqualTo(report.EarningsSeconds+skippedTicks/10d));
            Assert.That(resumed.State.Tick-capped.State.Tick,Is.EqualTo(skippedTicks));
            Assert.That(resumed.State.PausedTrafficTicks,Is.EqualTo(initial.State.PausedTrafficTicks+skippedTicks));
            Assert.That(ClinicRules.TrafficTick(resumed.State),Is.EqualTo(ClinicRules.TrafficTick(capped.State)));
            Assert.That(resumed.State.TotalEarned,Is.EqualTo(capped.State.TotalEarned));
            Assert.That(resumed.State.TotalCollected,Is.EqualTo(capped.State.TotalCollected));
            Assert.That(resumed.State.Wallet,Is.EqualTo(capped.State.Wallet));
            var after=resumed.State.Patients.Single(p=>p.Id==driver.Id);
            Assert.That(after.Phase,Is.EqualTo(ClinicPatientPhase.DrivingFromParking));
            Assert.That(after.ParkingBayId,Is.EqualTo(driver.ParkingBayId));
            Assert.That(after.PhaseEndsTick-resumed.State.Tick,Is.EqualTo(driver.PhaseEndsTick-capped.State.Tick));
            var restored=new ClinicSimulation(Clone(resumed.State));
            for(int tick=0;tick<600;tick++)
            {
                capped.Advance(.1,false);resumed.Advance(.1,false);restored.Advance(.1,false);
                long cycle=ClinicRules.TrafficTick(restored.State)%ClinicRules.StreetCrossingCycleTicks;
                if(restored.State.Patients.Any(p=>p.Phase==ClinicPatientPhase.DrivingFromParking))
                    Assert.That(cycle<ClinicRules.StreetCrossingStartsTick||cycle>=ClinicRules.StreetCrossingEndsTick,Is.True);
            }
            Assert.That(restored.State.TotalEarned,Is.EqualTo(capped.State.TotalEarned));
            Assert.That(restored.State.TotalTreatments,Is.EqualTo(capped.State.TotalTreatments));
            Assert.That(ClinicRules.TrafficTick(restored.State),Is.EqualTo(ClinicRules.TrafficTick(capped.State)));
            AssertSavedValue(resumed.State,restored.State,"capped-offline reloaded save");
            Valid(capped);Valid(resumed);Valid(restored);
        }

        [Test] public void TrafficPauseOffsetDefaultsToZeroAndRejectsInvalidSavedRanges()
        {
            var state=ClinicSimulation.CreateNew().State;
            Assert.That(state.PausedTrafficTicks,Is.Zero);Assert.That(ClinicRules.TrafficTick(state),Is.Zero);
            state.PausedTrafficTicks=-1;Assert.That(ClinicSimulation.IsValidState(state),Is.False);
            state.PausedTrafficTicks=state.Tick+1;Assert.That(ClinicSimulation.IsValidState(state),Is.False);
            state.PausedTrafficTicks=0;Assert.That(ClinicSimulation.IsValidState(state),Is.True);
        }

        [Test] public void SaveValidationRejectsUnreservedDrivingAndConcurrentAisleUse()
        {
            var game=ParkingClinic();var car=WaitFor(game,p=>p.Phase==ClinicPatientPhase.DrivingToParking);
            var bad=Clone(game.State);bad.Patients.Single(p=>p.Id==car.Id).ParkingBayId=-1;Assert.That(ClinicSimulation.IsValidState(bad),Is.False);
            bad=Clone(game.State);bad.Room(ClinicRoom.Reception).FacilitiesLevel=2;
            var first=bad.Patients.Single(p=>p.Id==car.Id);long originalEnd=first.PhaseEndsTick;
            first.Phase=ClinicPatientPhase.WaitingToPark;first.PhaseEndsTick=0;
            var second=(ClinicPatientState)Copy(car);second.Id=((bad.NextPatientId+2)/3)*3;bad.NextPatientId=second.Id+1;
            second.AppearanceId=ClinicRules.PatientAppearance(bad.Seed,second.Id);second.QueueIndex=bad.Patients.Max(p=>p.QueueIndex)+1;
            second.ParkingBayId=Enumerable.Range(0,6).First(bay=>bad.Patients.All(p=>p.ParkingBayId!=bay));
            second.FromAnchor=ClinicRules.ParkingPatientAnchor(second.ParkingBayId);second.ToAnchor=ClinicRules.QueueAnchor(second.QueueIndex);bad.Patients.Add(second);
            Assert.That(ClinicSimulation.IsValidState(bad),Is.True,"One moving car plus one waiting car is an otherwise valid save");
            first.Phase=ClinicPatientPhase.DrivingToParking;first.PhaseEndsTick=originalEnd;
            Assert.That(ClinicSimulation.IsValidState(bad),Is.False,"The second concurrent maneuver alone invalidates the save");
        }

        private static void MaximizeClinic(ClinicSimulation game)
        {
            // Arrange a valid mature clinic while retaining the existing authoritative ledger.
            var state=game.State;
            foreach(var room in state.Rooms){room.Built=true;room.Tier=3;room.EquipmentLevel=6;room.FacilitiesLevel=6;room.DecorationLevel=6;}
            state.Room(ClinicRoom.Reception).StationCount=2;state.Room(ClinicRoom.FirstAid).StationCount=2;
            state.ReceptionDesks.Add(new ReceptionDeskState{Id=1,EquipmentLevel=6});state.TreatmentStations.Add(new TreatmentStationState{Id=1,EquipmentLevel=6});
            foreach(var desk in state.ReceptionDesks)desk.EquipmentLevel=6;foreach(var station in state.TreatmentStations)station.EquipmentLevel=6;
            foreach(var person in state.Staff)person.TrainingLevel=6;
            state.Staff.Add(new ClinicStaffState{Id=1,Role=ClinicStaffRole.Receptionist,StationId=1,TrainingLevel=6,FromAnchor=ClinicRules.DeskStaffAnchor(1),ToAnchor=ClinicRules.DeskStaffAnchor(1)});
            state.Staff.Add(new ClinicStaffState{Id=101,Role=ClinicStaffRole.Nurse,StationId=1,TrainingLevel=6,FromAnchor=ClinicRules.TreatmentStaffAnchor(1),ToAnchor=ClinicRules.TreatmentStaffAnchor(1)});
            Valid(game);
        }

        private static ClinicSimulation ParkingClinic()
        {
            var game=ClinicSimulation.CreateNew();game.Advance(25);game.Collect(0);game.HireNurse();game.Advance(30);
            for(int i=0;i<400&&game.State.Wallet<1600;i++){game.Advance(10,false);game.Collect(0);}
            game.UpgradeAmenity(ClinicAmenity.Parking);game.UpgradeAmenity(ClinicAmenity.Parking);game.UpgradeAmenity(ClinicAmenity.Parking);Valid(game);return game;
        }
        private static ClinicPatientState WaitFor(ClinicSimulation game,Func<ClinicPatientState,bool> predicate)
        { for(int i=0;i<12000;i++){var patient=game.State.Patients.FirstOrDefault(predicate);if(patient!=null)return patient;game.Advance(.1,false);}Assert.Fail("Parking transition not reached.");return null; }
        private static void Valid(ClinicSimulation game)=>Assert.That(ClinicSimulation.IsValidState(game.State),Is.True,"Invalid parking state at "+game.State.Tick);
        private static void AssertSavedValue(object expected,object actual,string path)
        {
            if(expected==null||expected.GetType().IsValueType||expected is string)
            {Assert.That(actual,Is.EqualTo(expected),path);return;}
            Assert.That(actual,Is.Not.Null,path);
            if(expected is IList list)
            {
                var other=(IList)actual;Assert.That(other.Count,Is.EqualTo(list.Count),path+" count");
                for(int i=0;i<list.Count;i++)AssertSavedValue(list[i],other[i],path+"["+i+"]");
                return;
            }
            foreach(var field in expected.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public))
                AssertSavedValue(field.GetValue(expected),field.GetValue(actual),path+"."+field.Name);
        }
        private static ClinicState Clone(ClinicState state)=>(ClinicState)Copy(state);
        private static object Copy(object value)
        {
            if(value==null||value.GetType().IsValueType||value is string)return value;
            if(value is IList items){var list=(IList)Activator.CreateInstance(value.GetType());foreach(var item in items)list.Add(Copy(item));return list;}
            var result=Activator.CreateInstance(value.GetType());foreach(var field in value.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public))field.SetValue(result,Copy(field.GetValue(value)));return result;
        }
    }
}
