using System.Linq;
using IdleClinic.Core;
using IdleClinic.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleClinic.Tests
{
    public sealed class ClinicParkingWorldTests
    {
        private GameObject host;private ClinicWorld world;
        [SetUp]public void SetUp(){host=new GameObject("Parking vehicle test");world=host.AddComponent<ClinicWorld>();world.Initialize();}
        [TearDown]public void TearDown()=>Object.DestroyImmediate(host);
        private Transform Find(string name)=>host.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        private ClinicState State(int bay,ClinicPatientPhase phase)
        {
            var state=ClinicSimulation.CreateNew().State;state.Patients.Clear();state.Staff.Clear();state.Amenity(ClinicAmenity.Parking).Level=3;
            for(int i=0;i<6;i++)state.Patients.Add(new ClinicPatientState{Id=901+i,ParkingBayId=i,Phase=i==bay?phase:ClinicPatientPhase.ReceptionQueue,PhaseStartedTick=0,PhaseEndsTick=i==bay?(phase==ClinicPatientPhase.DrivingToParking?ClinicRules.ParkingEntryTicks:ClinicRules.ParkingExitTicks):0});
            return state;
        }
        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)]
        public void VehiclePathsClearAllOtherOccupiedBaysAndJoinParkedPose(int bay)
        {
            var state=State(bay,ClinicPatientPhase.DrivingToParking);var car=Find("Parked patient car "+bay);
            for(int trip=0;trip<2;trip++)
            {
                var p=state.Patients[bay];p.Phase=trip==0?ClinicPatientPhase.DrivingToParking:ClinicPatientPhase.DrivingFromParking;
                p.PhaseEndsTick=trip==0?ClinicRules.ParkingEntryTicks:ClinicRules.ParkingExitTicks;
                for(int tick=0;tick<=p.PhaseEndsTick;tick++)
                {
                    state.Tick=tick;world.Render(state,.1f,true);
                    for(int other=0;other<6;other++)if(other!=bay)
                        Assert.That(Overlaps(car,Find("Parked patient car "+other)),Is.False,"Car "+bay+" clips bay "+other+" trip "+trip+" tick "+tick);
                }
                if(trip==0)
                {
                    Vector3 parked=car.position;Quaternion facing=car.rotation;p.Phase=ClinicPatientPhase.Arriving;world.Render(state,0,true);
                    Assert.That(Vector3.Distance(car.position,parked),Is.LessThan(.001f));Assert.That(Quaternion.Angle(car.rotation,facing),Is.LessThan(.1f));
                    p.Phase=ClinicPatientPhase.DrivingFromParking;p.PhaseEndsTick=ClinicRules.ParkingExitTicks;state.Tick=0;world.Render(state,0,true);
                    Assert.That(Vector3.Distance(car.position,parked),Is.LessThan(.001f));Assert.That(Quaternion.Angle(car.rotation,facing),Is.LessThan(.1f));
                }
                else Assert.That(car.position.x,Is.GreaterThan(25),"Car disappears only after reaching the road beyond the visible map");
            }
        }
        [Test]
        public void ReverseGearPauseAndWheelTravelRemainVisibleWithReducedMotion()
        {
            var state=State(0,ClinicPatientPhase.DrivingFromParking);var car=Find("Parked patient car 0");
            world.Render(state,0,true);Vector3 start=car.position;Assert.That(car.GetComponentsInChildren<Transform>().Count(t=>t.name=="Car wheel spoke"),Is.EqualTo(8));var wheel=car.GetComponentsInChildren<Transform>().First(t=>t.name=="Car hubcap");Quaternion initialWheel=wheel.localRotation;
            state.Tick=15;world.Render(state,.1f,true);Vector3 reversing=car.position;
            Assert.That(Vector3.Dot(reversing-start,car.forward),Is.LessThan(0));Assert.That(Quaternion.Angle(initialWheel,wheel.localRotation),Is.GreaterThan(1));
            state.Tick=ClinicRules.ParkingReverseTicks;world.Render(state,.1f,true);Vector3 shift=car.position;
            state.Tick+=ClinicRules.ParkingGearChangeTicks-1;world.Render(state,.1f,true);Assert.That(Vector3.Distance(shift,car.position),Is.LessThan(.001f));
            state.Tick+=10;world.Render(state,.1f,true);Assert.That(Vector3.Distance(shift,car.position),Is.GreaterThan(.1f));
        }
        [Test]
        public void EntryAndExitAreDistinctAndWaitingCarsRemainBeyondTheVisibleMap()
        {
            var state=State(0,ClinicPatientPhase.WaitingToPark);world.Render(state,0,true);
            Assert.That(Find("Parking entrance gate").position.x,Is.LessThan(Find("Parking exit gate").position.x-2));
            Assert.That(Find("Parked patient car 0").position.x,Is.LessThan(-25));
            Assert.That(Find("Parking drive aisle").GetComponent<Renderer>().bounds.size.x,Is.GreaterThanOrEqualTo(2.9f));
            Assert.That(Find("Parking exit arrow"),Is.Not.Null);
        }
        [TestCase(0)][TestCase(1)][TestCase(4)][TestCase(5)]
        public void ManeuversClearGatePedestalsIslandAndBoundaryKerbs(int bay)
        {
            var state=State(bay,ClinicPatientPhase.DrivingToParking);var car=Find("Parked patient car "+bay);
            world.Render(state,0,true);
            var obstacles=host.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Parking gate pedestal"||r.name=="Parking entrance island"||r.name=="Parking west kerb"||r.name=="Parking north kerb").ToArray();
            var collisions=new System.Collections.Generic.List<string>();var seen=new System.Collections.Generic.HashSet<string>();
            foreach(var phase in new[]{ClinicPatientPhase.DrivingToParking,ClinicPatientPhase.DrivingFromParking})
            {
                var patient=state.Patients[bay];patient.Phase=phase;patient.PhaseEndsTick=phase==ClinicPatientPhase.DrivingToParking?ClinicRules.ParkingEntryTicks:ClinicRules.ParkingExitTicks;
                for(int tick=0;tick<=patient.PhaseEndsTick;tick++)
                {
                    state.Tick=tick;world.Render(state,.1f,true);
                    foreach(var obstacle in obstacles)
                    {
                        var box=obstacle.bounds;float dx=Mathf.Abs(car.position.x-box.center.x),dz=Mathf.Abs(car.position.z-box.center.z);
                        float hx=Mathf.Abs(car.right.x)*.56f+Mathf.Abs(car.forward.x),hz=Mathf.Abs(car.right.z)*.56f+Mathf.Abs(car.forward.z);
                        string key=phase+" "+obstacle.name+" "+box.center;
                        if(dx<hx+box.extents.x&&dz<hz+box.extents.z&&seen.Add(key))collisions.Add(key+" at tick "+tick+" car "+car.position);
                    }
                }
            }
            Assert.That(collisions,Is.Empty,string.Join("; ",collisions));
        }
        [Test]
        public void AmbientWalkersLeaveTheParkingPassengerFrontWalkwayClear()
        {
            var state=State(0,ClinicPatientPhase.WaitingToPark);
            for(int tick=0;tick<800;tick+=5)
            {
                state.Tick=tick;world.Render(state,.5f);
                for(int i=1;i<6;i+=2)Assert.That(Find("Street pedestrian "+i).position.x,Is.GreaterThanOrEqualTo(2.6f));
                var crossing=Find("Street pedestrian 0").position;if(crossing.z>-7.4f)Assert.That(crossing.x,Is.GreaterThanOrEqualTo(1.38f));
                for(int i=0;i<4;i++)Assert.That(Find("Traffic car "+i).position.z,Is.EqualTo(-9.75f));
            }
        }
        [Test]
        public void ToiletDoorwayIsClosedByTheLeafOrFutureConstructionPanel()
        {
            var state=ClinicSimulation.CreateNew().State;world.Render(state,0,true);
            var closure=Find("Future toilet doorway closure");Assert.That(closure.gameObject.activeInHierarchy,Is.True);
            Assert.That(closure.GetComponent<Renderer>().bounds.max.y,Is.EqualTo(Find("Toilet doorway transom").GetComponent<Renderer>().bounds.min.y).Within(.001f));
            state.Amenity(ClinicAmenity.Toilet).Level=1;world.Render(state,0,true);Assert.That(closure.gameObject.activeInHierarchy,Is.False);
            Assert.That(Find("Toilet door panel").GetComponent<Renderer>().bounds.max.y,Is.EqualTo(Find("Toilet doorway transom").GetComponent<Renderer>().bounds.min.y).Within(.001f));
        }

        private static bool Overlaps(Transform a,Transform b)
        {
            Vector3 d=b.position-a.position;Vector3[] axes={a.right,a.forward,b.right,b.forward};
            foreach(var axis in axes)
            {
                float ra=Mathf.Abs(Vector3.Dot(a.right,axis))*.56f+Mathf.Abs(Vector3.Dot(a.forward,axis))*1.0f;
                float rb=Mathf.Abs(Vector3.Dot(b.right,axis))*.56f+Mathf.Abs(Vector3.Dot(b.forward,axis))*1.0f;
                if(Mathf.Abs(Vector3.Dot(d,axis))>=ra+rb)return false;
            }
            return true;
        }
    }
}
