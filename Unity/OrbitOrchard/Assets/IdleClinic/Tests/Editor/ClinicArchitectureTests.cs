using System.Linq;
using IdleClinic.Core;
using IdleClinic.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleClinic.Tests
{
    public sealed class ClinicArchitectureTests
    {
        private GameObject host;
        private ClinicWorld world;
        [SetUp] public void SetUp(){host=new GameObject("Joined architecture test");world=host.AddComponent<ClinicWorld>();world.Initialize();}
        [TearDown] public void TearDown()=>Object.DestroyImmediate(host);
        private Transform Find(string name)=>host.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);

        [Test]
        public void WallRunsHaveContinuousCornersAndOnlyAuthoredOpenings()
        {
            var walls=Find("Joined clinic walls").GetComponentsInChildren<Renderer>();
            System.Action<float,float> covered=(x,z)=>Assert.That(walls.Any(r=>r.bounds.Contains(new Vector3(x,.40f,z))),Is.True,"Unframed wall gap at "+x+", "+z);
            for(float z=-5.13f;z<=4.97f;z+=.025f){covered(-5.65f,z);covered(5.69f,z);}
            for(float x=-5.65f;x<=5.69f;x+=.025f)
            {
                if(x<-.235f||x>1.585f)covered(x,-5.13f);
                if(x<2.955f||x>3.92f)covered(x,4.97f);
                if(x<-.235f)covered(x,-.15f);
            }
            covered(-5.65f,-.15f);covered(5.69f,-1.66f);
            Assert.That(Find("Waiting corridor doorway"),Is.Not.Null);
            Assert.That(Find("Waiting refreshment doorway"),Is.Not.Null);
        }

        [TestCase("entrance","reception.queue.0")]
        [TestCase("entrance","reception.queue.4")]
        [TestCase("entrance","reception.queue.10")]
        [TestCase("reception.queue.4","reception.desk.0.patient")]
        [TestCase("reception.queue.10","reception.desk.1.patient")]
        [TestCase("reception.queue.4","reception.queue.3")]
        [TestCase("reception.desk.0.patient","waiting.seat.0")]
        [TestCase("waiting.seat.0","firstaid.station.1.patient")]
        [TestCase("waiting.seat.13","waiting.toilet.patient")]
        [TestCase("waiting.seat.0","waiting.vending.patient")]
        [TestCase("waiting.vending.patient","waiting.seat.0")]
        public void PatientRoutesUseFramedDoorsAndClearContinuousWalls(string from,string to)
        {
            var state=ClinicSimulation.CreateNew().State;state.Patients.Clear();state.Staff.Clear();
            state.Room(ClinicRoom.Waiting).Built=true;state.Room(ClinicRoom.Waiting).FacilitiesLevel=6;
            state.Room(ClinicRoom.FirstAid).StationCount=2;
            state.Amenity(ClinicAmenity.Toilet).Level=1;state.Amenity(ClinicAmenity.Vending).Level=1;
            state.Patients.Add(new ClinicPatientState{Id=901,Phase=ClinicPatientPhase.WalkingToWaiting,FromAnchor=from,ToAnchor=to,PhaseStartedTick=0,PhaseEndsTick=150});
            world.Render(state,0,true);
            var walls=Find("Joined clinic walls").GetComponentsInChildren<Renderer>()
                .Concat(new[]{"Clinic entrance doorway","Care wing doorway","Waiting corridor doorway","Waiting refreshment doorway"}.SelectMany(n=>Find(n).GetComponentsInChildren<Renderer>())).ToArray();
            for(int tick=0;tick<=150;tick++)
            {
                state.Tick=tick;world.Render(state,.1f);
                var body=Find("Patient 901").position+Vector3.up*.35f;
                foreach(var wall in walls)
                {
                    var bounds=wall.bounds;bounds.Expand(new Vector3(.58f,0,.58f));
                    Assert.That(bounds.Contains(body),Is.False,wall.name+" obstructs "+from+" -> "+to+" at tick "+tick+" / "+body);
                }
            }
        }

        [TestCase("Clinic entrance doorway")][TestCase("Care wing doorway")]
        [TestCase("Waiting corridor doorway")][TestCase("Waiting refreshment doorway")]
        public void DoorLeavesMeetTheFrameHeadWithoutAnOpenStrip(string doorway)
        {
            world.Render(ClinicSimulation.CreateNew().State,0,true);
            var root=Find(doorway);
            var seal=root.GetComponentsInChildren<Transform>().First(t=>t.name=="Door head seal").GetComponent<Renderer>().bounds;
            var leaves=root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Entrance panel frame"||t.name=="Door panel frame").Select(t=>t.GetComponent<Renderer>().bounds).ToArray();
            Assert.That(leaves.Length,Is.EqualTo(2));
            foreach(var leaf in leaves)Assert.That(Mathf.Abs(seal.min.y-leaf.max.y),Is.LessThan(.005f));
        }
    }
}
