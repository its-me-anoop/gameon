using System;
using System.Linq;
using System.Reflection;
using IdleClinic.Core;
using IdleClinic.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleClinic.Tests
{
    public sealed class ClinicPassingTests
    {
        private GameObject host;
        private ClinicWorld world;
        [SetUp] public void SetUp(){host=new GameObject("Clinic passing test");world=host.AddComponent<ClinicWorld>();world.Initialize();}
        [TearDown] public void TearDown()=>UnityEngine.Object.DestroyImmediate(host);

        [TestCase("care",false)][TestCase("care",true)]
        [TestCase("entrance",false)][TestCase("entrance",true)]
        [TestCase("waiting",false)][TestCase("waiting",true)]
        public void OpposingPatientsKeepBodyClearanceThroughSharedDoors(string doorway,bool reduced)
        {
            string firstFrom="reception.desk.0.patient",firstTo="firstaid.station.1.patient";
            string secondFrom="firstaid.station.0.patient",secondTo="exit";
            bool horizontal=doorway=="waiting";float crossing=doorway=="care"?-.15f:-5.13f;
            if(doorway=="entrance"){firstFrom="entrance";firstTo="reception.queue.0";}
            if(horizontal){firstTo="waiting.seat.13";secondFrom="waiting.seat.0";secondTo="firstaid.station.1.patient";crossing=1.67f;}
            var firstRoute=new RouteProbe(world,firstFrom,firstTo);var secondRoute=new RouteProbe(world,secondFrom,secondTo);
            var state=ClinicSimulation.CreateNew().State;state.Patients.Clear();state.Staff.Clear();
            state.Room(ClinicRoom.Waiting).Built=true;state.Room(ClinicRoom.Waiting).FacilitiesLevel=6;
            state.Room(ClinicRoom.FirstAid).StationCount=2;
            state.Patients.Add(Journey(901,3,firstFrom,firstTo,firstRoute.CrossingProgress(crossing,horizontal)));
            state.Patients.Add(Journey(902,9,secondFrom,secondTo,secondRoute.CrossingProgress(crossing,horizontal)));
            state.Patients[1].Phase=doorway=="waiting"?ClinicPatientPhase.WalkingToTreatment:ClinicPatientPhase.Leaving;
            int window=doorway=="entrance"?200:100;
            state.Tick=1000-window;world.Render(state,0,true);
            var first=Find("Patient 901");var second=Find("Patient 902");
            var walls=Find("Joined clinic walls").GetComponentsInChildren<Renderer>()
                .Concat(new[]{"Clinic entrance doorway","Care wing doorway","Waiting corridor doorway"}.SelectMany(n=>Find(n).GetComponentsInChildren<Renderer>())).ToArray();
            float minimum=float.MaxValue;bool before=false,after=false;var initialFoot=Foot(first);float footMovement=0;
            for(int tick=1000-window;tick<=1000+window;tick+=5)
            {
                state.Tick=tick;world.Render(state,.1f,reduced);
                float along=horizontal?first.position.x-second.position.x:first.position.z-second.position.z;
                before|=along<-.05f;after|=along>.05f;
                var delta=first.position-second.position;delta.y=0;minimum=Mathf.Min(minimum,delta.magnitude);
                Assert.That(delta.magnitude,Is.GreaterThanOrEqualTo(.65f),"Opposing bodies overlap at "+doorway+" tick "+tick+": "+first.position+" / "+second.position);
                foreach(var actor in new[]{first,second})foreach(var wall in walls)
                {
                    var bounds=wall.bounds;bounds.Expand(new Vector3(.60f,0,.60f));
                    Assert.That(bounds.Contains(actor.position+Vector3.up*.35f),Is.False,"Passing lane clips "+wall.name+" at "+actor.position);
                }
                footMovement=Mathf.Max(footMovement,Vector3.Distance(initialFoot,Foot(first)));
            }
            Assert.That(before&&after,Is.True,"The fixture must actually pass the patients, not leave them on one side.");
            Assert.That(minimum,Is.LessThan(.90f),"Both visitors must meet inside the same door aperture.");
            Assert.That(footMovement,Is.GreaterThan(.03f),"Lane separation must retain articulated travel, including reduced motion.");
        }

        [TestCase("reception.desk.0.patient","firstaid.station.1.patient")]
        [TestCase("firstaid.station.0.patient","exit")]
        [TestCase("waiting.seat.0","firstaid.station.1.patient")]
        [TestCase("reception.desk.0.patient","waiting.seat.13")]
        public void PassingRoutesKeepTheirAuthoredInteractionEndpoints(string from,string to)
        {
            var route=new RouteProbe(world,from,to);
            Assert.That(Vector3.Distance(route.Sample(0),world.GetAnchorPoint(from)),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(route.Sample(1),world.GetAnchorPoint(to)),Is.LessThan(.001f));
        }

        private static ClinicPatientState Journey(int id,int appearance,string from,string to,float crossingProgress)=>
            new ClinicPatientState{Id=id,AppearanceId=appearance,Phase=ClinicPatientPhase.WalkingToTreatment,
                FromAnchor=from,ToAnchor=to,PhaseStartedTick=0,PhaseEndsTick=(long)Math.Round(1000/crossingProgress)};
        private Transform Find(string name)=>host.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        private static Vector3 Foot(Transform actor)
        {
            var foot=actor.GetComponentInChildren<SkinnedMeshRenderer>().bones.First(b=>b.name=="foot.L");
            return actor.InverseTransformPoint(foot.position);
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
            internal float CrossingProgress(float coordinate,bool horizontal)
            {
                var previous=Sample(0);
                for(int i=1;i<=2000;i++)
                {
                    var point=Sample(i/2000f);float a=horizontal?previous.x:previous.z,b=horizontal?point.x:point.z;
                    if((a-coordinate)*(b-coordinate)<=0&&Mathf.Abs(b-a)>.000001f)
                        return (i-1+Mathf.InverseLerp(a,b,coordinate))/2000f;
                    previous=point;
                }
                Assert.Fail("Fixture route does not cross the selected door.");return 0;
            }
        }
    }
}
