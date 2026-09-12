using System;
using System.Reflection;
using IdleClinic.Core;
using IdleClinic.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleClinic.Tests
{
    public sealed class ClinicWalkingTests
    {
        private GameObject host;
        private ClinicWorld world;

        [SetUp] public void SetUp()
        { host=new GameObject("Clinic walking test");world=host.AddComponent<ClinicWorld>();world.Initialize(); }
        [TearDown] public void TearDown()=>UnityEngine.Object.DestroyImmediate(host);

        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)]
        [TestCase(6)][TestCase(7)][TestCase(8)][TestCase(9)][TestCase(10)][TestCase(11)]
        public void EveryVisitorVariantStepsWhileTravellingWithReducedMotion(int appearance)
        {
            var state=PatientJourney(ClinicPatientPhase.Arriving,"entrance","reception.queue.0");
            state.Patients[0].AppearanceId=appearance;
            AssertTravellingFeetMove(state,"Patient 1",true);
        }

        [TestCase(ClinicPatientPhase.Arriving,true)][TestCase(ClinicPatientPhase.Arriving,false)]
        [TestCase(ClinicPatientPhase.WalkingToReception,true)][TestCase(ClinicPatientPhase.WalkingToReception,false)]
        [TestCase(ClinicPatientPhase.WalkingToWaiting,true)][TestCase(ClinicPatientPhase.WalkingToWaiting,false)]
        [TestCase(ClinicPatientPhase.WalkingToTreatment,true)][TestCase(ClinicPatientPhase.WalkingToTreatment,false)]
        [TestCase(ClinicPatientPhase.WalkingToAmenity,true)][TestCase(ClinicPatientPhase.WalkingToAmenity,false)]
        [TestCase(ClinicPatientPhase.ReturningFromAmenity,true)][TestCase(ClinicPatientPhase.ReturningFromAmenity,false)]
        [TestCase(ClinicPatientPhase.Leaving,true)][TestCase(ClinicPatientPhase.Leaving,false)]
        public void TravelPhasesAnimateLegsIncludingCallingAndAmenityVisits(ClinicPatientPhase phase,bool reduced)
        {
            string from="entrance",to="reception.queue.0";
            if(phase==ClinicPatientPhase.WalkingToReception){from="reception.queue.3";to="reception.desk.0.patient";}
            if(phase==ClinicPatientPhase.WalkingToWaiting){from="reception.desk.0.patient";to="waiting.seat.0";}
            if(phase==ClinicPatientPhase.WalkingToTreatment){from="waiting.seat.0";to="firstaid.station.0.patient";}
            if(phase==ClinicPatientPhase.WalkingToAmenity){from="waiting.seat.0";to="waiting.toilet.patient";}
            if(phase==ClinicPatientPhase.ReturningFromAmenity){from="waiting.vending.patient";to="waiting.seat.0";}
            if(phase==ClinicPatientPhase.Leaving){from="firstaid.station.0.patient";to="exit";}
            AssertTravellingFeetMove(PatientJourney(phase,from,to),"Patient 1",reduced);
        }

        [TestCase(ClinicStaffRole.Nurse,true)][TestCase(ClinicStaffRole.Nurse,false)]
        [TestCase(ClinicStaffRole.Receptionist,true)][TestCase(ClinicStaffRole.Receptionist,false)]
        public void HiredStaffAlsoStepInsteadOfGliding(ClinicStaffRole role,bool reduced)
        {
            var state=ClinicSimulation.CreateNew().State;state.Patients.Clear();state.Staff.Clear();
            state.Staff.Add(new ClinicStaffState { Id=101,Role=role,PatientId=-1,FromAnchor="entrance",
                ToAnchor=role==ClinicStaffRole.Nurse?"firstaid.station.0.staff":"reception.desk.0.staff",MoveEndsTick=100 });
            AssertTravellingFeetMove(state,"Staff 101",reduced);
        }

        [TestCase(1,0,true)][TestCase(1,0,false)][TestCase(3,2,true)][TestCase(3,2,false)]
        [TestCase(6,5,true)][TestCase(6,5,false)]
        public void QueueAdvanceWalksBetweenSlotsWithoutAQueuePhaseChange(int from,int to,bool reduced)
        {
            var state=PatientJourney(ClinicPatientPhase.ReceptionQueue,"reception.queue."+from,"reception.queue."+from);
            state.Patients[0].PhaseEndsTick=0;world.Render(state,.1f,reduced);
            var actor=Find("Patient 1");var before=actor.position;var initialFoot=Foot(actor);
            state.Patients[0].FromAnchor=state.Patients[0].ToAnchor="reception.queue."+to;
            state.Tick=1;world.Render(state,.1f,reduced);
            Assert.That(Vector3.Distance(actor.position,before),Is.InRange(.05f,.14f),"Queue advancement must be continuous.");
            Assert.That(Vector3.Distance(actor.position,world.GetAnchorPoint(state.Patients[0].ToAnchor)),Is.GreaterThan(.1f));
            float footTravel=0;
            for(int tick=2;tick<9;tick++){state.Tick=tick;world.Render(state,.1f,reduced);footTravel=Mathf.Max(footTravel,Vector3.Distance(Foot(actor),initialFoot));}
            Assert.That(footTravel,Is.GreaterThan(.04f),"An advancing queued visitor needs articulated walking legs.");
            state.Tick=300;world.Render(state,.1f,reduced);
            Assert.That(Vector3.Distance(actor.position,world.GetAnchorPoint(state.Patients[0].ToAnchor)),Is.LessThan(.001f));
        }

        [TestCase(true)][TestCase(false)]
        public void ASecondQueueAdvanceStartsFromTheCurrentVisiblePosition(bool reduced)
        {
            var state=PatientJourney(ClinicPatientPhase.ReceptionQueue,"reception.queue.6","reception.queue.6");
            state.Patients[0].PhaseEndsTick=0;world.Render(state,.1f,reduced);
            state.Patients[0].FromAnchor=state.Patients[0].ToAnchor="reception.queue.5";
            state.Tick=1;world.Render(state,.1f,reduced);var actor=Find("Patient 1");var before=actor.position;
            state.Patients[0].FromAnchor=state.Patients[0].ToAnchor="reception.queue.4";
            state.Tick=2;world.Render(state,.1f,reduced);
            Assert.That(Vector3.Distance(actor.position,before),Is.InRange(.05f,.14f),"Retargeting cannot snap back to an authoritative slot.");
        }

        [Test]
        public void EqualGroundTravelProducesTheSameStrideAtDifferentServiceSpeeds()
        {
            var state=PatientJourney(ClinicPatientPhase.Arriving,"entrance","reception.queue.0");
            state.Patients[0].PhaseEndsTick=20;world.Render(state,.1f);
            state.Tick=4;world.Render(state,.1f);var first=Foot(Find("Patient 1"));var point=Find("Patient 1").position;
            state.Patients.Clear();world.Render(state,.1f);
            state=PatientJourney(ClinicPatientPhase.Arriving,"entrance","reception.queue.0");world.Render(state,.1f);
            state.Tick=20;world.Render(state,.1f);
            Assert.That(Vector3.Distance(Find("Patient 1").position,point),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(Foot(Find("Patient 1")),first),Is.LessThan(.002f),"Stride cadence follows distance, not elapsed seconds.");
        }

        [Test]
        public void AStationaryTravelSnapshotCannotCycleItsFeetInPlace()
        {
            var state=PatientJourney(ClinicPatientPhase.Arriving,"entrance","entrance");world.Render(state,.1f);
            var actor=Find("Patient 1");var foot=Foot(actor);
            state.Tick=4;world.Render(state,.1f);
            Assert.That(Vector3.Distance(Foot(actor),foot),Is.LessThan(.002f));
        }

        [TestCase(true)][TestCase(false)]
        public void WalkingResumesAfterAPooledRigOrCarOccupantBecomesVisible(bool pooled)
        {
            var state=PatientJourney(ClinicPatientPhase.Arriving,"entrance","reception.queue.0");
            world.Render(state,.1f);state.Tick=3;world.Render(state,.1f);var original=Find("Patient 1");
            var person=state.Patients[0];
            if(pooled){state.Patients.Clear();world.Render(state,.1f);person.Id=2;state.Patients.Add(person);}
            else {person.Phase=ClinicPatientPhase.WaitingToExit;world.Render(state,.1f);}
            Assert.That(original.gameObject.activeSelf,Is.False);
            person.Phase=ClinicPatientPhase.Arriving;person.FromAnchor="entrance";person.ToAnchor="reception.queue.0";
            person.PhaseStartedTick=3;person.PhaseEndsTick=103;world.Render(state,.1f);
            var actor=Find("Patient "+person.Id);Assert.That(actor,Is.SameAs(original));Assert.That(actor.gameObject.activeSelf,Is.True);
            var foot=Foot(actor);float moved=0;
            for(int tick=4;tick<=15;tick++){state.Tick=tick;world.Render(state,.1f);moved=Mathf.Max(moved,Vector3.Distance(foot,Foot(actor)));}
            Assert.That(moved,Is.GreaterThan(.03f),"Reactivated rigs must resume their walking animation.");
        }

        [TestCase(ClinicPatientPhase.WaitingToPark)][TestCase(ClinicPatientPhase.DrivingToParking)]
        [TestCase(ClinicPatientPhase.WaitingToExit)][TestCase(ClinicPatientPhase.DrivingFromParking)]
        public void PatientsInsideCarsAreHiddenAndStartWalkingAtTheBayDoor(ClinicPatientPhase phase)
        {
            var state=PatientJourney(phase,"parking.bay.0.patient","parking.bay.0.patient");world.Render(state,.1f);
            Assert.That(Find("Patient 1").gameObject.activeSelf,Is.False);
            var person=state.Patients[0];person.Phase=ClinicPatientPhase.Arriving;person.ToAnchor="reception.queue.0";
            world.Render(state,.1f);var actor=Find("Patient 1");
            Assert.That(actor.gameObject.activeSelf,Is.True);
            Assert.That(Vector3.Distance(actor.position,world.GetAnchorPoint(person.FromAnchor)),Is.LessThan(.001f));
            state.Tick=3;world.Render(state,.1f);
            Assert.That(Vector3.Distance(actor.position,world.GetAnchorPoint(person.FromAnchor)),Is.GreaterThan(.05f));
        }

        [TestCase("reception.queue.3","reception.desk.0.patient")]
        [TestCase("reception.queue.6","reception.desk.1.patient")]
        [TestCase("reception.queue.3","reception.queue.2")]
        [TestCase("reception.queue.2","reception.queue.3")]
        [TestCase("reception.queue.6","reception.queue.2")]
        [TestCase("reception.queue.2","reception.queue.6")]
        [TestCase("reception.queue.6","firstaid.station.0.patient")]
        public void QueueRoutesCrossTheFrontWallOnlyThroughTheEntrance(string from,string to)
        {
            var route=new RouteProbe(world,from,to);bool crossed=false;
            var previous=route.Sample(0);
            for(int i=1;i<=400;i++)
            {
                var point=route.Sample(i/400f);
                if((previous.z+5.13f)*(point.z+5.13f)<=0&&Mathf.Abs(previous.z-point.z)>.000001f)
                {
                    float t=(-5.13f-previous.z)/(point.z-previous.z);var atWall=Vector3.Lerp(previous,point,t);
                    Assert.That(atWall.x,Is.InRange(.125f,1.225f),"Allow 0.30m body clearance inside the entrance.");crossed=true;
                }
                previous=point;
            }
            bool oppositeSides=(world.GetAnchorPoint(from).z<-5.13f)!=(world.GetAnchorPoint(to).z<-5.13f);
            Assert.That(crossed,Is.EqualTo(oppositeSides),"Interior journeys stay inside; outdoor journeys use the entrance.");
        }

        [TestCase(0)][TestCase(5)]
        public void ParkingWalksUseTheCrosswalkAndOuterPedestrianLaneInBothDirections(int bay)
        {
            string anchor="parking.bay."+bay+".patient";var door=world.GetAnchorPoint(anchor);
            var required=new[]{new Vector3(door.x,door.y,door.z-.58f),new Vector3(-6.65f,door.y,door.z-.58f),
                new Vector3(-6.65f,door.y,-6.90f)};
            foreach(bool returning in new[]{false,true})
            {
                var route=new RouteProbe(world,returning?"firstaid.station.0.patient":anchor,
                    returning?anchor:"reception.queue.0");
                foreach(var waypoint in required)
                {
                    float closest=float.MaxValue;
                    for(int sample=0;sample<=1000;sample++)closest=Mathf.Min(closest,Vector3.Distance(route.Sample(sample/1000f),waypoint));
                    Assert.That(closest,Is.LessThan(.045f),"Parking pedestrian route omitted "+waypoint);
                }
            }
        }

        [TestCase(0)][TestCase(5)]
        public void ParkingPassengersClearTheFinalOutdoorQueueRowOnArrivalAndDeparture(int bay)
        {
            string parking="parking.bay."+bay+".patient";
            foreach(bool returning in new[]{false,true})
            {
                var route=new RouteProbe(world,returning?"firstaid.station.0.patient":parking,
                    returning?parking:"reception.queue.0");
                for(int sample=0;sample<=1000;sample++)
                {
                    var point=route.Sample(sample/1000f);
                    for(int queue=8;queue<=10;queue++)
                    {
                        var separation=point-world.GetAnchorPoint("reception.queue."+queue);separation.y=0;
                        Assert.That(separation.magnitude,Is.GreaterThanOrEqualTo(.60f),
                            "Allow both the walking passenger and queued visitor a 0.30m body radius.");
                    }
                }
            }
        }

        private ClinicState PatientJourney(ClinicPatientPhase phase,string from,string to)
        {
            var state=ClinicSimulation.CreateNew().State;state.Patients.Clear();state.Staff.Clear();
            state.Room(ClinicRoom.Waiting).Built=true;state.Amenity(ClinicAmenity.Toilet).Level=1;state.Amenity(ClinicAmenity.Vending).Level=1;
            state.Patients.Add(new ClinicPatientState { Id=1,Phase=phase,FromAnchor=from,ToAnchor=to,PhaseEndsTick=100 });return state;
        }

        private void AssertTravellingFeetMove(ClinicState state,string name,bool reduced)
        {
            world.Render(state,.1f,reduced);var actor=Find(name);var origin=actor.position;var initial=Foot(actor);float footTravel=0;
            for(int tick=1;tick<=12;tick++)
            { state.Tick=tick;world.Render(state,.1f,reduced);footTravel=Mathf.Max(footTravel,Vector3.Distance(Foot(actor),initial)); }
            Assert.That(Vector3.Distance(actor.position,origin),Is.GreaterThan(.05f));
            Assert.That(footTravel,Is.GreaterThan(.03f),name+" moves through the scene but its feet are frozen.");
        }

        private Transform Find(string name)=>Array.Find(host.GetComponentsInChildren<Transform>(true),t=>t.name==name);
        private static Vector3 Foot(Transform actor)
        {
            var bones=actor.GetComponentInChildren<SkinnedMeshRenderer>(true).bones;
            var foot=Array.Find(bones,b=>b.name=="foot.L");Assert.That(foot,Is.Not.Null);return actor.InverseTransformPoint(foot.position);
        }

        private sealed class RouteProbe
        {
            private readonly object route;
            private readonly MethodInfo sample;
            internal RouteProbe(ClinicWorld world,string from,string to)
            {
                var type=typeof(ClinicWorld).Assembly.GetType("IdleClinic.Presentation.ClinicRoute",true);
                route=Activator.CreateInstance(type,true);
                type.GetMethod("Build",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(route,new object[]{world,from,to,false,null});
                sample=type.GetMethod("Sample",BindingFlags.Instance|BindingFlags.NonPublic);
            }
            internal Vector3 Sample(float progress)=>(Vector3)sample.Invoke(route,new object[]{progress,Vector3.zero});
        }
    }
}
