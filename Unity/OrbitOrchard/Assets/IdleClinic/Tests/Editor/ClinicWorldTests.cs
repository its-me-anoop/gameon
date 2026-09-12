using IdleClinic.Core;
using IdleClinic.App;
using IdleClinic.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleClinic.Tests
{
    public sealed class ClinicWorldTests
    {
        private GameObject host;
        private ClinicWorld world;
        [SetUp] public void SetUp() { host=new GameObject("Clinic world test");world=host.AddComponent<ClinicWorld>();world.Initialize(); }
        [TearDown] public void TearDown() => Object.DestroyImmediate(host);

        [TestCase("Patient")][TestCase("Nurse")][TestCase("Receptionist")]
        public void CharactersHaveSkinningAndArticulatedBones(string name)
        {
            var model=Resources.Load<GameObject>("Clinic/Models/"+name);
            Assert.That(model,Is.Not.Null);
            var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(skin,Is.Not.Null,"Characters must use an authored skeleton, not floating separate body pieces.");
            Assert.That(skin.bones.Length,Is.GreaterThanOrEqualTo(16));
            var clips=Resources.LoadAll<AnimationClip>("Clinic/Models/"+name);
            foreach(var action in new[]{"Idle","Walk","CheckIn","Treat","Sit","Call"})
                Assert.That(System.Array.Exists(clips,c=>c.name.EndsWith(action,System.StringComparison.Ordinal)),Is.True,action);
        }

        [TestCase(393,852)][TestCase(852,393)][TestCase(320,568)]
        public void PanPreservesTheGrabbedGroundPoint(int width,int height)
        {
            world.SetRenderSize(width,height);
            var from=new Vector2(.45f,.53f);var to=new Vector2(.50f,.56f);
            Assert.That(world.TryViewportToGround(from,out var expected),Is.True);
            world.Pan(from,to);
            Assert.That(world.TryViewportToGround(to,out var actual),Is.True);
            Assert.That(Vector3.Distance(expected,actual),Is.LessThan(.005f));
        }

        [TestCase(.3f,.6f)][TestCase(.7f,.35f)]
        public void ZoomPreservesTheGroundPointBeneathItsAnchor(float x,float y)
        {
            world.SetRenderSize(393,852);var anchor=new Vector2(x,y);
            world.TryViewportToGround(anchor,out var before);
            world.Zoom(.78f,anchor);
            world.TryViewportToGround(anchor,out var after);
            Assert.That(Vector3.Distance(before,after),Is.LessThan(.005f));
        }

        [Test]
        public void RoomSelectionDoesNotResetTheUserCamera()
        {
            world.Zoom(.8f,new Vector2(.5f,.5f));
            var before=world.SceneCamera.transform.position;var size=world.SceneCamera.orthographicSize;
            world.SelectRoom(ClinicRoom.FirstAid);
            Assert.That(world.SceneCamera.transform.position,Is.EqualTo(before));
            Assert.That(world.SceneCamera.orthographicSize,Is.EqualTo(size));
        }

        [Test]
        public void BothDesksHaveDistinctPayAndWorkSockets()
        {
            Assert.That(Vector3.Distance(world.GetCashPoint(0),world.GetCashPoint(1)),Is.GreaterThan(1));
            for(int i=0;i<2;i++)
                Assert.That(Vector3.Distance(world.GetAnchorPoint("reception.desk."+i+".patient"),world.GetAnchorPoint("reception.desk."+i+".staff")),Is.GreaterThan(1));
        }

        [TestCase(375,667,.2f)][TestCase(375,667,1f)][TestCase(375,667,5f)]
        [TestCase(320,568,.2f)][TestCase(320,568,1f)][TestCase(320,568,5f)]
        public void ReceptionSelectionStaysOnClearFloorWithBothPaidDesksAcrossZoomLimits(int width,int height,float zoom)
        {
            world.SetRenderSize(width,height);
            var state=ClinicSimulation.CreateNew().State;state.ReceptionDesks[0].Till=50;
            state.ReceptionDesks.Add(new ReceptionDeskState{Id=1,Till=60});state.Room(ClinicRoom.Reception).StationCount=2;
            world.Render(state,0,true);
            var target=world.WorldToViewport(ClinicSelectionPolicy.ReceptionFloorPoint);
            world.Zoom(zoom,target);target=world.WorldToViewport(ClinicSelectionPolicy.ReceptionFloorPoint);
            Assert.That(world.Pick(target).Kind,Is.EqualTo(ClinicHitKind.Reception),"The floor target must not intersect either 3D cash hit box.");
            var tap=new Vector2(target.x*width,(1-target.y)*height);
            for(var desk=0;desk<2;desk++)
            {
                var cash=world.WorldToViewport(world.GetCashPoint(desk));
                // Deliberately wider than the measured 60–67pt cash labels, preserving
                // their actual 44pt height and 30pt upward offset from the counter.
                var marker=new Rect(cash.x*width-44,(1-cash.y)*height-52,88,44);
                Assert.That(marker.Contains(tap),Is.False,"Room selection cannot hit cash marker "+desk);
            }
            Assert.That(state.ReceptionDesks[0].Till,Is.EqualTo(50));Assert.That(state.ReceptionDesks[1].Till,Is.EqualTo(60));
        }

        [Test]
        public void WaitingSeatsAndStaffStationsHaveDistinctOccupancyAnchors()
        {
            for(int i=0;i<14;i++) for(int j=i+1;j<14;j++)
                Assert.That(Vector3.Distance(world.GetAnchorPoint("waiting.seat."+i),world.GetAnchorPoint("waiting.seat."+j)),Is.GreaterThan(.48f));
            for(int i=0;i<2;i++)
                Assert.That(Vector3.Distance(world.GetAnchorPoint("firstaid.station."+i+".staff"),world.GetAnchorPoint("firstaid.station."+i+".patient")),Is.GreaterThan(.65f));
        }

        [Test]
        public void RenderTargetIsReusedAndRecoversAfterRelease()
        {
            var texture=world.SetRenderSize(393,852);texture.Release();
            Assert.That(world.SetRenderSize(393,852),Is.SameAs(texture));
            Assert.That(texture.IsCreated(),Is.True);
            Assert.That(Mathf.Max(world.SetRenderSize(4000,6000).width,world.Texture.height),Is.LessThanOrEqualTo(1536));
        }

        [Test]
        public void HiredNurseWalksFromEntranceBeforeTakingHerWorkSocket()
        {
            var state=new ClinicState();state.Staff.Add(new ClinicStaffState { Id=100,Role=ClinicStaffRole.Nurse,
                FromAnchor="entrance",ToAnchor="firstaid.station.0.staff",MoveStartedTick=0,MoveEndsTick=40 });
            world.Render(state,.016f);
            var nurse=Find("Staff 100");Assert.That(nurse,Is.Not.Null);
            Assert.That(Vector3.Distance(nurse.position,world.GetAnchorPoint("entrance")),Is.LessThan(.005f));
            state.Tick=20;world.Render(state,.016f);
            Assert.That(Vector3.Distance(nurse.position,world.GetAnchorPoint("entrance")),Is.GreaterThan(1));
            Assert.That(Vector3.Distance(nurse.position,world.GetAnchorPoint("firstaid.station.0.staff")),Is.GreaterThan(.3f));
            state.Tick=40;world.Render(state,.016f);
            Assert.That(Vector3.Distance(nurse.position,world.GetAnchorPoint("firstaid.station.0.staff")),Is.LessThan(.005f));
        }

        [Test]
        public void DepartedPatientsReuseTheExistingRigInsteadOfGrowingTheScene()
        {
            var state=new ClinicState();state.Patients.Add(new ClinicPatientState { Id=1,Phase=ClinicPatientPhase.ReceptionQueue,
                FromAnchor="reception.queue.0",ToAnchor="reception.queue.0" });
            world.Render(state,.016f);var patient=Find("Patient 1");var skin=patient.GetComponentInChildren<SkinnedMeshRenderer>();
            state.Patients.Clear();world.Render(state,.016f);
            Assert.That(patient.gameObject.activeSelf,Is.False);
            state.Patients.Add(new ClinicPatientState { Id=2,Phase=ClinicPatientPhase.Seated,
                FromAnchor="waiting.seat.0",ToAnchor="waiting.seat.0" });
            world.Render(state,.016f);
            Assert.That(Find("Patient 2").GetComponentInChildren<SkinnedMeshRenderer>(),Is.SameAs(skin));
            Assert.That(Vector3.Distance(patient.position,world.GetAnchorPoint("waiting.seat.0")),Is.LessThan(.005f));
        }

        [TestCase("firstaid.station.0.patient",ClinicPatientPhase.Treating)]
        [TestCase("waiting.seat.0",ClinicPatientPhase.Seated)]
        public void SeatedAnimationKeepsHeadAndHandsAttachedAboveItsAuthoredSeat(string socket,ClinicPatientPhase phase)
        {
            var state=new ClinicState();state.Rooms.Add(new ClinicRoomState { Kind=ClinicRoom.Waiting,Built=true });
            state.Patients.Add(new ClinicPatientState { Id=17,Phase=phase,FromAnchor=socket,ToAnchor=socket });
            world.Render(state,.016f,true);var actor=Find("Patient 17");
            var bones=actor.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            var head=System.Array.Find(bones,b=>b.name=="head");
            var hip=System.Array.Find(bones,b=>b.name=="upper_leg.L");
            var knee=System.Array.Find(bones,b=>b.name=="lower_leg.L");
            var hand=System.Array.Find(bones,b=>b.name=="hand.L");
            Assert.That(head,Is.Not.Null);Assert.That(hand,Is.Not.Null);Assert.That(hip,Is.Not.Null);Assert.That(knee,Is.Not.Null);
            Assert.That(head.position.y-actor.position.y,Is.InRange(1.08f,1.30f));
            Assert.That(hand.position.y-actor.position.y,Is.InRange(.43f,.65f));
            Assert.That(hip.position.y-knee.position.y,Is.InRange(.05f,.22f),"Thighs bend across the seat rather than hanging in a standing pose.");
            Assert.That(Vector3.Distance(hip.position,knee.position),Is.InRange(.28f,.42f));
        }

        [TestCase(0,false)][TestCase(1,false)][TestCase(0,true)][TestCase(1,true)]
        public void WaitingAtAStandingAdmissionSocketUsesAnUprightPose(int slot,bool reducedMotion)
        {
            string socket="firstaid.standing."+slot;var state=new ClinicState { Tick=10 };
            state.Patients.Add(new ClinicPatientState { Id=7,Phase=ClinicPatientPhase.Seated,FromAnchor=socket,ToAnchor=socket });
            world.Render(state,.016f,reducedMotion);var actor=Find("Patient 7");
            var bones=actor.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            var head=System.Array.Find(bones,b=>b.name=="head");
            var hip=System.Array.Find(bones,b=>b.name=="upper_leg.L");
            var knee=System.Array.Find(bones,b=>b.name=="lower_leg.L");
            Assert.That(head.position.y-actor.position.y,Is.InRange(1.30f,1.50f));
            Assert.That(hip.position.y-knee.position.y,Is.GreaterThan(.28f),"Standing admission spaces have no chair to support bent thighs.");
        }

        [TestCase(402,874)][TestCase(393,852)][TestCase(320,568)]
        public void HomeFramesTheFirstCashDeskStaffAndStarterClinicWithPortraitMargins(int width,int height)
        {
            world.SetRenderSize(width,height);
            var state=new ClinicState();state.ReceptionDesks.Add(new ReceptionDeskState { Id=0,Till=50 });world.Render(state,.016f,true);
            foreach(var renderer in Find("ReceptionDesk").GetComponentsInChildren<Renderer>())
                AssertBoundsInside(renderer.bounds,.06f,.94f,.14f,.80f);
            AssertViewport(world.GetAnchorPoint("reception.desk.0.staff")+Vector3.up*1.60f,.06f,.94f,.14f,.80f);
            foreach(float x in new[]{-5.65f,1.30f})foreach(float z in new[]{-6.72f,4.95f})
                AssertViewport(new Vector3(x,.14f,z),.055f,.945f,.13f,.81f);
            var cash=world.WorldToViewport(world.GetCashPoint(0));
            Assert.That(cash.x,Is.InRange(.12f,.88f));Assert.That(cash.y,Is.InRange(.18f,.72f));
            Assert.That(world.Pick(cash).Kind,Is.EqualTo(ClinicHitKind.Cash));
        }

        [Test]
        public void FreeCameraSurvivesSimulationUpdatesAndRenderTargetResize()
        {
            world.Pan(new Vector2(.5f,.5f),new Vector2(.56f,.53f));world.Zoom(.8f,new Vector2(.5f,.5f));
            var position=world.SceneCamera.transform.position;var zoom=world.SceneCamera.orthographicSize;
            world.Render(new ClinicState(),.016f);world.SetRenderSize(402,874);
            Assert.That(world.SceneCamera.transform.position,Is.EqualTo(position));Assert.That(world.SceneCamera.orthographicSize,Is.EqualTo(zoom));
        }

        [Test]
        public void PhysicalPortraitStartupKeepsTheCashDeskFramedAfterTemporaryRenderSizes()
        {
            var state=new ClinicState();state.ReceptionDesks.Add(new ReceptionDeskState { Id=0,Till=50 });
            world.Render(state,0);var defaultCash=world.WorldToViewport(world.GetCashPoint(0));
            foreach(var request in new[]{new Vector2Int(1320,2868),new Vector2Int(2868,1320),new Vector2Int(1320,2868)})
            {
                world.SetRenderSize(request.x,request.y);world.Render(state,0);
                Assert.That(world.Pick(world.WorldToViewport(world.GetCashPoint(0))).Kind,Is.EqualTo(ClinicHitKind.Cash));
                AssertBoundsInside(Find("ReceptionDesk").GetComponentInChildren<Renderer>().bounds,.06f,.94f,.14f,.80f);
            }
            Assert.That(world.Texture.height,Is.EqualTo(1536));
            Assert.That(Vector2.Distance(defaultCash,world.WorldToViewport(world.GetCashPoint(0))),Is.LessThan(.002f));
            Assert.That(world.SceneCamera.transform.position.x,Is.LessThan(0),"Home remains over the starter clinic, not the right expansion plot.");
        }

        [TestCase("Clinic cube",1,1,1)]
        [TestCase("Clinic sphere",1,1,1)]
        [TestCase("Clinic cylinder",1,2,1)]
        public void RuntimePrimitivesHaveOwnedMeshesAndTheOriginalDimensionContract(string name,float x,float y,float z)
        {
            Mesh mesh=null;foreach(var filter in host.GetComponentsInChildren<MeshFilter>(true))
                if(filter.sharedMesh!=null&&filter.sharedMesh.name==name){mesh=filter.sharedMesh;break;}
            Assert.That(mesh,Is.Not.Null,"Runtime art must construct meshes directly rather than invoke implicit stripped colliders.");
            Assert.That(Vector3.Distance(mesh.bounds.size,new Vector3(x,y,z)),Is.LessThan(.001f));
            Assert.That(mesh.vertexCount,Is.LessThan(300));
            var vertices=mesh.vertices;var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
                var normal=Vector3.Cross(b-a,c-a);if(normal.sqrMagnitude<.000001f)continue;
                Assert.That(Vector3.Dot(normal,(a+b+c)/3),Is.GreaterThan(0),"Outward triangle winding at "+i);
            }
            Assert.That(host.GetComponentsInChildren<Collider>(true),Is.Empty);
        }

        [TestCase(false)][TestCase(true)]
        public void DepartingPatientShowsDressingAndReliefWithoutLeakingIntoReusedRig(bool reducedMotion)
        {
            var state=new ClinicState { Tick=5 };state.Patients.Add(new ClinicPatientState { Id=70,Phase=ClinicPatientPhase.Leaving,
                FromAnchor="firstaid.station.0.patient",ToAnchor="exit",PhaseStartedTick=0,PhaseEndsTick=40 });
            world.Render(state,.1f,reducedMotion);var actor=Find("Patient 70");var relief=Find("Departure relief");var dressing=Find("Forehead dressing");
            Assert.That(relief.gameObject.activeInHierarchy,Is.True);var head=System.Array.Find(actor.GetComponentInChildren<SkinnedMeshRenderer>().bones,b=>b.name=="head");
            Assert.That(Vector3.Dot(dressing.position-head.position,actor.forward),Is.GreaterThan(.13f));
            Assert.That(dressing.position.y-head.position.y,Is.InRange(.04f,.10f));
            state.Patients.Clear();world.Render(state,.1f,reducedMotion);
            state.Patients.Add(new ClinicPatientState { Id=71,Phase=ClinicPatientPhase.ReceptionQueue,FromAnchor="reception.queue.0",ToAnchor="reception.queue.0" });
            world.Render(state,.1f,reducedMotion);
            Assert.That(Find("Patient 71"),Is.SameAs(actor));Assert.That(relief.gameObject.activeSelf,Is.False);
        }

        [Test]
        public void DepartureWaveLayersAnArmGestureOverWalkingLegs()
        {
            var state=new ClinicState { Tick=7 };var person=new ClinicPatientState { Id=8,Phase=ClinicPatientPhase.WalkingToTreatment,
                FromAnchor="entrance",ToAnchor="firstaid.station.0.patient",PhaseStartedTick=0,PhaseEndsTick=40 };state.Patients.Add(person);
            world.Render(state,.1f);var actor=Find("Patient 8");var bones=actor.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            var knee=System.Array.Find(bones,b=>b.name=="lower_leg.R");var hand=System.Array.Find(bones,b=>b.name=="hand.R");
            var walkingKnee=actor.InverseTransformPoint(knee.position);float walkingHand=actor.InverseTransformPoint(hand.position).y;
            person.Phase=ClinicPatientPhase.Leaving;world.Render(state,.1f);
            Assert.That(Vector3.Distance(actor.InverseTransformPoint(knee.position),walkingKnee),Is.LessThan(.005f));
            Assert.That(actor.InverseTransformPoint(hand.position).y,Is.GreaterThan(walkingHand+.40f));
        }

        [TestCase(0)][TestCase(1)][TestCase(2)]
        public void CareDoorsOpenBeforeTravellingActorsReachThemAndCloseAfterwards(int journey)
        {
            var state=new ClinicState();
            if(journey==0)state.Staff.Add(new ClinicStaffState { Id=100,Role=ClinicStaffRole.Nurse,FromAnchor="entrance",ToAnchor="firstaid.station.0.staff",MoveStartedTick=0,MoveEndsTick=40 });
            else state.Patients.Add(new ClinicPatientState { Id=9,Phase=journey==1?ClinicPatientPhase.WalkingToWaiting:ClinicPatientPhase.Leaving,
                FromAnchor=journey==1?"reception.desk.0.patient":"firstaid.station.0.patient",ToAnchor=journey==1?"firstaid.standing.0":"exit",PhaseStartedTick=0,PhaseEndsTick=40 });
            world.Render(state,.1f);var left=Find("Care door left panel");float closed=left.localPosition.x;bool crossed=false;
            for(int tick=1;tick<=40;tick++)
            {
                state.Tick=tick;world.Render(state,.1f);var actor=Find(journey==0?"Staff 100":"Patient 9");
                if(Mathf.Abs(actor.position.z+.15f)>.35f)continue;crossed=true;
                Assert.That(left.localPosition.x,Is.LessThan(closed-.70f));
                foreach(var renderer in Find("Care wing doorway").GetComponentsInChildren<Renderer>())
                { var bounds=renderer.bounds;bounds.Expand(new Vector3(.60f,0,.60f));Assert.That(bounds.Contains(actor.position+Vector3.up*.80f),Is.False,"Door panel obstructs actor clearance."); }
            }
            Assert.That(crossed,Is.True);
            for(int tick=41;tick<65;tick++){state.Tick=tick;world.Render(state,.1f);}
            Assert.That(left.localPosition.x,Is.EqualTo(closed).Within(.005f));
        }

        [TestCase(false)][TestCase(true)]
        public void RestoringAnActorNearTheDoorStartsWithAClearPassage(bool reducedMotion)
        {
            var state=new ClinicState { Tick=19 };state.Staff.Add(new ClinicStaffState { Id=100,Role=ClinicStaffRole.Nurse,
                FromAnchor="entrance",ToAnchor="firstaid.station.0.staff",MoveStartedTick=0,MoveEndsTick=40 });
            world.Render(state,0,reducedMotion);
            Assert.That(Find("Care door left panel").localPosition.x,Is.LessThan(-1.2f));
            Assert.That(Find("Care door right panel").localPosition.x,Is.GreaterThan(1.2f));
        }

        private void AssertBoundsInside(Bounds bounds,float left,float right,float bottom,float top)
        { for(int i=0;i<8;i++)AssertViewport(new Vector3((i&1)==0?bounds.min.x:bounds.max.x,(i&2)==0?bounds.min.y:bounds.max.y,(i&4)==0?bounds.min.z:bounds.max.z),left,right,bottom,top); }
        private void AssertViewport(Vector3 point,float left,float right,float bottom,float top)
        { var uv=world.WorldToViewport(point);Assert.That(uv.x,Is.InRange(left,right),"Horizontal framing: "+point);Assert.That(uv.y,Is.InRange(bottom,top),"Vertical framing: "+point); }

        private Transform Find(string name)
        { foreach(var item in host.GetComponentsInChildren<Transform>(true))if(item.name==name)return item;return null; }

        [Test]
        public void PrivacyPartitionsAppearOnlyWhenTheSecondWorkplaceOpens()
        {
            var state=new ClinicState();var care=new ClinicRoomState{Kind=ClinicRoom.FirstAid,StationCount=1};state.Rooms.Add(care);
            state.ReceptionDesks.Add(new ReceptionDeskState{Id=0});world.Render(state,.1f);
            var reception=Find("Reception privacy divider");var treatment=Find("Treatment privacy partition");
            Assert.That(reception,Is.Not.Null);Assert.That(treatment,Is.Not.Null);
            Assert.That(reception.gameObject.activeSelf,Is.False);Assert.That(treatment.gameObject.activeSelf,Is.False);
            state.ReceptionDesks.Add(new ReceptionDeskState{Id=1});care.StationCount=2;world.Render(state,.1f);
            Assert.That(reception.gameObject.activeInHierarchy,Is.True);Assert.That(treatment.gameObject.activeInHierarchy,Is.True);
        }

        [TestCase(false,0)][TestCase(false,1)][TestCase(true,0)][TestCase(true,1)]
        public void PrivacyPartitionsLeaveOccupiedSocketsAndApproachRoutesClear(bool treatment,int station)
        {
            var state=new ClinicState();state.Rooms.Add(new ClinicRoomState{Kind=ClinicRoom.FirstAid,StationCount=2});
            state.ReceptionDesks.Add(new ReceptionDeskState{Id=0});state.ReceptionDesks.Add(new ReceptionDeskState{Id=1});world.Render(state,.1f);
            var divider=Find(treatment?"Treatment privacy partition":"Reception privacy divider");Assert.That(divider,Is.Not.Null);
            string prefix=treatment?"firstaid.station.":"reception.desk.";
            // Each hire starts from the entrance in a fresh scene. Reusing the same
            // actor after finishing another station would test a different journey.
            var patient=new ClinicPatientState{Id=90,Phase=ClinicPatientPhase.WalkingToTreatment,
                FromAnchor="entrance",ToAnchor=prefix+station+".patient",PhaseStartedTick=0,PhaseEndsTick=100};state.Patients.Add(patient);
            var staff=new ClinicStaffState{Id=100,Role=treatment?ClinicStaffRole.Nurse:ClinicStaffRole.Receptionist,
                FromAnchor="entrance",ToAnchor=prefix+station+".staff",MoveStartedTick=0,MoveEndsTick=100};state.Staff.Add(staff);
            for(int tick=0;tick<=100;tick++)
            {
                state.Tick=tick;world.Render(state,.1f,true);
                foreach(var renderer in divider.GetComponentsInChildren<Renderer>())
                {
                    var bounds=renderer.bounds;bounds.Expand(new Vector3(.60f,0,.60f));
                    foreach(string actor in new[]{"Patient 90","Staff 100"})
                        Assert.That(bounds.Contains(Find(actor).position+Vector3.up*.80f),Is.False,renderer.name+" blocks "+actor+" at tick "+tick);
                }
            }
        }

        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)]
        [TestCase(6)][TestCase(7)][TestCase(8)][TestCase(9)][TestCase(10)]
        public void EntrancePanelsLeaveEveryQueueApproachClear(int queue)
        {
            var state=new ClinicState();state.Patients.Add(new ClinicPatientState{Id=91,Phase=ClinicPatientPhase.Arriving,
                FromAnchor="entrance",ToAnchor="reception.queue."+queue,PhaseStartedTick=0,PhaseEndsTick=100});
            var door=Find("Clinic entrance doorway");Assert.That(door,Is.Not.Null);
            for(int tick=0;tick<=100;tick++)
            {
                state.Tick=tick;world.Render(state,.1f);var position=Find("Patient 91").position+Vector3.up*.80f;
                foreach(var renderer in door.GetComponentsInChildren<Renderer>())
                { var bounds=renderer.bounds;bounds.Expand(new Vector3(.60f,0,.60f));Assert.That(bounds.Contains(position),Is.False,renderer.name+" blocks queue "+queue+" at tick "+tick); }
            }
        }

        [Test]
        public void EntranceHasAuthoredClinicNameAndOpensForTheArrivingNurse()
        {
            Assert.That(Resources.Load<GameObject>("Clinic/Models/ClinicSign"),Is.Not.Null);
            Assert.That(Find("ClinicSign").GetComponentsInChildren<Renderer>(),Is.Not.Empty);
            var state=new ClinicState();state.Staff.Add(new ClinicStaffState{Id=100,Role=ClinicStaffRole.Nurse,
                FromAnchor="entrance",ToAnchor="firstaid.station.0.staff",MoveStartedTick=0,MoveEndsTick=40});
            bool crossed=false;
            for(int tick=0;tick<=40;tick++)
            {
                state.Tick=tick;world.Render(state,.1f);var position=Find("Staff 100").position;
                if(Mathf.Abs(position.z+5.13f)>.3f)continue;crossed=true;
                Assert.That(Find("Entrance door left panel").localPosition.x,Is.LessThan(-1.2f));
                foreach(var renderer in Find("Clinic entrance doorway").GetComponentsInChildren<Renderer>())
                { var bounds=renderer.bounds;bounds.Expand(new Vector3(.60f,0,.60f));Assert.That(bounds.Contains(position+Vector3.up*.80f),Is.False); }
            }
            Assert.That(crossed,Is.True);
        }

        [Test]
        public void TownSurroundsClinicWithoutIntrudingOnItsCirculationOrCashViews()
        {
            var town=Find("Clinic neighbourhood");Assert.That(town,Is.Not.Null);
            Assert.That(Find("Neighbourhood street"),Is.Not.Null);Assert.That(Find("Town house 0"),Is.Not.Null);Assert.That(Find("Street tree 0"),Is.Not.Null);
            foreach(var renderer in town.GetComponentsInChildren<Renderer>())
            {
                var bounds=renderer.bounds;if(bounds.max.y<.3f)continue;
                var operatingArea=new Bounds(new Vector3(0,1,-.8f),new Vector3(11.8f,2,12.8f));
                Assert.That(bounds.Intersects(operatingArea),Is.False,renderer.name+" intrudes on the clinic");
            }
            world.SetRenderSize(402,874);world.Home(true);
            var state=new ClinicState();state.ReceptionDesks.Add(new ReceptionDeskState{Id=0,Till=50});world.Render(state,.1f);
            var cash=world.GetCashPoint(0);var uv=world.WorldToViewport(cash);Assert.That(world.Pick(uv).Kind,Is.EqualTo(ClinicHitKind.Cash));
            var ray=world.SceneCamera.ViewportPointToRay(uv);float cashDistance=Vector3.Dot(cash-ray.origin,ray.direction);
            foreach(var renderer in town.GetComponentsInChildren<Renderer>())
                if(renderer.bounds.IntersectRay(ray,out float distance))Assert.That(distance,Is.GreaterThan(cashDistance),renderer.name+" obscures cash");
        }

        [TestCase(ClinicRoom.Reception,UpgradeTrack.Equipment)]
        [TestCase(ClinicRoom.Reception,UpgradeTrack.Facilities)]
        [TestCase(ClinicRoom.Reception,UpgradeTrack.Decoration)]
        [TestCase(ClinicRoom.FirstAid,UpgradeTrack.Equipment)]
        [TestCase(ClinicRoom.FirstAid,UpgradeTrack.Facilities)]
        [TestCase(ClinicRoom.FirstAid,UpgradeTrack.Decoration)]
        [TestCase(ClinicRoom.Waiting,UpgradeTrack.Equipment)]
        [TestCase(ClinicRoom.Waiting,UpgradeTrack.Facilities)]
        [TestCase(ClinicRoom.Waiting,UpgradeTrack.Decoration)]
        public void EveryUpgradeTrackAddsAnAuthoredVisibleFitting(ClinicRoom room,UpgradeTrack track)
        {
            var state=new ClinicState();var item=new ClinicRoomState{Kind=room,Built=true};state.Rooms.Add(item);
            world.Render(state,.016f);
            for(int level=2;level<=6;level++)
            {
                var detail=Find(room+" "+track+" level "+level);Assert.That(detail,Is.Not.Null);
                Assert.That(detail.gameObject.activeInHierarchy,Is.False);
                if(track==UpgradeTrack.Equipment)item.EquipmentLevel=level;
                else if(track==UpgradeTrack.Facilities)item.FacilitiesLevel=level;
                else item.DecorationLevel=level;
                world.Render(state,.016f);
                Assert.That(detail.gameObject.activeInHierarchy,Is.True);
                Assert.That(detail.GetComponentsInChildren<Renderer>().Length,Is.GreaterThan(0));
            }
        }

        [Test]
        public void WaitingRoomTierFittingsLeaveAllFourteenOccupiedSeatsClear()
        {
            var state=new ClinicState();state.Rooms.Add(new ClinicRoomState { Kind=ClinicRoom.Waiting,Built=true,Tier=3,
                EquipmentLevel=6,FacilitiesLevel=6,DecorationLevel=6 });world.Render(state,.016f);
            var room=Find("Waiting room");
            foreach(Transform child in room)
            {
                if(!child.name.StartsWith("Tier ",System.StringComparison.Ordinal))continue;
                foreach(var renderer in child.GetComponentsInChildren<Renderer>())
                {
                    var bounds=renderer.bounds;bounds.Expand(new Vector3(.40f,0,.40f));
                    for(int seat=0;seat<14;seat++)
                    {
                        var point=world.GetAnchorPoint("waiting.seat."+seat)+Vector3.up*.60f;
                        Assert.That(bounds.Contains(point),Is.False,renderer.name+" blocks seat "+seat);
                    }
                }
            }
        }

        [Test]
        public void RenovationSuppliesRemainOutsideOperatingCareAndWaitingSockets()
        {
            var state=new ClinicState();for(int i=0;i<3;i++)
            { state.Rooms.Add(new ClinicRoomState{Kind=(ClinicRoom)i,Built=true,FacilitiesLevel=6});state.Construction.Add(new ClinicConstructionState{Room=(ClinicRoom)i}); }
            world.Render(state,.016f);
            var points=new System.Collections.Generic.List<Vector3>();
            for(int i=0;i<14;i++)points.Add(world.GetAnchorPoint("waiting.seat."+i));
            for(int i=0;i<2;i++)foreach(string department in new[]{"reception.desk.","firstaid.station."})
                foreach(string role in new[]{"patient","staff"})points.Add(world.GetAnchorPoint(department+i+"."+role));
            foreach(var transform in host.GetComponentsInChildren<Transform>())
            {
                if(transform.name!="Renovation at work")continue;
                foreach(var renderer in transform.GetComponentsInChildren<Renderer>())
                { var bounds=renderer.bounds;bounds.Expand(new Vector3(.4f,0,.4f));foreach(var point in points)Assert.That(bounds.Contains(point+Vector3.up*.2f),Is.False,renderer.name); }
            }
        }

        [Test]
        public void VisitorPassingToSecondCounterClearsTheVisitorAtFirstCounter()
        {
            var state=new ClinicState();
            state.Patients.Add(new ClinicPatientState { Id=1,Phase=ClinicPatientPhase.CheckingIn,
                FromAnchor="reception.desk.0.patient",ToAnchor="reception.desk.0.patient" });
            var walking=new ClinicPatientState { Id=2,Phase=ClinicPatientPhase.WalkingToReception,
                FromAnchor="reception.queue.0",ToAnchor="reception.desk.1.patient",PhaseStartedTick=0,PhaseEndsTick=100 };
            state.Patients.Add(walking);float closest=float.MaxValue;
            for(int tick=0;tick<=100;tick++)
            {
                state.Tick=tick;world.Render(state,.016f,true);
                var passing=Find("Patient 2");var paying=Find("Patient 1");
                closest=Mathf.Min(closest,Vector3.Distance(passing.position,paying.position));
            }
            // Authored torso/upper-arm span is 0.60m. Two 0.30m visitor radii require at least 0.60m between centers.
            Assert.That(closest,Is.GreaterThanOrEqualTo(.60f));
        }

        [Test]
        public void AllQueueSocketsHavePavingBeneathTheirFeet()
        {
            var paving=Find("Reception forecourt paving").GetComponent<Renderer>().bounds;
            for(int i=0;i<11;i++)
            {
                var point=world.GetAnchorPoint("reception.queue."+i);
                Assert.That(point.x,Is.InRange(paving.min.x+.25f,paving.max.x-.25f));
                Assert.That(point.z,Is.InRange(paving.min.z+.25f,paving.max.z+.001f));
                Assert.That(Mathf.Abs(point.y-paving.max.y),Is.LessThan(.005f));
            }
        }
        [Test]
        public void ClinicFloorAndNeighbourhoodHaveDesignedFurnishingsInsteadOfBlankGround()
        {
            foreach(var name in new[]{"Meadow ground","Reception welcome rug","Care floor inlay","Waiting woven rug",
                "Reception notice board","Waiting notice board","Community notice board","Street bicycle rack","Bus shelter","Flower border"})
                Assert.That(Find(name),Is.Not.Null,name);
            Assert.That(Find("Paper ground"),Is.Null,"The world backdrop must be landscaped rather than blank paper.");
        }

        [Test]
        public void NewAmenitiesHaveDistinctStableAnchorsOutsideOccupiedSeats()
        {
            var toilet=world.GetAnchorPoint("waiting.toilet.patient");var vending=world.GetAnchorPoint("waiting.vending.patient");
            Assert.That(toilet.z,Is.GreaterThan(5));Assert.That(vending.z,Is.LessThan(-1));
            for(int i=0;i<14;i++)
            {
                var seat=world.GetAnchorPoint("waiting.seat."+i);
                Assert.That(Vector3.Distance(toilet,seat),Is.GreaterThan(.65f));
                Assert.That(Vector3.Distance(vending,seat),Is.GreaterThan(.65f));
            }
            for(int bay=0;bay<6;bay++)Assert.That(world.GetAnchorPoint("parking.bay."+bay+".patient").x,Is.LessThan(-7));
        }

        [Test]
        public void AmbientTrafficIsBoundedAndReducedMotionFreezesItsTravel()
        {
            var state=new ClinicState { Tick=30 };world.Render(state,.1f,true);
            var car=Find("Traffic car 0");var pedestrian=Find("Street pedestrian 0");
            Assert.That(car,Is.Not.Null);Assert.That(pedestrian,Is.Not.Null);
            var carPosition=car.position;var personPosition=pedestrian.position;int count=host.GetComponentsInChildren<Transform>(true).Length;
            state.Tick=900;world.Render(state,.1f,true);
            Assert.That(car.position,Is.EqualTo(carPosition));Assert.That(pedestrian.position,Is.EqualTo(personPosition));
            Assert.That(host.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(count));
            world.Render(state,.1f,false);Assert.That(car.position,Is.Not.EqualTo(carPosition));
        }

        [Test]
        public void RenovationScaffoldProgressReflectsActualJobAndDisappearsOnCompletion()
        {
            var state=new ClinicState { Tick=50 };state.Construction.Add(new ClinicConstructionState{Room=ClinicRoom.FirstAid,StartedTick=0,EndsTick=100});
            world.Render(state,.1f,true);var scaffold=Find("FirstAid renovation scaffold");
            Assert.That(scaffold,Is.Not.Null);Assert.That(scaffold.gameObject.activeInHierarchy,Is.True);
            var progress=Find("FirstAid construction progress");Assert.That(progress.localScale.x,Is.EqualTo(.5f).Within(.001f));
            state.Tick=75;world.Render(state,.1f,true);Assert.That(progress.localScale.x,Is.EqualTo(.75f).Within(.001f));
            state.Construction.Clear();world.Render(state,.1f,true);Assert.That(scaffold.gameObject.activeSelf,Is.False);
        }

        [TestCase(ClinicAmenity.Parking,ClinicHitKind.Parking)]
        [TestCase(ClinicAmenity.Toilet,ClinicHitKind.Toilet)]
        [TestCase(ClinicAmenity.Vending,ClinicHitKind.Vending)]
        public void AmenityBuildSitesAndTheirLevelsHaveMatchingPhysicalPickTargets(ClinicAmenity amenity,ClinicHitKind kind)
        {
            var state=ClinicSimulation.CreateNew().State;world.SetRenderSize(1000,700);world.Zoom(2,new Vector2(.5f,.5f));
            for(int level=0;level<=3;level++)
            {
                state.Amenity(amenity).Level=level;world.Render(state,0,true);var uv=world.WorldToViewport(world.GetAmenityPoint(amenity));
                var hit=world.Pick(uv);Assert.That(hit.Kind,Is.EqualTo(kind));Assert.That(hit.Id,Is.EqualTo((int)amenity));
            }
        }

        [TestCase(0)][TestCase(1)]
        public void IndividualEquipmentTargetsPickTheCorrectWorkstationWithoutMovingItsSockets(int id)
        {
            var state=ClinicSimulation.CreateNew().State;state.ReceptionDesks.Add(new ReceptionDeskState{Id=1});
            state.Room(ClinicRoom.FirstAid).StationCount=2;state.TreatmentStations.Add(new TreatmentStationState{Id=1});
            world.SetRenderSize(1000,700);world.Render(state,0,true);
            var deskPoint=world.GetAnchorPoint("reception.desk."+id+".staff");
            var stationPoint=world.GetAnchorPoint("firstaid.station."+id+".patient");
            Assert.That(world.Pick(world.WorldToViewport(world.GetDeskPoint(id))).Kind,Is.EqualTo(ClinicHitKind.Desk));
            Assert.That(world.Pick(world.WorldToViewport(world.GetStationPoint(id))).Kind,Is.EqualTo(ClinicHitKind.Station));
            state.ReceptionDesks[id].EquipmentLevel=3;state.TreatmentStations[id].EquipmentLevel=3;world.Render(state,0,true);
            Assert.That(Find("Desk "+id+" equipment 3").gameObject.activeInHierarchy,Is.True);
            Assert.That(Find("Station "+id+" equipment 3").gameObject.activeInHierarchy,Is.True);
            Assert.That(Find("Desk "+(1-id)+" equipment 3").gameObject.activeSelf,Is.False);
            Assert.That(world.GetAnchorPoint("reception.desk."+id+".staff"),Is.EqualTo(deskPoint));
            Assert.That(world.GetAnchorPoint("firstaid.station."+id+".patient"),Is.EqualTo(stationPoint));
        }

        [Test]
        public void RoomSelectionPointsRemainRoomTargetsWithAmenitiesAndAllWorkstationsBuilt()
        {
            var state=ClinicSimulation.CreateNew().State;state.ReceptionDesks.Add(new ReceptionDeskState{Id=1,Till=55});state.ReceptionDesks[0].Till=50;
            state.Room(ClinicRoom.FirstAid).StationCount=2;state.Room(ClinicRoom.Waiting).Built=true;
            foreach(var amenity in state.Amenities)amenity.Level=3;world.SetRenderSize(1000,700);world.Render(state,0,true);
            Assert.That(world.Pick(world.WorldToViewport(ClinicSelectionPolicy.ReceptionFloorPoint)).Kind,Is.EqualTo(ClinicHitKind.Reception));
            Assert.That(world.Pick(world.WorldToViewport(world.GetAnchorPoint("firstaid.progress"))).Kind,Is.EqualTo(ClinicHitKind.Treatment));
            Assert.That(world.Pick(world.WorldToViewport(world.GetAnchorPoint("waiting.progress"))).Kind,Is.EqualTo(ClinicHitKind.Waiting));
        }

        [Test]
        public void DifferentVisitorsHaveDifferentSilhouettesAndReusedActorsResetTheirWardrobe()
        {
            var state=new ClinicState();state.Patients.Add(new ClinicPatientState{Id=1,AppearanceId=8,Phase=ClinicPatientPhase.ReceptionQueue,FromAnchor="reception.queue.0",ToAnchor="reception.queue.0"});
            world.Render(state,0,true);var actor=Find("Patient 1");var scale=actor.localScale;
            Assert.That(Find("Silver beard").gameObject.activeInHierarchy,Is.True);
            state.Patients.Clear();world.Render(state,0,true);
            state.Patients.Add(new ClinicPatientState{Id=2,AppearanceId=6,Phase=ClinicPatientPhase.ReceptionQueue,FromAnchor="reception.queue.0",ToAnchor="reception.queue.0"});world.Render(state,0,true);
            Assert.That(Find("Patient 2"),Is.SameAs(actor));Assert.That(actor.localScale,Is.Not.EqualTo(scale));
            Assert.That(Find("Silver beard").gameObject.activeInHierarchy,Is.False);Assert.That(Find("Visitor cap").gameObject.activeInHierarchy,Is.True);
        }

        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)]
        [TestCase(6)][TestCase(7)][TestCase(8)][TestCase(9)][TestCase(10)][TestCase(11)]
        public void VisitorHeightNeverChangesSeatedPelvisHeight(int appearance)
        {
            var state=new ClinicState();state.Rooms.Add(new ClinicRoomState{Kind=ClinicRoom.Waiting,Built=true});
            var patient=new ClinicPatientState{Id=1,AppearanceId=0,Phase=ClinicPatientPhase.Seated,FromAnchor="waiting.seat.0",ToAnchor="waiting.seat.0"};state.Patients.Add(patient);
            world.Render(state,0,true);var actor=Find("Patient 1");var pelvis=System.Array.Find(actor.GetComponentInChildren<SkinnedMeshRenderer>().bones,b=>b.name=="pelvis");
            float expected=pelvis.position.y;patient.AppearanceId=appearance;world.Render(state,0,true);
            Assert.That(pelvis.position.y,Is.EqualTo(expected).Within(.003f));
        }

        [TestCase("waiting.toilet.patient",ClinicAmenity.Toilet)][TestCase("waiting.vending.patient",ClinicAmenity.Vending)]
        public void AmenityVisitorsFollowAnAisleAndReturnToTheSameReservedSeat(string anchor,ClinicAmenity amenity)
        {
            var state=ClinicSimulation.CreateNew().State;state.Room(ClinicRoom.Waiting).Built=true;state.Amenity(amenity).Level=1;
            var patient=new ClinicPatientState{Id=99,Phase=ClinicPatientPhase.WalkingToAmenity,VisitingAmenity=amenity,
                FromAnchor="waiting.seat.13",ToAnchor=anchor,PhaseStartedTick=0,PhaseEndsTick=100,SeatId=13,HasAdmissionReservation=true};state.Patients.Add(patient);
            world.Render(state,0,true);Assert.That(Find("Patient 99").position,Is.EqualTo(world.GetAnchorPoint("waiting.seat.13")));
            state.Tick=100;world.Render(state,0,true);Assert.That(Vector3.Distance(Find("Patient 99").position,world.GetAnchorPoint(anchor)),Is.LessThan(.001f));
            patient.Phase=ClinicPatientPhase.ReturningFromAmenity;patient.FromAnchor=anchor;patient.ToAnchor="waiting.seat.13";patient.PhaseStartedTick=100;patient.PhaseEndsTick=200;
            state.Tick=200;world.Render(state,0,true);Assert.That(Vector3.Distance(Find("Patient 99").position,world.GetAnchorPoint("waiting.seat.13")),Is.LessThan(.001f));
            Assert.That(patient.SeatId,Is.EqualTo(13));Assert.That(patient.HasAdmissionReservation,Is.True);
        }

        [Test]
        public void OccupiedParkingBaysShowCarsAndVendingTipsNeverModifyMoneyWhileRendering()
        {
            var state=ClinicSimulation.CreateNew().State;state.Amenity(ClinicAmenity.Parking).Level=1;state.Amenity(ClinicAmenity.Vending).Level=1;state.Amenity(ClinicAmenity.Vending).Till=33;
            state.Patients.Add(new ClinicPatientState{Id=2,ParkingBayId=1,Phase=ClinicPatientPhase.ReceptionQueue});world.Render(state,0,true);
            Assert.That(Find("Parked patient car 1").gameObject.activeInHierarchy,Is.True);Assert.That(Find("Parked patient car 0").gameObject.activeSelf,Is.False);
            Assert.That(Find("Parking bay 2").gameObject.activeSelf,Is.False);Assert.That(Find("Collectable vending tips").gameObject.activeInHierarchy,Is.True);
            for(int i=0;i<20;i++){state.Tick++;world.Render(state,.1f);}
            Assert.That(state.Wallet,Is.Zero);Assert.That(state.Amenity(ClinicAmenity.Vending).Till,Is.EqualTo(33));
        }

        [TestCase(375,667)][TestCase(1000,700)]
        public void ZoomedOutCameraCannotPanAwayFromTheFurnishedNeighbourhood(int width,int height)
        {
            world.SetRenderSize(width,height);world.Zoom(10,new Vector2(.5f,.5f));
            world.Pan(new Vector2(.1f,.1f),new Vector2(.95f,.95f));
            for(int corner=0;corner<4;corner++)
            {
                Assert.That(world.TryViewportToGround(new Vector2(corner&1,(corner>>1)&1),out var point),Is.True);
                Assert.That(point.x,Is.InRange(-25.01f,25.01f));Assert.That(point.z,Is.InRange(-22.01f,20.01f));
            }
        }

        [TestCase(393,852)][TestCase(852,393)][TestCase(320,568)]
        public void HomeIsAlreadyInsideCameraBoundsBeforeTheFirstGesture(int width,int height)
        {
            world.SetRenderSize(width,height);world.Home(true);
            var position=world.SceneCamera.transform.position;var size=world.SceneCamera.orthographicSize;
            world.Pan(new Vector2(.5f,.5f),new Vector2(.5f,.5f));
            Assert.That(Vector3.Distance(world.SceneCamera.transform.position,position),Is.LessThan(.001f));
            Assert.That(world.SceneCamera.orthographicSize,Is.EqualTo(size));
        }

        [TestCase(375,667,1.35f)][TestCase(375,667,3.2f)][TestCase(852,393,1.35f)]
        public void IncrementalPinchKeepsItsTotalScaleAndDoesNotDriftAfterRelease(int width,int height,float scale)
        {
            world.SetRenderSize(width,height);world.Home(true);var state=ClinicSimulation.CreateNew().State;
            var anchor=world.WorldToViewport(world.GetAnchorPoint("firstaid.progress"));
            var center=new Vector2(anchor.x*width,(1-anchor.y)*height);float initialSize=world.SceneCamera.orthographicSize;
            var gesture=new ClinicGesture();gesture.Begin(1,center+Vector2.left*22);gesture.Begin(2,center+Vector2.right*22);
            for(int step=1;step<=64;step++)for(int finger=0;finger<2;finger++)
            {
                float distance=22*Mathf.Lerp(1,scale,step/64f);var point=center+Vector2.right*(finger==0?-distance:distance);
                Assert.That(gesture.Move(finger+1,point,out var from,out var to,out float zoom),Is.True);
                var fromViewport=new Vector2(from.x/width,1-from.y/height);var toViewport=new Vector2(to.x/width,1-to.y/height);
                world.Pan(fromViewport,toViewport);world.Zoom(zoom,toViewport);world.Render(state,1f/60f);
            }
            Assert.That(world.SceneCamera.orthographicSize,Is.EqualTo(initialSize/scale).Within(.003f));
            Assert.That(Vector2.Distance(world.WorldToViewport(world.GetAnchorPoint("firstaid.progress")),anchor),Is.LessThan(.002f));
            gesture.End(1,center+Vector2.left*22*scale);gesture.End(2,center+Vector2.right*22*scale);
            var finalPosition=world.SceneCamera.transform.position;float finalSize=world.SceneCamera.orthographicSize;
            for(int frame=0;frame<60;frame++)world.Render(state,1f/60f);
            Assert.That(world.SceneCamera.transform.position,Is.EqualTo(finalPosition));Assert.That(world.SceneCamera.orthographicSize,Is.EqualTo(finalSize));
        }

        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)]
        public void UnavailableParkingBaysRemainDressedUntilTheirOwnPairOpens(int level)
        {
            var state=ClinicSimulation.CreateNew().State;state.Amenity(ClinicAmenity.Parking).Level=level;
            world.Render(state,0,true);
            for(int bay=0;bay<6;bay++)
            {
                var staging=Find("Future parking bay "+bay);Assert.That(staging,Is.Not.Null);
                Assert.That(staging.gameObject.activeInHierarchy,Is.EqualTo(bay>=level*2));
                Assert.That(Find("Parking bay "+bay).gameObject.activeInHierarchy,Is.EqualTo(bay<level*2));
                Assert.That(staging.GetComponentsInChildren<Renderer>(true).Length,Is.GreaterThanOrEqualTo(8));
                float x=bay%2==0?-13f:-8.2f,z=-3f+(bay/2)*2.9f;
                foreach(var renderer in staging.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.That(renderer.bounds.min.x,Is.GreaterThanOrEqualTo(x-1.0f));Assert.That(renderer.bounds.max.x,Is.LessThanOrEqualTo(x+1.0f));
                    Assert.That(renderer.bounds.min.z,Is.GreaterThanOrEqualTo(z-1.18f));Assert.That(renderer.bounds.max.z,Is.LessThanOrEqualTo(z+1.18f));
                }
            }
            state.Amenity(ClinicAmenity.Parking).Level=0;world.Render(state,0,true);
            for(int bay=0;bay<6;bay++)Assert.That(Find("Future parking bay "+bay).gameObject.activeInHierarchy,Is.True,"Restoring an unbuilt clinic restores its staging.");
        }

        [Test]
        public void FutureParkingDressingLeavesBothPassengerLanesAndEntrancesClear()
        {
            var state=ClinicSimulation.CreateNew().State;state.Patients.Clear();
            var person=new ClinicPatientState{Id=97,Phase=ClinicPatientPhase.Arriving,ToAnchor="reception.queue.0",PhaseStartedTick=0,PhaseEndsTick=100};
            state.Patients.Add(person);world.Render(state,0,true);
            var dressing=new System.Collections.Generic.List<Renderer>();
            for(int bay=0;bay<6;bay++)dressing.AddRange(Find("Future parking bay "+bay).GetComponentsInChildren<Renderer>());
            for(int bay=0;bay<6;bay++)
            {
                person.FromAnchor="parking.bay."+bay+".patient";
                for(int tick=0;tick<=100;tick++)
                {
                    state.Tick=tick;world.Render(state,.1f,true);var actor=Find("Patient 97").position+Vector3.up*.30f;
                    foreach(var renderer in dressing)
                    {
                        var bounds=renderer.bounds;if(bounds.max.y<.22f)continue;bounds.Expand(new Vector3(.60f,0,.60f));
                        Assert.That(bounds.Contains(actor),Is.False,renderer.name+" obstructs parking passenger "+bay+" at tick "+tick);
                    }
                }
            }
        }

    }
}
