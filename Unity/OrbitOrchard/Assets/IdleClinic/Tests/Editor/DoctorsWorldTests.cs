using System;
using System.Linq;
using System.Reflection;
using IdleClinic.Core;
using IdleClinic.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleClinic.Tests
{
    public sealed class DoctorsWorldTests
    {
        private GameObject host;private ClinicWorld world;private ClinicState state;
        [SetUp] public void SetUp()
        {host=new GameObject("Doctors world verification");world=host.AddComponent<ClinicWorld>();world.Initialize(ClinicLocation.DoctorsClinic);state=ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic).State;}
        [TearDown] public void TearDown()=>UnityEngine.Object.DestroyImmediate(host);
        [TestCase(ClinicRoom.Reception,5.35f*4.70f)]
        [TestCase(ClinicRoom.FirstAid,5.35f*4.70f)]
        [TestCase(ClinicRoom.Waiting,4.35f*6.55f)]
        public void CorrespondingRoomsHaveExactlyTwiceFloorArea(ClinicRoom room,float starterArea)
        {var floor=Find(room+" room footprint").Find("Doctors room floor");Assert.That(floor.localScale.x*floor.localScale.z,Is.EqualTo(starterArea*2).Within(.001f));}
        [Test] public void NormalScaleWorkstationsHaveFourDistinctConsultingRoomsAndFourNursingStations()
        {
            foreach(var role in new[]{ClinicStaffRole.Receptionist,ClinicStaffRole.Nurse,ClinicStaffRole.Doctor,ClinicStaffRole.Pharmacist})
            {
                int max=role==ClinicStaffRole.Pharmacist?2:4;
                for(int i=0;i<max;i++)
                {var a=world.GetAnchorPoint(ClinicRules.StationPatientAnchor(role,i));var b=world.GetAnchorPoint(ClinicRules.StationStaffAnchor(role,i));Assert.That(Vector3.Distance(a,b),Is.GreaterThan(.65f));
                    for(int j=0;j<i;j++)Assert.That(Vector3.Distance(a,world.GetAnchorPoint(ClinicRules.StationPatientAnchor(role,j))),Is.GreaterThan(1.5f));}
            }
            for(int i=1;i<=4;i++)Assert.That(Find("Consultation "+i+" doorway"),Is.Not.Null);
            foreach(var t in host.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="ReceptionDesk"||t.name=="TreatmentBay"))Assert.That(t.localScale,Is.EqualTo(Vector3.one));
        }
        [Test] public void AllExpandedReservationsHaveDistinctRealAnchors()
        {
            foreach(var pair in new[]{("reception.queue.",23),("waiting.seat.",30),("parking.bay.",12),("taxi.dock.",2),("waiting.toilet.",2)})
                for(int i=0;i<pair.Item2;i++)
                {string suffix=pair.Item1=="parking.bay."||pair.Item1=="taxi.dock."||pair.Item1=="waiting.toilet."?".patient":"";var a=world.GetAnchorPoint(pair.Item1+i+suffix);for(int j=0;j<i;j++)Assert.That(Vector3.Distance(a,world.GetAnchorPoint(pair.Item1+j+suffix)),Is.GreaterThan(.65f));}
            Assert.Throws<ArgumentException>(()=>world.GetAnchorPoint("consultation.station.8.patient"));
        }
        [Test] public void EveryMovementAnchorMatchesTheAuthoritativeCoreLayout()
        {
            var names=new System.Collections.Generic.List<string>{"entrance","exit","waiting.vending.patient"};
            foreach(var role in new[]{ClinicStaffRole.Receptionist,ClinicStaffRole.Nurse,ClinicStaffRole.Doctor,ClinicStaffRole.Pharmacist})for(int i=0;i<(role==ClinicStaffRole.Pharmacist?2:4);i++){names.Add(ClinicRules.StationPatientAnchor(role,i));names.Add(ClinicRules.StationStaffAnchor(role,i));}
            for(int i=0;i<30;i++)names.Add("waiting.seat."+i);for(int i=0;i<23;i++)names.Add("reception.queue."+i);for(int i=0;i<12;i++)names.Add("parking.bay."+i+".patient");for(int i=0;i<2;i++){names.Add("waiting.toilet."+i+".patient");names.Add("taxi.dock."+i+".patient");}
            foreach(string name in names){var a=ClinicDoctorsNavigation.Anchor(name);var p=world.GetAnchorPoint(name);Assert.That(p.x,Is.EqualTo(a.x).Within(.002f),name+" x");Assert.That(p.z,Is.EqualTo(a.z).Within(.002f),name+" z");}
        }
        [Test] public void LocationSwitchPreservesTextureAndDropsPreviousSceneActors()
        {
            var texture=world.Texture;world.Render(state,0);Assert.That(Find("Staff 200"),Is.Not.Null);
            world.ConfigureLocation(ClinicLocation.StarterClinic);Assert.That(world.Texture,Is.SameAs(texture));Assert.That(host.GetComponentsInChildren<Transform>().Any(t=>t.name=="Staff 200"),Is.False);
            world.ConfigureLocation(ClinicLocation.DoctorsClinic);world.Render(state,0);Assert.That(world.Texture,Is.SameAs(texture));Assert.That(host.GetComponentsInChildren<Transform>().Count(t=>t.name=="Staff 200"),Is.EqualTo(1));
        }
        [Test] public void UpgradingEveryTrackAndStationShowsTwelfthLevelAndKeepsSockets()
        {
            var point=world.GetAnchorPoint("consultation.station.0.patient");world.Render(state,0);
            foreach(var r in state.Rooms){r.Built=true;r.Tier=6;r.EquipmentLevel=r.FacilitiesLevel=r.DecorationLevel=12;}
            foreach(var s in state.ConsultationStations)s.EquipmentLevel=12;
            foreach(var a in state.Amenities)a.Level=6;
            world.Render(state,0);
            Assert.That(Find("Consultation Equipment level 12").gameObject.activeSelf,Is.True);Assert.That(Find("Doctor station 0 equipment 12").gameObject.activeSelf,Is.True);
            Assert.That(Find("Taxi stand tier 6").gameObject.activeSelf,Is.True);Assert.That(world.GetAnchorPoint("consultation.station.0.patient"),Is.EqualTo(point));
            Assert.That(host.GetComponentsInChildren<Transform>().Count(t=>t.name=="Seat"),Is.GreaterThanOrEqualTo(30));
        }
        [TestCase("reception.desk.0.patient","consultation.station.3.patient",false)]
        [TestCase("consultation.station.3.patient","firstaid.station.0.patient",false)]
        [TestCase("firstaid.station.3.patient","pharmacy.station.1.patient",false)]
        [TestCase("pharmacy.station.1.patient","taxi.dock.1.patient",false)]
        [TestCase("entrance","consultation.station.0.staff",true)]
        [TestCase("entrance","pharmacy.station.1.staff",true)]
        [TestCase("waiting.seat.29","waiting.toilet.1.patient",false)]
        [TestCase("parking.bay.11.patient","reception.queue.0",false)]
        [TestCase("reception.queue.0","reception.desk.0.patient",false)]
        [TestCase("reception.queue.8","reception.desk.3.patient",false)]
        [TestCase("reception.queue.16","reception.desk.1.patient",false)]
        public void ServiceRoutesUseRealDoorwaysAndKeepTheirEndpoints(string from,string to,bool staff)
        {
            var route=new Route(world,from,to,staff);Assert.That(route.Length,Is.EqualTo(ClinicDoctorsNavigation.PathLength(from,to,staff)).Within(.002f));Assert.That(Vector3.Distance(route.Point(0),world.GetAnchorPoint(from)),Is.LessThan(.001f));Assert.That(Vector3.Distance(route.Point(1),world.GetAnchorPoint(to)),Is.LessThan(.001f));
            // Static wall bodies and jambs are authoritative geometry. Sliding leaves are deliberately excluded.
            var walls=host.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.Contains("partition")||r.name.Contains("wall")||r.name.Contains("jamb")||r.name.StartsWith("Doctors entrance west")||r.name.StartsWith("Doctors entrance east")).Where(r=>!r.name.EndsWith(" cap")&&!r.name.EndsWith(" skirting")).ToArray();
            for(int step=0;step<=400;step++)
            {var p=route.Point(step/400f)+Vector3.up*.4f;foreach(var wall in walls){var b=wall.bounds;b.Expand(new Vector3(.46f,0,.46f));Assert.That(b.Contains(p),Is.False,from+" → "+to+" crosses "+wall.name+" at "+p);}}
        }
        [TestCase(false)][TestCase(true)]
        public void ConsultationAndPharmacyJourneysKeepArticulatedWalking(bool reduced)
        {
            state.Patients.Clear();state.Staff.Clear();state.Patients.Add(new ClinicPatientState{Id=900,AppearanceId=3,Phase=ClinicPatientPhase.WalkingToConsultation,FromAnchor="reception.desk.0.patient",ToAnchor="consultation.station.3.patient",PhaseStartedTick=0,PhaseEndsTick=240});
            world.Render(state,0,reduced);var actor=Find("Patient 900");var bone=actor.GetComponentInChildren<SkinnedMeshRenderer>().bones.First(b=>b.name=="foot.L");var first=actor.InverseTransformPoint(bone.position);float articulated=0;
            for(int t=2;t<240;t+=3){state.Tick=t;world.Render(state,.3f,reduced);articulated=Mathf.Max(articulated,Vector3.Distance(first,actor.InverseTransformPoint(bone.position)));}
            Assert.That(articulated,Is.GreaterThan(.03f));Assert.That(world.MovingActorCount,Is.EqualTo(1));
        }
        [TestCase(ClinicPatientPhase.WalkingToPharmacy)][TestCase(ClinicPatientPhase.WalkingToTaxi)]
        public void CompletedFirstAidDressingRemainsVisibleThroughPharmacyAndTaxiDeparture(ClinicPatientPhase phase)
        {
            state.Staff.Clear();state.Patients.Clear();state.Patients.Add(new ClinicPatientState{Id=905,FirstAidComplete=true,Phase=phase,FromAnchor=phase==ClinicPatientPhase.WalkingToPharmacy?"firstaid.station.0.patient":"pharmacy.station.0.patient",ToAnchor=phase==ClinicPatientPhase.WalkingToPharmacy?"pharmacy.station.0.patient":"taxi.dock.0.patient",PhaseEndsTick=200});world.Render(state,0);
            Assert.That(Find("Patient 905").GetComponentsInChildren<Transform>().Any(t=>t.name=="Forehead dressing"),Is.True);
        }
        [Test] public void TwelveBayCarParkAndTaxiFleetRenderDurableOwners()
        {
            state.Amenity(ClinicAmenity.Parking).Level=6;state.Amenity(ClinicAmenity.Taxi).Level=6;state.Patients.Clear();state.Staff.Clear();
            for(int i=0;i<12;i++)state.Patients.Add(new ClinicPatientState{Id=900+i,ParkingBayId=i,Phase=ClinicPatientPhase.ReceptionQueue,ToAnchor="reception.queue."+i});
            state.TaxiRides.Add(new ClinicTaxiState{Id=222,PatientId=111,DockId=1,Phase=ClinicTaxiPhase.Approaching,PhaseStartedTick=0,PhaseEndsTick=160});world.Render(state,0);
            Assert.That(host.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Parked patient car ")),Is.EqualTo(12));var taxi=Find("Patient taxi 1");var start=taxi.position;
            state.Tick=80;world.Render(state,.1f);Assert.That(Vector3.Distance(taxi.position,start),Is.GreaterThan(10));state.Tick=160;world.Render(state,.1f);Assert.That(Vector3.Distance(taxi.position,new Vector3(21.6f,-.11f,-10.80f)),Is.LessThan(.01f));
            state.TaxiRides[0].Phase=ClinicTaxiPhase.Departing;state.TaxiRides[0].PhaseStartedTick=160;state.TaxiRides[0].PhaseEndsTick=320;state.Tick=240;world.Render(state,.1f);Assert.That(taxi.position.x,Is.GreaterThan(15));
        }
        [TestCase("reception")][TestCase("firstaid")][TestCase("waiting")][TestCase("consultation")][TestCase("pharmacy")][TestCase("entrance")]
        public void OpposingRoutesUseSeparatedLanesThroughSharedDoor(string door)
        {
            string a="entrance",b="reception.desk.0.patient",c="reception.desk.1.patient",d="consultation.station.3.patient";float crossing=-1.5f;bool z=false;
            if(door=="firstaid"){a="reception.desk.0.patient";b="firstaid.station.3.patient";c="firstaid.station.0.patient";d="pharmacy.station.0.patient";}
            if(door=="waiting"){a="reception.desk.0.patient";b="waiting.seat.29";c="waiting.seat.0";d="consultation.station.3.patient";crossing=1.5f;}
            if(door=="consultation"){a="reception.desk.0.patient";b="consultation.station.3.patient";c="consultation.station.3.patient";d="firstaid.station.0.patient";crossing=4.6f;z=true;}
            if(door=="pharmacy"){a="firstaid.station.0.patient";b="pharmacy.station.1.patient";c="pharmacy.station.0.patient";d="taxi.dock.1.patient";crossing=1.5f;}
            if(door=="entrance"){a="entrance";b="reception.queue.0";c="pharmacy.station.0.patient";d="taxi.dock.0.patient";crossing=-10.3f;z=true;}
            var first=new Route(world,a,b,false);var second=new Route(world,c,d,false);float af=first.Cross(crossing,z),bf=second.Cross(crossing,z);
            Assert.That(Vector3.Distance(first.Point(af),second.Point(bf)),Is.GreaterThanOrEqualTo(.65f),door+" must have two physical visitor lanes.");
        }
        [Test] public void FullReceptionQueueClearsWallsAndOutsidePassengerPath()
        {
            var walls=host.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.EndsWith(" wall")).ToArray();
            for(int i=0;i<23;i++){var p=world.GetAnchorPoint("reception.queue."+i);foreach(var wall in walls){var b=wall.bounds;b.Expand(new Vector3(.60f,0,.60f));Assert.That(b.Contains(p+Vector3.up*.4f),Is.False,"queue "+i+" clips "+wall.name);}Assert.That(Mathf.Abs(p.z+10.90f),Is.GreaterThan(.65f));}
        }
        [Test] public void FullQueueAdvancesLocallyWithoutCrowdingTheEntrance()
        {
            state.Staff.Clear();state.Patients.Clear();
            for(int i=0;i<23;i++)state.Patients.Add(new ClinicPatientState{Id=930+i,Phase=ClinicPatientPhase.ReceptionQueue,FromAnchor="reception.queue."+i,ToAnchor="reception.queue."+i});
            world.Render(state,0);var previous=state.Patients.Select(p=>Find("Patient "+p.Id).position).ToArray();
            state.Patients.RemoveAt(0);
            for(int i=0;i<state.Patients.Count;i++)
            {
                var person=state.Patients[i];person.ToAnchor="reception.queue."+i;
                person.QueueMovePath=ClinicDoctorsNavigation.ArrivalPath(person.FromAnchor,person.ToAnchor);
                person.QueueMoveStartedTick=0;
                person.QueueMoveEndsTick=ClinicDoctorsNavigation.QueueMoveTicks(person.QueueMovePath);
            }
            var actors=state.Patients.Select(p=>Find("Patient "+p.Id)).ToArray();
            var destinations=state.Patients.Select(p=>world.GetAnchorPoint(p.ToAnchor)).ToArray();
            for(int tick=1;tick<=25;tick++)
            {
                state.Tick=tick;world.Render(state,.1f);
                for(int i=0;i<actors.Length;i++)
                {
                    var p=actors[i].position;var before=previous[i+1];var after=destinations[i];
                    Assert.That(p.x,Is.InRange(Mathf.Min(before.x,after.x)-.701f,Mathf.Max(before.x,after.x)+.701f),"Queue movement must stay by its old and new slots, not visit the entrance.");
                    Assert.That(p.z,Is.InRange(Mathf.Min(before.z,after.z)-.001f,Mathf.Max(before.z,after.z)+.001f));
                    for(int j=0;j<i;j++)Assert.That(Vector3.Distance(p,actors[j].position),Is.GreaterThan(.54f),"Queue patients overlap during advancement: "+i+" and "+j);
                }
            }
            for(int i=0;i<actors.Length;i++)Assert.That(Vector3.Distance(actors[i].position,destinations[i]),Is.LessThan(.002f));
        }
        [TestCase(false)][TestCase(true)]
        public void SavedQueueTravelRestoresAtTheSamePositionAcrossFrameCadences(bool reduced)
        {
            state.Staff.Clear();state.Patients.Clear();
            var person=new ClinicPatientState{Id=975,Phase=ClinicPatientPhase.ReceptionQueue,FromAnchor="reception.queue.8",ToAnchor="reception.queue.7",QueueMoveStartedTick=10,QueueMoveEndsTick=26};
            person.QueueMovePath=ClinicDoctorsNavigation.ArrivalPath(person.FromAnchor,person.ToAnchor);state.Patients.Add(person);
            state.Tick=10;world.Render(state,0,reduced);
            for(int tick=11;tick<=17;tick++){state.Tick=tick;world.Render(state,.1f,reduced);}
            state.SubTick=.5;world.Render(state,.05f,reduced);var before=Find("Patient 975").position;
            world.ConfigureLocation(ClinicLocation.StarterClinic);world.ConfigureLocation(ClinicLocation.DoctorsClinic);
            world.Render(state,0,reduced);
            Assert.That(Vector3.Distance(Find("Patient 975").position,before),Is.LessThan(.001f),"Restored queue movement must sample the saved clock, not restart at a logical slot.");
            Assert.That(world.MovingActorCount,Is.EqualTo(1));
            state.Tick=26;state.SubTick=0;world.Render(state,.85f,reduced);
            Assert.That(Vector3.Distance(Find("Patient 975").position,world.GetAnchorPoint(person.ToAnchor)),Is.LessThan(.001f));
            Assert.That(world.MovingActorCount,Is.Zero);
        }
        [TestCase(8)][TestCase(16)]
        public void QueueRowTurnsClearActualVestibuleWalls(int index)
        {
            var route=new Route(world,"reception.queue."+index,"reception.queue."+(index-1),false);
            var walls=host.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.EndsWith(" wall")||r.name.Contains("jamb")).ToArray();
            Assert.That(route.Length,Is.LessThanOrEqualTo(2.11f),"A row turn must remain local to the queue.");
            for(int step=0;step<=100;step++)
            {
                var p=route.Point(step/100f)+Vector3.up*.4f;
                foreach(var wall in walls){var b=wall.bounds;b.Expand(new Vector3(.70f,0,.70f));Assert.That(b.Contains(p),Is.False,"Queue turn intersects "+wall.name+" with .35m body clearance at "+p);}
            }
        }
        [TestCase(9)][TestCase(15)][TestCase(22)]
        public void ArrivalsReachTheQueueTailWithoutCrossingEarlierPatientsOrWalls(int index)
        {
            var path=ClinicDoctorsNavigation.ArrivalPath("entrance","reception.queue."+index);
            AssertQueueArrivalClear(path,index);
        }
        [TestCase(15,7)][TestCase(22,15)]
        public void ReindexedArrivalKeepsItsPositionAndAQueueApproachClearOfWalls(int fromIndex,int toIndex)
        {
            var original=ClinicDoctorsNavigation.ArrivalPath("entrance","reception.queue."+fromIndex);
            foreach(double progress in new[]{.35,.70,.90})
            {
                var before=ClinicDoctorsNavigation.SampleArrivalPath(original,progress);
                var remaining=ClinicDoctorsNavigation.RetargetArrivalPath(original,progress,"reception.queue."+toIndex);
                Assert.That(remaining[0].X,Is.EqualTo(before.X).Within(.0001));Assert.That(remaining[0].Z,Is.EqualTo(before.Z).Within(.0001));
                AssertQueueArrivalClear(remaining,toIndex);
            }
        }
        private void AssertQueueArrivalClear(System.Collections.Generic.IList<ClinicMovementPoint> path,int index)
        {
            var walls=host.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.EndsWith(" wall")||r.name.Contains("jamb")).ToArray();
            var queued=Enumerable.Range(0,index).Select(i=>world.GetAnchorPoint("reception.queue."+i)).ToArray();
            for(int step=0;step<=500;step++)
            {
                var sample=ClinicDoctorsNavigation.SampleArrivalPath(path,step/500d);var p=new Vector3(sample.X,.14f,sample.Z);
                for(int i=0;i<queued.Length;i++)Assert.That(Vector3.Distance(p,queued[i]),Is.GreaterThan(.54f),"Arrival to slot "+index+" crosses waiting patient "+i+" at "+p);
                foreach(var wall in walls){var b=wall.bounds;b.Expand(new Vector3(.60f,0,.60f));Assert.That(b.Contains(p+Vector3.up*.4f),Is.False,"Arrival to slot "+index+" intersects "+wall.name+" at "+p);}
            }
        }
        [Test] public void AmbientPavementLaneSeparatesPatientWalkersAndRoadCars()
        {
            state.Tick=270;world.Render(state,0,false);
            foreach(int id in new[]{1,3,5})
            {
                var person=Find("Street pedestrian "+id);Assert.That(person.position.z,Is.EqualTo(-11.85f).Within(.002f));
                var skins=person.GetComponentsInChildren<Renderer>();float half=skins.Max(r=>Mathf.Max(Mathf.Abs(r.bounds.min.z-person.position.z),Mathf.Abs(r.bounds.max.z-person.position.z)));
                Assert.That(person.position.z-half,Is.GreaterThanOrEqualTo(-12.35f),"The walker must remain on the pavement.");
                Assert.That(Mathf.Abs(person.position.z-(-13.2f)),Is.GreaterThan(.55f+half),"A near-lane car must clear the walker's actual geometry.");
                Assert.That(Mathf.Abs(person.position.z-(-10.90f)),Is.GreaterThan(.255f+half),"Opposing clinic visitors and ambient walkers need separate pavement lanes.");
            }
        }
        [TestCase("parking.bay.11.patient")][TestCase("taxi.dock.1.patient")]
        public void ReindexedArrivalKeepsItsSavedPositionSpeedAndIndoorPathAfterRelaunch(string from)
        {
            state.Staff.Clear();state.Patients.Clear();var p=new ClinicPatientState{Id=902,Phase=ClinicPatientPhase.Arriving,FromAnchor=from,ToAnchor="reception.queue.22",ArrivalPath=ClinicDoctorsNavigation.ArrivalPath(from,"reception.queue.22")};p.PhaseEndsTick=ClinicDoctorsNavigation.WalkTicks(p.ArrivalPath);state.Patients.Add(p);
            for(int tick=0;tick<p.PhaseEndsTick;tick++){var point=ClinicDoctorsNavigation.SampleArrivalPath(p.ArrivalPath,tick/(double)p.PhaseEndsTick);if(point.X>-.1f&&point.X<.9f&&point.Z>-10.3f){state.Tick=tick;break;}}
            Assert.That(state.Tick,Is.GreaterThan(0),"The actual arrival must enter the clinic before reindexing.");world.Render(state,0);var before=Find("Patient 902").position;
            p.ArrivalPath=ClinicDoctorsNavigation.RetargetArrivalPath(p.ArrivalPath,state.Tick/(double)p.PhaseEndsTick,"reception.queue.21");p.ToAnchor="reception.queue.21";p.PhaseStartedTick=state.Tick;p.PhaseEndsTick=state.Tick+ClinicDoctorsNavigation.WalkTicks(p.ArrivalPath);
            world.Render(state,0);Assert.That(Vector3.Distance(before,Find("Patient 902").position),Is.LessThan(.002f));
            // Rebuilding the displayed location represents a fresh actor pool after restoring the saved path.
            var restored=JsonUtility.FromJson<ClinicState>(JsonUtility.ToJson(state));world.ConfigureLocation(ClinicLocation.StarterClinic);world.ConfigureLocation(ClinicLocation.DoctorsClinic);world.Render(restored,0);Assert.That(Vector3.Distance(before,Find("Patient 902").position),Is.LessThan(.002f));
            var patient=restored.Patients[0];var previous=Find("Patient 902").position;
            for(long tick=restored.Tick+1;tick<=patient.PhaseEndsTick;tick++)
            {restored.Tick=tick;world.Render(restored,.1f,true);var current=Find("Patient 902").position;Assert.That(current.z,Is.GreaterThanOrEqualTo(-10.30f));Assert.That(Vector3.Distance(current,previous),Is.LessThanOrEqualTo(.171f),"Retargeting must not compress the walking deadline.");previous=current;}
            Assert.That(Vector3.Distance(previous,world.GetAnchorPoint(patient.ToAnchor)),Is.LessThan(.002f));
        }
        [Test] public void ServiceAudioCountsActualServicePhasesRatherThanReservations()
        {
            state.Patients.Clear();state.Patients.Add(new ClinicPatientState{Id=901,Phase=ClinicPatientPhase.WalkingToConsultation,FromAnchor="entrance",ToAnchor="consultation.station.0.patient",PhaseEndsTick=100});state.Staff.First(s=>s.Role==ClinicStaffRole.Doctor).PatientId=901;
            world.Render(state,0);Assert.That(world.ActiveServiceCount,Is.Zero);state.Patients[0].Phase=ClinicPatientPhase.Consulting;world.Render(state,0);Assert.That(world.ActiveServiceCount,Is.EqualTo(1));
        }
        [Test] public void MeasuresClinicalAndTransportRouteLengthEnvelopes()
        {
            float Max(string[] from,string[] to,bool staff=false){float m=0;foreach(var a in from)foreach(var b in to)m=Mathf.Max(m,new Route(world,a,b,staff).Length);return m;}
            string[] Names(string prefix,int n,string suffix="")=>Enumerable.Range(0,n).Select(i=>prefix+i+suffix).ToArray();
            var desks=Names("reception.desk.",4,".patient");var nurse=Names("firstaid.station.",4,".patient");var doctor=Names("consultation.station.",4,".patient");var pharmacy=Names("pharmacy.station.",2,".patient");var waiting=Names("waiting.seat.",30);var bays=Names("parking.bay.",12,".patient");var queues=Names("reception.queue.",23);var taxis=Names("taxi.dock.",2,".patient");
            foreach(var role in new[]{ClinicStaffRole.Receptionist,ClinicStaffRole.Nurse,ClinicStaffRole.Doctor,ClinicStaffRole.Pharmacist})TestContext.WriteLine("ROUTE entrance to "+role+" = "+Max(new[]{"entrance"},Enumerable.Range(0,role==ClinicStaffRole.Pharmacist?2:4).Select(i=>ClinicRules.StationStaffAnchor(role,i)).ToArray(),true));
            TestContext.WriteLine("ROUTE queue to desk = "+Max(queues,desks));TestContext.WriteLine("ROUTE waiting to doctor = "+Max(waiting,doctor));TestContext.WriteLine("ROUTE waiting to nurse = "+Max(waiting,nurse));TestContext.WriteLine("ROUTE consultation to nurse = "+Max(doctor,nurse));TestContext.WriteLine("ROUTE nurse to pharmacy = "+Max(nurse,pharmacy));TestContext.WriteLine("ROUTE care to waiting = "+Max(doctor.Concat(nurse).Concat(pharmacy).Concat(desks).ToArray(),waiting));TestContext.WriteLine("ROUTE bay to queue = "+Max(bays,queues));TestContext.WriteLine("ROUTE pharmacy to bay = "+Max(pharmacy,bays));TestContext.WriteLine("ROUTE taxi to queue = "+Max(taxis,queues));TestContext.WriteLine("ROUTE pharmacy to taxi = "+Max(pharmacy,taxis));TestContext.WriteLine("ROUTE waiting to amenity = "+Max(waiting,new[]{"waiting.toilet.0.patient","waiting.toilet.1.patient","waiting.vending.patient"}));
        }
        private Transform Find(string name)=>host.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        private sealed class Route
        {
            private readonly object route;private readonly MethodInfo sample;private readonly PropertyInfo length;
            internal float Length=>(float)length.GetValue(route);
            internal Route(ClinicWorld world,string from,string to,bool staff,Vector3? visibleStart=null)
            {var type=typeof(ClinicWorld).Assembly.GetType("IdleClinic.Presentation.ClinicRoute",true);route=Activator.CreateInstance(type,true);type.GetMethod("Build",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(route,new object[]{world,from,to,staff,visibleStart});sample=type.GetMethod("Sample",BindingFlags.NonPublic|BindingFlags.Instance);length=type.GetProperty("Length",BindingFlags.NonPublic|BindingFlags.Instance);}
            internal float Cross(float at,bool z)
            {var previous=Point(0);for(int i=1;i<=2000;i++){float t=i/2000f;var p=Point(t);float a=z?previous.z:previous.x,b=z?p.z:p.x;if((a<at&&b>=at)||(a>at&&b<=at))return (i-1+Mathf.InverseLerp(a,b,at))/2000f;previous=p;}Assert.Fail("Route does not cross doorway.");return 0;}
            internal Vector3 Point(float t){var args=new object[]{t,Vector3.zero};return (Vector3)sample.Invoke(route,args);}
        }
    }
}
